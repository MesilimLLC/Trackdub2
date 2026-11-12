using System.Security.Cryptography;
using System.Text.Json;

namespace Trackdub2.Pipeline;

/// <summary>
/// The effective QA settings for a run, plus the SHA-256 of the config file they came from. The hash goes into
/// each run's capture record so a result can be traced to the exact settings that produced it.
/// </summary>
public sealed record LoadedQaConfig(QaOptions Options, string Sha256);

/// <summary>
/// Reads the committed QA settings file. Every key is required and unknown keys are rejected, so a typo cannot
/// silently fall back to a default. Values are validated with <see cref="QaOptions.Validate"/>.
/// </summary>
public static class QaConfigLoader
{
    public const string MaxAttemptsKey = "maxAttempts";
    public const string ToleranceFractionKey = "toleranceFraction";
    public const string CharactersPerSecondKey = "charactersPerSecond";

    private static readonly string[] KnownKeys = [MaxAttemptsKey, ToleranceFractionKey, CharactersPerSecondKey];

    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    public static LoadedQaConfig Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Parse(File.ReadAllBytes(path));
    }

    public static LoadedQaConfig Parse(byte[] utf8Json)
    {
        ArgumentNullException.ThrowIfNull(utf8Json);

        int start = utf8Json.AsSpan().StartsWith(Utf8Bom) ? Utf8Bom.Length : 0;
        using JsonDocument document = JsonDocument.Parse(utf8Json.AsMemory(start));
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("QA config must be a JSON object.");
        }

        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (Array.IndexOf(KnownKeys, property.Name) < 0)
            {
                throw new InvalidDataException($"Unknown QA config key '{property.Name}'.");
            }
        }

        var options = new QaOptions
        {
            MaxAttempts = ReadInt(root, MaxAttemptsKey),
            ToleranceFraction = ReadDouble(root, ToleranceFractionKey),
            CharactersPerSecond = ReadDouble(root, CharactersPerSecondKey),
        };
        options.Validate();

        string hash = Convert.ToHexString(SHA256.HashData(utf8Json)).ToLowerInvariant();
        return new LoadedQaConfig(options, hash);
    }

    private static int ReadInt(JsonElement root, string key)
    {
        JsonElement value = RequireNumber(root, key);
        if (!value.TryGetInt32(out int result))
        {
            throw new InvalidDataException($"QA config key '{key}' must be a whole number.");
        }

        return result;
    }

    private static double ReadDouble(JsonElement root, string key)
    {
        JsonElement value = RequireNumber(root, key);
        if (!value.TryGetDouble(out double result))
        {
            throw new InvalidDataException($"QA config key '{key}' must be a number.");
        }

        return result;
    }

    private static JsonElement RequireNumber(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out JsonElement value))
        {
            throw new InvalidDataException($"QA config is missing key '{key}'.");
        }

        if (value.ValueKind != JsonValueKind.Number)
        {
            throw new InvalidDataException($"QA config key '{key}' must be a number.");
        }

        return value;
    }
}
