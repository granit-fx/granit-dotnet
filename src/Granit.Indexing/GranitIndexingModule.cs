using Granit.Indexing.Extensions;
using Granit.LanguageDetection;
using Granit.Modularity;

namespace Granit.Indexing;

/// <summary>
/// Granit module for the horizontal indexing / full-text + semantic search framework.
/// </summary>
/// <remarks>
/// Depends on <see cref="GranitLanguageDetectionModule"/> for the
/// <see cref="ILanguageDetector"/> abstraction; concrete backends (EF/tsvector,
/// Elasticsearch, vector stores) and AI providers plug in via their dedicated packages.
/// <para>
/// Localization resources (<c>Localization/Indexing/{culture}.json</c>) are embedded
/// in this assembly and auto-discovered by <c>GranitLocalizationModule</c> via
/// <see cref="IndexingLocalizationResource"/>.
/// </para>
/// </remarks>
[DependsOn(typeof(GranitLanguageDetectionModule))]
public sealed class GranitIndexingModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitIndexing();
}
