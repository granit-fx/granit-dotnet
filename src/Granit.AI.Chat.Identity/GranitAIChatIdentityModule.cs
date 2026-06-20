using Granit.AI.Chat.Identity.Extensions;
using Granit.Identity;
using Granit.Identity.Endpoints;
using Granit.Modularity;

namespace Granit.AI.Chat.Identity;

/// <summary>
/// Wires <c>Granit.Identity</c> as an <c>IAIMentionResolver</c> for <c>Granit.AI.Chat</c>: the chat
/// <c>@</c> picker can search the identity directory and a <c>@user</c> mention resolves to the
/// user's context, both under the caller's ACLs (ADR-067). Depends on the identity endpoints module
/// so the <c>Identity.Users.Read</c> permission the resolver gates on is declared at runtime.
/// </summary>
[DependsOn(
    typeof(GranitAIChatModule),
    typeof(GranitIdentityModule),
    typeof(GranitIdentityEndpointsModule))]
public sealed class GranitAIChatIdentityModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddIdentityChatMentions();
}
