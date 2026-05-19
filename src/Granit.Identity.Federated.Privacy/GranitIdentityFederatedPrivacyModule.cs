using Granit.Identity.Federated.Privacy.DataExport;
using Granit.Modularity;
using Granit.Privacy.BlobStorage;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Identity.Federated.Privacy;

/// <summary>
/// Granit module that registers the <see cref="IdentityFederatedPrivacyDataProvider"/> so the
/// federated identity cache entry participates in the privacy export scatter-gather saga.
/// </summary>
/// <remarks>
/// The matching Wolverine handler is discovered automatically by assembly scanning. Apps
/// opt in via
/// <c>AddGranitPrivacy(p =&gt; p.AddGranitIdentityFederatedPrivacyProvider())</c>.
/// </remarks>
[DependsOn(
    typeof(GranitIdentityFederatedModule),
    typeof(GranitPrivacyBlobStorageModule))]
public sealed class GranitIdentityFederatedPrivacyModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.TryAddScoped<IdentityFederatedPrivacyDataProvider>();
}
