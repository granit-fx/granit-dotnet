using Granit.Diagnostics;
using Granit.Parties.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Parties.Extensions;

/// <summary>Extension methods for registering the Granit parties infrastructure.</summary>
public static class PartiesHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the Granit parties module — diagnostics (metrics + ActivitySource) and any
    /// shared singletons. Persistence (<c>Granit.Parties.EntityFrameworkCore</c>),
    /// endpoints, and privacy handlers register their own services.
    /// </summary>
    public static IHostApplicationBuilder AddGranitParties(this IHostApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton<PartiesMetrics>();
        GranitActivitySourceRegistry.Register(PartiesActivitySource.Name);
        return builder;
    }
}
