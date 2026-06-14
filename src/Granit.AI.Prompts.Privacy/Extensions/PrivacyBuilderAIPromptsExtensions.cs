using Granit.AI.Prompts.Privacy.DataExport;
using Granit.Privacy;
using Granit.Privacy.BlobStorage.Extensions;

namespace Granit.AI.Prompts.Privacy.Extensions;

/// <summary>
/// <see cref="GranitPrivacyBuilder"/> extensions that register the AI prompts privacy provider.
/// </summary>
public static class PrivacyBuilderAIPromptsExtensions
{
    /// <summary>
    /// Registers <see cref="PromptTemplatePrivacyDataProvider"/> as an <c>IPrivacyDataProvider</c> and
    /// adds <c>"ai-prompts"</c> to the scatter-gather export registry. Wires the
    /// <c>Granit.Privacy.BlobStorage</c> staging infrastructure (idempotent). The matching export
    /// handler is discovered automatically; the deletion handler likewise. Requires
    /// <c>GranitAIPromptsModule</c> (and its EF Core data manager) to be loaded.
    /// </summary>
    public static GranitPrivacyBuilder AddGranitAIPromptsPrivacyProvider(this GranitPrivacyBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddGranitPrivacyBlobStorage();
        return builder.AddDataProvider<PromptTemplatePrivacyDataProvider>();
    }
}
