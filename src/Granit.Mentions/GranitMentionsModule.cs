using Granit.Authorization;
using Granit.DataLookup;
using Granit.Mentions.Extensions;
using Granit.Modularity;

namespace Granit.Mentions;

/// <summary>
/// Registers the domain-neutral <c>@</c> mention seam: the registry and the <c>mentions</c> facade
/// over <c>Granit.DataLookup</c>. Applications add resolvers with
/// <c>AddGranitMentions(b =&gt; b.Add&lt;TResolver&gt;())</c>. Depends on the data-lookup module (the
/// picker transport) and the authorization module (per-type ACL).
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
