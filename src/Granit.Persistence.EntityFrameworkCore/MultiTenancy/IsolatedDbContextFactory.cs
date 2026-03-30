using Granit.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Granit.Persistence.EntityFrameworkCore.MultiTenancy;

/// <summary>
/// Unified <see cref="IDbContextFactory{TContext}"/> that dispatches to the appropriate
/// per-strategy factory based on the result of <see cref="ITenantIsolationStrategyProvider"/>.
/// </summary>
/// <remarks>
/// <para>
/// The underlying factory is resolved as a keyed DI service using the
/// <see cref="TenantIsolationStrategy"/> enum value as the service key.
/// The keyed registrations are set up by <c>AddGranitIsolatedDbContext&lt;TContext&gt;()</c>.
/// </para>
/// <para>
/// The resolved strategy is logged at <see cref="LogLevel.Debug"/> level for traceability.
/// </para>
/// <para>
/// Throws <see cref="InvalidOperationException"/> when the resolved strategy has no
/// corresponding registered factory — fail-fast, no silent cross-tenant fallback.
/// </para>
/// </remarks>
/// <typeparam name="TContext">The <see cref="DbContext"/> type.</typeparam>
internal sealed partial class IsolatedDbContextFactory<TContext>(
    ICurrentTenant currentTenant,
    ITenantIsolationStrategyProvider strategyProvider,
    IServiceProvider serviceProvider,
    ILogger<IsolatedDbContextFactory<TContext>> logger) : IDbContextFactory<TContext>
    where TContext : DbContext
{

    /// <inheritdoc/>
    public TContext CreateDbContext() =>
        CreateDbContextAsync(CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc/>
    public async Task<TContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        TenantIsolationStrategy strategy = await strategyProvider
            .GetStrategyAsync(currentTenant.IsAvailable ? currentTenant.Id : null, cancellationToken)
            .ConfigureAwait(false);

        LogResolvingDbContext(typeof(TContext).Name, strategy, currentTenant.Id);

        IDbContextFactory<TContext> factory =
            serviceProvider.GetKeyedService<IDbContextFactory<TContext>>(strategy)
            ?? throw new InvalidOperationException(
                $"No factory registered for isolation strategy '{strategy}'. " +
                $"Configure the '{strategy}' strategy in AddGranitIsolatedDbContext<{typeof(TContext).Name}>().");

        return await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Resolving DbContext<{ContextType}> using isolation strategy {Strategy} for tenant {TenantId}.")]
    private partial void LogResolvingDbContext(string contextType, TenantIsolationStrategy strategy, Guid? tenantId);
}
