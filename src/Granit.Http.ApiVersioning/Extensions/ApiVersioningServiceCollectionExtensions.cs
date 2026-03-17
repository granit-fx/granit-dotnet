using Asp.Versioning;
using Granit.Http.ApiVersioning.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Http.ApiVersioning.Extensions;

/// <summary>
/// Extensions for registering Granit API versioning services.
/// </summary>
public static class ApiVersioningServiceCollectionExtensions
{
    /// <summary>
    /// Adds URL-based API versioning with query string fallback.
    /// Primary reader: <c>/api/v{version:apiVersion}/resource</c>.
    /// Fallback reader: <c>?api-version=1.0</c> (visible in access logs).
    /// </summary>
    public static IServiceCollection AddGranitApiVersioning(
        this IServiceCollection services)
    {
        services
            .AddOptions<GranitApiVersioningOptions>()
            .BindConfiguration(GranitApiVersioningOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddApiVersioning()
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

        // Deferred configuration: ApiVersioningOptions reads GranitApiVersioningOptions at resolution time.
        services
            .AddOptions<ApiVersioningOptions>()
            .Configure<IOptions<GranitApiVersioningOptions>>((options, granitOpts) =>
            {
                GranitApiVersioningOptions granit = granitOpts.Value;
                options.DefaultApiVersion = new ApiVersion(granit.DefaultMajorVersion);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = granit.ReportApiVersions;
                options.ApiVersionReader = ApiVersionReader.Combine(
                    new UrlSegmentApiVersionReader(),
                    new QueryStringApiVersionReader("api-version"));
            });

        return services;
    }
}
