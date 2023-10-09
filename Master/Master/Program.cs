using Grpc.Net.Client;
using GrpcService1.Services;
using Log;
using LogService = GrpcService1.Services.LogService;

var builder = WebApplication.CreateBuilder(args);

// Additional configuration is required to successfully run gRPC on macOS.
// For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682

// Add services to the container.
builder.Services.AddGrpc();

var app = builder.Build();

List<string> logs = new();
List<string> secondaryAddresses = new() { "http://secondary1:5000", "http://secondary2:5000" };

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
            return Results.Problem("Failed to replicate message on secondary");
        }
    }

    return null;
});

app.MapGet("/log", () => logs);

app.Run();

// Configure the HTTP request pipeline.
app.MapGrpcService<GreeterService>();
app.MapGrpcService<LogService>();

app.MapGet("/",
    () =>
        "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();