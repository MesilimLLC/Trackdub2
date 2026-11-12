using System.Security.Cryptography;
using System.Text;
using Trackdub2.Cli;

namespace Trackdub2.Cli.Tests;

public class CliRunnerTests
{
    private const string Valid = "{\"maxAttempts\": 3, \"toleranceFraction\": 0.1, \"charactersPerSecond\": 15}";

    private static string RepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Trackdub2.slnx")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static (int Exit, string Out, string Err) Invoke(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        int exit = CliRunner.Run(args, output, error);
        return (exit, output.ToString(), error.ToString());
    }

    [Fact]
    public void Committed_config_reports_effective_values_and_its_hash()
    {
        string path = Path.Combine(RepoRoot(), "config", "qa-config.json");
        string expectedHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

        var result = Invoke("--config", path);

        Assert.Equal(CliRunner.ExitOk, result.Exit);
        Assert.Empty(result.Err);
        Assert.Contains($"sha256: {expectedHash}", result.Out);
        Assert.Contains("maxAttempts: 3", result.Out);
        Assert.Contains("toleranceFraction: 0.1", result.Out);
        Assert.Contains("charactersPerSecond: 15", result.Out);
    }

    [Fact]
    public void Missing_file_exits_with_config_error_and_names_the_path()
    {
        string path = Path.Combine(Path.GetTempPath(), $"absent-{Guid.NewGuid():N}.json");

        var result = Invoke("--config", path);

        Assert.Equal(CliRunner.ExitConfigError, result.Exit);
        Assert.Contains(path, result.Err);
        Assert.Empty(result.Out);
    }

    [Fact]
    public void Unknown_key_exits_with_config_error_and_names_the_key()
    {
        using var config = new TempConfig("{\"maxAttempts\": 3, \"toleranceFraction\": 0.1, \"charactersPerSecond\": 15, \"tolerence\": 0.2}");

        var result = Invoke("--config", config.FilePath);

        Assert.Equal(CliRunner.ExitConfigError, result.Exit);
        Assert.Contains("tolerence", result.Err);
    }

    [Fact]
    public void Malformed_json_exits_with_config_error()
    {
        using var config = new TempConfig("{\"maxAttempts\": 3,");

        var result = Invoke("--config", config.FilePath);

        Assert.Equal(CliRunner.ExitConfigError, result.Exit);
        Assert.Empty(result.Out);
    }

    [Fact]
    public void Out_of_range_value_exits_with_config_error()
    {
        using var config = new TempConfig("{\"maxAttempts\": -1, \"toleranceFraction\": 0.1, \"charactersPerSecond\": 15}");

        var result = Invoke("--config", config.FilePath);

        Assert.Equal(CliRunner.ExitConfigError, result.Exit);
    }

    [Fact]
    public void Valid_temp_config_is_reported_with_its_own_values()
    {
        using var config = new TempConfig("{\"maxAttempts\": 5, \"toleranceFraction\": 0.2, \"charactersPerSecond\": 12.5}");

        var result = Invoke("--config", config.FilePath);

        Assert.Equal(CliRunner.ExitOk, result.Exit);
        Assert.Contains("maxAttempts: 5", result.Out);
        Assert.Contains("toleranceFraction: 0.2", result.Out);
        Assert.Contains("charactersPerSecond: 12.5", result.Out);
    }

    [Fact]
    public void Unknown_argument_exits_with_usage_error()
    {
        var result = Invoke("--max-attempts", "5");

        Assert.Equal(CliRunner.ExitUsageError, result.Exit);
        Assert.Contains("--max-attempts", result.Err);
    }

    [Fact]
    public void Config_flag_without_a_value_exits_with_usage_error()
    {
        var result = Invoke("--config");

        Assert.Equal(CliRunner.ExitUsageError, result.Exit);
    }

    [Fact]
    public void Help_prints_usage_and_succeeds()
    {
        var result = Invoke("--help");

        Assert.Equal(CliRunner.ExitOk, result.Exit);
        Assert.Contains("--config", result.Out);
    }

    private sealed class TempConfig : IDisposable
    {
        public TempConfig(string json)
        {
            FilePath = Path.Combine(Path.GetTempPath(), $"qa-config-{Guid.NewGuid():N}.json");
            File.WriteAllText(FilePath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        public string FilePath { get; }

        public void Dispose() => File.Delete(FilePath);
    }
}
