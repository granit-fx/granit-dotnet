using Granit.Modularity;
using Granit.Parties.Privacy.DataExport;
using Granit.Privacy.BlobStorage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Parties.Privacy;

/// <summary>
/// Granit module that registers the <see cref="PartiesPrivacyDataProvider"/> so the
/// parties module participates in the privacy export scatter-gather saga, and wires
/// the personal-data deletion handler that pseudonymises the user-linked party on
/// Article 17 erasure.
/// </summary>
/// <remarks>
/// Kept separate from <c>Granit.Parties</c> so apps that do not load the privacy stack
/// do not inherit Privacy + BlobStorage dependencies. Apps opt in via
/// <c>AddGranitPrivacy(p =&gt; p.AddGranitPartiesPrivacyProvider())</c>.
/// </remarks>
[DependsOn(
    typeof(GranitPartiesModule),
    typeof(GranitPrivacyBlobStorageModule))]
public sealed class GranitPartiesPrivacyModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.TryAddScoped<PartiesPrivacyDataProvider>();
}
