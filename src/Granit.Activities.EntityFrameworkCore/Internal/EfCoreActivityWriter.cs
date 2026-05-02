using Granit.Activities.Abstractions;
using Granit.Activities.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Activities.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IActivityWriter"/>. Loads the
/// targeted aggregate, invokes the matching behavior on <see cref="Activity"/>,
/// and persists in a single SaveChanges call so the lifecycle interceptors
/// emit the right Created / Updated event for the change.
/// </summary>
internal sealed class EfCoreActivityWriter(
    IDbContextFactory<ActivitiesDbContext> contextFactory,
    IActivityRegistry registry) : IActivityWriter
{
    public async Task<Activity> CreateAsync(
        string entityType,
        Guid entityId,
        string type,
        Guid assignedToUserId,
        DateTimeOffset dueAt,
        Guid? createdByUserId = null,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        await using ActivitiesDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var activity = Activity.Create(
            id: Guid.CreateVersion7(),
            entityType: entityType,
            entityId: entityId,
            type: type,
            assignedToUserId: assignedToUserId,
            dueAt: dueAt,
            registry: registry,
            createdByUserId: createdByUserId,
            description: description);

        context.Activities.Add(activity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return activity;
    }

    public Task CompleteAsync(Guid activityId, Guid completedByUserId, DateTimeOffset at, CancellationToken cancellationToken = default) =>
        MutateAsync(activityId, a => a.Complete(completedByUserId, at), cancellationToken);

    public Task CancelAsync(Guid activityId, Guid cancelledByUserId, DateTimeOffset at, CancellationToken cancellationToken = default) =>
        MutateAsync(activityId, a => a.Cancel(cancelledByUserId, at), cancellationToken);

    public Task ReassignAsync(Guid activityId, Guid newAssigneeUserId, CancellationToken cancellationToken = default) =>
        MutateAsync(activityId, a => a.Reassign(newAssigneeUserId), cancellationToken);

    public Task RescheduleAsync(Guid activityId, DateTimeOffset newDueAt, CancellationToken cancellationToken = default) =>
        MutateAsync(activityId, a => a.Reschedule(newDueAt), cancellationToken);

    private async Task MutateAsync(Guid activityId, Action<Activity> mutate, CancellationToken cancellationToken)
    {
        await using ActivitiesDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        Activity? activity = await context.Activities
            .FirstOrDefaultAsync(a => a.Id == activityId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Activity '{activityId}' not found.");

        mutate(activity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
