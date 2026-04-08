using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Persistence.EntityFrameworkCore.MultiTenancy;

/// <summary>
/// <see cref="IDbContextFactory{TContext}"/> for the <see cref="TenantIsolationStrategy.SharedDatabase"/>
/// strategy. Creates a standard <typeparamref name="TContext"/> where tenant isolation is
/// enforced by the global EF Core query filter on <c>TenantId</c>.
/// </summary>
/// <remarks>
/// <para>
/// No <c>search_path</c> override is applied — all tenants share the same schema.
/// <see cref="Interceptors.AuditedEntityInterceptor"/> is wired automatically when available in DI.
/// </para>
/// <para>
/// Uses <c>ActivatorUtilities.CreateInstance</c> instead of <c>Activator.CreateInstance</c>
/// so that DbContexts with optional DI parameters (e.g. <c>ICurrentTenant?</c>, <c>IDataFilter?</c>)
/// are constructed correctly. <c>Activator.CreateInstance</c> does not resolve optional parameters.
/// </para>
/// </remarks>
internal sealed class SharedDatabaseDbContextFactory<TContext>(
    IServiceProvider serviceProvider,
    SharedDatabaseDbContextOptions<TContext> options) : IDbContextFactory<TContext>
    where TContext : DbContext
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly SharedDatabaseDbContextOptions<TContext> _options = options;

    /// <inheritdoc/>
    public TContext CreateDbContext()
    {
        DbContextOptionsBuilder<TContext> optionsBuilder = new();
        _options.Configure(optionsBuilder);
        optionsBuilder.UseGranitInterceptors(_serviceProvider);
        return ActivatorUtilities.CreateInstance<TContext>(_serviceProvider, optionsBuilder.Options);
    }

    /// <inheritdoc/>
    public Task<TContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateDbContext());
}
