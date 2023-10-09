using Grpc.Core;
using Log;

namespace GrpcService1.Services;

public class LogService : Log.LogService.LogServiceClient
{
    private static readonly List<string> Logs = new List<string>();

    public override MessageReply AppendMessage(MessageRequest request, Metadata headers, DateTime? deadline, CancellationToken cancellationToken)
    {
        Logs.Add(request.Text);
        
        return new MessageReply { Success = true };
    }
}