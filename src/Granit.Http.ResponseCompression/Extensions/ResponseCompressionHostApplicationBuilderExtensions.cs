using Granit.Http.ResponseCompression.Internal;
using Granit.Http.ResponseCompression.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.Http.ResponseCompression.Extensions;

/// <summary>
/// Extension methods for registering Granit response compression services.
/// </summary>
public static class ResponseCompressionHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds standardized response compression for Granit applications.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reads <see cref="GranitResponseCompressionOptions"/> from the <c>"ResponseCompression"</c>
    /// configuration section and configures ASP.NET Core response compression middleware
    /// with Brotli (primary) + gzip (fallback) providers.
    /// </para>
    /// <para>
    /// Call <c>app.UseGranitResponseCompression()</c> in the middleware pipeline
    /// <b>before</b> any middleware that produces response bodies.
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitResponseCompression(
        this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<GranitResponseCompressionOptions>()
            .BindConfiguration(GranitResponseCompressionOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddSingleton<IConfigureOptions<ResponseCompressionOptions>,
            ConfigureResponseCompressionOptions>();

        builder.Services
            .AddOptions<BrotliCompressionProviderOptions>()
            .Configure<IOptions<GranitResponseCompressionOptions>>((brotli, granit) =>
                brotli.Level = granit.Value.BrotliLevel);

        builder.Services
            .AddOptions<GzipCompressionProviderOptions>()
            .Configure<IOptions<GranitResponseCompressionOptions>>((gzip, granit) =>
                gzip.Level = granit.Value.GzipLevel);

        builder.Services.AddResponseCompression();

        return builder;
    }
}
