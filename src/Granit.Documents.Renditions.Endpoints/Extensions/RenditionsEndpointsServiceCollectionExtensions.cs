using Granit.Documents.Renditions.Endpoints.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// DI extensions for the Granit.Documents.Renditions endpoints surface.
/// </summary>
public static class RenditionsEndpointsServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="RenditionsEndpointsOptions"/> bound from configuration section
    /// <c>Documents:Renditions:Endpoints</c>. Data annotations are validated at startup.
    /// </summary>
    public static IServiceCollection AddGranitDocumentsRenditionsEndpoints(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddOptions<RenditionsEndpointsOptions>()
            .BindConfiguration(RenditionsEndpointsOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
