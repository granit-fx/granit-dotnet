using System.Diagnostics.CodeAnalysis;
using Granit.Diagnostics;
using Granit.Imaging.Diagnostics;
using Granit.Imaging.MagickNet.Diagnostics;
using Granit.Imaging.MagickNet.Internal;
using Granit.Imaging.MagickNet.Options;
using Granit.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.Imaging.MagickNet.Extensions;

/// <summary>
/// Extension methods for registering <c>Granit.Imaging.MagickNet</c> services.
/// </summary>
// DI wiring only — behavior covered by ImagingMagickNetServiceRegistrationTests.
[ExcludeFromCodeCoverage]
public static class ImagingMagickNetHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers the Magick.NET implementation of <see cref="IImageProcessor"/>
    /// with secure defaults for resource limits and format allowlisting.
    /// </summary>
    /// <remarks>
    /// Binds <see cref="ImagingMagickNetOptions"/> from the <c>Imaging:MagickNet</c>
    /// configuration section (validated at startup). The <paramref name="configure"/>
    /// callback runs after configuration binding, so code-supplied values win over
    /// configuration. ImageMagick resource limits are applied when the host starts,
    /// via <see cref="MagickNetResourceLimitsInitializer"/>.
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">Optional callback to customize security and resource options.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitImagingMagickNet(
        this IHostApplicationBuilder builder,
        Action<ImagingMagickNetOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        GranitActivitySourceRegistry.Register(ImagingMagickNetActivitySource.Name);

        builder.Services
            .AddOptions<ImagingMagickNetOptions>()
            .BindConfiguration(ImagingMagickNetOptions.SectionName)
            .ValidateOnStart();

        if (configure is not null)
        {
            // Registered AFTER BindConfiguration so code-supplied values win over configuration.
            builder.Services.Configure(configure);
        }

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<ImagingMagickNetOptions>, ImagingMagickNetOptionsValidator>());
        builder.Services.AddHostedService<MagickNetResourceLimitsInitializer>();

        // Soft multi-tenancy dependency: real implementation comes from Granit.MultiTenancy when present.
        builder.Services.TryAddSingleton<ICurrentTenant>(NullTenantContext.Instance);
        builder.Services.TryAddSingleton<ImagingMetrics>();
        builder.Services.TryAddSingleton<IImageProcessor, MagickNetImageProcessor>();
        return builder;
    }
}
