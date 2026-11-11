namespace Trackdub2.Pipeline.Tests;

public class MeaningGateTests
{
    [Fact]
    public void Pass_without_issues_is_pass()
    {
        var result = MeaningGate.ParseAndNormalize("{\"verdict\": \"pass\", \"issues\": [], \"explanation\": \"Meaning preserved.\"}");

        Assert.Equal(MeaningVerdict.Pass, result.Verdict);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Pass_with_material_issues_is_normalized_to_fail()
    {
        var result = MeaningGate.ParseAndNormalize("{\"verdict\": \"pass\", \"issues\": [\"drops negation\"], \"explanation\": \"\"}");

        Assert.Equal(MeaningVerdict.Fail, result.Verdict);
        Assert.Equal(["drops negation"], result.Issues);
    }

    [Fact]
    public void Fail_keeps_its_issues()
    {
        var result = MeaningGate.ParseAndNormalize("{\"verdict\": \"fail\", \"issues\": [\"wrong number\"], \"explanation\": \"Count differs.\"}");

        Assert.Equal(MeaningVerdict.Fail, result.Verdict);
        Assert.Equal(["wrong number"], result.Issues);
        Assert.Equal("Count differs.", result.Explanation);
    }

    [Fact]
    public void Verdict_is_case_insensitive()
    {
        var result = MeaningGate.ParseAndNormalize("{\"verdict\": \"PASS\", \"issues\": []}");

        Assert.Equal(MeaningVerdict.Pass, result.Verdict);
    }

    [Fact]
    public void JSON_wrapped_in_prose_is_extracted()
    {
        var result = MeaningGate.ParseAndNormalize("Here is my answer: {\"verdict\": \"pass\", \"issues\": []} Hope that helps }");

        Assert.Equal(MeaningVerdict.Pass, result.Verdict);
    }

    [Fact]
    public void Braces_inside_string_values_do_not_end_the_object_early()
    {
        var result = MeaningGate.ParseAndNormalize("{\"verdict\": \"fail\", \"issues\": [\"uses } in place of {\"], \"explanation\": \"x\"}");

        Assert.Equal(MeaningVerdict.Fail, result.Verdict);
        Assert.Equal(["uses } in place of {"], result.Issues);
    }

    [Fact]
    public void Unparseable_reply_fails_closed()
    {
        var result = MeaningGate.ParseAndNormalize("I think it is fine.");

        Assert.Equal(MeaningVerdict.Fail, result.Verdict);
        Assert.Equal(["unparseable meaning response"], result.Issues);
    }

    [Fact]
    public void Missing_verdict_fails_closed()
    {
        var result = MeaningGate.ParseAndNormalize("{\"issues\": []}");

        Assert.Equal(MeaningVerdict.Fail, result.Verdict);
    }

    [Fact]
    public void Non_array_issues_fail_closed()
    {
        var result = MeaningGate.ParseAndNormalize("{\"verdict\": \"pass\", \"issues\": \"none\"}");

        Assert.Equal(MeaningVerdict.Fail, result.Verdict);
    }

    [Fact]
    public void Error_result_carries_message_truncated_to_500()
    {
        var result = MeaningGate.ForError(new string('x', 800));

        Assert.Equal(MeaningVerdict.Error, result.Verdict);
        Assert.Equal(500, result.Explanation.Length);
    }

    [Fact]
    public void User_prompt_wraps_both_texts_in_delimiters()
    {
        string prompt = MeaningGate.BuildUserPrompt("Hi.", "Hola.", "en", "es");

        Assert.Contains("<source_text>\nHi.\n</source_text>", prompt);
        Assert.Contains("<candidate_translation>\nHola.\n</candidate_translation>", prompt);
    }

    [Fact]
    public void System_prompt_carries_the_judge_marker()
    {
        Assert.Contains(MeaningGate.MeaningMarker, MeaningGate.BuildSystemPrompt());
    }
}
