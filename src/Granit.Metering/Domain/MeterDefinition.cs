using System.ComponentModel.DataAnnotations.Schema;
using Granit.Domain;
using Granit.Metering.Domain.ValueObjects;
using Granit.MultiTenancy;
using Granit.Workflow.Domain;

namespace Granit.Metering.Domain;

/// <summary>
/// Defines a usage meter (e.g., API calls, storage GB, messages sent).
/// </summary>
/// <remarks>
/// <para>
/// Meters follow a lifecycle managed by <see cref="WorkflowLifecycleStatus"/>:
/// <c>Draft → Published → Archived</c>. Only <see cref="WorkflowLifecycleStatus.Published"/>
/// meters accept ingestion. <c>Draft</c> lets admins author and review without polluting
/// production aggregates; <c>Archived</c> preserves history but rejects new events.
/// </para>
/// <para>
/// Meter events are recorded against a definition and aggregated into
/// <see cref="UsageAggregate"/> rollups by background jobs.
/// </para>
/// </remarks>
public sealed class MeterDefinition : AuditedAggregateRoot, IMultiTenant, IWorkflowStateful
{
    private MeterDefinition() { }

    /// <summary>
    /// Creates a new meter definition in <see cref="WorkflowLifecycleStatus.Draft"/> status.
    /// <paramref name="productId"/> is an optional soft reference (no SQL FK across
    /// modules) to a <c>Granit.Catalog.Product</c> — the catalog item this meter
    /// measures. The application layer is responsible for catalog deletion safety.
    /// </summary>
    public static MeterDefinition Create(
        Guid id,
        string name,
        string unit,
        AggregationType aggregationType,
        string? description = null,
        Guid? productId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);

        return new MeterDefinition
        {
            Id = id,
            Name = name,
            Unit = unit,
            AggregationType = aggregationType,
            Description = description,
            LifecycleStatus = WorkflowLifecycleStatus.Draft,
            ProductId = productId,
        };
    }

    /// <summary>Meter display name (e.g., "API Calls", "Storage").</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Unit of measure (e.g., "requests", "GB", "messages").</summary>
    public string Unit { get; private set; } = string.Empty;

    /// <summary>Optional description.</summary>
    public string? Description { get; private set; }

    /// <summary>How events are aggregated into rollups.</summary>
    public AggregationType AggregationType { get; private set; }

    /// <summary>Current lifecycle status (Draft, Published, Archived).</summary>
    public WorkflowLifecycleStatus LifecycleStatus { get; private set; }

    /// <summary>
    /// Computed compatibility alias for <see cref="LifecycleStatus"/> — <c>true</c> only when
    /// <see cref="WorkflowLifecycleStatus.Published"/>.
    /// </summary>
    /// <remarks>
    /// Kept for one release to ease migration of read-side consumers; new code should
    /// reason about <see cref="LifecycleStatus"/> directly. Not mapped to a column —
    /// the underlying <c>Activated</c> column is dropped by the
    /// <c>MeterDefinition_StatusFromActivated</c> migration in consuming apps.
    /// </remarks>
    [NotMapped]
    [Obsolete("Use LifecycleStatus instead. Removed in next major release.")]
    public bool Activated => LifecycleStatus == WorkflowLifecycleStatus.Published;

    /// <summary>
    /// Optional reference to a <c>Granit.Catalog.Product</c> identifier — the
    /// catalog item this meter measures. Soft reference (no SQL FK); enables
    /// invoice line item provenance and cross-module reporting (ORB-style audit
    /// chain: <c>event → meter → product → invoice line</c>).
    /// </summary>
    public Guid? ProductId { get; private set; }

    /// <summary>
    /// Re-attaches the meter to a different catalog product (or detaches by
    /// passing <c>null</c>). No lifecycle constraint — admins may re-target an
    /// active meter without rebuilding its history.
    /// </summary>
    public void SetProduct(Guid? productId) => ProductId = productId;

    /// <inheritdoc/>
    public Guid? TenantId { get; private set; }

    /// <summary>Explicit interface for interceptor injection.</summary>
    Guid? IMultiTenant.TenantId { get => TenantId; set => TenantId = value; }

    // ── IWorkflowStateful ──────────────────────────────────────────────

    static string IWorkflowStateful.StatusPropertyName => nameof(LifecycleStatus);

    static string IWorkflowStateful.WorkflowEntityType => "MeterDefinition";

    /// <inheritdoc />
    public string GetWorkflowEntityId() => Id.ToString();

    // ── Behavior methods ───────────────────────────────────────────────

    /// <summary>Updates the meter definition metadata. Only allowed in Draft status.</summary>
    public void Update(string name, string unit, string? description)
    {
        EnsureDraft();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);

        Name = name;
        Unit = unit;
        Description = description;
    }

    /// <summary>Publishes the meter, allowing it to accept ingestion events.</summary>
    public void Publish()
    {
        if (LifecycleStatus != WorkflowLifecycleStatus.Draft)
        {
            throw new InvalidOperationException(
                $"Meter '{Id}' is in '{LifecycleStatus}' status. Only Draft meters can be published.");
        }

        LifecycleStatus = WorkflowLifecycleStatus.Published;
    }

    /// <summary>Archives the meter. Existing aggregates are preserved; new events are rejected.</summary>
    public void Archive()
    {
        if (LifecycleStatus != WorkflowLifecycleStatus.Published)
        {
            throw new InvalidOperationException(
                $"Meter '{Id}' is in '{LifecycleStatus}' status. Only Published meters can be archived.");
        }

        LifecycleStatus = WorkflowLifecycleStatus.Archived;
    }

    /// <summary>
    /// Backward-compatible alias for <see cref="Archive"/>. Existing callers of
    /// <c>POST /metering/meters/{id}/deactivate</c> still funnel through here.
    /// </summary>
    [Obsolete("Use Archive() instead. Removed in next major release.")]
    public void Deactivate() => Archive();

    private void EnsureDraft()
    {
        if (LifecycleStatus != WorkflowLifecycleStatus.Draft)
        {
            throw new InvalidOperationException(
                $"Meter '{Id}' is in '{LifecycleStatus}' status. Only Draft meters can be modified.");
        }
    }
}
