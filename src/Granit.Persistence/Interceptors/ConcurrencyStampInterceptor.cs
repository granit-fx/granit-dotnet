using Granit.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Granit.Persistence.Interceptors;

/// <summary>
/// EF Core interceptor that auto-generates a new <see cref="IConcurrencyAware.ConcurrencyStamp"/>
/// on every <c>SaveChanges</c>/<c>SaveChangesAsync</c> for entities implementing
/// <see cref="IConcurrencyAware"/>.
/// </summary>
/// <remarks>
/// <para>
/// On <c>EntityState.Added</c> and <c>EntityState.Modified</c>:
/// <list type="bullet">
///   <item>Sets <see cref="IConcurrencyAware.ConcurrencyStamp"/> to a new <see cref="Guid"/>
///         string (36 characters).</item>
/// </list>
/// </para>
/// <para>
/// The stamp is configured as an EF Core concurrency token by
/// <see cref="Extensions.ModelBuilderExtensions.ApplyGranitConventions"/>
/// (<c>.IsConcurrencyToken()</c>). EF Core includes it in the <c>WHERE</c> clause
/// on update — if the stored value differs from the original, a
/// <see cref="DbUpdateConcurrencyException"/> is thrown.
/// </para>
/// <para>
/// Registered as Scoped. Must be ordered after <see cref="VersioningInterceptor"/>
/// and before <see cref="DomainEventDispatcherInterceptor"/>.
/// </para>
/// </remarks>
public sealed class ConcurrencyStampInterceptor : SaveChangesInterceptor
{
    /// <inheritdoc/>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyConcurrencyStamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyConcurrencyStamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void ApplyConcurrencyStamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (EntityEntry<IConcurrencyAware> entry in context.ChangeTracker.Entries<IConcurrencyAware>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified))
        {
#pragma warning disable GRSEC002 // Concurrency stamp is opaque, not a business identifier — no UUIDv7 needed
            entry.Entity.ConcurrencyStamp = Guid.NewGuid().ToString();
#pragma warning restore GRSEC002
        }
    }
}
