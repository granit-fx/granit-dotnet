namespace Granit.Guids;

/// <summary>
/// Selects the GUID generation strategy used by <see cref="IGuidGenerator"/>.
/// </summary>
public enum GuidStrategy
{
    /// <summary>
    /// UUID version 7 — time-ordered, monotonic, standard (RFC 9562).
    /// Uses <see cref="Guid.CreateVersion7(DateTimeOffset)"/>.
    /// Recommended for PostgreSQL, MySQL, and any database with clustered indexes on UUID columns.
    /// </summary>
    UuidV7,

    /// <summary>
    /// Legacy sequential GUID optimized per database engine byte ordering.
    /// Uses <see cref="SequentialGuidGenerator"/> with a configurable <see cref="SequentialGuidType"/>.
    /// Required for Oracle (<see cref="SequentialGuidType.SequentialAsBinary"/>) and
    /// pre-2019 SQL Server (<see cref="SequentialGuidType.SequentialAtEnd"/>).
    /// </summary>
    Sequential,

    /// <summary>
    /// Cryptographically random GUID — <see cref="Guid.NewGuid()"/>.
    /// No ordering guarantee. Use when sequential ordering is undesirable
    /// (e.g., public-facing identifiers where enumeration must be prevented).
    /// </summary>
    Random,
}
