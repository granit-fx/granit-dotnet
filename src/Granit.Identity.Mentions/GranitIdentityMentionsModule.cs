using Granit.Identity.Endpoints;
using Granit.Identity.Mentions.Extensions;
using Granit.Mentions;
using Granit.Modularity;

namespace Granit.Identity.Mentions;

/// <summary>
/// Wires <c>Granit.Identity</c> as an <c>IMentionResolver</c> (the <c>@user</c> type) for the
/// domain-neutral mention seam. Depends on the mentions module (registry + picker facade) and the
/// identity endpoints module so the <c>Identity.Users.Read</c> permission the resolver gates on is
/// declared at runtime.
/// </summary>
[DependsOn(
    typeof(GranitMentionsModule),
    typeof(GranitIdentityModule),
    typeof(GranitIdentityEndpointsModule))]
public sealed class GranitIdentityMentionsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddIdentityMentions();
}
