using Granit.MultiTenancy;
using Granit.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Persistence.MultiTenancy;

/// <summary>
/// Scoped <see cref="IDbContextFactory{TContext}"/> that routes all EF Core queries
/// to the current tenant's dedicated schema via <see cref="TenantSchemaConnectionInterceptor"/>
/// and <see cref="ITenantSchemaActivator"/>.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="TenantPerDatabaseDbContextFactory{TContext}"/>, this factory uses a
/// shared database connection string. Physical isolation is achieved by activating the
/// tenant schema at connection open time via the provider-specific
/// <see cref="ITenantSchemaActivator"/>. The EF Core compiled model is shared across
/// all tenants — no per-tenant model recompilation, no memory leak.
/// </para>
/// <para>
/// Throws <see cref="InvalidOperationException"/> when no tenant is active.
/// There is no silent fallback: allowing a query to run without schema activation
/// would expose data from a previously-pooled tenant connection (ISO 27001 breach).
/// </para>
/// <para>
/// <see cref="AuditedEntityInterceptor"/> is wired automatically when available in DI,
/// satisfying the 3-year ISO 27001 audit trail requirement.
/// </para>
/// </remarks>
/// <typeparam name="TContext">The <see cref="DbContext"/> type shared across tenants.</typeparam>
internal sealed class TenantPerSchemaDbContextFactory<TContext>(
    ICurrentTenant currentTenant,
    ITenantSchemaProvider schemaProvider,
    ITenantSchemaActivator schemaActivator,
    IServiceProvider serviceProvider,
    TenantPerSchemaDbContextOptions<TContext> options) : IDbContextFactory<TContext>
    where TContext : DbContext
{

    /// <inheritdoc/>
    public TContext CreateDbContext() =>
        CreateDbContextAsync(CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc/>
    public async Task<TContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        if (!currentTenant.IsAvailable)
        {
            throw new InvalidOperationException(
                "No active tenant context. Ensure the tenant is resolved before accessing " +
                "per-schema data (HTTP: TenantResolutionMiddleware; messaging: TenantContextBehavior).");
        }

        return BuildContext();
    }

    private TContext BuildContext()
    {
        DbContextOptionsBuilder<TContext> optionsBuilder = new();
        options.Configure(optionsBuilder);

        TenantSchemaConnectionInterceptor schemaInterceptor = new(currentTenant, schemaProvider, schemaActivator);
        optionsBuilder.AddInterceptors(schemaInterceptor);
        optionsBuilder.UseGranitInterceptors(serviceProvider);

        return (TContext)Activator.CreateInstance(typeof(TContext), optionsBuilder.Options)!;
    }
}
