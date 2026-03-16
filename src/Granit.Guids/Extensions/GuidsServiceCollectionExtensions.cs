using Granit.Guids;
using Granit.Guids.Options;
using Granit.Timing.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Granit.Guids.Extensions;

/// <summary>
/// Extensions for registering Guids module services.
/// </summary>
public static class GuidsServiceCollectionExtensions
{
    /// <summary>
    /// Adds <see cref="IGuidGenerator"/> using the configured <see cref="GuidStrategy"/>.
    /// Defaults to <see cref="GuidStrategy.UuidV7"/> (<see cref="Guid.CreateVersion7()"/>).
    /// </summary>
    /// <remarks>
    /// Uses <see cref="ServiceCollectionDescriptorExtensions.TryAddSingleton{TService}(IServiceCollection)"/>
    /// — a custom <see cref="IGuidGenerator"/> registered before this call takes precedence.
    /// </remarks>
    public static IServiceCollection AddGranitGuids(
        this IServiceCollection services,
        Action<GuidGeneratorOptions>? configure = null)
    {
        if (configure is not null)
        {
            services.Configure(configure);
        }

        // IClock is required by SequentialGuidGenerator; TimeProvider by UuidV7GuidGenerator
        services.AddGranitTiming();

        services.TryAddSingleton<SequentialGuidGenerator>();
        services.TryAddSingleton<UuidV7GuidGenerator>();

        services.TryAddSingleton<IGuidGenerator>(sp =>
        {
            GuidGeneratorOptions opts = sp.GetRequiredService<IOptions<GuidGeneratorOptions>>().Value;
            return opts.Strategy switch
            {
                GuidStrategy.Sequential => sp.GetRequiredService<SequentialGuidGenerator>(),
                GuidStrategy.Random => SimpleGuidGenerator.Instance,
                _ => sp.GetRequiredService<UuidV7GuidGenerator>(),
            };
        });

        return services;
    }
}
