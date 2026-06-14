using Granit.AI.Prompts.Privacy.DataExport;
using Granit.Modularity;
using Granit.Privacy.BlobStorage;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.AI.Prompts.Privacy;

/// <summary>
/// Granit module that registers the <see cref="PromptTemplatePrivacyDataProvider"/> so user prompts
/// participate in the privacy export scatter-gather saga (GDPR Art. 15/20). The matching export
/// handler and the personal-data deletion handler are discovered automatically by Wolverine assembly
/// scanning. Apps opt in via
/// <c>AddGranitPrivacy(p =&gt; p.AddGranitAIPromptsPrivacyProvider())</c>.
/// </summary>
[DependsOn(
    typeof(GranitAIPromptsModule),
    typeof(GranitPrivacyBlobStorageModule))]
public sealed class GranitAIPromptsPrivacyModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.TryAddScoped<PromptTemplatePrivacyDataProvider>();
}
