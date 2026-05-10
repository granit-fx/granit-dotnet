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
public static class IoServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ITempFileFactory"/>, <see cref="IoMetrics"/>, and the
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
            .ValidateOnStart();

        if (configure is not null)
        {
            services.PostConfigure(configure);
        }

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IoMetrics>();
        services.TryAddSingleton<ITempFileFactory, DefaultTempFileFactory>();
        services.AddHostedService<TempFileJanitor>();

        GranitActivitySourceRegistry.Register(IoActivitySource.Name);

        return services;
    }
}
