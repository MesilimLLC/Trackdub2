namespace Trackdub2.Pipeline.Tests;

public class TimingLoopTests
{
    private static readonly QaOptions Options = new();

    private static string Text(int chars) => new('a', chars);

    [Fact]
    public async Task First_attempt_that_fits_is_returned_without_revision()
    {
        int calls = 0;
        var result = await TimingLoop.RunAsync(Text(15), 1.0, Options, (t, _) => { calls++; return Task.FromResult(t); });

        Assert.True(result.Passed);
        Assert.Equal(0, result.Revisions);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Revision_that_fits_stops_the_loop()
    {
        int calls = 0;
        var result = await TimingLoop.RunAsync(Text(30), 1.0, Options, (_, _) =>
        {
            calls++;
            return Task.FromResult(Text(12));
        });

        Assert.True(result.Passed);
        Assert.Equal(1, result.Revisions);
        Assert.Equal(1, calls);
        Assert.Equal(Text(12), result.Translation);
    }

    [Fact]
    public async Task Never_fitting_text_stops_after_max_attempts()
    {
        int calls = 0;
        var result = await TimingLoop.RunAsync(Text(60), 1.0, Options, (_, _) =>
        {
            calls++;
            return Task.FromResult(Text(60));
        });

        Assert.False(result.Passed);
        Assert.Equal(QaOptions.DefaultMaxAttempts, calls);
        Assert.Equal(QaOptions.DefaultMaxAttempts, result.Revisions);
    }

    [Fact]
    public async Task Zero_max_attempts_makes_no_revision_call()
    {
        int calls = 0;
        var result = await TimingLoop.RunAsync(Text(60), 1.0, Options with { MaxAttempts = 0 }, (t, _) =>
        {
            calls++;
            return Task.FromResult(t);
        });

        Assert.False(result.Passed);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Each_revision_is_fed_the_previous_translation()
    {
        var seen = new List<string>();
        string[] revisions = [Text(40), Text(10)];
        int next = 0;
        var result = await TimingLoop.RunAsync(Text(60), 1.0, Options, (t, _) =>
        {
            seen.Add(t);
            return Task.FromResult(revisions[next++]);
        });

        Assert.True(result.Passed);
        Assert.Equal([Text(60), Text(40)], seen);
    }

    [Fact]
    public async Task Cancellation_before_revision_is_honoured()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            TimingLoop.RunAsync(Text(60), 1.0, Options, (t, _) => Task.FromResult(t), cts.Token));
    }

    [Theory]
    [InlineData(-1, 0.1, 15.0)]
    [InlineData(3, -0.1, 15.0)]
    [InlineData(3, 0.1, 0.0)]
    public async Task Invalid_options_are_rejected(int maxAttempts, double tolerance, double cps)
    {
        var options = new QaOptions { MaxAttempts = maxAttempts, ToleranceFraction = tolerance, CharactersPerSecond = cps };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            TimingLoop.RunAsync(Text(10), 1.0, options, (t, _) => Task.FromResult(t)));
    }
}
