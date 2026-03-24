using Granit.Events;

namespace Granit.Encryption.Events;

/// <summary>
/// Domain event raised after a per-entity encryption key is permanently destroyed.
/// </summary>
/// <remarks>
/// In-process, transactional event. Handlers run within the same execution context.
/// Used by <see cref="CryptoShredding.ICryptoShreddingAuditRecorder"/> implementations
/// and metrics recording.
/// </remarks>
public sealed record CryptoShreddedEvent(
    string EntityType,
    string EntityId,
    DateTimeOffset ShreddedAt) : IDomainEvent;
