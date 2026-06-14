using Granit.AI.Prompts.Domain;
using Microsoft.Extensions.Localization;

namespace Granit.AI.Prompts.Endpoints.Internal;

/// <summary>
/// Resolves a prompt's display name and description. Framework-seeded system prompts store
/// localization keys (e.g. <c>Prompt:Summarize:Name</c>) rather than literal text; those are resolved
/// to the request culture from the <c>AIPrompts</c> resource. User prompts carry literal text.
/// </summary>
internal static class PromptDisplay
{
    public static (string Name, string ShortDescription) Resolve(
        IStringLocalizer<AIPromptsLocalizationResource> localizer, PromptTemplate prompt) =>
        prompt.IsSystem
            ? (Localize(localizer, prompt.Name), Localize(localizer, prompt.ShortDescription))
            : (prompt.Name, prompt.ShortDescription);

    private static string Localize(IStringLocalizer<AIPromptsLocalizationResource> localizer, string key)
    {
        LocalizedString localized = localizer[key];
        return localized.ResourceNotFound ? key : localized.Value;
    }
}
