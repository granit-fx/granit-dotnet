using Granit.AI.Chat.Endpoints.Internal;
using Granit.Authorization;
using Granit.Http.ApiDocumentation;
using Granit.Http.RateLimiting;
using Granit.Localization.Extensions;
using Granit.Modularity;
using Granit.Validation;

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
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddLocalizationResource<AIChatEndpointsLocalizationResource>();
}
