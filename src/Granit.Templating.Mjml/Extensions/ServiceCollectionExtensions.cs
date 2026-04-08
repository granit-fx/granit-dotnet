using Granit.Templating.Extensions;
using Granit.Templating.Mjml.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Templating.Mjml.Extensions;

/// <summary>
/// Extension methods for registering <c>Granit.Templating.Mjml</c> services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the MJML template transformer that compiles MJML markup to email-safe HTML.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Templates containing <c>&lt;mjml&gt;</c> root elements are compiled to table-based HTML
    /// with inline CSS and MSO conditional comments. Plain HTML templates pass through unchanged.
    /// </para>
    /// <para>
    /// Also calls <see cref="Templating.Extensions.ServiceCollectionExtensions.AddGranitTemplating"/>
    /// to ensure the core pipeline is registered.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitTemplatingWithMjml(
        this IServiceCollection services)
    {
        services.AddGranitTemplating();
        services.AddRenderedContentTransformer<MjmlTransformer>();
        return services;
    }
}
