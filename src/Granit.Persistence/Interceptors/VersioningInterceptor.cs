using Granit.Domain;
using Granit.Guids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Granit.Persistence.Interceptors;

/// <summary>
/// EF Core interceptor that auto-assigns <see cref="IVersioned.VersionId"/> and
/// <see cref="IVersioned.Version"/> on newly added entities.
/// </summary>
/// <remarks>
/// <para>
/// On <c>EntityState.Added</c>:
/// <list type="bullet">
///   <item>If <see cref="IVersioned.VersionId"/> is <see cref="Guid.Empty"/>,
///         a new identifier is generated (first version of a new business entity).</item>
///   <item><see cref="IVersioned.Version"/> is set to the next value for that
///         <see cref="IVersioned.VersionId"/> (max existing + 1), starting at 1.</item>
/// </list>
/// </para>
/// <para>
/// <c>EntityState.Modified</c> entities are left untouched — updates are in-place.
/// Creating a new version is an explicit operation (add a new entity with the same VersionId).
/// </para>
/// <para>
/// Registered as Scoped. Must be ordered after <see cref="AuditedEntityInterceptor"/>
/// and before <c>SoftDeleteInterceptor</c>.
/// </para>
/// </remarks>
public sealed class VersioningInterceptor(IGuidGenerator guidGenerator) : SaveChangesInterceptor
{

    /// <inheritdoc/>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyVersioning(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyVersioning(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyVersioning(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        // Collect all Added IVersioned entries first to handle multiple adds for the same VersionId
        List<IVersioned> addedEntities = [];

        foreach (EntityEntry entry in context.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added))
        {
            if (entry.Entity is IVersioned versioned)
            {
                addedEntities.Add(versioned);
            }
        }

        foreach (IVersioned versioned in addedEntities)
        {
            // Assign a new VersionId for brand-new logical entities
            if (versioned.VersionId == Guid.Empty)
            {
                versioned.VersionId = guidGenerator.Create();
            }

            // Determine the next version for this VersionId.
            // Check both the ChangeTracker (for other pending adds) and existing tracked entities.
            int maxVersion = GetMaxTrackedVersion(context, versioned.VersionId, versioned);

            versioned.Version = maxVersion + 1;
        }
    }

    private static int GetMaxTrackedVersion(DbContext context, Guid businessId, IVersioned current)
    {
        int max = 0;

        foreach (EntityEntry entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is IVersioned tracked
                && !ReferenceEquals(tracked, current)
                && tracked.VersionId == businessId
                && tracked.Version > max)
            {
                max = tracked.Version;
            }
        }

        return max;
    }
}
