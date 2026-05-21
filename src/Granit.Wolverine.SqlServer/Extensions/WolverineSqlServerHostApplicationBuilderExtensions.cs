using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Granit.Wolverine.Internal;
using Granit.Wolverine.SqlServer.Internal;
using Granit.Wolverine.SqlServer.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.SqlServer;

namespace Granit.Wolverine.SqlServer.Extensions;

/// <summary>
/// Extension methods for registering the Granit Wolverine SQL Server provider.
/// </summary>
public static class WolverineSqlServerHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds SQL Server-backed durable messaging (Outbox) for Wolverine.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Requires <c>AddGranitWolverine()</c> to be called first on the builder,
    /// or use <see cref="GranitWolverineSqlServerModule"/> which handles the ordering
    /// automatically via <see cref="Granit.Modularity.DependsOnAttribute"/>.
    /// </para>
    /// <para>
    /// Reads <see cref="WolverineSqlServerOptions"/> from the
    /// <c>"Wolverine:SqlServer"</c> configuration section and validates at startup.
    /// </para>
    /// <para>
    /// Configures:
    /// <list type="bullet">
    ///   <item>SQL Server Outbox — durable at-least-once delivery (ISO 27001-compliant).</item>
    ///   <item>EF Core transaction integration — message dispatch atomic with DB write.</item>
    ///   <item><see cref="WolverineSqlServerOptions.TransactionMode"/> applied to all handlers.</item>
    /// </list>
    /// </para>
    /// <para>
    /// EF Core <c>DbContext</c> types that participate in Wolverine transactions must be
    /// registered via <c>services.AddDbContextWithWolverineIntegration&lt;TContext&gt;()</c>
    /// instead of the standard <c>services.AddDbContext&lt;TContext&gt;()</c>.
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">Optional additional Wolverine configuration.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitWolverineWithSqlServer(
        this IHostApplicationBuilder builder,
        Action<WolverineOptions>? configure = null)
        => AddGranitWolverineWithSqlServerCore(builder, configure);

    /// <summary>
    /// Adds per-tenant database support for Wolverine: each tenant has its own isolated
    /// SQL Server database, required for the strictest GDPR/ISO 27001 physical isolation mandates.
    /// </summary>
    /// <typeparam name="TContext">The tenant-specific <see cref="DbContext"/> type.</typeparam>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">Optional additional Wolverine configuration.</param>
    /// <returns>The builder for chaining.</returns>
    /// <remarks>
    /// <para>
    /// Requires <c>AddGranitWolverine()</c> to be called first on the builder,
    /// and an <see cref="ITenantConnectionStringProvider"/> registered in DI
    /// before the host is built.
    /// </para>
    /// <para>
    /// Reads <see cref="WolverineSqlServerOptions"/> from the <c>"Wolverine:SqlServer"</c>
    /// configuration section. The <c>TransportConnectionString</c> targets the shared Wolverine
    /// Outbox database; per-tenant application data is routed by
    /// <see cref="ITenantConnectionStringProvider"/>.
    /// </para>
    /// <para>
    /// Relies on <see cref="Granit.Wolverine.Behaviors.TenantContextBehavior"/> (registered by
    /// <c>AddGranitWolverine()</c>) to restore <see cref="Granit.MultiTenancy.ICurrentTenant"/>
    /// from the <c>X-Tenant-Id</c> envelope header before the handler resolves its
    /// <typeparamref name="TContext"/>.
    /// </para>
    /// <para>
    /// <see cref="AddGranitWolverineWithSqlServerPerTenant{TContext}"/> and
    /// <see cref="AddGranitWolverineWithSqlServer"/> are mutually exclusive: call only one per host.
    /// </para>
    /// </remarks>
    public static IHostApplicationBuilder AddGranitWolverineWithSqlServerPerTenant<TContext>(
        this IHostApplicationBuilder builder,
        Action<WolverineOptions>? configure = null)
        where TContext : DbContext
    {
        // Register the per-tenant factory and DbContext as Scoped via Granit.Persistence.
        // TryAdd semantics preserve any existing registration (e.g., overrides from integration tests).
        builder.Services.AddTenantPerDatabaseDbContext<TContext>(
            static (opts, connectionString) => opts.UseSqlServer(connectionString));

        return AddGranitWolverineWithSqlServerCore(builder, configure);
    }

    /// <summary>
    /// Shared core setup: options binding, connection string validation, and SQL Server Wolverine configuration.
    /// </summary>
    private static IHostApplicationBuilder AddGranitWolverineWithSqlServerCore(
        IHostApplicationBuilder builder,
        Action<WolverineOptions>? configure)
    {
        // Bind and validate options at startup via DI.
        builder.Services
            .AddOptions<WolverineSqlServerOptions>()
            .BindConfiguration(WolverineSqlServerOptions.SectionName)
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<WolverineSqlServerOptions>,
            WolverineSqlServerOptionsValidator>();

        // Read options directly from IConfiguration: the DI container is not yet
        // built at this point, so IOptions<> is not resolvable inside ConfigureWolverine().
        WolverineSqlServerOptions options = new();
        builder.Configuration
            .GetSection(WolverineSqlServerOptions.SectionName)
            .Bind(options);

        // Resolve the WolverineOptions instance captured by AddGranitWolverine().
        // We apply SQL Server configuration directly on this instance instead of using
        // ConfigureWolverine(), which registers a deferred LambdaWolverineExtension in
        // the IoC container. As of Wolverine 3.0, deferred extensions face a read-only
        // IServiceCollection during DI resolution — PersistMessagesWithSqlServer()
        // needs to register services and would throw InvalidOperationException.
        // Calling it here (during ConfigureServices, Phase 1) keeps services writable.
        WolverineOptions wolverineOptions = builder.Services
            .Select(d => d.ImplementationInstance)
            .OfType<WolverineOptionsHolder>()
            .FirstOrDefault()
            ?.Options
            ?? throw new InvalidOperationException(
                "AddGranitWolverine() must be called before AddGranitWolverineWithSqlServer(). " +
                "Ensure GranitWolverineModule is declared in the [DependsOn] chain.");

        wolverineOptions.PersistMessagesWithSqlServer(options.TransportConnectionString);
        wolverineOptions.UseEntityFrameworkCoreTransactions(options.TransactionMode);
        wolverineOptions.Policies.AutoApplyTransactions();
        configure?.Invoke(wolverineOptions);

        return builder;
    }
}
