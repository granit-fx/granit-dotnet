using Granit.Http.ApiDocumentation.Scalar.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Http.ApiDocumentation.Scalar.Extensions;

/// <summary>
/// Extensions for registering the Scalar UI options.
/// </summary>
public static class ScalarServiceCollectionExtensions
{
    /// <summary>
    /// Binds <see cref="ScalarOptions"/> from the
    /// <c>"Http:ApiDocumentation:Scalar"</c> configuration section.
    /// Call <c>app.UseGranitApiDocumentation()</c> in <c>Program.cs</c> to map the
    /// OpenAPI JSON endpoints and the Scalar interactive UI.
    /// </summary>
    public static IServiceCollection AddGranitApiDocumentationScalar(
        this IServiceCollection services)
    {
        services
            .AddOptions<ScalarOptions>()
            .BindConfiguration(ScalarOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
