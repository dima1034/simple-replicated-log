using Grpc.Net.Client;
using Log;
using Master.Services;
using Microsoft.AspNetCore.Mvc;
using LogService = Master.Services.LogService;

var builder = WebApplication.CreateBuilder(args);

// Additional configuration is required to successfully run gRPC on macOS.
// For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682

// Add services to the container.
builder.Services.AddGrpc();
builder.Logging.ClearProviders().AddConsole();

var app = builder.Build();

List<string> logs = new();
List<string> secondaryAddresses = new() { "http://secondary1:5300", "http://secondary2:5301" };

var logger = app.Services.GetRequiredService<ILogger<Program>>();

app.MapPost("/log", (string message, int w) =>
{
    logs.Add(message);

    int ackCount = 1; // Start with master ACK
    List<Task> replicationTasks = new List<Task>();
    string sharedId = Guid.NewGuid().ToString();
    
    foreach (var address in secondaryAddresses)
    {
        var channel = GrpcChannel.ForAddress(address);
        var client = new Log.LogService.LogServiceClient(channel);

        // Start replication asynchronously
        replicationTasks.Add(Task.Run(() => 
        {
            var reply = client.AppendMessage(new MessageRequest { Text = message, Id = sharedId });
            if (reply.Success)
            {
                Interlocked.Increment(ref ackCount);
            }
        }));
    }

    Task.WaitAll(replicationTasks.Take(w - 1).ToArray()); // Wait only for 'w - 1' tasks (acknowledgments)

    if (ackCount < w)
    {
        logger.LogError("Failed to get enough ACKs");
        return Results.Problem("Failed to replicate message based on write concern");
    }

    logger.LogInformation($"Message '{message}' logged with {ackCount} ACKs");
    return Results.Accepted();
});

app.MapGet("/log", () => logs);

// Configure the HTTP request pipeline.
app.MapGrpcService<GreeterService>();
app.MapGrpcService<LogService>();

app.MapGet("/",
    () =>
        "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();