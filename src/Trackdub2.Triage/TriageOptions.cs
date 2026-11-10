namespace Trackdub2.Triage;

/// <summary>Configuration bound from the "Triage" section (App Service settings use Triage__Name).</summary>
public sealed class TriageOptions
{
    public string ProjectEndpoint { get; set; } = "";
    public string ModelDeployment { get; set; } = "gpt-6-luna";
    public string GitHubAppId { get; set; } = "";
    public string GitHubPrivateKey { get; set; } = "";
    public string GitHubWebhookSecret { get; set; } = "";
    public string[] AllowedRepositories { get; set; } = [];
    public int MaxAgentCallsPerHour { get; set; } = 30;
    public bool SkipBotAuthors { get; set; } = true;

    // An App Service Key Vault reference that can't resolve (secret not created yet, or no access)
    // is passed through as the literal reference text, so treat that the same as missing.
    public bool IsGitHubConfigured =>
        IsSet(GitHubAppId) && IsSet(GitHubPrivateKey) && IsSet(GitHubWebhookSecret);

    private static bool IsSet(string value) =>
        !string.IsNullOrWhiteSpace(value) && !value.StartsWith("@Microsoft.KeyVault(", StringComparison.OrdinalIgnoreCase);
}
