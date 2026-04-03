using Granit.Diagnostics;
using Granit.Tax.Diagnostics;
using Granit.Tax.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Tax.Extensions;

/// <summary>
/// Extension methods for registering the Granit tax infrastructure.
/// </summary>
public static class TaxHostApplicationBuilderExtensions
{
    /// <summary>Adds the Granit tax infrastructure.</summary>
    public static IHostApplicationBuilder AddGranitTax(
        this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<TaxOptions>()
            .BindConfiguration(TaxOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.TryAddSingleton<TaxMetrics>();
        GranitActivitySourceRegistry.Register(TaxActivitySource.Name);

        return builder;
    }
}
