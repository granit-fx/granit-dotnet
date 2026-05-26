using Granit.LanguageDetection.Extensions;
using Granit.Modularity;

namespace Granit.LanguageDetection;

/// <summary>
/// Granit module for the language-detection cross-cutting concern.
/// </summary>
/// <remarks>
/// Zero declared dependencies (Granit is the implicit base). Add a concrete detector
/// package (e.g. <c>Granit.LanguageDetection.Trigram</c>) alongside this module to get
/// a working out-of-the-box default; without one, the composite returns <c>null</c>.
/// </remarks>
public sealed class GranitLanguageDetectionModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitLanguageDetection();
}
