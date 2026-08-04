using Microsoft.EntityFrameworkCore;

namespace Granit.Persistence.EntityFrameworkCore.MultiTenancy;

/// <summary>
/// Carries the provider configuration delegate for <see cref="TenantPerDatabaseDbContextFactory{TContext}"/>.
/// Registered in DI by <c>AddGranitTenantPerDatabaseDbContext&lt;TContext&gt;()</c>.
/// </summary>
internal sealed class TenantPerDatabaseDbContextOptions<TContext>
    where TContext : DbContext
{
    /// <summary>
    /// Configures the <see cref="DbContextOptionsBuilder{TContext}"/> using the resolved
    /// connection string. Typically calls <c>UseNpgsql(connectionString)</c> or equivalent.
    /// </summary>
    public required Action<DbContextOptionsBuilder<TContext>, string> Configure { get; init; }
}
