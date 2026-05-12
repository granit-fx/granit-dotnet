using Granit.Documents.PublicLinks.Endpoints.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// DI extensions for <c>Granit.Documents.PublicLinks.Endpoints</c>.
/// </summary>
public static class DocumentsPublicLinksEndpointsServiceCollectionExtensions
{
    /// <summary>
    /// Binds <see cref="DocumentsPublicLinksEndpointsOptions"/> from configuration
    /// (section <see cref="DocumentsPublicLinksEndpointsOptions.SectionName"/>) and
    /// enables data-annotation validation at startup. Hosts that prefer code-only
    /// configuration can skip this call and pass an
    /// <see cref="System.Action{T}"/> to <c>MapGranitDocumentsPublicLinks</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitDocumentsPublicLinksEndpoints(this IServiceCollection services)
    {
        System.ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<DocumentsPublicLinksEndpointsOptions>()
            .BindConfiguration(DocumentsPublicLinksEndpointsOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
