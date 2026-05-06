using Granit.Domain;

namespace Granit.Taxonomy.Domain;

/// <summary>
/// Polymorphic link between a <see cref="Category"/> and a target aggregate root —
/// single-assignment per ADR-054 (one category per <c>(TargetType, TargetId)</c>,
/// opposite of <see cref="TagAssignment"/> which is many-to-many).
/// </summary>
public sealed class CategoryAssignment : Entity, IMultiTenant
{
    /// <summary>Maximum length, in characters, of the polymorphic <see cref="TargetType"/> discriminator.</summary>
    public const int MaxTargetTypeLength = 256;

    private CategoryAssignment() { }

    /// <summary>Creates a new category assignment.</summary>
    public static CategoryAssignment Create(
        Guid id,
        Guid? tenantId,
        Guid categoryId,
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

        return new CategoryAssignment
        {
            Id = id,
            TenantId = tenantId,
            CategoryId = categoryId,
            TargetType = targetType,
            TargetId = targetId,
            AssignedByUserId = assignedByUserId,
            AssignedAt = assignedAt,
        };
    }

    /// <summary>Replaces the assigned category. Bumps timestamp + assigning user.</summary>
    public void ChangeCategory(Guid newCategoryId, Guid assignedByUserId, DateTimeOffset assignedAt)
    {
        if (newCategoryId == Guid.Empty)
        {
            throw new ArgumentException("Category id cannot be empty.", nameof(newCategoryId));
        }
        CategoryId = newCategoryId;
        AssignedByUserId = assignedByUserId;
        AssignedAt = assignedAt;
    }

    /// <summary>Identifier of the tenant that owns this assignment; <c>null</c> for host-global.</summary>
    public Guid? TenantId { get; private set; }

    /// <inheritdoc />
    Guid? IMultiTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    /// <summary>Identifier of the assigned <see cref="Category"/>.</summary>
    public Guid CategoryId { get; private set; }

    /// <summary>Polymorphic discriminator (assembly-qualified type name of the target aggregate).</summary>
    public string TargetType { get; private set; } = string.Empty;

    /// <summary>Identifier of the target aggregate row.</summary>
    public Guid TargetId { get; private set; }

    /// <summary>UTC instant of assignment / re-assignment.</summary>
    public DateTimeOffset AssignedAt { get; private set; }

    /// <summary>Identifier of the user who created or last changed the assignment.</summary>
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
