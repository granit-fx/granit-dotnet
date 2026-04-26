using Granit.Contacts.Privacy.DataExport;
using Granit.Modularity;
using Granit.Privacy.BlobStorage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Contacts.Privacy;

/// <summary>
/// Granit module that registers the <see cref="ContactsPrivacyDataProvider"/> so the
/// contacts module participates in the privacy export scatter-gather saga, and wires
/// the personal-data deletion handler that pseudonymises the user-linked contact on
/// Article 17 erasure.
/// </summary>
/// <remarks>
/// Kept separate from <c>Granit.Contacts</c> so apps that do not load the privacy stack
/// do not inherit Privacy + BlobStorage dependencies. Apps opt in via
/// <c>AddGranitPrivacy(p =&gt; p.AddGranitContactsPrivacyProvider())</c>.
/// </remarks>
[DependsOn(
    typeof(GranitContactsModule),
    typeof(GranitPrivacyBlobStorageModule))]
public sealed class GranitContactsPrivacyModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.TryAddScoped<ContactsPrivacyDataProvider>();
}
