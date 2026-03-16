namespace Granit.Localization.AI;

/// <summary>
/// Service that generates AI-powered translation suggestions for localization keys.
/// </summary>
public interface ITranslationSuggestionService
{
    /// <summary>
    /// Generates translation suggestions for the given source text across target cultures.
    /// </summary>
    /// <param name="key">The localization key (e.g. <c>"Login:SubmitButton"</c>), provided for context only.</param>
    /// <param name="sourceValue">The source text to translate.</param>
    /// <param name="sourceCulture">The culture of the source text (e.g. <c>"en"</c>).</param>
    /// <param name="targetCultures">The list of target culture codes to translate into.</param>
    /// <param name="context">The type of text being translated, for tone and style guidance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of translation suggestions, one per successfully translated culture.</returns>
    Task<IReadOnlyList<TranslationSuggestion>> SuggestTranslationsAsync(
        string key,
        string sourceValue,
        string sourceCulture,
        IReadOnlyList<string> targetCultures,
        TranslationContext context = TranslationContext.UiLabel,
        CancellationToken cancellationToken = default);
}
