using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Persistence.EntityFrameworkCore.MultiTenancy;

/// <summary>
/// Scoped <see cref="IDbContextFactory{TContext}"/> that builds a <typeparamref name="TContext"/>
/// configured for the current tenant's isolated database.
/// </summary>
/// <remarks>
/// <para>
/// Reads <see cref="ICurrentTenant.Id"/> and delegates connection string resolution to
/// <see cref="ITenantConnectionStringProvider"/>. The database provider (Npgsql, SQL Server…)
/// is configured by the <c>Action&lt;DbContextOptionsBuilder, string&gt;</c> delegate
/// registered at startup via <c>AddTenantPerDatabaseDbContext&lt;TContext&gt;()</c>.
/// </para>
/// <para>
/// Throws <see cref="InvalidOperationException"/> when no tenant is active.
/// There is no silent fallback: accessing data without a tenant context would violate
/// GDPR/ISO 27001 inter-tenant isolation requirements.
/// </para>
/// <para>
/// <see cref="AuditedEntityInterceptor"/> is wired automatically when available in DI,
/// satisfying the 3-year ISO 27001 audit trail requirement.
/// </para>
/// <para>
/// Prefer <see cref="CreateDbContextAsync"/> over <see cref="CreateDbContext"/>: the synchronous
/// overload uses <c>GetAwaiter().GetResult()</c> and is provided only for framework compatibility.
/// Connection strings are expected to be cached in memory by the provider.
/// </para>
/// </remarks>
/// <typeparam name="TContext">The tenant-specific <see cref="DbContext"/> type.</typeparam>
internal sealed class TenantPerDatabaseDbContextFactory<TContext>(
    ICurrentTenant currentTenant,
    ITenantConnectionStringProvider connectionStringProvider,
    IServiceProvider serviceProvider,
    TenantPerDatabaseDbContextOptions<TContext> options) : IDbContextFactory<TContext>
    where TContext : DbContext
{

    /// <inheritdoc/>
    public TContext CreateDbContext() =>
        CreateDbContextAsync(CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc/>
    public async Task<TContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        Guid tenantId = currentTenant.Id
            ?? throw new InvalidOperationException(
                "No active tenant context. Ensure the tenant is resolved before accessing " +
                "per-tenant data (HTTP: TenantResolutionMiddleware; messaging: TenantContextBehavior).");

        string connectionString = await connectionStringProvider
            .GetConnectionStringAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);

        return BuildContext(connectionString);
    }

    private TContext BuildContext(string connectionString)
    {
        DbContextOptionsBuilder<TContext> optionsBuilder = new();
        options.Configure(optionsBuilder, connectionString);
        optionsBuilder.UseGranitInterceptors(serviceProvider);
        return (TContext)Activator.CreateInstance(typeof(TContext), optionsBuilder.Options)!;
    }
}
