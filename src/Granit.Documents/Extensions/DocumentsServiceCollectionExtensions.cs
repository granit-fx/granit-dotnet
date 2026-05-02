using Granit.Documents.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Documents.Extensions;

/// <summary>
/// Extensions for registering Granit.Documents module services.
/// </summary>
public static class DocumentsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Granit.Documents module with the provided configuration.
    /// </summary>
    /// <param name="services">The DI service collection.</param>
    /// <param name="configure">
    /// Optional callback to override the defaults of <see cref="GranitDocumentsOptions"/>.
    /// When omitted, options are bound from the <c>"Documents"</c> configuration section
    /// (when present) and validated on application start.
    /// </param>
    /// <remarks>
    /// Phase-1 scaffolding registration: only options binding is performed. Domain
    /// services, endpoints, and persistence are wired by subsequent stories of the
    /// Granit.Documents Epic.
    /// </remarks>
    public static IServiceCollection AddGranitDocuments(
        this IServiceCollection services,
        Action<GranitDocumentsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        OptionsBuilder<GranitDocumentsOptions> optionsBuilder = services
            .AddOptions<GranitDocumentsOptions>()
            .BindConfiguration(GranitDocumentsOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        return services;
    }
}
