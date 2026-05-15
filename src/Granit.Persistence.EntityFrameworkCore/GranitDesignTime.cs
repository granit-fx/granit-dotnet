using Granit.DataFiltering;
using Granit.MultiTenancy;

namespace Granit.Persistence.EntityFrameworkCore;

/// <summary>
/// Framework-provided stubs to feed into a <c>DbContext</c> from an
/// <see cref="Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory{TContext}"/>.
/// Pass these to the same constructor parameters the runtime DI container would resolve, so
/// that <c>ApplyGranitConventions</c> registers every named query filter (in particular the
/// <c>IMultiTenant</c> one, which is gated on a non-<c>null</c> <see cref="ICurrentTenant"/>).
/// Without that parity, EF Core 10 raises
/// <c>Microsoft.EntityFrameworkCore.Migrations.PendingModelChangesWarning</c> at runtime
/// because the design-time snapshot is missing filters present in the runtime model.
/// </summary>
/// <example>
/// <code>
/// public sealed class MyDbContextFactory : IDesignTimeDbContextFactory&lt;MyDbContext&gt;
/// {
///     public MyDbContext CreateDbContext(string[] args)
///     {
///         var options = new DbContextOptionsBuilder&lt;MyDbContext&gt;()
///             .UseSqlServer("Server=...;Database=design-time;")
///             .Options;
///
///         return new MyDbContext(
///             options,
///             GranitDesignTime.CurrentTenant,
///             GranitDesignTime.DataFilter);
///     }
/// }
/// </code>
/// </example>
public static class GranitDesignTime
{
    /// <summary>
    /// Stateless <see cref="ICurrentTenant"/> stub (<see cref="NullTenantContext.Instance"/>).
    /// Reports <c>IsAvailable = false</c>; <c>Change()</c> is a no-op. Hand this to a
    /// <c>DbContext</c> constructor at design time so the <c>IMultiTenant</c> query filter
    /// is registered on the model snapshot.
    /// </summary>
    public static ICurrentTenant CurrentTenant => NullTenantContext.Instance;

    /// <summary>
    /// Stateless <see cref="IDataFilter"/> stub (<see cref="NullDataFilter.Instance"/>).
    /// Reports every filter as enabled and treats <c>Disable</c> / <c>Enable</c> as no-ops.
    /// Hand this to a <c>DbContext</c> constructor at design time so the <c>IDataFilter</c>
    /// bypass branches show up in the model snapshot exactly as they do at runtime.
    /// </summary>
    public static IDataFilter DataFilter => NullDataFilter.Instance;
}
