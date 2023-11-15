namespace Master;

// Exponential backoff strategy class
public class ExponentialBackoff
{
    private const int MaxDelay = 1000; // Maximum delay in milliseconds
    private int attempt = 0;

    public TimeSpan NextDelay()
    {
        var nextDelay = Math.Min(Math.Pow(2, attempt) * 100, MaxDelay); // Exponential backoff formula
        
        attempt++;
        
        return TimeSpan.FromMilliseconds(nextDelay);
    }

    public void Reset()
    {
        attempt = 0;
    }
}