using Granit.Events;
using Granit.MultiTenancy;
using Granit.Parties.Diagnostics;
using Granit.Parties.Domain;
using Granit.Parties.Domain.ValueObjects;
using Granit.Parties.Events;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Granit.Parties.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IPartyReader"/> and <see cref="IPartyWriter"/>.
/// Eager-loads child collections (<see cref="Party.Addresses"/>, <see cref="Party.Emails"/>,
/// <see cref="Party.Phones"/>, <see cref="Party.ExternalMappings"/>) on every read because
/// downstream consumers (Invoicing → DefaultBillingAddress, payment provider → Stripe ID
/// lookup, notifications → PrimaryEmail) almost always need them.
/// </summary>
internal sealed class EfPartyStore(
    IDbContextFactory<PartiesDbContext> contextFactory,
    ICurrentTenant currentTenant,
    PartiesMetrics metrics)
    : EfStoreBase<Party, PartiesDbContext>(contextFactory, currentTenant),
      IPartyReader, IPartyWriter
{
    /// <summary>
    /// Overrides <see cref="EfStoreBase{TEntity,TContext}.Query(TContext)"/> to keep the
    /// multi-tenant query filter active in <i>both</i> tenant and host scope. The dual-use
    /// design treats <c>TenantId == null</c> as the host's own scope (its tenants-as-customers,
    /// vendors, internal staff) — leaking other tenants' parties into a host browser would be
    /// a privacy regression. The standard host-mode bypass remains available explicitly via
    /// <c>IDataFilter.Disable&lt;IMultiTenant&gt;()</c>.
    /// </summary>
    private static DbSet<Party> PartyQuery(PartiesDbContext db) => db.Parties;

    public Task<Party?> GetByIdAsync(PartyId id, CancellationToken cancellationToken = default) =>
        ReadAsync(
            db => PartyQuery(db)
                .Include(c => c.Addresses)
                .Include(c => c.Emails)
                .Include(c => c.Phones)
                .Include(c => c.ExternalMappings)
                .AsSplitQuery()
                .FirstOrDefaultAsync(c => c.Id == id.Value, cancellationToken),
            cancellationToken);

    public Task<Party?> GetByExternalIdAsync(
        string providerName, string externalId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        return ReadAsync(
            db => PartyQuery(db)
                .Include(c => c.Addresses)
                .Include(c => c.Emails)
                .Include(c => c.Phones)
                .Include(c => c.ExternalMappings)
                .AsSplitQuery()
                .FirstOrDefaultAsync(
                    c => c.ExternalMappings.Any(m =>
                        m.ProviderName == providerName && m.ExternalId == externalId),
                    cancellationToken),
            cancellationToken);
    }

    public Task<Party?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User identifier must not be empty.", nameof(userId));
        }

        return ReadAsync(
            db => PartyQuery(db)
                .Include(c => c.Addresses)
                .Include(c => c.Emails)
                .Include(c => c.Phones)
                .Include(c => c.ExternalMappings)
                .AsSplitQuery()
                .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken),
            cancellationToken);
    }

    public Task<IReadOnlyList<Party>> ListAsync(CancellationToken cancellationToken = default) =>
        ReadAsync<IReadOnlyList<Party>>(
            async db => await PartyQuery(db).ToListAsync(cancellationToken).ConfigureAwait(false),
            cancellationToken);

    public Task<IReadOnlyList<Party>> ListByRoleAsync(
        PartyRoles role, CancellationToken cancellationToken = default) =>
        ReadAsync<IReadOnlyList<Party>>(
            async db => await PartyQuery(db)
                .Where(c => (c.Roles & role) == role)
                .ToListAsync(cancellationToken).ConfigureAwait(false),
            cancellationToken);

    async Task IPartyWriter.AddAsync(Party party, CancellationToken cancellationToken)
    {
        await base.AddAsync(party, cancellationToken).ConfigureAwait(false);
        EmitMetrics(party);
    }

    /// <summary>
    /// Persists a party aggregate. <c>DbSet.Update()</c> marks the entire disconnected
    /// graph as Modified — including child entities (<c>PartyAddress</c>, <c>PartyEmail</c>,
    /// <c>PartyPhone</c>, <c>PartyExternalMapping</c>) freshly added in memory and not
    /// yet in the DB. We reconcile by reading existing child IDs and re-marking new ones
    /// as Added. Mirrors the workaround in <c>EfPlanWriter</c>.
    /// </summary>
    async Task IPartyWriter.UpdateAsync(Party party, CancellationToken cancellationToken)
    {
        await WriteAsync(async db =>
        {
            HashSet<Guid> existingAddressIds = await CollectChildIdsAsync<PartyAddress>(db, party.Id, cancellationToken).ConfigureAwait(false);
            HashSet<Guid> existingEmailIds = await CollectChildIdsAsync<PartyEmail>(db, party.Id, cancellationToken).ConfigureAwait(false);
            HashSet<Guid> existingPhoneIds = await CollectChildIdsAsync<PartyPhone>(db, party.Id, cancellationToken).ConfigureAwait(false);
            HashSet<Guid> existingMappingIds = await CollectChildIdsAsync<PartyExternalMapping>(db, party.Id, cancellationToken).ConfigureAwait(false);

            db.Set<Party>().Update(party);

            ReconcileNewChildren<PartyAddress>(db, existingAddressIds);
            ReconcileNewChildren<PartyEmail>(db, existingEmailIds);
            ReconcileNewChildren<PartyPhone>(db, existingPhoneIds);
            ReconcileNewChildren<PartyExternalMapping>(db, existingMappingIds);
        }, cancellationToken).ConfigureAwait(false);
        EmitMetrics(party);
    }

    /// <summary>
    /// Emits one OpenTelemetry counter increment per significant domain event raised by the
    /// aggregate during the current unit of work. Inspecting <see cref="AggregateRoot.DomainEvents"/>
    /// before the framework's event-dispatch interceptor clears the collection lets every
    /// security-relevant operation (suspend / archive / pseudonymise / external-mapping
    /// registration / tax-status change) become observable without a separate handler chain.
    /// </summary>
    private void EmitMetrics(Party party)
    {
        string? tenantId = party.TenantId?.ToString();
        foreach (IDomainEvent evt in party.DomainEvents)
        {
            switch (evt)
            {
                case PartyCreatedEvent: metrics.RecordCreated(tenantId); break;
                case PartyUpdatedEvent: metrics.RecordUpdated(tenantId); break;
                case PartySuspendedEvent: metrics.RecordSuspended(tenantId); break;
                case PartyActivatedEvent: metrics.RecordActivated(tenantId); break;
                case PartyArchivedEvent: metrics.RecordArchived(tenantId); break;
                case PartyPersonalDataPseudonymizedEvent: metrics.RecordPseudonymized(tenantId); break;
                case PartyExternalMappingAddedEvent m: metrics.RecordExternalMappingAdded(tenantId, m.ProviderName); break;
                case PartyRoleAddedEvent or PartyRoleRemovedEvent: metrics.RecordRoleChanged(tenantId); break;
                case PartyTaxStatusChangedEvent: metrics.RecordTaxStatusChanged(tenantId); break;
            }
        }
    }

    private static async Task<HashSet<Guid>> CollectChildIdsAsync<TChild>(
        PartiesDbContext db, Guid partyId, CancellationToken cancellationToken)
        where TChild : class
    {
        List<Guid> ids = await db.Set<TChild>()
            .AsNoTracking()
            .Where(e => EF.Property<Guid>(e, "PartyId") == partyId)
            .Select(e => EF.Property<Guid>(e, "Id"))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return ids.ToHashSet();
    }

    private static void ReconcileNewChildren<TChild>(PartiesDbContext db, HashSet<Guid> existingIds)
        where TChild : class
    {
        foreach (EntityEntry<TChild> entry in db.ChangeTracker.Entries<TChild>())
        {
            var id = (Guid)entry.Property("Id").CurrentValue!;
            if (entry.State == EntityState.Modified && !existingIds.Contains(id))
            {
                entry.State = EntityState.Added;

                // Cascade Added state to owned references (e.g., PartyAddress.Value).
                // Without this, the provider tries to UPDATE a row whose owner doesn't exist yet.
                foreach (ReferenceEntry reference in entry.References)
                {
                    EntityEntry? owned = reference.TargetEntry;
                    if (owned is not null
                        && owned.Metadata.IsOwned()
                        && owned.State == EntityState.Modified)
                    {
                        owned.State = EntityState.Added;
                    }
                }
            }
        }
    }
}
