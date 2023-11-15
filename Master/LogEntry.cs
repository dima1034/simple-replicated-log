namespace Master;

public class LogEntry
{
    public string Message { get; set; }
    public DateTime Timestamp { get; set; }

    public LogEntry(string message)
    {
        Message = message;
        Timestamp = DateTime.UtcNow; // Using UTC time to avoid timezone issues
    }
}