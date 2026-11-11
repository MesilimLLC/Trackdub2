namespace Trackdub2.Pipeline.Tests;

public class GateLoopTests
{
    private static readonly QaOptions Options = new();

    private static string Text(int chars) => new('a', chars);

    private static Task<MeaningCheckResult> Pass(string _, CancellationToken __) =>
        Task.FromResult(new MeaningCheckResult(MeaningVerdict.Pass, [], "ok"));

    private static Task<MeaningCheckResult> FailWith(params string[] issues) =>
        Task.FromResult(new MeaningCheckResult(MeaningVerdict.Fail, issues, "diverges"));

    [Fact]
    public async Task First_attempt_that_fits_and_passes_meaning_is_returned_without_revision()
    {
        int revisions = 0;
        var result = await GateLoop.RunAsync(Text(15), 1.0, Options,
            (t, _, _) => { revisions++; return Task.FromResult(t); }, Pass);

        Assert.True(result.Passed);
        Assert.Equal(0, result.Revisions);
        Assert.Equal(0, revisions);
    }

    [Fact]
    public async Task Revision_that_fits_and_passes_meaning_stops_the_loop()
    {
        int revisions = 0;
        var result = await GateLoop.RunAsync(Text(30), 1.0, Options,
            (_, _, _) => { revisions++; return Task.FromResult(Text(12)); }, Pass);

        Assert.True(result.Passed);
        Assert.Equal(1, result.Revisions);
        Assert.Equal(1, revisions);
        Assert.Equal(Text(12), result.Translation);
    }

    [Fact]
    public async Task Never_passing_text_stops_after_max_attempts()
    {
        int revisions = 0;
        var result = await GateLoop.RunAsync(Text(60), 1.0, Options,
            (_, _, _) => { revisions++; return Task.FromResult(Text(60)); }, Pass);

        Assert.False(result.Passed);
        Assert.Equal(QaOptions.DefaultMaxAttempts, revisions);
        Assert.Equal(QaOptions.DefaultMaxAttempts, result.Revisions);
    }

    [Fact]
    public async Task Zero_max_attempts_makes_no_revision_call()
    {
        int revisions = 0;
        var result = await GateLoop.RunAsync(Text(60), 1.0, Options with { MaxAttempts = 0 },
            (t, _, _) => { revisions++; return Task.FromResult(t); }, Pass);

        Assert.False(result.Passed);
        Assert.Equal(0, revisions);
    }

    [Fact]
    public async Task Each_revision_is_fed_the_previous_translation()
    {
        var seen = new List<string>();
        string[] revisions = [Text(40), Text(10)];
        int next = 0;
        var result = await GateLoop.RunAsync(Text(60), 1.0, Options,
            (t, _, _) => { seen.Add(t); return Task.FromResult(revisions[next++]); }, Pass);

        Assert.True(result.Passed);
        Assert.Equal([Text(60), Text(40)], seen);
    }

    [Fact]
    public async Task Meaning_failure_passes_its_issues_to_the_next_revision()
    {
        IReadOnlyList<string>? sentIssues = null;
        int checks = 0;
        var result = await GateLoop.RunAsync(Text(10), 1.0, Options,
            (t, issues, _) => { sentIssues = issues; return Task.FromResult(t); },
            (t, ct) => ++checks == 1 ? FailWith("drops the negation") : Pass(t, ct));

        Assert.True(result.Passed);
        Assert.Equal(1, result.Revisions);
        Assert.NotNull(sentIssues);
        Assert.Equal(["drops the negation"], sentIssues);
    }

    [Fact]
    public async Task Meaning_failure_on_every_attempt_fails_the_segment()
    {
        var result = await GateLoop.RunAsync(Text(10), 1.0, Options,
            (t, _, _) => Task.FromResult(t), (_, _) => FailWith("wrong subject"));

        Assert.False(result.Passed);
        Assert.Equal(QaOptions.DefaultMaxAttempts, result.Revisions);
        Assert.All(result.Attempts, a => Assert.Equal(MeaningVerdict.Fail, a.Meaning));
    }

    [Fact]
    public async Task Meaning_error_on_first_attempt_is_terminal()
    {
        int revisions = 0;
        var result = await GateLoop.RunAsync(Text(10), 1.0, Options,
            (t, _, _) => { revisions++; return Task.FromResult(t); },
            (_, _) => Task.FromResult(MeaningGate.ForError("judge unavailable")));

        Assert.False(result.Passed);
        Assert.Equal(0, revisions);
        Assert.Equal(MeaningVerdict.Error, result.Attempts[0].Meaning);
    }

    [Fact]
    public async Task Meaning_error_during_revision_stops_the_loop()
    {
        int revisions = 0;
        int checks = 0;
        var result = await GateLoop.RunAsync(Text(60), 1.0, Options,
            (_, _, _) => { revisions++; return Task.FromResult(Text(10)); },
            (t, ct) => ++checks == 1 ? FailWith("drops the negation") : Task.FromResult(MeaningGate.ForError("timeout")));

        Assert.False(result.Passed);
        Assert.Equal(2, revisions);
        Assert.Equal(MeaningVerdict.Error, result.Attempts[^1].Meaning);
    }

    [Fact]
    public async Task Meaning_check_is_skipped_for_attempts_that_do_not_fit()
    {
        int checks = 0;
        var result = await GateLoop.RunAsync(Text(60), 1.0, Options,
            (_, _, _) => Task.FromResult(Text(10)),
            (t, ct) => { checks++; return Pass(t, ct); });

        Assert.True(result.Passed);
        Assert.Equal(MeaningVerdict.NotEvaluated, result.Attempts[0].Meaning);
        Assert.Equal(1, checks);
    }


    [Fact]
    public async Task Exception_from_meaning_check_is_recorded_as_error()
    {
        var result = await GateLoop.RunAsync(Text(10), 1.0, Options,
            (t, _, _) => Task.FromResult(t),
            (_, _) => throw new InvalidOperationException("boom"));

        Assert.False(result.Passed);
        Assert.Equal(MeaningVerdict.Error, result.Attempts[0].Meaning);
    }

    [Fact]
    public async Task Cancellation_before_revision_is_honoured()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            GateLoop.RunAsync(Text(60), 1.0, Options, (t, _, _) => Task.FromResult(t), Pass, cts.Token));
    }

    [Theory]
    [InlineData(-1, 0.1, 15.0)]
    [InlineData(3, -0.1, 15.0)]
    [InlineData(3, 0.1, 0.0)]
    public async Task Invalid_options_are_rejected(int maxAttempts, double tolerance, double cps)
    {
        var options = new QaOptions { MaxAttempts = maxAttempts, ToleranceFraction = tolerance, CharactersPerSecond = cps };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            GateLoop.RunAsync(Text(10), 1.0, options, (t, _, _) => Task.FromResult(t), Pass));
    }
}
