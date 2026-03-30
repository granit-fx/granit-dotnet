using Granit.Persistence.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Persistence.EntityFrameworkCore.Extensions;

/// <summary>
/// Centralized extension for registering isolated Granit DbContexts with automatic
/// interceptor wiring.
/// </summary>
public static class PersistenceDbContextServiceCollectionExtensions
{
    /// <summary>
    /// Registers an isolated Granit <see cref="DbContext"/> via
    /// <see cref="EntityFrameworkServiceCollectionExtensions.AddDbContextFactory{TContext}(IServiceCollection, Action{IServiceProvider, DbContextOptionsBuilder}, ServiceLifetime)"/>
    /// with <see cref="ServiceLifetime.Scoped"/> lifetime and automatic Granit interceptor wiring.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method replaces the manual boilerplate pattern:
    /// <code>
    /// services.AddDbContextFactory&lt;TContext&gt;((sp, options) =&gt;
    /// {
    ///     configure(options);
    ///     options.UseGranitInterceptors(sp);
    /// }, ServiceLifetime.Scoped);
    /// </code>
    /// </para>
    /// <para>
    /// The <typeparamref name="TContext"/> must follow the Granit isolated DbContext pattern:
    /// constructor accepting <c>DbContextOptions&lt;TContext&gt;</c> with optional
    /// <c>ICurrentTenant?</c> and <c>IDataFilter?</c> parameters, calling
    /// <c>modelBuilder.ApplyGranitConventions(currentTenant, dataFilter)</c> in
    /// <c>OnModelCreating</c>.
    /// </para>
    /// </remarks>
    /// <typeparam name="TContext">The isolated DbContext type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Provider-specific configuration (e.g. <c>options.UseNpgsql(connectionString)</c>).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitDbContext<TContext>(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configure)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddDbContextFactory<TContext>((sp, options) =>
        {
            configure(options);
            options.UseGranitInterceptors(sp);
        }, ServiceLifetime.Scoped);

        return services;
    }

    /// <summary>
    /// Registers a generic <see cref="IInternalDbContextEnsurer"/> for the specified
    /// <typeparamref name="TContext"/> so that its tables are created automatically
    /// during <c>--migrate</c>.
    /// </summary>
    /// <remarks>
    /// Call this after <see cref="AddGranitDbContext{TContext}"/> for any isolated DbContext
    /// whose tables are not included in the host application's EF Core migrations.
    /// Uses <c>TryAddEnumerable</c> — safe to call multiple times for the same context.
    /// </remarks>
    /// <typeparam name="TContext">The isolated DbContext type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddInternalDbContextEnsurer<TContext>(
        this IServiceCollection services)
        where TContext : DbContext
    {
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IInternalDbContextEnsurer, InternalDbContextEnsurer<TContext>>());

        return services;
    }
}
