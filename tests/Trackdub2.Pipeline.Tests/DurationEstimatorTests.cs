namespace Trackdub2.Pipeline.Tests;

public class DurationEstimatorTests
{
    [Fact]
    public void Estimate_divides_characters_by_rate()
    {
        Assert.Equal(1.0, DurationEstimator.EstimateSeconds(new string('a', 15), 15.0), precision: 9);
    }

    [Fact]
    public void Estimate_of_empty_text_is_zero()
    {
        Assert.Equal(0.0, DurationEstimator.EstimateSeconds(string.Empty, 15.0));
    }

    [Fact]
    public void Estimate_rejects_non_positive_rate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DurationEstimator.EstimateSeconds("hola", 0));
    }

    [Fact]
    public void Fits_allows_exactly_the_tolerance_band()
    {
        Assert.True(DurationEstimator.Fits(1.10, 1.0, 0.10));
        Assert.False(DurationEstimator.Fits(1.11, 1.0, 0.10));
    }

    [Fact]
    public void Fits_with_zero_tolerance_requires_estimate_within_window()
    {
        Assert.True(DurationEstimator.Fits(1.0, 1.0, 0.0));
        Assert.False(DurationEstimator.Fits(1.0001, 1.0, 0.0));
    }

    [Fact]
    public void MaxChars_floors_the_window_times_rate()
    {
        Assert.Equal(18, DurationEstimator.MaxCharsForWindow(1.2, 15.0));
    }
}
