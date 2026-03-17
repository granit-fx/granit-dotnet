using Granit.Http.Cors.Internal;
using Granit.Http.Cors.Options;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.Http.Cors.Extensions;

/// <summary>
/// Extension methods for registering Granit CORS services.
/// </summary>
public static class CorsHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds standardized CORS configuration for Granit applications.
    /// </summary>
    /// <remarks>
    /// Reads <see cref="GranitCorsOptions"/> from the <c>"Cors"</c> configuration section
    /// and configures ASP.NET Core CORS middleware with a default policy.
    /// <para>
    /// The default policy applies <see cref="CorsPolicyBuilder.AllowAnyHeader"/> and
    /// <see cref="CorsPolicyBuilder.AllowAnyMethod"/> (standard for REST APIs).
    /// Origins are restricted to <see cref="GranitCorsOptions.AllowedOrigins"/>.
    /// </para>
    /// <para>
    /// ISO 27001 compliance: wildcard origins are rejected in non-development environments
    /// at startup via options validation.
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitCors(
        this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<GranitCorsOptions>()
            .BindConfiguration(GranitCorsOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddSingleton<IValidateOptions<GranitCorsOptions>,
            GranitCorsOptionsValidator>();

        builder.Services.AddSingleton<IConfigureOptions<CorsOptions>,
            ConfigureCorsPolicyOptions>();

        builder.Services.AddCors();

        return builder;
    }
}
