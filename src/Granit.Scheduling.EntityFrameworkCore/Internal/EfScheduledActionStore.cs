using Granit.MultiTenancy;
using Granit.Persistence;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Scheduling.Domain;
using Granit.Scheduling.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Granit.Scheduling.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IScheduledActionReader"/> and
/// <see cref="IScheduledActionWriter"/>.
/// </summary>
/// <remarks>
/// Registered as <b>Scoped</b>. <c>AddGranitDbContext</c> registers
/// <see cref="IDbContextFactory{TContext}"/> as Scoped (interceptors depend on
/// <c>ICurrentTenant</c> and <c>ICurrentUser</c>); this store must therefore also
/// be Scoped to avoid captive dependency violations.
/// </remarks>
internal sealed class EfScheduledActionStore(
    IDbContextFactory<SchedulingDbContext> contextFactory,
    ICurrentTenant currentTenant)
    : EfStoreBase<ScheduledAction, SchedulingDbContext>(contextFactory, currentTenant),
      IScheduledActionReader, IScheduledActionWriter
{
    /// <inheritdoc/>
    public Task<ScheduledAction?> GetByIdAsync(
        ScheduledActionId id,
        CancellationToken cancellationToken = default) =>
        FindByIdAsync(id.Value, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<ScheduledAction>> GetOverduePendingAsync(
        DateTimeOffset overdueThreshold,
        CancellationToken cancellationToken = default) =>
        ListAsync(
            Spec.For<ScheduledAction>()
                .Where(a => a.Status == ScheduledActionStatus.Pending && a.ExecuteAt < overdueThreshold),
            cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<ScheduledAction>> GetByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken = default) =>
        ListAsync(
            Spec.For<ScheduledAction>()
                .Where(a => a.CorrelationId == correlationId),
            cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<ScheduledAction>> GetByStatusAsync(
        ScheduledActionStatus? status,
        CancellationToken cancellationToken = default) =>
        status.HasValue
            ? ListAsync(
                Spec.For<ScheduledAction>().Where(a => a.Status == status.Value),
                cancellationToken)
            : ListAsync(
                Spec.For<ScheduledAction>(),
                cancellationToken);

    /// <inheritdoc/>
    Task IScheduledActionWriter.AddAsync(
        ScheduledAction action,
        CancellationToken cancellationToken) =>
        base.AddAsync(action, cancellationToken);

    /// <inheritdoc/>
    Task IScheduledActionWriter.UpdateAsync(
        ScheduledAction action,
        CancellationToken cancellationToken) =>
        base.UpdateAsync(action, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> TryClaimForExecutionAsync(
        ScheduledActionId id,
        CancellationToken cancellationToken = default) =>
        WriteAsync(async db =>
        {
            int affected = await db.ScheduledActions
                .Where(a => a.Id == id.Value && a.Status == ScheduledActionStatus.Pending)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(a => a.Status, ScheduledActionStatus.Processing),
                    cancellationToken)
                .ConfigureAwait(false);

            return affected > 0;
        }, cancellationToken);
}
