using Granit.Domain;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Timing;
using Granit.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Granit.Persistence.EntityFrameworkCore.Interceptors;

/// <summary>
/// EF Core interceptor that automatically populates audit fields
/// on entities implementing <see cref="ICreationAuditedObject"/>,
/// and the <see cref="IMultiTenant.TenantId"/> on multi-tenant entities.
/// </summary>
/// <remarks>
/// <para>
/// <b>⚠ ExecuteUpdate bypass:</b> <c>ExecuteUpdate()</c> and <c>ExecuteUpdateAsync()</c>
/// run directly as SQL <c>UPDATE</c> statements and do NOT go through this interceptor.
/// Audit fields (<c>ModifiedAt</c>, <c>ModifiedBy</c>) and <c>TenantId</c> will NOT be
/// automatically set. Set them explicitly in the <c>setPropertyCalls</c> expression,
/// or load the entity and modify it via the change tracker.
/// </para>
/// <para>
/// <b>Cross-tenant write guard:</b> on insert, a <see cref="IMultiTenant.TenantId"/> left
/// <c>null</c> is stamped with the active tenant (or <c>null</c> in host context). A pre-set
/// <c>TenantId</c> that points at a <i>different</i> tenant while a tenant is active throws —
/// the query filter only protects reads, so this closes the insert-side cross-tenant gap.
/// Imports/migrations that assign an explicit <c>TenantId</c> must run in host context.
/// </para>
/// </remarks>
public sealed class AuditedEntityInterceptor(
    ICurrentUserService currentUserService,
    IClock clock,
    IGuidGenerator guidGenerator,
    ICurrentTenant currentTenant) : SaveChangesInterceptor
{

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyAuditFields(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditFields(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAuditFields(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        DateTimeOffset now = clock.Now;
        string userId = currentUserService.UserId ?? "system";

        // Pivot on ICreationAuditedObject rather than the CreationAuditedEntity base
        // class so entities that cannot inherit it — e.g. LocalIdentity, forced to
        // extend ASP.NET Identity's IdentityUser<Guid> — still get their audit fields.
        // Without this, CreatedAt stayed at default(DateTimeOffset), which Npgsql
        // persists as -infinity.
        foreach (EntityEntry<ICreationAuditedObject> entry in context.ChangeTracker.Entries<ICreationAuditedObject>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    ApplyCreationFields(entry, now, userId);
                    break;

                case EntityState.Modified:
                    ApplyModificationFields(entry, now, userId);
                    break;
            }
        }
    }

    private void ApplyCreationFields(EntityEntry<ICreationAuditedObject> entry, DateTimeOffset now, string userId)
    {
        entry.Entity.CreatedAt = now;
        entry.Entity.CreatedBy = userId;

        // GUID generation is an Entity concern. Entities outside that hierarchy
        // (e.g. IdentityUser-derived) own their key and set it before save.
        if (entry.Entity is Entity entity && entity.Id == Guid.Empty)
        {
            entity.Id = guidGenerator.Create();
        }

        // Multi-tenant isolation. Explicit IsAvailable check per soft-dependency contract
        // (NullTenantContext returns null).
        if (entry.Entity is IMultiTenant multiTenant)
        {
            Guid? activeTenantId = currentTenant.IsAvailable ? currentTenant.Id : null;

            if (multiTenant.TenantId is null)
            {
                // Stamp the active tenant (or null in host context).
                multiTenant.TenantId = activeTenantId;
            }
            else if (activeTenantId is not null && multiTenant.TenantId != activeTenantId)
            {
                // A pre-set TenantId that targets a DIFFERENT tenant under an active tenant is a
                // cross-tenant write: the named query filter guards reads but never insertions.
                // Fail closed. The explicit-override seam (migration/import) stays supported, but
                // only in host context (no active tenant), where there is no tenant to violate.
                throw new InvalidOperationException(
                    $"Cross-tenant write blocked: '{entry.Entity.GetType().Name}' was created with " +
                    $"TenantId '{multiTenant.TenantId}' while the active tenant is '{activeTenantId}'. " +
                    "A multi-tenant entity may only be written for the active tenant; run imports and " +
                    "migrations in host context (no active tenant) to assign a specific TenantId.");
            }
        }
    }

    private static void ApplyModificationFields(EntityEntry<ICreationAuditedObject> entry, DateTimeOffset now, string userId)
    {
        // Protect creation fields from modification
        entry.Property(e => e.CreatedAt).IsModified = false;
        entry.Property(e => e.CreatedBy).IsModified = false;

        // Both the AuditedEntity and AuditedAggregateRoot hierarchies implement
        // IModificationAuditedObject. Pivoting on the interface (rather than on a
        // single base class) is required because the two hierarchies are disjoint:
        // an aggregate root is a CreationAuditedEntity but never an AuditedEntity.
        if (entry.Entity is IModificationAuditedObject modificationAudited)
        {
            modificationAudited.ModifiedAt = now;
            modificationAudited.ModifiedBy = userId;
        }
    }
}
