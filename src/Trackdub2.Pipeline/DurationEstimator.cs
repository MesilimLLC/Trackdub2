namespace Trackdub2.Pipeline;

/// <summary>
/// Deterministic spoken-duration estimate: characters divided by characters per second. No model call.
/// Ported from Trackdub Director's DurationEstimator. The boundary handling differs from Director on
/// purpose: values within 1e-9 of a limit are treated as on the limit, because binary floating point
/// puts exact decimal limits (for example 4.2 s = 3.0 s + 40%) just below the value.
/// </summary>
public static class DurationEstimator
{
    private const double BoundaryEpsilon = 1e-9;

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
        ArgumentOutOfRangeException.ThrowIfNegative(estimatedSeconds);
        ArgumentOutOfRangeException.ThrowIfNegative(windowSeconds);
        ArgumentOutOfRangeException.ThrowIfNegative(toleranceFraction);

        double allowed = windowSeconds * (1.0 + toleranceFraction);
        return estimatedSeconds <= allowed + BoundaryEpsilon;
    }

    /// <summary>
    /// The largest whole character count whose estimate fits the window. Products within 1e-9 of an integer
    /// count as that integer, so 0.29 s at 100 cps gives 29 rather than 28.
    /// </summary>
    public static int MaxCharsForWindow(double windowSeconds, double charactersPerSecond)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(windowSeconds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(charactersPerSecond);

        return (int)Math.Floor(Math.Round(windowSeconds * charactersPerSecond, 9));
    }
}
