using System.Text;

namespace Trackdub2.Triage;

/// <summary>
/// Builds the agent's input. Pull request text and CI logs are attacker-controllable, so they are
/// wrapped in tags the instructions declare untrusted, and closing tags inside them are defused.
/// </summary>
public static class PromptBuilder
{
    public const int MaxBodyChars = 2_000;
    public const int MaxPatchChars = 40_000;
    public const int MaxLogChars = 24_000;

    public static string ForPullRequest(PullRequestContext pr)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Task: write the pull request triage comment.");
        sb.AppendLine("<untrusted_pull_request>");
        sb.AppendLine($"Title: {Defuse(pr.Title)}");
        sb.AppendLine($"Branches: {Defuse(pr.HeadRef)} -> {Defuse(pr.BaseRef)}");
        sb.AppendLine("Description:");
        sb.AppendLine(Defuse(Truncate(pr.Body, MaxBodyChars)));
        sb.AppendLine($"Changed files ({pr.Files.Count}):");
        foreach (ChangedFile f in pr.Files)
        {
            sb.AppendLine($"- {Defuse(f.Path)} ({f.Status}, +{f.Additions}/-{f.Deletions})");
        }

        sb.AppendLine("Diffs:");
        int budget = MaxPatchChars;
        int omitted = 0;
        foreach (ChangedFile f in pr.Files)
        {
            if (f.Patch is null)
            {
                continue;
            }

            if (f.Patch.Length > budget)
            {
                omitted++;
                continue;
            }

            sb.AppendLine($"--- {Defuse(f.Path)}");
            sb.AppendLine(Defuse(f.Patch));
            budget -= f.Patch.Length;
        }

        if (omitted > 0)
        {
            sb.AppendLine($"({omitted} file diffs omitted for length.)");
        }

        sb.AppendLine("</untrusted_pull_request>");
        return sb.ToString();
    }

    public static string ForFailedRun(FailedRunContext run)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Task: write the failed CI run triage comment.");
        sb.AppendLine("<untrusted_ci_log>");
        sb.AppendLine($"Workflow: {Defuse(run.WorkflowName)}");
        int perJob = run.Jobs.Count == 0 ? MaxLogChars : MaxLogChars / run.Jobs.Count;
        foreach (FailedJob job in run.Jobs)
        {
            sb.AppendLine($"Job: {Defuse(job.Name)}");
            sb.AppendLine($"Failed steps: {Defuse(string.Join(", ", job.FailedSteps))}");
            sb.AppendLine("Log tail:");
            sb.AppendLine(Defuse(Tail(job.Log, perJob)));
        }

        sb.AppendLine("</untrusted_ci_log>");
        return sb.ToString();
    }

    internal static string Truncate(string text, int max) =>
        text.Length <= max ? text : string.Concat(text.AsSpan(0, max), "\n(truncated)");

    internal static string Tail(string text, int max)
    {
        if (text.Length <= max)
        {
            return text;
        }

        int start = text.Length - max;
        int lineStart = text.IndexOf('\n', start);
        return "(earlier lines omitted)\n" + (lineStart >= 0 ? text[(lineStart + 1)..] : text[start..]);
    }

    internal static string Defuse(string text) =>
        text.Replace("</untrusted", "<\\/untrusted", StringComparison.OrdinalIgnoreCase);
}
