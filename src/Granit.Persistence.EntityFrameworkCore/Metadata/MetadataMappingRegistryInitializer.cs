using Microsoft.Extensions.Hosting;
using static Granit.Persistence.EntityFrameworkCore.Metadata.MetadataServiceCollectionExtensions;

namespace Granit.Persistence.EntityFrameworkCore.Metadata;

/// <summary>
/// Hosted service that populates the <see cref="MetadataMappingRegistry"/>
/// from all registered <see cref="IConfigureMetadataRegistry"/> instances at startup.
/// </summary>
internal sealed class MetadataMappingRegistryInitializer(
    MetadataMappingRegistry registry,
    IEnumerable<IConfigureMetadataRegistry> configurators) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (IConfigureMetadataRegistry configurator in configurators)
        {
            configurator.Configure(registry);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
