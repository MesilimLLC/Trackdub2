namespace Trackdub2.Pipeline;

public sealed record GateAttempt(
    int Number,
    string Translation,
    double EstimatedSeconds,
    bool Fits,
    MeaningVerdict Meaning,
    IReadOnlyList<string>? MeaningIssues)
{
    public bool Passed => Fits && Meaning == MeaningVerdict.Pass;
}

public sealed record GateLoopResult(string Translation, bool Passed, IReadOnlyList<GateAttempt> Attempts)
{
    public int Revisions => Attempts.Count - 1;
}

/// <summary>
/// Timing and meaning gates with bounded revision, as in Director's QaService.ProcessSegmentAsync and
/// RunRevisionLoopAsync. The meaning check runs only on attempts that fit the window. A meaning error ends
/// the segment as a failure: terminal on the first attempt, and after the failing revision otherwise. A meaning
/// failure's issues are passed to the next revision.
/// </summary>
public static class GateLoop
{
    public static async Task<GateLoopResult> RunAsync(
        string initialTranslation,
        double windowSeconds,
        QaOptions options,
        Func<string, IReadOnlyList<string>?, CancellationToken, Task<string>> reviseForTiming,
        Func<string, CancellationToken, Task<MeaningCheckResult>> checkMeaning,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(initialTranslation);
        if (!double.IsFinite(windowSeconds) || windowSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(windowSeconds), "Must be finite and zero or more.");
        }

        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(reviseForTiming);
        ArgumentNullException.ThrowIfNull(checkMeaning);
        options.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        var attempts = new List<GateAttempt>();
        string current = initialTranslation;

        GateAttempt first = await EvaluateAsync(1, current, windowSeconds, options, checkMeaning, cancellationToken).ConfigureAwait(false);
        attempts.Add(first);
        if (first.Passed)
        {
            return new GateLoopResult(current, true, attempts);
        }

        if (first.Meaning == MeaningVerdict.Error)
        {
            return new GateLoopResult(current, false, attempts);
        }

        IReadOnlyList<string>? lastMeaningIssues = first.Meaning == MeaningVerdict.Fail ? first.MeaningIssues : null;

        for (int revision = 1; revision <= options.MaxAttempts; revision++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            current = await reviseForTiming(current, lastMeaningIssues, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            GateAttempt attempt = await EvaluateAsync(1 + revision, current, windowSeconds, options, checkMeaning, cancellationToken).ConfigureAwait(false);
            attempts.Add(attempt);

            if (attempt.Passed)
            {
                return new GateLoopResult(current, true, attempts);
            }

            if (attempt.Meaning == MeaningVerdict.Error)
            {
                break;
            }

            if (attempt.Meaning == MeaningVerdict.Fail)
            {
                lastMeaningIssues = attempt.MeaningIssues;
            }
        }

        return new GateLoopResult(current, false, attempts);
    }

    private static async Task<GateAttempt> EvaluateAsync(
        int number,
        string translation,
        double windowSeconds,
        QaOptions options,
        Func<string, CancellationToken, Task<MeaningCheckResult>> checkMeaning,
        CancellationToken cancellationToken)
    {
        double estimate = DurationEstimator.EstimateSeconds(translation, options.CharactersPerSecond);
        bool fits = DurationEstimator.Fits(estimate, windowSeconds, options.ToleranceFraction);
        if (!fits)
        {
            return new GateAttempt(number, translation, estimate, false, MeaningVerdict.NotEvaluated, null);
        }

        MeaningCheckResult meaning;
        try
        {
            meaning = await checkMeaning(translation, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            meaning = MeaningGate.ForError(ex.Message);
        }

        cancellationToken.ThrowIfCancellationRequested();

        MeaningVerdict verdict = meaning.Verdict == MeaningVerdict.Pass && meaning.Issues.Length > 0
            ? MeaningVerdict.Fail
            : meaning.Verdict;
        return new GateAttempt(number, translation, estimate, true, verdict, meaning.Issues);
    }
}
