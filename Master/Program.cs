using Grpc.Core;
using Grpc.Net.Client;
using Log;
using Master;
using Microsoft.Extensions.Logging.Console;
using LogService = Master.Services.LogService;
using ID = System.String;

var builder = WebApplication.CreateBuilder(args);

// Additional configuration is required to successfully run gRPC on macOS.
// For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682

// Add services to the container.
builder.Services.AddGrpc();
builder.Logging.ClearProviders().AddConsole(options =>
{
    options.FormatterName = ConsoleFormatterNames.Systemd;
});

var app = builder.Build();

Dictionary<ID, LogEntry> logs = new Dictionary<ID, LogEntry>();
List<string> secondaryAddresses = app.Configuration.GetSection("SecondaryUrls").Get<List<string>>() ?? new();
var healthCheckDelay = app.Configuration.GetSection("HealthCheckDelayInSeconds").Get<int?>() ?? 5;
var healthCheckDelayInSeconds = TimeSpan.FromSeconds(healthCheckDelay);
HashSet<ID> previouslyUnhealthyServers = new HashSet<ID>();

var logger = app.Services.GetRequiredService<ILogger<Program>>() ?? throw new NullReferenceException();

var healthCheckCancellationTokenSource = new CancellationTokenSource();
var healthCheckTask = PeriodicHealthCheckAsync(healthCheckCancellationTokenSource.Token);

app.MapPost("/log", async (HttpContext httpContext, string message, int w) =>
{
    // Check if there's a quorum
    int healthySecondaries = secondaryAddresses.Count - previouslyUnhealthyServers.Count;
    int requiredQuorum = (secondaryAddresses.Count / 2); // + 1 Simple majority

    if (healthySecondaries < requiredQuorum)
    {
        logger.LogError("Not enough secondaries for a quorum. Switching to read-only mode.");
        return Results.Problem("The system is currently in read-only mode due to insufficient secondary servers.");
    }

    var cts = new CancellationTokenSource();
    var sharedId = Guid.NewGuid().ToString();

    logs.Add(sharedId, new LogEntry(message)); // Assuming this is thread-safe

    var replicationTasks = secondaryAddresses.Select(address =>
        ReplicateMessageWithRetryAsync(address, message, sharedId, cts.Token)).ToList();

    // Wait for all tasks if w equals the number of secondaries plus one (for master)
    // Otherwise, wait for w - 1 tasks because we already have the master's ACK
    var requiredAcks = w == secondaryAddresses.Count + 1 ? replicationTasks : replicationTasks.Take(w - 1);
    var ackTasks = await Task.WhenAll(requiredAcks);
    var ackCount = ackTasks.Count(success => success) + 1; // Including the master ACK

    // ALTERNATIVE
    // var ackCount = 1;
    // Task.WaitAll(replicationTasks.Take(w - 1).ToArray()); // Wait only for 'w - 1' tasks (acknowledgments)

    if (ackCount < w)
    {
        logger.LogError("Failed to get enough ACKs");
        return Results.Problem("Failed to replicate message based on write concern");
    }

    logger.LogInformation($"Message '{message}' logged with {ackCount} ACKs");
    return Results.Accepted();
});

async Task<bool> ReplicateMessageWithRetryAsync(string address, string message, string id, CancellationToken cts)
{
    var backoff = new ExponentialBackoff();
    var attempts = 0;
    var maxAttempts = 3;

    while (attempts < maxAttempts && !cts.IsCancellationRequested)
    {
        try
        {
            var channel = GrpcChannel.ForAddress(address);
            var client = new Log.LogService.LogServiceClient(channel);
            
            var messageRequest = new MessageRequest
            {
                Id = id,
                Text = message,
                Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(logs[id].Timestamp)
            };
            
            var reply = await client.AppendMessageAsync(messageRequest, cancellationToken: cts);
            if (reply.Success)
            {
                return true;
            }
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            logger.LogWarning($"Secondary at {address} is unavailable, retrying attempt {attempts + 1}...");
            await Task.Delay(backoff.NextDelay(), cts);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning($"Replication to {address} was canceled.");
            break;
        }

        attempts++;
    }

    return false;
}

async Task ReplicateMissedMessagesAsync(string secondaryAddress, string lastId, CancellationToken cts)
{
    var missedMessages = logs
        .Where(m => string.CompareOrdinal(m.Key, lastId) > 0)
        .Select(m => new MessageRequest
        {
            Id = m.Key, 
            Text = m.Value.Message, 
            Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(m.Value.Timestamp)
        })
        .ToList();

    if (!missedMessages.Any())
    {
        logger.LogInformation($"No missed messages to replicate to {secondaryAddress}");
        return;
    }

    var batchRequest = new BatchMessageRequest();
    batchRequest.Messages.AddRange(missedMessages);

    var channel = GrpcChannel.ForAddress(secondaryAddress);
    var client = new Log.LogService.LogServiceClient(channel);

    var backoff = new ExponentialBackoff();
    var maxAttempts = 3;

    for (int attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            var reply = await client.BatchAppendMessagesAsync(batchRequest);
            if (reply.Success)
            {
                logger.LogInformation(
                    $"Successfully replicated missed messages to {secondaryAddress} on attempt {attempt}");
                return;
            }
            
            logger.LogWarning(
                $"Replication to {secondaryAddress} was unsuccessful on attempt {attempt}. Retrying...");
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable && attempt < maxAttempts)
        {
            logger.LogWarning(
                $"Secondary at {secondaryAddress} is unavailable. Attempt {attempt} failed: {ex.Message}. Retrying after delay...");
        }
        catch (RpcException ex)
        {
            logger.LogError(
                $"Failed to replicate missed messages to {secondaryAddress} on attempt {attempt}: {ex.Status}");
            // If it's not a retryable exception or we've reached the max retries, break out of the loop
            break;
        }

        await Task.Delay(backoff.NextDelay(), cts); // Wait before retrying
    }

    logger.LogError($"Failed to replicate missed messages to {secondaryAddress} after {maxAttempts} attempts.");
}

async Task<string?> GetLastMessageIDFromSecondary(string secondaryAddress)
{
    var channel = GrpcChannel.ForAddress(secondaryAddress);
    var client = new Log.LogService.LogServiceClient(channel);

    try
    {
        var reply = await client.GetLastMessageIDAsync(new Empty());
        return reply.Id;
    }
    catch (RpcException ex)
    {
        logger.LogError($"Failed to get last message ID from {secondaryAddress}");
        // Handle exception (e.g., assume no messages received, retry logic)
        throw;
    }
}

// Method to check the health of secondary servers and replicate missed messages if necessary
async Task CheckSecondaryHealthAsync(CancellationToken cancellationToken)
{
    foreach (var address in secondaryAddresses)
    {
        try
        {
            var messageId = await GetLastMessageIDFromSecondary(address);

            if (String.IsNullOrEmpty(messageId) && previouslyUnhealthyServers.Contains(address))
            {
                await ReplicateMissedMessagesAsync(address, messageId, cancellationToken);
                previouslyUnhealthyServers.Remove(address);
            }
            // else if (messageId is null) {
            //     previouslyUnhealthyServers.Add(address);
            // }
        }
        catch (RpcException)
        {
            logger.LogError($"{address} was added into UnhealthyServers.");
            previouslyUnhealthyServers.Add(address);
        }
    }
}

// Background task to periodically check the health of secondary servers
async Task PeriodicHealthCheckAsync(CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        await Task.Delay(healthCheckDelayInSeconds, cancellationToken); // Check every 2 seconds, adjust as needed
        await CheckSecondaryHealthAsync(cancellationToken);
    }
}

app.MapGet("/log", () => logs);

// Configure the HTTP request pipeline.
app.MapGrpcService<LogService>();

app.MapGet("/",
    () =>
        "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();

await healthCheckTask;