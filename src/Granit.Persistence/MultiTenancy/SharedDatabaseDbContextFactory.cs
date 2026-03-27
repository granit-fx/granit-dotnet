using Granit.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Persistence.MultiTenancy;

/// <summary>
/// <see cref="IDbContextFactory{TContext}"/> for the <see cref="TenantIsolationStrategy.SharedDatabase"/>
/// strategy. Creates a standard <typeparamref name="TContext"/> where tenant isolation is
/// enforced by the global EF Core query filter on <c>TenantId</c>.
/// </summary>
/// <remarks>
/// No <c>search_path</c> override is applied — all tenants share the same schema.
/// <see cref="AuditedEntityInterceptor"/> is wired automatically when available in DI.
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
        return (TContext)Activator.CreateInstance(typeof(TContext), optionsBuilder.Options)!;
    }

    /// <inheritdoc/>
    public Task<TContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateDbContext());
}
