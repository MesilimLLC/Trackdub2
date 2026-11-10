using System.Text.Json;

namespace Trackdub2.Triage;

public enum TriageKind
{
    PullRequest,
    FailedRun,
}

public sealed record TriageJob(
    TriageKind Kind,
    string Owner,
    string Repo,
    long InstallationId,
    int PullNumber,
    long? RunId = null,
    string? WorkflowName = null);

/// <summary>Turns a GitHub webhook delivery into a triage job, or null when it should be ignored.</summary>
public static class WebhookRouter
{
    private static readonly HashSet<string> PullRequestActions =
        new(StringComparer.Ordinal) { "opened", "reopened", "ready_for_review", "synchronize" };

    public static TriageJob? Route(string? eventName, JsonElement payload, TriageOptions options)
    {
        if (!payload.TryGetProperty("repository", out JsonElement repository) ||
            !payload.TryGetProperty("installation", out JsonElement installation))
        {
            return null;
        }

        string fullName = repository.GetProperty("full_name").GetString() ?? "";
        if (!options.AllowedRepositories.Contains(fullName, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        string owner = repository.GetProperty("owner").GetProperty("login").GetString() ?? "";
        string repo = repository.GetProperty("name").GetString() ?? "";
        long installationId = installation.GetProperty("id").GetInt64();
        string action = payload.TryGetProperty("action", out JsonElement a) ? a.GetString() ?? "" : "";

        return eventName switch
        {
            "pull_request" => RoutePullRequest(payload, action, owner, repo, installationId, options),
            "workflow_run" => RouteWorkflowRun(payload, action, owner, repo, installationId),
            _ => null,
        };
    }

    private static TriageJob? RoutePullRequest(
        JsonElement payload, string action, string owner, string repo, long installationId, TriageOptions options)
    {
        if (!PullRequestActions.Contains(action))
        {
            return null;
        }

        JsonElement pr = payload.GetProperty("pull_request");
        if (pr.TryGetProperty("draft", out JsonElement draft) && draft.ValueKind == JsonValueKind.True)
        {
            return null;
        }

        if (options.SkipBotAuthors && IsBot(pr.GetProperty("user")))
        {
            return null;
        }

        return new TriageJob(TriageKind.PullRequest, owner, repo, installationId, pr.GetProperty("number").GetInt32());
    }

    private static TriageJob? RouteWorkflowRun(
        JsonElement payload, string action, string owner, string repo, long installationId)
    {
        if (action != "completed")
        {
            return null;
        }

        JsonElement run = payload.GetProperty("workflow_run");
        if (run.GetProperty("conclusion").GetString() != "failure")
        {
            return null;
        }

        // Only failures tied to a pull request get a comment; there is nowhere else to post.
        JsonElement pullRequests = run.GetProperty("pull_requests");
        if (pullRequests.GetArrayLength() == 0)
        {
            return null;
        }

        return new TriageJob(
            TriageKind.FailedRun,
            owner,
            repo,
            installationId,
            pullRequests[0].GetProperty("number").GetInt32(),
            run.GetProperty("id").GetInt64(),
            run.GetProperty("name").GetString());
    }

    private static bool IsBot(JsonElement user) =>
        user.TryGetProperty("type", out JsonElement type) && type.GetString() == "Bot";
}
