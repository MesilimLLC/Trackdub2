using System.Threading.Channels;

namespace Trackdub2.Triage;

/// <summary>Bounded queue between the webhook endpoint, which must answer GitHub within 10 s, and the worker.</summary>
public sealed class TriageQueue
{
    private readonly Channel<TriageJob> _channel = Channel.CreateBounded<TriageJob>(
        new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    public bool TryEnqueue(TriageJob job) => _channel.Writer.TryWrite(job);

    public ChannelReader<TriageJob> Reader => _channel.Reader;
}

public sealed class TriageWorker(
    TriageQueue queue,
    GitHubClient github,
    TriageAgent agent,
    HourlyCallBudget budget,
    TriageOptions options,
    ILogger<TriageWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (TriageJob job in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Triage failed for {Owner}/{Repo}#{Pull} ({Kind})", job.Owner, job.Repo, job.PullNumber, job.Kind);
            }
        }
    }

    private async Task ProcessAsync(TriageJob job, CancellationToken ct)
    {
        if (!budget.TryTake())
        {
            logger.LogWarning("Hourly agent call limit reached; skipped {Owner}/{Repo}#{Pull} ({Kind})", job.Owner, job.Repo, job.PullNumber, job.Kind);
            return;
        }

        string prompt;
        string marker;
        string title;
        if (job.Kind == TriageKind.PullRequest)
        {
            PullRequestContext pr = await github.GetPullRequestAsync(job, ct);
            prompt = PromptBuilder.ForPullRequest(pr);
            marker = CommentFormatter.PullRequestMarker;
            title = "Pull request triage";
        }
        else
        {
            FailedRunContext run = await github.GetFailedRunAsync(job, ct);
            if (run.Jobs.Count == 0)
            {
                return;
            }

            prompt = PromptBuilder.ForFailedRun(run);
            marker = CommentFormatter.FailedRunMarker(run.WorkflowName);
            title = $"Failed run: {run.WorkflowName}";
        }

        string summary = await agent.RunAsync(prompt, ct);
        string body = CommentFormatter.Format(marker, title, summary, options.ModelDeployment);
        await github.UpsertCommentAsync(job, marker, body, ct);
        logger.LogInformation("Posted triage for {Owner}/{Repo}#{Pull} ({Kind})", job.Owner, job.Repo, job.PullNumber, job.Kind);
    }
}
