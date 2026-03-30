using System.Reflection;
using Granit.Modularity;
using Granit.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Granit.Extensions;

/// <summary>
/// Extensions sur <see cref="IHostApplicationBuilder"/> pour enregistrer
/// le systeme de modules Granit.
/// </summary>
public static class GranitHostBuilderExtensions
{
    /// <summary>
    /// Discovers and configures all Granit modules from the root module
    /// <typeparamref name="TModule"/> (synchronous version).
    /// </summary>
    /// <typeparam name="TModule">Root module of the application.</typeparam>
    public static IHostApplicationBuilder AddGranit<TModule>(
        this IHostApplicationBuilder builder)
        where TModule : GranitModule =>
        AddGranitCore(builder, ModuleLoader.LoadModules<TModule>());

    /// <summary>
    /// Configures Granit modules using a fluent builder API (synchronous version).
    /// Coexists with <see cref="DependsOnAttribute"/> — modules added via the builder
    /// still have their <c>[DependsOn]</c> dependencies resolved automatically.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">Action to configure the <see cref="GranitBuilder"/>.</param>
    /// <returns>The host application builder for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.AddGranit(granit => granit
    ///     .AddModule&lt;GranitPersistenceEntityFrameworkCoreModule&gt;()
    ///     .AddModule&lt;GranitObservabilityModule&gt;()
    ///     .AddModule&lt;AppHostModule&gt;()
    /// );
    /// </code>
    /// </example>
    public static IHostApplicationBuilder AddGranit(
        this IHostApplicationBuilder builder,
        Action<GranitBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        GranitBuilder granitBuilder = new();
        configure(granitBuilder);

        IReadOnlyList<ModuleDescriptor> modules = ModuleLoader.LoadModules(granitBuilder.ModuleTypes);
        return AddGranitCore(builder, modules);
    }

    /// <summary>
    /// Discovers and configures all Granit modules from the root module
    /// <typeparamref name="TModule"/> (asynchronous version).
    /// </summary>
    /// <typeparam name="TModule">Root module of the application.</typeparam>
    public static Task<IHostApplicationBuilder> AddGranitAsync<TModule>(
        this IHostApplicationBuilder builder)
        where TModule : GranitModule =>
        AddGranitCoreAsync(builder, ModuleLoader.LoadModules<TModule>());

    /// <summary>
    /// Configures Granit modules using a fluent builder API (asynchronous version).
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">Action to configure the <see cref="GranitBuilder"/>.</param>
    /// <returns>The host application builder for chaining.</returns>
    public static Task<IHostApplicationBuilder> AddGranitAsync(
        this IHostApplicationBuilder builder,
        Action<GranitBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        GranitBuilder granitBuilder = new();
        configure(granitBuilder);

        IReadOnlyList<ModuleDescriptor> modules = ModuleLoader.LoadModules(granitBuilder.ModuleTypes);
        return AddGranitCoreAsync(builder, modules);
    }

    private static IHostApplicationBuilder AddGranitCore(
        IHostApplicationBuilder builder,
        IReadOnlyList<ModuleDescriptor> modules)
    {
        ILogger<GranitApplication> logger = CreateBootstrapLogger(builder);
        GranitApplication application = new(modules, logger);
        IReadOnlyList<Assembly> moduleAssemblies = GetDistinctModuleAssemblies(modules);

        ServiceConfigurationContext context = new(
            builder.Services,
            builder.Configuration,
            builder,
            moduleAssemblies);

        builder.Services.TryAddSingleton<ICurrentTenant>(NullTenantContext.Instance);

        application.ConfigureServices(context);

        builder.Services.AddSingleton(application);

        return builder;
    }

    private static async Task<IHostApplicationBuilder> AddGranitCoreAsync(
        IHostApplicationBuilder builder,
        IReadOnlyList<ModuleDescriptor> modules)
    {
        ILogger<GranitApplication> logger = CreateBootstrapLogger(builder);
        GranitApplication application = new(modules, logger);
        IReadOnlyList<Assembly> moduleAssemblies = GetDistinctModuleAssemblies(modules);

        ServiceConfigurationContext context = new(
            builder.Services,
            builder.Configuration,
            builder,
            moduleAssemblies);

        builder.Services.TryAddSingleton<ICurrentTenant>(NullTenantContext.Instance);

        await application.ConfigureServicesAsync(context).ConfigureAwait(false);

        builder.Services.AddSingleton(application);

        return builder;
    }

    private static IReadOnlyList<Assembly> GetDistinctModuleAssemblies(
        IReadOnlyList<ModuleDescriptor> modules) =>
        [.. modules.Select(m => m.ModuleType.Assembly).Distinct()];

    /// <summary>
    /// Creates a bootstrap logger from the host builder's logging configuration.
    /// This logger is available before the full DI container is built.
    /// </summary>
    private static ILogger<GranitApplication> CreateBootstrapLogger(
        IHostApplicationBuilder builder)
    {
        using ILoggerFactory factory = LoggerFactory.Create(lb =>
            lb.AddConfiguration(builder.Configuration.GetSection("Logging")));
        return factory.CreateLogger<GranitApplication>();
    }
}
