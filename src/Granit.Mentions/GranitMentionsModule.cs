using Granit.Authorization;
using Granit.DataLookup;
using Granit.Mentions.Extensions;
using Granit.Modularity;

namespace Granit.Mentions;

/// <summary>
/// Registers the domain-neutral <c>@</c> mention seam: the <c>mentions</c> facade source over
/// <c>Granit.DataLookup</c>. A mention is just an existing lookup source tagged mentionable;
/// applications opt sources in with <c>services.AddMentionSource("user")</c>. Depends on the
/// data-lookup module (the picker transport) and the authorization module (per-type ACL).
/// </summary>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitDataLookupModule))]
public sealed class GranitMentionsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitMentions();
}
