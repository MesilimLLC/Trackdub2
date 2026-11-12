using Azure.Identity;
using Trackdub2.Triage;

namespace Trackdub2.Triage.Tests;

public class TriageAgentTests
{
    [Fact]
    public void ConstructsWithoutAProjectEndpoint() =>
        Assert.Null(Record.Exception(() => new TriageAgent(new TriageOptions(), new DefaultAzureCredential())));

    [Fact]
    public async Task RunFailsClearlyWithoutAProjectEndpoint()
    {
        var agent = new TriageAgent(new TriageOptions(), new DefaultAzureCredential());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => agent.RunAsync("prompt", CancellationToken.None));
        Assert.Contains("ProjectEndpoint", ex.Message);
    }
}
