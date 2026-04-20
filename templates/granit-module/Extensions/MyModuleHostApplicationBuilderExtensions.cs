using Granit.Diagnostics;
using Granit.MyModule.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.MyModule.Extensions;

/// <summary>Extension methods for registering the Granit MyModule infrastructure.</summary>
public static class MyModuleHostApplicationBuilderExtensions
{
    /// <summary>Adds the Granit MyModule infrastructure (metrics, tracing, services).</summary>
    public static IHostApplicationBuilder AddGranitMyModule(this IHostApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton<MyModuleMetrics>();

        // Register your module services here:
        // builder.Services.TryAddTransient<IMyService, DefaultMyService>();

        // Register query / export definitions when the module exposes admin-visible entities:
        // builder.Services.AddQueryDefinition<MyEntity, MyEntityQueryDefinition>();
        // builder.Services.AddExportDefinition<MyEntity, MyEntityExportDefinition>();

        GranitActivitySourceRegistry.Register(MyModuleActivitySource.Name);
        return builder;
    }
}
