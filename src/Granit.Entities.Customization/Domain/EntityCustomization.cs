using Granit.Domain;
using Granit.Entities.Customization.Domain.Deltas;

namespace Granit.Entities.Customization.Domain;

/// <summary>
/// Per-tenant Layer 1 customization for one entity layout (ADR-053). Holds the
/// ordered list of <see cref="LayoutDelta"/>s applied on top of the compiled
/// <c>EntityDefinitionDescriptor</c> when the manifest composer (B4) renders
/// the layout for a request.
/// </summary>
/// <remarks>
/// <para>
/// One row per <c>(TenantId, EntityName, LayoutKind)</c> — uniqueness enforced
/// by the EF companion (B2). Persistence is full-replace: a write replaces the
/// previous delta list in its entirety; revert-to-default is achieved by
/// deleting the row.
/// </para>
/// <para>
/// Validation against the compiled descriptor (does <c>FieldName</c> resolve?
/// is <c>GroupKey</c> a known group?) is performed at the endpoint boundary
/// (B3) before <see cref="Replace"/>; the aggregate itself only enforces the
/// shape-level invariants that do not require a descriptor lookup
/// (delta non-null, reorder anchor well-formed).
/// </para>
/// </remarks>
public sealed class EntityCustomization : FullAuditedAggregateRoot, IMultiTenant
{
    private readonly List<LayoutDelta> _deltas = [];

    // Parameterless constructor required by EF Core materializer (B2).
    private EntityCustomization() { }

    /// <summary>
    /// Creates a new customization row. Validation against the live descriptor
    /// is the caller's responsibility; this factory only enforces shape.
    /// </summary>
    public static EntityCustomization Create(
        Guid id,
        string entityName,
        LayoutKind layoutKind,
        IEnumerable<LayoutDelta> deltas,
        Guid? tenantId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        ArgumentNullException.ThrowIfNull(deltas);

        EntityCustomization customization = new()
        {
            Id = id,
            EntityName = entityName,
            LayoutKind = layoutKind,
            TenantId = tenantId,
        };
        customization.AssignDeltas(deltas);
        return customization;
    }

    /// <summary>Wire identifier of the entity these deltas customize (e.g. <c>"Granit.Parties.Party"</c>).</summary>
    public string EntityName { get; private set; } = string.Empty;

    /// <summary>The layout kind this customization applies to.</summary>
    public LayoutKind LayoutKind { get; private set; }

    /// <summary>Deltas in declaration order — application order matters.</summary>
    public IReadOnlyList<LayoutDelta> Deltas => _deltas;

    /// <inheritdoc/>
    public Guid? TenantId { get; private set; }

    /// <inheritdoc/>
    Guid? IMultiTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    /// <summary>
    /// Replaces the delta list in its entirety. The endpoint (B3) calls this
    /// after validating the new payload against the compiled descriptor.
    /// </summary>
    public void Replace(IEnumerable<LayoutDelta> deltas)
    {
        ArgumentNullException.ThrowIfNull(deltas);
        _deltas.Clear();
        AssignDeltas(deltas);
    }

    private void AssignDeltas(IEnumerable<LayoutDelta> deltas)
    {
        foreach (LayoutDelta delta in deltas)
        {
            if (delta is null)
            {
                throw new ArgumentException("Delta entries cannot be null.", nameof(deltas));
            }
            if (delta is ReorderDelta reorder && !reorder.IsAnchorWellFormed)
            {
                throw new ArgumentException(
                    $"ReorderDelta for '{reorder.FieldName}' must set exactly one of BeforeFieldName / AfterFieldName.",
                    nameof(deltas));
            }
            _deltas.Add(delta);
        }
    }
}
