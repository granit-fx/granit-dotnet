using Granit.AI.Chat.Endpoints.Internal;
using Granit.AI.Chat.Mentions;
using Granit.Authorization;
using Granit.Http.ApiDocumentation;
using Granit.Http.RateLimiting;
using Granit.Localization.Extensions;
using Granit.Modularity;
using Granit.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.AI.Chat.Endpoints;

/// <summary>
/// Module for chat conversation management endpoints. Permission definition providers are
/// auto-discovered by <c>GranitAuthorizationModule</c>. Depends on the HTTP rate-limiting module so
/// the send endpoint's quota (<c>AIChatRateLimitPolicies.Send</c>) is always wired.
/// </summary>
[DependsOn(
    typeof(GranitAIChatModule),
    typeof(GranitAuthorizationModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitHttpRateLimitingModule),
    typeof(GranitValidationModule))]
public sealed class GranitAIChatEndpointsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddLocalizationResource<AIChatEndpointsLocalizationResource>();

        // Replace the base permissive authorizer: in an HTTP host, mention access is bound to the
        // caller's permission grants (a resolver's RequiredPermission).
        context.Services.Replace(
            ServiceDescriptor.Scoped<IAIMentionAuthorizer, PermissionMentionAuthorizer>());
    }
}
