namespace Trackdub2.Triage;

/// <summary>
/// Hard cap on agent calls in any rolling hour. Azure budget alerts arrive hours late, so this is
/// the stop that actually holds if a webhook storm or loop starts.
/// </summary>
public sealed class HourlyCallBudget(int limit, TimeProvider time)
{
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);
    private readonly Queue<DateTimeOffset> _calls = new();
    private readonly Lock _gate = new();

    public bool TryTake()
    {
        lock (_gate)
        {
            DateTimeOffset now = time.GetUtcNow();
            while (_calls.Count > 0 && now - _calls.Peek() >= Window)
            {
                _calls.Dequeue();
            }

            if (_calls.Count >= limit)
            {
                return false;
            }

            _calls.Enqueue(now);
            return true;
        }
    }
}
