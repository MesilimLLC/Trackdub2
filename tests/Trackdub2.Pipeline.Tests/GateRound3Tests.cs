namespace Trackdub2.Pipeline.Tests;

public class GateRound3Tests
{
    [Fact]
    public void Pass_followed_by_a_brace_in_prose_is_still_a_pass()
    {
        var result = MeaningGate.ParseAndNormalize(
            "{\"verdict\":\"pass\",\"issues\":[]} (format: {verdict, issues})");

        Assert.Equal(MeaningVerdict.Pass, result.Verdict);
    }

    [Fact]
    public void Second_complete_object_after_the_first_is_still_rejected()
    {
        var result = MeaningGate.ParseAndNormalize(
            "{\"verdict\":\"pass\",\"issues\":[]} {\"verdict\":\"fail\",\"issues\":[\"negation lost\"]}");

        Assert.Equal(MeaningVerdict.Fail, result.Verdict);
        Assert.Equal(["ambiguous meaning response: more than one JSON object"], result.Issues);
    }
}
