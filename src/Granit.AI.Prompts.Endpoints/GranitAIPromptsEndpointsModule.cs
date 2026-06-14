using Granit.AI.Prompts.Endpoints.Internal;
using Granit.Authorization;
using Granit.Http.ApiDocumentation;
using Granit.Localization.Extensions;
using Granit.Modularity;
using Granit.Validation;

namespace Granit.AI.Prompts.Endpoints;

/// <summary>
/// Module for the prompt-catalogue endpoints (CRUD, copy-on-customise, picker). Permission
/// definition providers are auto-discovered by <c>GranitAuthorizationModule</c>.
/// </summary>
[DependsOn(
    typeof(GranitAIPromptsModule),
    typeof(GranitAuthorizationModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitValidationModule))]
public sealed class GranitAIPromptsEndpointsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddLocalizationResource<AIPromptsEndpointsLocalizationResource>();
}
