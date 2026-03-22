using Granit.Bff.Options;
using Granit.Bff.Yarp.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Transforms;

namespace Granit.Bff.Yarp.Extensions;

/// <summary>
/// Extension methods for configuring BFF YARP reverse proxy services.
/// </summary>
public static class BffYarpHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the YARP reverse proxy with BFF token injection and CSRF validation transforms.
    /// Reads YARP configuration from the <c>ReverseProxy</c> configuration section.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for further chaining.</returns>
    public static WebApplicationBuilder AddGranitBffYarp(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.Configure<GranitBffOptions>(
            builder.Configuration.GetSection(GranitBffOptions.SectionName));

        builder.Services
            .AddReverseProxy()
            .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
            .AddTransforms(context =>
            {
                // CSRF validation runs first (blocks 403 before token injection)
                context.RequestTransforms.Add(
                    context.Services.GetRequiredService<BffCsrfValidationTransform>());

                // Token injection + silent refresh
                context.RequestTransforms.Add(
                    context.Services.GetRequiredService<BffTokenInjectionTransform>());
            });

        builder.Services.AddScoped<BffTokenInjectionTransform>();
        builder.Services.AddScoped<BffCsrfValidationTransform>();

        return builder;
    }
}
