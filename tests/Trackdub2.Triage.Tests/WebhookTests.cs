using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Trackdub2.Triage;

namespace Trackdub2.Triage.Tests;

public class WebhookSignatureTests
{
    private const string Secret = "test-secret";
    private static readonly byte[] Body = Encoding.UTF8.GetBytes("{\"zen\":\"Keep it logically awesome.\"}");

    private static string Sign(string secret, byte[] body) =>
        "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body));

    [Fact]
    public void AcceptsCorrectSignature() =>
        Assert.True(WebhookSignature.IsValid(Secret, Body, Sign(Secret, Body)));

    [Fact]
    public void RejectsSignatureFromAnotherSecret() =>
        Assert.False(WebhookSignature.IsValid(Secret, Body, Sign("other", Body)));

    [Fact]
    public void RejectsTamperedBody() =>
        Assert.False(WebhookSignature.IsValid(Secret, Encoding.UTF8.GetBytes("{}"), Sign(Secret, Body)));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sha1=abcdef")]
    [InlineData("sha256=not-hex")]
    public void RejectsMissingOrMalformedHeader(string? header) =>
        Assert.False(WebhookSignature.IsValid(Secret, Body, header));

    [Fact]
    public void RejectsWhenSecretIsEmpty() =>
        Assert.False(WebhookSignature.IsValid("", Body, Sign("", Body)));
}

public class WebhookRouterTests
{
    private static readonly TriageOptions Options = new() { AllowedRepositories = ["MesilimLLC/Trackdub2"] };

    private static JsonElement PullRequestPayload(
        string action = "opened", bool draft = false, string userType = "User", string repo = "MesilimLLC/Trackdub2") =>
        JsonDocument.Parse($$"""
            {
              "action": "{{action}}",
              "installation": { "id": 42 },
              "repository": { "full_name": "{{repo}}", "name": "Trackdub2", "owner": { "login": "MesilimLLC" } },
              "pull_request": { "number": 7, "draft": {{(draft ? "true" : "false")}}, "user": { "type": "{{userType}}" } }
            }
            """).RootElement;

    private static JsonElement WorkflowRunPayload(string conclusion = "failure", bool withPullRequest = true) =>
        JsonDocument.Parse($$"""
            {
              "action": "completed",
              "installation": { "id": 42 },
              "repository": { "full_name": "MesilimLLC/Trackdub2", "name": "Trackdub2", "owner": { "login": "MesilimLLC" } },
              "workflow_run": {
                "id": 9001, "name": "CI", "conclusion": "{{conclusion}}",
                "pull_requests": [{{(withPullRequest ? "{ \"number\": 7 }" : "")}}]
              }
            }
            """).RootElement;

    [Theory]
    [InlineData("opened")]
    [InlineData("reopened")]
    [InlineData("ready_for_review")]
    [InlineData("synchronize")]
    public void RoutesPullRequestActions(string action)
    {
        TriageJob? job = WebhookRouter.Route("pull_request", PullRequestPayload(action), Options);
        Assert.Equal(new TriageJob(TriageKind.PullRequest, "MesilimLLC", "Trackdub2", 42, 7), job);
    }

    [Theory]
    [InlineData("closed")]
    [InlineData("labeled")]
    [InlineData("edited")]
    public void IgnoresOtherPullRequestActions(string action) =>
        Assert.Null(WebhookRouter.Route("pull_request", PullRequestPayload(action), Options));

    [Fact]
    public void IgnoresDraftPullRequests() =>
        Assert.Null(WebhookRouter.Route("pull_request", PullRequestPayload(draft: true), Options));

    [Fact]
    public void IgnoresBotAuthorsByDefault() =>
        Assert.Null(WebhookRouter.Route("pull_request", PullRequestPayload(userType: "Bot"), Options));

    [Fact]
    public void RoutesBotAuthorsWhenEnabled()
    {
        var options = new TriageOptions { AllowedRepositories = Options.AllowedRepositories, SkipBotAuthors = false };
        Assert.NotNull(WebhookRouter.Route("pull_request", PullRequestPayload(userType: "Bot"), options));
    }

    [Fact]
    public void IgnoresRepositoriesNotAllowed() =>
        Assert.Null(WebhookRouter.Route("pull_request", PullRequestPayload(repo: "someone/else"), Options));

    [Fact]
    public void RoutesFailedWorkflowRunWithPullRequest()
    {
        TriageJob? job = WebhookRouter.Route("workflow_run", WorkflowRunPayload(), Options);
        Assert.Equal(new TriageJob(TriageKind.FailedRun, "MesilimLLC", "Trackdub2", 42, 7, 9001, "CI"), job);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("cancelled")]
    public void IgnoresWorkflowRunsThatDidNotFail(string conclusion) =>
        Assert.Null(WebhookRouter.Route("workflow_run", WorkflowRunPayload(conclusion), Options));

    [Fact]
    public void IgnoresFailedRunsWithoutPullRequest() =>
        Assert.Null(WebhookRouter.Route("workflow_run", WorkflowRunPayload(withPullRequest: false), Options));

    [Fact]
    public void IgnoresOtherEvents() =>
        Assert.Null(WebhookRouter.Route("push", PullRequestPayload(), Options));

    [Fact]
    public void IgnoresPayloadWithoutInstallation() =>
        Assert.Null(WebhookRouter.Route("pull_request", JsonDocument.Parse("{\"repository\":{}}").RootElement, Options));
}
