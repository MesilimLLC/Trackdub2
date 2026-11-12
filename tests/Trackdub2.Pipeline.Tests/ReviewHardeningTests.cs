namespace Trackdub2.Pipeline.Tests;

public class ReviewHardeningTests
{
    private static Task<MeaningCheckResult> Pass(string _, CancellationToken __) =>
        Task.FromResult(new MeaningCheckResult(MeaningVerdict.Pass, [], "ok"));

    [Fact]
    public void Fits_accepts_exact_decimal_boundary_despite_binary_rounding()
    {
        Assert.True(DurationEstimator.Fits(4.2, 3.0, 0.4));
        Assert.False(DurationEstimator.Fits(4.21, 3.0, 0.4));
    }

    [Fact]
    public void Fits_rejects_negative_estimate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DurationEstimator.Fits(-1.0, 1.0, 0.1));
    }

    [Fact]
    public void MaxChars_rounds_near_integer_products_up_to_the_integer()
    {
        Assert.Equal(29, DurationEstimator.MaxCharsForWindow(0.29, 100));
    }

    [Fact]
    public void MaxChars_still_floors_genuinely_fractional_budgets()
    {
        Assert.Equal(18, DurationEstimator.MaxCharsForWindow(1.25, 15));
    }

    [Theory]
    [InlineData(-1.0)]
    public async Task Negative_window_is_rejected_at_the_loop_entry_point(double window)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            GateLoop.RunAsync("hola", window, new QaOptions(), (t, _, _) => Task.FromResult(t), Pass));
    }

    [Fact]
    public void Non_finite_options_are_rejected()
    {
        double[] bad = [double.NaN, double.PositiveInfinity, double.NegativeInfinity];
        foreach (double value in bad)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new QaOptions { CharactersPerSecond = value }.Validate());
            Assert.Throws<ArgumentOutOfRangeException>(() => new QaOptions { ToleranceFraction = value }.Validate());
        }
    }

    [Fact]
    public async Task Cancelled_token_is_honoured_before_the_first_evaluation_even_with_zero_attempts()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            GateLoop.RunAsync(new string('a', 60), 1.0, new QaOptions { MaxAttempts = 0 },
                (t, _, _) => Task.FromResult(t), Pass, cts.Token));
    }

    [Fact]
    public async Task Non_cancellation_exception_during_cancellation_propagates_instead_of_becoming_an_error()
    {
        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            GateLoop.RunAsync("hola", 1.0, new QaOptions(), (t, _, _) => Task.FromResult(t),
                (_, _) => { cts.Cancel(); throw new InvalidOperationException("boom"); }, cts.Token));
    }
}
