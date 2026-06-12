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

        // Multi-tenant isolation: inject current TenantId if the entity supports it.
        // Explicit IsAvailable check per soft-dependency contract (NullTenantContext returns null).
        if (entry.Entity is IMultiTenant multiTenant && multiTenant.TenantId is null)
        {
            multiTenant.TenantId = currentTenant.IsAvailable ? currentTenant.Id : null;
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
