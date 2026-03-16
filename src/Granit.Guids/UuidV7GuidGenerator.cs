namespace Granit.Guids;

/// <summary>
/// <see cref="IGuidGenerator"/> implementation using <see cref="Guid.CreateVersion7(DateTimeOffset)"/> (RFC 9562 UUID v7).
/// </summary>
/// <remarks>
/// UUIDv7 embeds a 48-bit Unix millisecond timestamp in the most-significant bits, making
/// generated GUIDs monotonically increasing within the same millisecond. This produces
/// efficient clustered index inserts on PostgreSQL, MySQL, and SQL Server 2019+.
/// <para>
/// <see cref="TimeProvider"/> is injected to allow deterministic timestamp control in tests.
/// </para>
/// </remarks>
public sealed class UuidV7GuidGenerator(TimeProvider timeProvider) : IGuidGenerator
{
    /// <inheritdoc />
    public Guid Create() => Guid.CreateVersion7(timeProvider.GetUtcNow());
}
