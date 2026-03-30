using Microsoft.EntityFrameworkCore;

namespace Granit.Persistence.EntityFrameworkCore.MultiTenancy;

/// <summary>
/// Carries the provider configuration delegate for <see cref="TenantPerSchemaDbContextFactory{TContext}"/>.
/// Registered in DI by <c>AddTenantPerSchemaDbContext&lt;TContext&gt;()</c>.
/// </summary>
internal sealed class TenantPerSchemaDbContextOptions<TContext>
    where TContext : DbContext
{
    /// <summary>
    /// Configures the <see cref="DbContextOptionsBuilder{TContext}"/> with the shared
    /// connection string. Typically calls <c>UseNpgsql(connectionString)</c> or equivalent.
    /// The <c>search_path</c> is set per-connection by
    /// <see cref="TenantSchemaConnectionInterceptor"/> — do not set it here.
    /// </summary>
    public required Action<DbContextOptionsBuilder<TContext>> Configure { get; init; }
}
