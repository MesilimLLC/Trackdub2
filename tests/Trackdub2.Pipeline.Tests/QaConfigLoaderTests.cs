using System.Security.Cryptography;
using System.Text;

namespace Trackdub2.Pipeline.Tests;

public class QaConfigLoaderTests
{
    private const string Valid = "{\"maxAttempts\": 3, \"toleranceFraction\": 0.1, \"charactersPerSecond\": 15}";

    private static byte[] Utf8(string json) => Encoding.UTF8.GetBytes(json);

    private static string RepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Trackdub2.slnx")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new InvalidOperationException("Repository root not found.");
    }

    [Fact]
    public void Valid_config_maps_to_options_and_hashes_the_bytes()
    {
        LoadedQaConfig loaded = QaConfigLoader.Parse(Utf8(Valid));

        Assert.Equal(3, loaded.Options.MaxAttempts);
        Assert.Equal(0.1, loaded.Options.ToleranceFraction);
        Assert.Equal(15.0, loaded.Options.CharactersPerSecond);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Utf8(Valid))).ToLowerInvariant(), loaded.Sha256);
    }

    [Fact]
    public void Committed_config_matches_the_built_in_defaults()
    {
        string path = Path.Combine(RepoRoot(), "config", "qa-config.json");

        LoadedQaConfig loaded = QaConfigLoader.Load(path);

        Assert.Equal(new QaOptions(), loaded.Options);
    }

    [Fact]
    public void Load_reads_from_disk()
    {
        string path = Path.Combine(Path.GetTempPath(), $"qa-config-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{\"maxAttempts\": 5, \"toleranceFraction\": 0.2, \"charactersPerSecond\": 12.5}");
        try
        {
            LoadedQaConfig loaded = QaConfigLoader.Load(path);

            Assert.Equal(5, loaded.Options.MaxAttempts);
            Assert.Equal(12.5, loaded.Options.CharactersPerSecond);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("{\"toleranceFraction\": 0.1, \"charactersPerSecond\": 15}", "maxAttempts")]
    [InlineData("{\"maxAttempts\": 3, \"charactersPerSecond\": 15}", "toleranceFraction")]
    [InlineData("{\"maxAttempts\": 3, \"toleranceFraction\": 0.1}", "charactersPerSecond")]
    public void Missing_key_is_rejected(string json, string missingKey)
    {
        var error = Assert.Throws<InvalidDataException>(() => QaConfigLoader.Parse(Utf8(json)));

        Assert.Contains(missingKey, error.Message);
    }

    [Fact]
    public void Unknown_key_is_rejected_so_a_typo_cannot_fall_back_to_a_default()
    {
        var error = Assert.Throws<InvalidDataException>(() =>
            QaConfigLoader.Parse(Utf8("{\"maxAttempts\": 3, \"toleranceFraction\": 0.1, \"charactersPerSecond\": 15, \"tolerence\": 0.2}")));

        Assert.Contains("tolerence", error.Message);
    }

    [Theory]
    [InlineData("{\"maxAttempts\": \"3\", \"toleranceFraction\": 0.1, \"charactersPerSecond\": 15}")]
    [InlineData("{\"maxAttempts\": 3.5, \"toleranceFraction\": 0.1, \"charactersPerSecond\": 15}")]
    [InlineData("{\"maxAttempts\": 3, \"toleranceFraction\": \"0.1\", \"charactersPerSecond\": 15}")]
    [InlineData("{\"maxAttempts\": 3, \"toleranceFraction\": 0.1, \"charactersPerSecond\": null}")]
    public void Wrong_value_type_is_rejected(string json)
    {
        Assert.Throws<InvalidDataException>(() => QaConfigLoader.Parse(Utf8(json)));
    }

    [Fact]
    public void Non_object_root_is_rejected()
    {
        Assert.Throws<InvalidDataException>(() => QaConfigLoader.Parse(Utf8("[3, 0.1, 15]")));
    }

    [Theory]
    [InlineData("{\"maxAttempts\": -1, \"toleranceFraction\": 0.1, \"charactersPerSecond\": 15}")]
    [InlineData("{\"maxAttempts\": 3, \"toleranceFraction\": -0.1, \"charactersPerSecond\": 15}")]
    [InlineData("{\"maxAttempts\": 3, \"toleranceFraction\": 0.1, \"charactersPerSecond\": 0}")]
    public void Out_of_range_values_fail_validation(string json)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => QaConfigLoader.Parse(Utf8(json)));
    }
}
