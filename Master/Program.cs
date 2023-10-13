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
List<string> secondaryAddresses = new() { "http://localhost:5300", "http://localhost:5301" };

var logger = app.Services.GetRequiredService<ILogger<Program>>();

app.MapPost("/log", (string message) =>
{
    logs.Add(message);

    foreach (var address in secondaryAddresses)
    {
        var channel = GrpcChannel.ForAddress(address);
        var client = new Log.LogService.LogServiceClient(channel); // Corrected this line
    
        var reply = client.AppendMessage(new MessageRequest { Text = message });
    
        if (!reply.Success)
        {
            logger.LogError("Failed to replicate message on secondary");
            return Results.Problem("Failed to replicate message on secondary");
        }
    }

    logger.LogInformation($"Message '{message}' logged");
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