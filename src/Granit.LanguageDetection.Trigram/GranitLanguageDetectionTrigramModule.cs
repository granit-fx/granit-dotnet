using Granit.LanguageDetection.Trigram.Extensions;
using Granit.Modularity;

namespace Granit.LanguageDetection.Trigram;

/// <summary>
/// Granit module for the default trigram language detector.
/// </summary>
/// <remarks>
/// Auto-registers <see cref="TrigramLanguageDetector"/> at priority 100 in the
/// composite chain. Add this module alongside <see cref="GranitLanguageDetectionModule"/>
/// to get working language detection out of the box; higher-priority overrides
/// (explicit metadata-hint detector, AI-backed detector) can be registered separately.
/// </remarks>
[DependsOn(typeof(GranitLanguageDetectionModule))]
public sealed class GranitLanguageDetectionTrigramModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitLanguageDetectionTrigram();
}
