namespace Trackdub2.Pipeline;

public sealed record QaOptions
{
    public const int DefaultMaxAttempts = 3;
    public const double DefaultToleranceFraction = 0.10;
    public const double DefaultCharactersPerSecond = 15.0;

    public int MaxAttempts { get; init; } = DefaultMaxAttempts;

    public double ToleranceFraction { get; init; } = DefaultToleranceFraction;

    public double CharactersPerSecond { get; init; } = DefaultCharactersPerSecond;

    public void Validate()
    {
        if (MaxAttempts < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxAttempts), "Must be zero or more.");
        }

        if (ToleranceFraction < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ToleranceFraction), "Must be zero or more.");
        }

        if (CharactersPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(CharactersPerSecond), "Must be positive.");
        }
    }
}
