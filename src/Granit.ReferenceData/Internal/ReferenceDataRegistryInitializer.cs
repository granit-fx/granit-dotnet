using Microsoft.Extensions.Hosting;

namespace Granit.ReferenceData.Internal;

/// <summary>
/// Hosted service that populates the <see cref="ReferenceDataRegistry"/>
/// from all registered <see cref="IReferenceDataRegistryContributor"/> instances at startup.
/// </summary>
internal sealed class ReferenceDataRegistryInitializer(
    ReferenceDataRegistry registry,
    IEnumerable<IReferenceDataRegistryContributor> contributors) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (IReferenceDataRegistryContributor contributor in contributors)
        {
            contributor.Configure(registry);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
