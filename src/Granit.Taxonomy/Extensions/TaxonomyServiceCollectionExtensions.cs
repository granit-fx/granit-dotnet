using Granit.Taxonomy.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Taxonomy.Extensions;

/// <summary>
/// Extensions for registering Granit.Taxonomy module services.
/// </summary>
public static class TaxonomyServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Granit.Taxonomy module.
    /// </summary>
    /// <param name="services">The DI service collection.</param>
    /// <param name="configure">
    /// Optional callback to override the defaults of <see cref="TaxonomyOptions"/>.
    /// When omitted, options are bound from the <c>"Taxonomy"</c> configuration section.
    /// </param>
    /// <remarks>
    /// Phase T1 — no-op beyond options binding. Tag and category services are wired
    /// by <c>Granit.Taxonomy.EntityFrameworkCore</c> in story T1.2.
    /// </remarks>
    public static IServiceCollection AddGranitTaxonomy(
        this IServiceCollection services,
        Action<TaxonomyOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        OptionsBuilder<TaxonomyOptions> optionsBuilder = services
            .AddOptions<TaxonomyOptions>()
            .BindConfiguration(TaxonomyOptions.SectionName);

        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        return services;
    }
}
