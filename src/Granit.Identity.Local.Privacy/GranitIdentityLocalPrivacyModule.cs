using Granit.Identity.Local.Privacy.DataExport;
using Granit.Modularity;
using Granit.Privacy.BlobStorage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Identity.Local.Privacy;

/// <summary>
/// Granit module that registers the <see cref="IdentityLocalPrivacyDataProvider"/> so the
/// built-in local identity module participates in the privacy export scatter-gather saga.
/// </summary>
/// <remarks>
/// The matching Wolverine handler (<see cref="IdentityLocalPersonalDataExportHandler"/>)
/// is discovered automatically by assembly scanning — loading this module is enough.
/// Apps still need to opt-in via
/// <c>AddGranitPrivacy(p =&gt; p.AddGranitIdentityLocalPrivacyProvider())</c>
/// so the provider name is registered with the scatter-gather registry.
/// </remarks>
[DependsOn(
    typeof(GranitIdentityLocalModule),
    typeof(GranitPrivacyBlobStorageModule))]
public sealed class GranitIdentityLocalPrivacyModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.TryAddScoped<IdentityLocalPrivacyDataProvider>();
}
