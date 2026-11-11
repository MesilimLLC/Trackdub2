namespace Trackdub2.Pipeline;

public sealed record TimingAttempt(int Number, string Translation, double EstimatedSeconds, bool Fits);

public sealed record TimingLoopResult(
    string Translation,
    bool Passed,
    IReadOnlyList<TimingAttempt> Attempts)
{
    public int Revisions => Attempts.Count - 1;
}

/// <summary>
/// Timing gate with bounded revision, as in Director's QaService.RunRevisionLoopAsync. The first attempt
/// is checked as-is; each failed attempt is revised by <paramref name="reviseForTiming"/> up to
/// <see cref="QaOptions.MaxAttempts"/> times. The meaning gate is not part of this loop yet.
/// </summary>
public static class TimingLoop
{
    public static async Task<TimingLoopResult> RunAsync(
        string initialTranslation,
        double windowSeconds,
        QaOptions options,
        Func<string, CancellationToken, Task<string>> reviseForTiming,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(initialTranslation);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(reviseForTiming);
        options.Validate();

        var attempts = new List<TimingAttempt>();
        string current = initialTranslation;

        TimingAttempt first = Evaluate(1, current, windowSeconds, options);
        attempts.Add(first);
        if (first.Fits)
        {
            return new TimingLoopResult(current, true, attempts);
        }

        for (int revision = 1; revision <= options.MaxAttempts; revision++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            current = await reviseForTiming(current, cancellationToken).ConfigureAwait(false);

            TimingAttempt attempt = Evaluate(1 + revision, current, windowSeconds, options);
            attempts.Add(attempt);
            if (attempt.Fits)
            {
                return new TimingLoopResult(current, true, attempts);
            }
        }

        return new TimingLoopResult(current, false, attempts);
    }

    private static TimingAttempt Evaluate(int number, string translation, double windowSeconds, QaOptions options)
    {
        double estimate = DurationEstimator.EstimateSeconds(translation, options.CharactersPerSecond);
        bool fits = DurationEstimator.Fits(estimate, windowSeconds, options.ToleranceFraction);
        return new TimingAttempt(number, translation, estimate, fits);
    }
}
