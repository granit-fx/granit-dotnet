using Granit.Persistence.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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

        // Wire HostSchema EAGERLY. EF Core caches the compiled model on first
        // DbContext creation — if GranitDbDefaults.HostDbSchema is not set before
        // that, host module *DbProperties.DbSchema returns null and the model is
        // cached with the wrong (public) schema permanently.
        // Idempotent: first caller wins; subsequent calls are no-ops.
        if (GranitDbDefaults.HostDbSchema is null)
        {
            var configuration = services
                .FirstOrDefault(d => d.ServiceType == typeof(IConfiguration))
                ?.ImplementationInstance as IConfiguration;
            string? hostSchema = configuration?["TenantIsolation:HostSchema"];
            if (hostSchema is not null)
            {
                GranitDbDefaults.HostDbSchema = hostSchema;
            }
        }

        services.AddDbContextFactory<TContext>((sp, options) =>
        {
            configure(options);
            options.UseGranitInterceptors(sp);
        }, ServiceLifetime.Scoped);

        return services;
    }

    /// <summary>
    /// Registers a host-level <see cref="IHostInternalDbContextEnsurer"/> for the specified
    /// <typeparamref name="TContext"/>. Tables are created in the host schema during <c>--migrate</c>.
    /// </summary>
    /// <typeparam name="TContext">The host DbContext type.</typeparam>
    public static IServiceCollection AddHostInternalDbContextEnsurer<TContext>(
        this IServiceCollection services)
        where TContext : DbContext
    {
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IHostInternalDbContextEnsurer, HostInternalDbContextEnsurer<TContext>>());

        return services;
    }

    /// <summary>
    /// Registers a tenant-level <see cref="ITenantInternalDbContextEnsurer"/> for the specified
    /// <typeparamref name="TContext"/>. Tables are created per-tenant schema/database during
    /// <c>--migrate</c> and hot provisioning.
    /// </summary>
    /// <typeparam name="TContext">The tenant DbContext type.</typeparam>
    public static IServiceCollection AddTenantInternalDbContextEnsurer<TContext>(
        this IServiceCollection services)
        where TContext : DbContext
    {
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<ITenantInternalDbContextEnsurer, TenantInternalDbContextEnsurer<TContext>>());

        return services;
    }
}
