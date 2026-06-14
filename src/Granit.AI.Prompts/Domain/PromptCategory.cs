using Granit.Domain;

namespace Granit.AI.Prompts.Domain;

/// <summary>
/// A tenant-defined, free-form grouping for prompts in the catalogue (ADR-067). Defined by
/// administrators; a prompt may belong to several categories (many-to-many). The well-known
/// <see cref="GeneralName"/> category groups framework-seeded prompts that have no explicit category.
/// </summary>
public sealed class PromptCategory : FullAuditedAggregateRoot, IMultiTenant
{
    /// <summary>The well-known default category name for seeded / uncategorised prompts.</summary>
    public const string GeneralName = "General";

    private PromptCategory()
    {
    }

    /// <summary>Creates a tenant-defined category.</summary>
    public static PromptCategory Create(Guid id, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new PromptCategory { Id = id, Name = name, IsSystem = false };
    }

    /// <summary>
    /// Creates a framework-defined system category (e.g. <see cref="GeneralName"/>) that the tenant
    /// cannot delete.
    /// </summary>
    public static PromptCategory CreateSystem(Guid id, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new PromptCategory { Id = id, Name = name, IsSystem = true };
    }

    /// <summary>Display name.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Whether this is a framework-defined category (not deletable by the tenant).</summary>
    public bool IsSystem { get; private set; }

    /// <summary>Owning tenant; stamped by the interceptor.</summary>
    public Guid? TenantId { get; private set; }

    /// <inheritdoc/>
    Guid? IMultiTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    /// <summary>Renames the category.</summary>
    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }
}
