using Granit.Contacts.Domain;
using Granit.Contacts.Domain.ValueObjects;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Granit.Contacts.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IContactReader"/> and <see cref="IContactWriter"/>.
/// Eager-loads child collections (<see cref="Contact.Addresses"/>, <see cref="Contact.Emails"/>,
/// <see cref="Contact.Phones"/>, <see cref="Contact.ExternalMappings"/>) on every read because
/// downstream consumers (Invoicing → DefaultBillingAddress, payment provider → Stripe ID
/// lookup, notifications → PrimaryEmail) almost always need them.
/// </summary>
internal sealed class EfContactStore(
    IDbContextFactory<ContactsDbContext> contextFactory,
    ICurrentTenant currentTenant)
    : EfStoreBase<Contact, ContactsDbContext>(contextFactory, currentTenant),
      IContactReader, IContactWriter
{
    /// <summary>
    /// Overrides <see cref="EfStoreBase{TEntity,TContext}.Query(TContext)"/> to keep the
    /// multi-tenant query filter active in <i>both</i> tenant and host scope. The dual-use
    /// design treats <c>TenantId == null</c> as the host's own scope (its tenants-as-customers,
    /// vendors, internal staff) — leaking other tenants' contacts into a host browser would be
    /// a privacy regression. The standard host-mode bypass remains available explicitly via
    /// <c>IDataFilter.Disable&lt;IMultiTenant&gt;()</c>.
    /// </summary>
    private static DbSet<Contact> ContactQuery(ContactsDbContext db) => db.Contacts;

    public Task<Contact?> GetByIdAsync(ContactId id, CancellationToken cancellationToken = default) =>
        ReadAsync(
            db => ContactQuery(db)
                .Include(c => c.Addresses)
                .Include(c => c.Emails)
                .Include(c => c.Phones)
                .Include(c => c.ExternalMappings)
                .FirstOrDefaultAsync(c => c.Id == id.Value, cancellationToken),
            cancellationToken);

    public Task<Contact?> GetByExternalIdAsync(
        string providerName, string externalId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        return ReadAsync(
            db => ContactQuery(db)
                .Include(c => c.Addresses)
                .Include(c => c.Emails)
                .Include(c => c.Phones)
                .Include(c => c.ExternalMappings)
                .FirstOrDefaultAsync(
                    c => c.ExternalMappings.Any(m =>
                        m.ProviderName == providerName && m.ExternalId == externalId),
                    cancellationToken),
            cancellationToken);
    }

    public Task<Contact?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User identifier must not be empty.", nameof(userId));
        }

        return ReadAsync(
            db => ContactQuery(db)
                .Include(c => c.Addresses)
                .Include(c => c.Emails)
                .Include(c => c.Phones)
                .Include(c => c.ExternalMappings)
                .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken),
            cancellationToken);
    }

    public Task<IReadOnlyList<Contact>> ListAsync(CancellationToken cancellationToken = default) =>
        ReadAsync<IReadOnlyList<Contact>>(
            async db => await ContactQuery(db).ToListAsync(cancellationToken).ConfigureAwait(false),
            cancellationToken);

    public Task<IReadOnlyList<Contact>> ListByRoleAsync(
        ContactRoles role, CancellationToken cancellationToken = default) =>
        ReadAsync<IReadOnlyList<Contact>>(
            async db => await ContactQuery(db)
                .Where(c => (c.Roles & role) == role)
                .ToListAsync(cancellationToken).ConfigureAwait(false),
            cancellationToken);

    Task IContactWriter.AddAsync(Contact contact, CancellationToken cancellationToken) =>
        base.AddAsync(contact, cancellationToken);

    /// <summary>
    /// Persists a contact aggregate. <c>DbSet.Update()</c> marks the entire disconnected
    /// graph as Modified — including child entities (<c>ContactAddress</c>, <c>ContactEmail</c>,
    /// <c>ContactPhone</c>, <c>ContactExternalMapping</c>) freshly added in memory and not
    /// yet in the DB. We reconcile by reading existing child IDs and re-marking new ones
    /// as Added. Mirrors the workaround in <c>EfPlanWriter</c>.
    /// </summary>
    Task IContactWriter.UpdateAsync(Contact contact, CancellationToken cancellationToken) =>
        WriteAsync(async db =>
        {
            HashSet<Guid> existingAddressIds = await CollectChildIdsAsync<ContactAddress>(db, contact.Id, cancellationToken).ConfigureAwait(false);
            HashSet<Guid> existingEmailIds = await CollectChildIdsAsync<ContactEmail>(db, contact.Id, cancellationToken).ConfigureAwait(false);
            HashSet<Guid> existingPhoneIds = await CollectChildIdsAsync<ContactPhone>(db, contact.Id, cancellationToken).ConfigureAwait(false);
            HashSet<Guid> existingMappingIds = await CollectChildIdsAsync<ContactExternalMapping>(db, contact.Id, cancellationToken).ConfigureAwait(false);

            db.Set<Contact>().Update(contact);

            ReconcileNewChildren<ContactAddress>(db, existingAddressIds);
            ReconcileNewChildren<ContactEmail>(db, existingEmailIds);
            ReconcileNewChildren<ContactPhone>(db, existingPhoneIds);
            ReconcileNewChildren<ContactExternalMapping>(db, existingMappingIds);
        }, cancellationToken);

    private static async Task<HashSet<Guid>> CollectChildIdsAsync<TChild>(
        ContactsDbContext db, Guid contactId, CancellationToken cancellationToken)
        where TChild : class
    {
        List<Guid> ids = await db.Set<TChild>()
            .AsNoTracking()
            .Where(e => EF.Property<Guid>(e, "ContactId") == contactId)
            .Select(e => EF.Property<Guid>(e, "Id"))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return ids.ToHashSet();
    }

    private static void ReconcileNewChildren<TChild>(ContactsDbContext db, HashSet<Guid> existingIds)
        where TChild : class
    {
        foreach (EntityEntry<TChild> entry in db.ChangeTracker.Entries<TChild>())
        {
            var id = (Guid)entry.Property("Id").CurrentValue!;
            if (entry.State == EntityState.Modified && !existingIds.Contains(id))
            {
                entry.State = EntityState.Added;

                // Cascade Added state to owned references (e.g., ContactAddress.Value).
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
