using System.Text.Json;
using Trackdub2.Pipeline;

namespace Trackdub2.Cli;

public static class CliRunner
{
    public const string DefaultConfigPath = "config/qa-config.json";

    public const int ExitOk = 0;
    public const int ExitConfigError = 1;
    public const int ExitUsageError = 2;

    private const string Usage = """
        Usage: trackdub2 [--config <path>]
          --config <path>  QA settings file (default: config/qa-config.json)
          -h, --help       Show this help
        """;

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        string path = DefaultConfigPath;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--config" when i + 1 < args.Length && !args[i + 1].StartsWith('-'):
                    path = args[++i];
                    break;
                case "--help" or "-h":
                    output.WriteLine(Usage);
                    return ExitOk;
                default:
                    error.WriteLine($"Unknown or incomplete argument '{args[i]}'.");
                    error.WriteLine(Usage);
                    return ExitUsageError;
            }
        }

        try
        {
            LoadedQaConfig loaded = QaConfigLoader.Load(path);
            WriteEffectiveSettings(path, loaded, output);
            return ExitOk;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException
                                       or InvalidDataException or ArgumentException)
        {
            error.WriteLine($"QA config '{path}' is invalid: {ex.Message}");
            return ExitConfigError;
        }
    }

    private static void WriteEffectiveSettings(string path, LoadedQaConfig loaded, TextWriter output)
    {
        output.WriteLine(FormattableString.Invariant($"QA config: {path}"));
        output.WriteLine(FormattableString.Invariant($"  sha256: {loaded.Sha256}"));
        output.WriteLine(FormattableString.Invariant($"  maxAttempts: {loaded.Options.MaxAttempts}"));
        output.WriteLine(FormattableString.Invariant($"  toleranceFraction: {loaded.Options.ToleranceFraction}"));
        output.WriteLine(FormattableString.Invariant($"  charactersPerSecond: {loaded.Options.CharactersPerSecond}"));
    }
}
