namespace Granit.Guids.Options;

/// <summary>
/// Configuration options for the Guids module.
/// </summary>
public sealed class GuidGeneratorOptions
{
    /// <summary>
    /// GUID generation strategy. Defaults to <see cref="GuidStrategy.UuidV7"/>.
    /// </summary>
    public GuidStrategy Strategy { get; set; } = GuidStrategy.UuidV7;

    /// <summary>
    /// Sequential GUID type used when <see cref="Strategy"/> is <see cref="GuidStrategy.Sequential"/>.
    /// <c>null</c> defaults to <see cref="SequentialGuidType.SequentialAsString"/> (PostgreSQL/MySQL).
    /// </summary>
    public SequentialGuidType? DefaultSequentialGuidType { get; set; }

    /// <summary>
    /// Returns the configured sequential type or <see cref="SequentialGuidType.SequentialAsString"/>
    /// as default (optimized for PostgreSQL).
    /// </summary>
    public SequentialGuidType GetDefaultSequentialGuidType() => DefaultSequentialGuidType ?? SequentialGuidType.SequentialAsString;
}
