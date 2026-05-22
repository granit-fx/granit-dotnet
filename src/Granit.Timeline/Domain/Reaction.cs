using Granit.Domain;

namespace Granit.Timeline.Domain;

/// <summary>
/// One reaction by a user on a <see cref="TimelineEntry"/>, identified by
/// any well-formed Unicode emoji sequence (see <see cref="EmojiValidator"/>).
/// Idempotency is enforced at the SQL level by a composite unique index on
/// <c>(EntryId, UserId, Emoji)</c>; the toggle endpoint (story C2) flips
/// between Add and Remove against that constraint.
/// </summary>
/// <remarks>
/// Cascade-deletes with the parent <see cref="TimelineEntry"/> — reactions
/// have no meaning without their entry. Soft-delete is intentionally NOT
/// supported: a removed reaction is removed (not preserved for audit), and
/// re-adding it is a fresh row.
/// </remarks>
public sealed class Reaction : CreationAuditedEntity, IMultiTenant
{
    // Parameterless constructor required by EF Core materializer.
    private Reaction() { }

    /// <summary>
    /// Creates a new reaction. Validates <paramref name="emoji"/> against
    /// <see cref="EmojiValidator"/>; malformed values throw at the factory
    /// boundary so they never hit persistence.
    /// </summary>
    public static Reaction Create(
        Guid id,
        Guid entryId,
        Guid userId,
        string emoji,
        DateTimeOffset createdAt,
        string createdBy,
        Guid? tenantId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emoji);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);
        if (entryId == Guid.Empty)
        {
            throw new ArgumentException("EntryId cannot be Guid.Empty.", nameof(entryId));
        }
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId cannot be Guid.Empty.", nameof(userId));
        }
        if (!EmojiValidator.IsValid(emoji))
        {
            throw new ArgumentException(
                $"Emoji '{emoji}' is not a valid Unicode emoji sequence (see EmojiValidator).",
                nameof(emoji));
        }

        return new Reaction
        {
            Id = id,
            EntryId = entryId,
            UserId = userId,
            Emoji = emoji,
            CreatedAt = createdAt,
            CreatedBy = createdBy,
            TenantId = tenantId,
        };
    }

    /// <summary>FK to the parent <see cref="TimelineEntry"/>.</summary>
    public Guid EntryId { get; private set; }

    /// <summary>The user who reacted.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Unicode emoji sequence (e.g. <c>"👍"</c>, <c>"👍🏽"</c>, <c>"❤️"</c>).</summary>
    public string Emoji { get; private set; } = string.Empty;

    /// <inheritdoc/>
    public Guid? TenantId { get; private set; }

    /// <inheritdoc/>
    Guid? IMultiTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }
}
