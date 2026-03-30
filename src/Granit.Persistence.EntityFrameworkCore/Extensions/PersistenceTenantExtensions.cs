using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Persistence.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering Granit per-tenant data isolation services.
/// </summary>
public static class PersistenceTenantExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TContext"/> as a per-tenant <see cref="IDbContextFactory{TContext}"/>:
    /// each call to <c>CreateDbContextAsync()</c> resolves the connection string from
    /// <see cref="ITenantConnectionStringProvider"/> using the current tenant context.
    /// </summary>
    /// <typeparam name="TContext">The <see cref="DbContext"/> to register.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">
    /// Configures the <see cref="DbContextOptionsBuilder{TContext}"/> with the resolved
    /// connection string. Typically: <c>(opts, cs) =&gt; opts.UseNpgsql(cs)</c>.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// Requires <see cref="ITenantConnectionStringProvider"/> to be registered in DI before
    /// the host is built. A missing registration causes a runtime exception on the first
    /// <c>CreateDbContextAsync()</c> call (fail-fast behaviour).
    /// </para>
    /// <para>
    /// Both <see cref="IDbContextFactory{TContext}"/> and <typeparamref name="TContext"/>
    /// are registered as <see cref="ServiceLifetime.Scoped"/>.
    /// <c>TryAdd</c> semantics are used so that integration tests can pre-register a
    /// custom factory without being overridden.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddTenantPerDatabaseDbContext<TContext>(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder<TContext>, string> configureOptions)
        where TContext : DbContext
    {
        services.AddSingleton(new TenantPerDatabaseDbContextOptions<TContext>
        {
            Configure = configureOptions,
        });

        services.TryAddScoped<IDbContextFactory<TContext>,
            TenantPerDatabaseDbContextFactory<TContext>>();

        services.TryAddScoped<TContext>(
            static sp => sp.GetRequiredService<IDbContextFactory<TContext>>().CreateDbContext());

        return services;
    }

    /// <summary>
    /// Registers <typeparamref name="TContext"/> as a per-schema <see cref="IDbContextFactory{TContext}"/>:
    /// each connection is routed to the current tenant's dedicated PostgreSQL schema via
    /// <c>SET search_path TO {schema}, public</c>, executed unconditionally at connection open.
    /// </summary>
    /// <typeparam name="TContext">The <see cref="DbContext"/> to register.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">
    /// Configures the <see cref="DbContextOptionsBuilder{TContext}"/> with the shared
    /// connection string. Typically: <c>opts =&gt; opts.UseNpgsql(sharedConnectionString)</c>.
    /// Do not set <c>search_path</c> here — it is managed by
    /// <see cref="TenantSchemaConnectionInterceptor"/>.
    /// </param>
    /// <param name="configureTenantSchema">
    /// Optional action to configure <see cref="TenantSchemaOptions"/> (prefix, naming convention).
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Connection pool safety</strong> — <see cref="TenantSchemaConnectionInterceptor"/>
    /// runs unconditionally on every connection lease from the pool, overwriting any
    /// previous tenant's schema. No bypass condition exists.
    /// </para>
    /// <para>
    /// If no custom <see cref="ITenantSchemaProvider"/> is registered, the default
    /// <see cref="DefaultTenantSchemaProvider"/> is used (convention from
    /// <see cref="TenantSchemaOptions"/>).
    /// </para>
    /// <para>
    /// No default <see cref="ITenantSchemaActivator"/> is registered. Call
    /// <c>AddGranitPostgres()</c> (from <c>Granit.Persistence.EntityFrameworkCore.Postgres</c>) before this
    /// method to register the PostgreSQL implementation, or register your own
    /// <see cref="ITenantSchemaActivator"/> for a different database provider.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddTenantPerSchemaDbContext<TContext>(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder<TContext>> configureOptions,
        Action<TenantSchemaOptions>? configureTenantSchema = null)
        where TContext : DbContext
    {
        services.AddOptions<TenantSchemaOptions>()
            .Configure(configureTenantSchema ?? (_ => { }))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton<ITenantSchemaProvider, DefaultTenantSchemaProvider>();

        services.AddSingleton(new TenantPerSchemaDbContextOptions<TContext>
        {
            Configure = configureOptions,
        });

        services.TryAddScoped<IDbContextFactory<TContext>,
            TenantPerSchemaDbContextFactory<TContext>>();

        services.TryAddScoped<TContext>(
            static sp => sp.GetRequiredService<IDbContextFactory<TContext>>().CreateDbContext());

        return services;
    }

    /// <summary>
    /// Registers a unified <see cref="IDbContextFactory{TContext}"/> that dynamically dispatches
    /// to the appropriate isolation strategy (SharedDatabase, DatabasePerTenant, or SchemaPerTenant)
    /// based on the result of <see cref="ITenantIsolationStrategyProvider"/>.
    /// </summary>
    /// <typeparam name="TContext">The <see cref="DbContext"/> to register.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configureShared">
    /// Configures the <see cref="DbContextOptionsBuilder{TContext}"/> for the
    /// <see cref="TenantIsolationStrategy.SharedDatabase"/> strategy.
    /// Typically: <c>opts =&gt; opts.UseNpgsql(connectionString)</c>.
    /// </param>
    /// <param name="configureDatabasePerTenant">
    /// Optionally configures <see cref="DbContextOptionsBuilder{TContext}"/> for the
    /// <see cref="TenantIsolationStrategy.DatabasePerTenant"/> strategy.
    /// Required if <c>TenantIsolation:Strategy = DatabasePerTenant</c> in configuration.
    /// </param>
    /// <param name="configureSchemaPerTenant">
    /// Optionally configures <see cref="DbContextOptionsBuilder{TContext}"/> for the
    /// <see cref="TenantIsolationStrategy.SchemaPerTenant"/> strategy.
    /// Required if <c>TenantIsolation:Strategy = SchemaPerTenant</c> in configuration.
    /// </param>
    /// <param name="configureTenantSchema">
    /// Optional action to configure <see cref="TenantSchemaOptions"/> (prefix, naming convention).
    /// Only relevant when <paramref name="configureSchemaPerTenant"/> is provided.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// The active strategy is resolved from <c>TenantIsolation:Strategy</c> in
    /// <c>appsettings.json</c> via <see cref="ConfigurationTenantIsolationStrategyProvider"/>.
    /// Register a custom <see cref="ITenantIsolationStrategyProvider"/> before this call to
    /// override the default (e.g., for per-tenant dynamic routing).
    /// </para>
    /// <para>
    /// An invalid <c>TenantIsolation:Strategy</c> value triggers a fail-fast
    /// <see cref="OptionsValidationException"/> at application startup.
    /// </para>
    /// <para>
    /// Each underlying factory is registered as a keyed scoped service using the
    /// <see cref="TenantIsolationStrategy"/> enum value as the key. Calling the
    /// individual <c>AddTenantPer*DbContext</c> extensions alongside this method is
    /// not required and may cause duplicate registrations.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddGranitIsolatedDbContext<TContext>(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder<TContext>> configureShared,
        Action<DbContextOptionsBuilder<TContext>, string>? configureDatabasePerTenant = null,
        Action<DbContextOptionsBuilder<TContext>>? configureSchemaPerTenant = null,
        Action<TenantSchemaOptions>? configureTenantSchema = null)
        where TContext : DbContext
    {
        // Isolation options — fail-fast on invalid appsettings value.
        services.AddOptions<TenantIsolationOptions>()
            .BindConfiguration("TenantIsolation")
            .Validate(
                opts => Enum.IsDefined(opts.Strategy),
                "TenantIsolation:Strategy is not a valid TenantIsolationStrategy value. " +
                "Valid values: SharedDatabase, DatabasePerTenant, SchemaPerTenant.")
            .ValidateOnStart();

        services.TryAddSingleton<ITenantIsolationStrategyProvider,
            ConfigurationTenantIsolationStrategyProvider>();

        // SharedDatabase — always registered; the default fallback strategy.
        SharedDatabaseDbContextOptions<TContext> sharedOpts = new()
        {
            Configure = configureShared,
        };
        services.AddKeyedScoped<IDbContextFactory<TContext>>(
            TenantIsolationStrategy.SharedDatabase,
            (sp, _) => new SharedDatabaseDbContextFactory<TContext>(sp, sharedOpts));

        // DatabasePerTenant — registered only when a configure delegate is provided.
        if (configureDatabasePerTenant is not null)
        {
            TenantPerDatabaseDbContextOptions<TContext> perDbOpts = new()
            {
                Configure = configureDatabasePerTenant,
            };
            services.AddKeyedScoped<IDbContextFactory<TContext>>(
                TenantIsolationStrategy.DatabasePerTenant,
                (sp, _) => new TenantPerDatabaseDbContextFactory<TContext>(
                    sp.GetRequiredService<ICurrentTenant>(),
                    sp.GetRequiredService<ITenantConnectionStringProvider>(),
                    sp,
                    perDbOpts));
        }

        // SchemaPerTenant — registered only when a configure delegate is provided.
        if (configureSchemaPerTenant is not null)
        {
            services.AddOptions<TenantSchemaOptions>()
                .Configure(configureTenantSchema ?? (_ => { }))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.TryAddSingleton<ITenantSchemaProvider, DefaultTenantSchemaProvider>();

            TenantPerSchemaDbContextOptions<TContext> perSchemaOpts = new()
            {
                Configure = configureSchemaPerTenant,
            };
            services.AddKeyedScoped<IDbContextFactory<TContext>>(
                TenantIsolationStrategy.SchemaPerTenant,
                (sp, _) => new TenantPerSchemaDbContextFactory<TContext>(
                    sp.GetRequiredService<ICurrentTenant>(),
                    sp.GetRequiredService<ITenantSchemaProvider>(),
                    sp.GetRequiredService<ITenantSchemaActivator>(),
                    sp,
                    perSchemaOpts));
        }

        // Facade — dispatches to the keyed factory resolved at runtime.
        services.TryAddScoped<IDbContextFactory<TContext>, IsolatedDbContextFactory<TContext>>();

        services.TryAddScoped<TContext>(
            static sp => sp.GetRequiredService<IDbContextFactory<TContext>>().CreateDbContext());

        return services;
    }
}
