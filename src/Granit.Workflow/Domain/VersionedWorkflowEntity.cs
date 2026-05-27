using Granit.Domain;

namespace Granit.Workflow.Domain;

/// <summary>
/// Base class for entities combining versioned history with a workflow lifecycle.
/// This is the "Case 3" entity: versioning + workflow for document-like entities.
/// </summary>
/// <remarks>
/// <para>
/// Inherits from <see cref="AuditedAggregateRoot"/> for audit tracking and domain/integration
/// event support. Implements both <see cref="IVersionedEntity"/> and <see cref="IWorkflowStateful"/>
/// to integrate with the global query filter system and the
/// <c>WorkflowTransitionInterceptor</c> audit trail.
/// </para>
/// <para>
/// <see cref="IsPublished"/> is kept in sync with <see cref="LifecycleStatus"/> by the
/// <c>WorkflowTransitionInterceptor</c> — set to <c>true</c> only when
/// <see cref="LifecycleStatus"/> is <see cref="WorkflowLifecycleStatus.Published"/>.
/// </para>
/// <para>
/// <see cref="IVersioned.Version"/> is auto-incremented by <c>VersioningInterceptor</c>
/// on insert.
/// </para>
/// <para>
/// For pure versioning without workflow, implement <see cref="IVersioned"/> directly.
/// For pure workflow without versioning, implement <see cref="IWorkflowStateful"/> directly.
/// </para>
/// <para>
/// Derived classes must provide <see cref="IWorkflowStateful.WorkflowEntityType"/>
/// by implementing the static abstract member explicitly.
/// </para>
/// </remarks>
public abstract class VersionedWorkflowEntity : AuditedAggregateRoot, IVersionedEntity, IWorkflowStateful, IConcurrencyAware
{
    /// <summary>
    /// Initializes a new instance for EF Core materialization.
    /// </summary>
    protected VersionedWorkflowEntity()
    {
    }

    /// <inheritdoc/>
    public Guid VersionId { get; private set; }

    /// <inheritdoc/>
    public int Version { get; private set; }

    /// <inheritdoc/>
    public WorkflowLifecycleStatus LifecycleStatus { get; private set; }

    /// <inheritdoc/>
    public bool IsPublished { get; private set; }

    /// <summary>
    /// Optimistic-concurrency token (ADR-061). Auto-managed by <c>ConcurrencyStampInterceptor</c>
    /// and configured as an EF Core concurrency token by <c>ApplyGranitConventions</c>. Guards
    /// every derived workflow entity against two concurrent lifecycle transitions racing.
    /// </summary>
    public string ConcurrencyStamp { get; private set; } = string.Empty;

    // Explicit interface implementations for interceptor write access.
    // VersioningInterceptor writes VersionId/Version via IVersioned cast.
    // WorkflowTransitionInterceptor writes IsPublished via IPublishable cast.
    // EF Core ChangeTracker bypasses C# setters entirely.

    /// <inheritdoc/>
    Guid IVersioned.VersionId { get => VersionId; set => VersionId = value; }

    /// <inheritdoc/>
    int IVersioned.Version { get => Version; set => Version = value; }

    /// <inheritdoc/>
    WorkflowLifecycleStatus IVersionedEntity.LifecycleStatus { get => LifecycleStatus; set => LifecycleStatus = value; }

    /// <inheritdoc/>
    bool IPublishable.IsPublished { get => IsPublished; set => IsPublished = value; }

    /// <inheritdoc/>
    string IConcurrencyAware.ConcurrencyStamp { get => ConcurrencyStamp; set => ConcurrencyStamp = value; }

    /// <inheritdoc/>
    static string IWorkflowStateful.StatusPropertyName => nameof(LifecycleStatus);

    /// <summary>
    /// Logical entity type name for the audit trail. Must be overridden by derived classes.
    /// </summary>
    static string IWorkflowStateful.WorkflowEntityType =>
        throw new NotSupportedException(
            "Derived classes must implement IWorkflowStateful.WorkflowEntityType explicitly.");

    /// <summary>
    /// Transitions the lifecycle status and keeps <see cref="IsPublished"/> in sync.
    /// Subtypes should expose domain-specific behavior methods (e.g., <c>Publish()</c>,
    /// <c>Archive()</c>) that call this method.
    /// </summary>
    /// <param name="status">The new lifecycle status.</param>
    /// <remarks>
    /// The <c>WorkflowTransitionInterceptor</c> also syncs <see cref="IsPublished"/>
    /// during <c>SaveChanges</c>, but setting it here ensures the entity is self-consistent
    /// immediately after the domain method call — important for InMemory test scenarios
    /// where interceptors do not run.
    /// </remarks>
    protected void SetLifecycleStatus(WorkflowLifecycleStatus status)
    {
        LifecycleStatus = status;
        ((IPublishable)this).IsPublished = status == WorkflowLifecycleStatus.Published;
    }

    /// <inheritdoc/>
    public virtual string GetWorkflowEntityId() => Id.ToString();
}
