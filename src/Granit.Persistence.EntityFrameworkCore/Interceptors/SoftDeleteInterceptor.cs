using Granit.Domain;
using Granit.Timing;
using Granit.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Granit.Persistence.EntityFrameworkCore.Interceptors;

/// <summary>
/// EF Core interceptor that converts physical deletions
/// to soft deletions for <see cref="ISoftDeletable"/> entities.
/// </summary>
/// <remarks>
/// <para>
/// <b>⚠ ExecuteDelete bypass:</b> <c>ExecuteDelete()</c> and <c>ExecuteDeleteAsync()</c>
/// run directly as SQL <c>DELETE</c> statements and do NOT go through this interceptor.
/// Use <c>ExecuteUpdate()</c> with <c>IsDeleted = true</c> / <c>DeletedAt</c> / <c>DeletedBy</c>
/// instead, or load the entity and call <c>DbContext.Remove()</c>.
/// </para>
/// </remarks>
public sealed class SoftDeleteInterceptor(
    ICurrentUserService currentUserService,
    IClock clock) : SaveChangesInterceptor
{

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplySoftDelete(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplySoftDelete(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplySoftDelete(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        DateTimeOffset now = clock.Now;
        string userId = currentUserService.UserId ?? "system";

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<ISoftDeletable> entry in context.ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State != EntityState.Deleted)
            {
                continue;
            }

            // Convert DELETE -> UPDATE (soft delete)
            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedAt = now;
            entry.Entity.DeletedBy = userId;
        }
    }
}
