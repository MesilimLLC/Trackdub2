namespace Trackdub2.Pipeline;

/// <summary>
/// Deterministic spoken-duration estimate: characters divided by characters per second. No model call.
/// Ported from Trackdub Director's DurationEstimator so both systems measure a line the same way.
/// </summary>
public static class DurationEstimator
{
    public static double EstimateSeconds(string text, double charactersPerSecond)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (charactersPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(charactersPerSecond), "Must be positive.");
        }

        return text.Length == 0 ? 0 : text.Length / charactersPerSecond;
    }

    /// <summary>
    /// Fits when the estimate is at most the window plus the tolerance fraction of the window.
    /// Zero tolerance means the estimate must not exceed the window.
    /// </summary>
    public static bool Fits(double estimatedSeconds, double windowSeconds, double toleranceFraction)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(windowSeconds);
        ArgumentOutOfRangeException.ThrowIfNegative(toleranceFraction);

        return estimatedSeconds <= windowSeconds * (1.0 + toleranceFraction);
    }

    public static int MaxCharsForWindow(double windowSeconds, double charactersPerSecond)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(windowSeconds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(charactersPerSecond);

        return (int)Math.Floor(windowSeconds * charactersPerSecond);
    }
}
