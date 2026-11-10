using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Trackdub2.Triage;

public sealed record ChangedFile(string Path, string Status, int Additions, int Deletions, string? Patch);

public sealed record PullRequestContext(string Title, string Body, string BaseRef, string HeadRef, IReadOnlyList<ChangedFile> Files);

public sealed record FailedJob(string Name, IReadOnlyList<string> FailedSteps, string Log);

public sealed record FailedRunContext(string WorkflowName, IReadOnlyList<FailedJob> Jobs);

/// <summary>Minimal GitHub REST client authenticated as a GitHub App installation.</summary>
public sealed class GitHubClient(HttpClient http, TriageOptions options, TimeProvider time)
{
    private const int MaxFilePages = 3;
    private const int MaxFailedJobs = 3;
    private readonly ConcurrentDictionary<long, (string Token, DateTimeOffset ExpiresAt)> _tokens = new();

    public async Task<PullRequestContext> GetPullRequestAsync(TriageJob job, CancellationToken ct)
    {
        string basePath = $"repos/{job.Owner}/{job.Repo}/pulls/{job.PullNumber}";
        using JsonDocument pr = await GetJsonAsync(job, basePath, ct);

        var files = new List<ChangedFile>();
        for (int page = 1; page <= MaxFilePages; page++)
        {
            using JsonDocument doc = await GetJsonAsync(job, $"{basePath}/files?per_page=100&page={page}", ct);
            foreach (JsonElement f in doc.RootElement.EnumerateArray())
            {
                files.Add(new ChangedFile(
                    f.GetProperty("filename").GetString() ?? "",
                    f.GetProperty("status").GetString() ?? "",
                    f.GetProperty("additions").GetInt32(),
                    f.GetProperty("deletions").GetInt32(),
                    f.TryGetProperty("patch", out JsonElement patch) ? patch.GetString() : null));
            }

            if (doc.RootElement.GetArrayLength() < 100)
            {
                break;
            }
        }

        JsonElement r = pr.RootElement;
        return new PullRequestContext(
            r.GetProperty("title").GetString() ?? "",
            r.GetProperty("body").GetString() ?? "",
            r.GetProperty("base").GetProperty("ref").GetString() ?? "",
            r.GetProperty("head").GetProperty("ref").GetString() ?? "",
            files);
    }

    public async Task<FailedRunContext> GetFailedRunAsync(TriageJob job, CancellationToken ct)
    {
        using JsonDocument jobsDoc = await GetJsonAsync(
            job, $"repos/{job.Owner}/{job.Repo}/actions/runs/{job.RunId}/jobs?filter=latest&per_page=50", ct);

        var failed = new List<FailedJob>();
        foreach (JsonElement j in jobsDoc.RootElement.GetProperty("jobs").EnumerateArray())
        {
            if (j.GetProperty("conclusion").GetString() != "failure")
            {
                continue;
            }

            var steps = new List<string>();
            if (j.TryGetProperty("steps", out JsonElement stepArray))
            {
                foreach (JsonElement step in stepArray.EnumerateArray())
                {
                    if (step.GetProperty("conclusion").GetString() == "failure")
                    {
                        steps.Add(step.GetProperty("name").GetString() ?? "");
                    }
                }
            }

            string log = await GetJobLogAsync(job, j.GetProperty("id").GetInt64(), ct);
            failed.Add(new FailedJob(j.GetProperty("name").GetString() ?? "", steps, log));
            if (failed.Count == MaxFailedJobs)
            {
                break;
            }
        }

        return new FailedRunContext(job.WorkflowName ?? "workflow", failed);
    }

    /// <summary>Edits the app's earlier comment carrying <paramref name="marker"/>, or posts a new one.</summary>
    public async Task UpsertCommentAsync(TriageJob job, string marker, string body, CancellationToken ct)
    {
        string issuePath = $"repos/{job.Owner}/{job.Repo}/issues/{job.PullNumber}";
        long? existingId = null;
        for (int page = 1; page <= 5 && existingId is null; page++)
        {
            using JsonDocument doc = await GetJsonAsync(job, $"{issuePath}/comments?per_page=100&page={page}", ct);
            foreach (JsonElement c in doc.RootElement.EnumerateArray())
            {
                // Only reuse comments written by an app, so a human pasting the marker can't redirect edits.
                JsonElement user = c.GetProperty("user");
                bool byApp = user.GetProperty("type").GetString() == "Bot" &&
                             (user.GetProperty("login").GetString() ?? "").EndsWith("[bot]", StringComparison.Ordinal);
                if (byApp && (c.GetProperty("body").GetString() ?? "").Contains(marker, StringComparison.Ordinal))
                {
                    existingId = c.GetProperty("id").GetInt64();
                    break;
                }
            }

            if (doc.RootElement.GetArrayLength() < 100)
            {
                break;
            }
        }

        using HttpRequestMessage request = existingId is long id
            ? await CreateRequestAsync(job, HttpMethod.Patch, $"repos/{job.Owner}/{job.Repo}/issues/comments/{id}", ct)
            : await CreateRequestAsync(job, HttpMethod.Post, $"{issuePath}/comments", ct);
        request.Content = JsonContent.Create(new { body });
        using HttpResponseMessage response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    private async Task<string> GetJobLogAsync(TriageJob job, long jobId, CancellationToken ct)
    {
        // GitHub answers with a redirect to a signed URL; HttpClient drops the Authorization
        // header when following it to another host, which is what the signed URL expects.
        using HttpRequestMessage request = await CreateRequestAsync(
            job, HttpMethod.Get, $"repos/{job.Owner}/{job.Repo}/actions/jobs/{jobId}/logs", ct);
        using HttpResponseMessage response = await http.SendAsync(request, ct);
        return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync(ct) : "(log unavailable)";
    }

    private async Task<JsonDocument> GetJsonAsync(TriageJob job, string path, CancellationToken ct)
    {
        using HttpRequestMessage request = await CreateRequestAsync(job, HttpMethod.Get, path, ct);
        using HttpResponseMessage response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(TriageJob job, HttpMethod method, string path, CancellationToken ct)
    {
        string token = await GetInstallationTokenAsync(job.InstallationId, ct);
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<string> GetInstallationTokenAsync(long installationId, CancellationToken ct)
    {
        if (_tokens.TryGetValue(installationId, out var cached) && cached.ExpiresAt > time.GetUtcNow().AddMinutes(5))
        {
            return cached.Token;
        }

        string jwt = GitHubAppJwt.Create(options.GitHubAppId, options.GitHubPrivateKey, time.GetUtcNow());
        using var request = new HttpRequestMessage(HttpMethod.Post, $"app/installations/{installationId}/access_tokens");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        using HttpResponseMessage response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(ct);
        using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        string token = doc.RootElement.GetProperty("token").GetString() ?? "";
        DateTimeOffset expiresAt = doc.RootElement.GetProperty("expires_at").GetDateTimeOffset();
        _tokens[installationId] = (token, expiresAt);
        return token;
    }
}
