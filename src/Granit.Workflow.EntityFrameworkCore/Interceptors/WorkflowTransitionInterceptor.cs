using System.Reflection;
using Granit.Domain;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Timing;
using Granit.Users;
using Granit.Workflow.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Granit.Workflow.EntityFrameworkCore.Interceptors;

/// <summary>
/// EF Core interceptor that detects workflow state changes on entities implementing
/// <see cref="IWorkflowStateful"/> and automatically creates <see cref="WorkflowTransitionRecord"/>
/// entries in the same transaction.
/// </summary>
/// <remarks>
/// <para>
/// ISO 27001 compliance: transition records are INSERT-only and immutable. They capture who
/// changed the state, when, and provide an optional comment field for regulatory justification
/// read from <see cref="WorkflowTransitionContext.Current"/>.
/// </para>
/// <para>
/// Also synchronizes <see cref="IPublishable.IsPublished"/> for entities implementing
/// both <see cref="IPublishable"/> and <see cref="IWorkflowStateful"/>
/// (keeps IsPublished in sync with the workflow status).
/// </para>
/// <para>
/// Registered as Scoped. Must be ordered after <c>AuditedEntityInterceptor</c> and
/// before <c>SoftDeleteInterceptor</c> in the interceptor chain.
/// </para>
/// </remarks>
public sealed class WorkflowTransitionInterceptor(
    ICurrentUserService currentUserService,
    IClock clock,
    IGuidGenerator guidGenerator,
    ICurrentTenant currentTenant) : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IClock _clock = clock;
    private readonly IGuidGenerator _guidGenerator = guidGenerator;
    private readonly ICurrentTenant _currentTenant = currentTenant;

    /// <inheritdoc/>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        DetectAndRecordTransitions(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        DetectAndRecordTransitions(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void DetectAndRecordTransitions(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        DateTimeOffset now = _clock.Now;
        string userId = _currentUserService.UserId ?? "system";

        // Sync IsPublished for versioned entities (Added + Modified)
        SyncPublishableFlag(context);

        // Detect state changes on Modified entities implementing IWorkflowStateful
        foreach (EntityEntry entry in context.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Modified))
        {
            if (entry.Entity is not IWorkflowStateful stateful)
            {
                continue;
            }

            Type entityType = entry.Entity.GetType();
            string statusPropertyName = GetStaticAbstract<string>(entityType, nameof(IWorkflowStateful.StatusPropertyName));
            string workflowEntityType = GetStaticAbstract<string>(entityType, nameof(IWorkflowStateful.WorkflowEntityType));

            PropertyEntry statusProperty = entry.Property(statusPropertyName);

            string? previousState = statusProperty.OriginalValue?.ToString();
            string? newState = statusProperty.CurrentValue?.ToString();

            if (string.Equals(previousState, newState, StringComparison.Ordinal))
            {
                continue;
            }

            WorkflowTransitionRecord record = new()
            {
                Id = _guidGenerator.Create(),
                EntityType = workflowEntityType,
                EntityId = stateful.GetWorkflowEntityId(),
                PreviousState = previousState ?? string.Empty,
                NewState = newState ?? string.Empty,
                TransitionedAt = now,
                TransitionedBy = userId,
                TenantId = _currentTenant.IsAvailable ? _currentTenant.Id : null,
                Comment = WorkflowTransitionContext.Current?.Comment,
            };

            context.Set<WorkflowTransitionRecord>().Add(record);
        }
    }

    private static void SyncPublishableFlag(DbContext context)
    {
        foreach (EntityEntry entry in context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            if (entry.Entity is not (IPublishable publishable and IWorkflowStateful))
            {
                continue;
            }

            Type entityType = entry.Entity.GetType();
            string statusPropertyName = GetStaticAbstract<string>(
                entityType, nameof(IWorkflowStateful.StatusPropertyName));
            object? statusValue = entry.Property(statusPropertyName).CurrentValue;

            publishable.IsPublished = statusValue is WorkflowLifecycleStatus status
                && status == WorkflowLifecycleStatus.Published;
        }
    }

    private static T GetStaticAbstract<T>(Type type, string propertyName)
    {
        // Implicit (public) implementation — e.g., `public static string StatusPropertyName => ...`
        PropertyInfo? property = type.GetProperty(
            propertyName, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

        if (property is null)
        {
            // Explicit interface implementation — CLR stores these as private static properties
            // with the fully-qualified interface name prefix (e.g., "Granit.Workflow.Domain.IWorkflowStateful.StatusPropertyName").
            // FlattenHierarchy does not include private statics from base types, so walk manually.
            string suffix = $".{propertyName}";
            for (Type? t = type; t is not null; t = t.BaseType)
            {
                property = Array.Find(
                    t.GetProperties(BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly), // NOSONAR S3011 — intentional: resolving explicit static abstract interface members via reflection
                    p => p.Name.EndsWith(suffix, StringComparison.Ordinal));

                if (property is not null)
                {
                    break;
                }
            }
        }

        return (T)(property?.GetValue(null)
            ?? throw new InvalidOperationException(
                $"Type '{type.FullName}' does not expose static property '{propertyName}' " +
                $"required by {nameof(IWorkflowStateful)}."));
    }
}
