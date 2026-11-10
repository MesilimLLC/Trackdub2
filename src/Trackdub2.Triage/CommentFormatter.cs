using System.Text.RegularExpressions;

namespace Trackdub2.Triage;

public static partial class CommentFormatter
{
    public const string PullRequestMarker = "<!-- trackdub2-triage:pr -->";

    public static string FailedRunMarker(string workflowName) =>
        $"<!-- trackdub2-triage:ci:{MarkerSafe().Replace(workflowName, "_")} -->";

    public static string Format(string marker, string title, string summary, string model) =>
        $"{marker}\n### {title}\n\n{NeutralizeMentions(summary.Trim())}\n\n<sub>Automated triage by {model}. Check before acting on it.</sub>";

    // Model output is shaped by untrusted input; a zero-width space after @ stops it notifying people or teams.
    internal static string NeutralizeMentions(string text) => Mention().Replace(text, "@​");

    [GeneratedRegex(@"@(?=[A-Za-z0-9])")]
    private static partial Regex Mention();

    // Keeps the marker a valid HTML comment whatever the workflow is called.
    [GeneratedRegex(@"[^A-Za-z0-9 _.]")]
    private static partial Regex MarkerSafe();
}
