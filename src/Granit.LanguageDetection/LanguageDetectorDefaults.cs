namespace Granit.LanguageDetection;

/// <summary>
/// Shared defaults applied by every <see cref="ILanguageDetectorProvider"/> in the
/// composite chain. Centralising them keeps the providers in lock-step on the cheap
/// pre-checks that should never disagree (e.g. the minimum sample length below which
/// no detector should burn budget guessing).
/// </summary>
public static class LanguageDetectorDefaults
{
    /// <summary>
    /// Smallest input length any provider should attempt to classify. Shorter inputs
    /// return <c>null</c> so the composite skips the chain entirely — the signal is
    /// too thin to be meaningful for trigram statistics or worth a paid LLM call.
    /// </summary>
    public const int MinimumSampleLength = 10;
}
