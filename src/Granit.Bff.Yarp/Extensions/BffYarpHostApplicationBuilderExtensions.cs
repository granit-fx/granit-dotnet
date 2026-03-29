using Granit.Bff.Options;
using Granit.Bff.Yarp.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
                // Resolve transforms from the request scope, not the root provider.
                // YARP calls AddTransforms during InitialLoadAsync (startup, no scope).
                // ScopedRequestTransform defers resolution to HttpContext.RequestServices.

                // CSRF validation runs first (blocks 403 before token injection)
                context.RequestTransforms.Add(
                    new ScopedRequestTransform<BffCsrfValidationTransform>());

                // Token injection + silent refresh
                context.RequestTransforms.Add(
                    new ScopedRequestTransform<BffTokenInjectionTransform>());
            });

        builder.Services.AddScoped<BffTokenInjectionTransform>();
        builder.Services.AddScoped<BffCsrfValidationTransform>();

        return builder;
    }

    /// <summary>
    /// Delegating transform that resolves a scoped <typeparamref name="T"/> from
    /// <see cref="HttpContext.RequestServices"/> at request time, avoiding the
    /// "Cannot resolve scoped service from root provider" error during YARP startup.
    /// </summary>
    private sealed class ScopedRequestTransform<T> : RequestTransform where T : RequestTransform
    {
        public override ValueTask ApplyAsync(RequestTransformContext context)
        {
            T transform = context.HttpContext.RequestServices.GetRequiredService<T>();
            return transform.ApplyAsync(context);
        }
    }
}
