using Granit.Core.Domain;
using Granit.Core.MultiTenancy;
using Granit.Guids;
using Granit.Security;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Granit.Persistence.Interceptors;

/// <summary>
/// EF Core interceptor that automatically populates audit fields
/// on entities inheriting from <see cref="CreationAuditedEntity"/>,
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

        foreach (EntityEntry<CreationAuditedEntity> entry in context.ChangeTracker.Entries<CreationAuditedEntity>())
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

    private void ApplyCreationFields(EntityEntry<CreationAuditedEntity> entry, DateTimeOffset now, string userId)
    {
        entry.Entity.CreatedAt = now;
        entry.Entity.CreatedBy = userId;

        if (entry.Entity.Id == Guid.Empty)
        {
            entry.Entity.Id = guidGenerator.Create();
        }

        // Multi-tenant isolation: inject current TenantId if the entity supports it.
        // Explicit IsAvailable check per soft-dependency contract (NullTenantContext returns null).
        if (entry.Entity is IMultiTenant multiTenant && multiTenant.TenantId is null)
        {
            multiTenant.TenantId = currentTenant.IsAvailable ? currentTenant.Id : null;
        }
    }

    private static void ApplyModificationFields(EntityEntry<CreationAuditedEntity> entry, DateTimeOffset now, string userId)
    {
        // Protect creation fields from modification
        entry.Property(e => e.CreatedAt).IsModified = false;
        entry.Property(e => e.CreatedBy).IsModified = false;

        if (entry.Entity is AuditedEntity audited)
        {
            audited.ModifiedAt = now;
            audited.ModifiedBy = userId;
        }
    }
}
