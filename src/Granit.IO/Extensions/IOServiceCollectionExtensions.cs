using Granit.Diagnostics;
using Granit.IO.Diagnostics;
using Granit.IO.Internal;
using Granit.IO.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.IO.Extensions;

/// <summary>
/// DI extensions for <c>Granit.IO</c>.
/// </summary>
public static class IOServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ITempFileFactory"/>, <see cref="IOMetrics"/>, and the
    /// temp-file janitor hosted service.
    /// </summary>
    /// <param name="services">DI container.</param>
    /// <param name="configure">Optional post-binding configuration delegate.</param>
    public static IServiceCollection AddGranitTempFiles(
        this IServiceCollection services,
        Action<TempFileOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<TempFileOptions>()
            .BindConfiguration(TempFileOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(
                o => o.MaxLifetime > TimeSpan.Zero && o.JanitorInterval > TimeSpan.Zero,
                "MaxLifetime and JanitorInterval must be strictly positive.")
            .ValidateOnStart();

        if (configure is not null)
        {
            services.PostConfigure(configure);
        }

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IOMetrics>();
        services.TryAddSingleton<ITempFileFactory, DefaultTempFileFactory>();
        services.AddHostedService<TempFileJanitor>();

        GranitActivitySourceRegistry.Register(IOActivitySource.Name);

        return services;
    }
}
