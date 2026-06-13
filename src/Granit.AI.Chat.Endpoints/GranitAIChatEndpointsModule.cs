using Granit.AI.Chat.Endpoints.Internal;
using Granit.Authorization;
using Granit.Http.ApiDocumentation;
using Granit.Localization.Extensions;
using Granit.Modularity;
using Granit.Validation;

namespace Granit.AI.Chat.Endpoints;

/// <summary>
/// Module for chat conversation management endpoints. Permission definition providers are
/// auto-discovered by <c>GranitAuthorizationModule</c>.
/// </summary>
[DependsOn(
    typeof(GranitAIChatModule),
    typeof(GranitAuthorizationModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitValidationModule))]
public sealed class GranitAIChatEndpointsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddLocalizationResource<AIChatEndpointsLocalizationResource>();
}
