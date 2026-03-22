using Microsoft.Extensions.Hosting;
using static Granit.Persistence.ExtraProperties.ExtraPropertyServiceCollectionExtensions;

namespace Granit.Persistence.ExtraProperties;

/// <summary>
/// Hosted service that populates the <see cref="ExtraPropertyMappingRegistry"/>
/// from all registered <see cref="IConfigureExtraPropertyRegistry"/> instances at startup.
/// </summary>
internal sealed class ExtraPropertyMappingRegistryInitializer(
    ExtraPropertyMappingRegistry registry,
    IEnumerable<IConfigureExtraPropertyRegistry> configurators) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (IConfigureExtraPropertyRegistry configurator in configurators)
        {
            configurator.Configure(registry);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
