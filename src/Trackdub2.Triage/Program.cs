using System.Net;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Trackdub2.Triage;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

TriageOptions options = builder.Configuration.GetSection("Triage").Get<TriageOptions>() ?? new TriageOptions();
builder.Services.AddSingleton(options);
builder.Services.AddSingleton(TimeProvider.System);

// Managed identity on App Service, the Azure CLI login locally. No keys.
builder.Services.AddSingleton<TokenCredential>(new DefaultAzureCredential());
builder.Services.AddSingleton(sp => new HourlyCallBudget(options.MaxAgentCallsPerHour, sp.GetRequiredService<TimeProvider>()));
builder.Services.AddSingleton<TriageQueue>();
builder.Services.AddSingleton<TriageAgent>();
builder.Services.AddSingleton(sp =>
{
    // Long-lived client with pooled-connection recycling, so the installation-token cache can live with it.
    var http = new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(15) })
    {
        BaseAddress = new Uri("https://api.github.com/"),
        Timeout = TimeSpan.FromSeconds(60),
    };
    http.DefaultRequestHeaders.UserAgent.ParseAdd("Trackdub2-Triage");
    http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    return new GitHubClient(http, options, sp.GetRequiredService<TimeProvider>());
});
builder.Services.AddHostedService<TriageWorker>();

if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddOpenTelemetry().UseAzureMonitor();
}

WebApplication app = builder.Build();

app.MapGet("/healthz", (TriageOptions o) => Results.Ok(new { status = "ok", githubConfigured = o.IsGitHubConfigured }));

app.MapPost("/github/webhook", async (HttpRequest request, TriageOptions o, TriageQueue queue, ILogger<Program> logger) =>
{
    if (!o.IsGitHubConfigured)
    {
        return Results.StatusCode((int)HttpStatusCode.ServiceUnavailable);
    }

    using var buffer = new MemoryStream();
    await request.Body.CopyToAsync(buffer);
    byte[] body = buffer.ToArray();

    if (!WebhookSignature.IsValid(o.GitHubWebhookSecret, body, request.Headers["X-Hub-Signature-256"]))
    {
        return Results.Unauthorized();
    }

    string? eventName = request.Headers["X-GitHub-Event"];
    if (eventName == "ping")
    {
        return Results.Ok();
    }

    TriageJob? job;
    try
    {
        using JsonDocument doc = JsonDocument.Parse(body);
        job = WebhookRouter.Route(eventName, doc.RootElement, o);
    }
    catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
    {
        logger.LogWarning(ex, "Unexpected {Event} payload shape", eventName);
        return Results.BadRequest();
    }

    if (job is null)
    {
        return Results.NoContent();
    }

    if (!queue.TryEnqueue(job))
    {
        logger.LogWarning("Triage queue full; dropped {Owner}/{Repo}#{Pull}", job.Owner, job.Repo, job.PullNumber);
    }

    return Results.Accepted();
});

app.Run();

public partial class Program;
