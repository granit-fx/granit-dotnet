using Granit.Persistence;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Granit.Subscriptions.EntityFrameworkCore.Internal;

internal sealed class EfSubscriptionReader(
    IDbContextFactory<SubscriptionsDbContext> contextFactory)
    : EfStoreBase<Subscription, SubscriptionsDbContext>(contextFactory),
      ISubscriptionReader
{
    public Task<Subscription?> GetByIdAsync(SubscriptionId id, CancellationToken cancellationToken = default) =>
        FindByIdAsync(id.Value, cancellationToken);

    public Task<Subscription?> GetActiveForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        FirstOrDefaultAsync(
            s => s.TenantId == tenantId &&
                 (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trial),
            cancellationToken);

    public Task<IReadOnlyList<Subscription>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        ListAsync(
            Spec.For<Subscription>().Where(s => s.TenantId == tenantId),
            cancellationToken);

    public Task<IReadOnlyList<Subscription>> GetExpiringTrialsAsync(
        DateTimeOffset threshold, CancellationToken cancellationToken = default) =>
        ListAsync(
            Spec.For<Subscription>().Where(s =>
                s.Status == SubscriptionStatus.Trial &&
                s.TrialEndsAt != null &&
                s.TrialEndsAt <= threshold),
            cancellationToken);

    public Task<IReadOnlyList<Subscription>> GetAtPeriodEndAsync(
        DateTimeOffset threshold, CancellationToken cancellationToken = default) =>
        ListAsync(
            Spec.For<Subscription>().Where(s =>
                s.Status == SubscriptionStatus.Active &&
                s.CurrentPeriodEnd <= threshold),
            cancellationToken);

    public Task<IReadOnlyList<Subscription>> GetPendingCancelAtPeriodEndAsync(
        DateTimeOffset now, CancellationToken cancellationToken = default) =>
        ListAsync(
            Spec.For<Subscription>().Where(s =>
                s.CancelAtPeriodEnd &&
                s.Status == SubscriptionStatus.Active &&
                s.CurrentPeriodEnd <= now),
            cancellationToken);
}
