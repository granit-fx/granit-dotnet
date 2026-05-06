using Granit.Domain;

namespace Granit.Taxonomy.Domain;

/// <summary>
/// Polymorphic link between a <see cref="Tag"/> and a target aggregate root, identified
/// by its assembly-qualified type name (<see cref="TargetType"/>) and identifier
/// (<see cref="TargetId"/>) per ADR-054.
/// </summary>
/// <remarks>
/// <para>
/// There is no DB-level FK on <see cref="TargetId"/>: the target table varies per
/// consumer module (Documents, Parties, Activities, …). Orphan rows left after a
/// hard-delete of the target are cleaned up by the
/// <c>EntityDeletedEto</c> listener (T5.1) and a nightly sweep job (T5.2).
/// </para>
/// <para>
/// Uniqueness is enforced on <c>(TenantId, TagId, TargetType, TargetId)</c>; the
/// supporting indexes <c>(TenantId, TargetType, TargetId)</c> and
/// <c>(TenantId, TagId)</c> serve the "tags of entity X" and "things tagged X"
/// lookups respectively.
/// </para>
/// </remarks>
public sealed class TagAssignment : Entity, IMultiTenant
{
    /// <summary>Maximum length, in characters, of the polymorphic <see cref="TargetType"/> discriminator.</summary>
    public const int MaxTargetTypeLength = 256;

    private TagAssignment() { }

    /// <summary>Creates a new tag assignment.</summary>
    public static TagAssignment Create(
        Guid id,
        Guid? tenantId,
        Guid tagId,
        string targetType,
        Guid targetId,
        Guid assignedByUserId,
        DateTimeOffset assignedAt)
    {
        ValidateTargetType(targetType);
        if (targetId == Guid.Empty)
        {
            throw new ArgumentException("Target id cannot be empty.", nameof(targetId));
        }

        return new TagAssignment
        {
            Id = id,
            TenantId = tenantId,
            TagId = tagId,
            TargetType = targetType,
            TargetId = targetId,
            AssignedByUserId = assignedByUserId,
            AssignedAt = assignedAt,
        };
    }

    /// <summary>Identifier of the tenant that owns this assignment; <c>null</c> for host-global tags.</summary>
    public Guid? TenantId { get; private set; }

    /// <inheritdoc />
    Guid? IMultiTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    /// <summary>Identifier of the assigned <see cref="Tag"/>.</summary>
    public Guid TagId { get; private set; }

    /// <summary>Polymorphic discriminator (assembly-qualified type name of the target aggregate).</summary>
    public string TargetType { get; private set; } = string.Empty;

    /// <summary>Identifier of the target aggregate row.</summary>
    public Guid TargetId { get; private set; }

    /// <summary>UTC instant of assignment.</summary>
    public DateTimeOffset AssignedAt { get; private set; }

    /// <summary>Identifier of the user who created the assignment.</summary>
    public Guid AssignedByUserId { get; private set; }

    private static void ValidateTargetType(string targetType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetType);
        if (targetType.Length > MaxTargetTypeLength)
        {
            throw new ArgumentException(
                $"TargetType discriminator exceeds {MaxTargetTypeLength} characters.",
                nameof(targetType));
        }
    }
}
