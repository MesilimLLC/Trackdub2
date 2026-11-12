namespace Trackdub2.Pipeline.Tests;

public class GateRound2Tests
{
    private static Task<MeaningCheckResult> Pass(string _, CancellationToken __) =>
        Task.FromResult(new MeaningCheckResult(MeaningVerdict.Pass, [], "ok"));

    [Fact]
    public async Task Infinite_or_nan_window_is_rejected()
    {
        foreach (double window in new[] { double.PositiveInfinity, double.NaN })
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                GateLoop.RunAsync("hola", window, new QaOptions(), (t, _, _) => Task.FromResult(t), Pass));
        }
    }

    [Fact]
    public async Task Pass_reported_with_issues_by_an_injected_judge_is_treated_as_fail()
    {
        var result = await GateLoop.RunAsync("hola", 1.0, new QaOptions { MaxAttempts = 0 },
            (t, _, _) => Task.FromResult(t),
            (_, _) => Task.FromResult(new MeaningCheckResult(MeaningVerdict.Pass, ["drops negation"], "")));

        Assert.False(result.Passed);
        Assert.Equal(MeaningVerdict.Fail, result.Attempts[0].Meaning);
    }

    [Fact]
    public async Task Cancellation_during_a_meaning_check_that_ignores_the_token_is_observed()
    {
        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            GateLoop.RunAsync("hola", 1.0, new QaOptions(), (t, _, _) => Task.FromResult(t),
                (_, _) => { cts.Cancel(); return Task.FromResult(new MeaningCheckResult(MeaningVerdict.Pass, [], "ok")); },
                cts.Token));
    }

    [Fact]
    public void Two_JSON_objects_in_one_reply_fail_closed()
    {
        var result = MeaningGate.ParseAndNormalize(
            "{\"verdict\":\"pass\",\"issues\":[]} then {\"verdict\":\"fail\",\"issues\":[\"negation lost\"]}");

        Assert.Equal(MeaningVerdict.Fail, result.Verdict);
    }

    [Fact]
    public void Reply_without_issues_field_fails_closed()
    {
        var result = MeaningGate.ParseAndNormalize("{\"verdict\":\"pass\"}");

        Assert.Equal(MeaningVerdict.Fail, result.Verdict);
    }
}
