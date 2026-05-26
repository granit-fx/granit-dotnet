using Granit.Indexing.Extensions;
using Granit.Modularity;

namespace Granit.Indexing;

/// <summary>
/// Granit module for the horizontal indexing / full-text + semantic search framework.
/// </summary>
/// <remarks>
/// Zero declared dependencies (Granit is the implicit base). The module is consumable on
/// its own — concrete backends (EF/tsvector, Elasticsearch, vector stores), language
/// detectors (Lingua), and AI providers (summariser, auto-tagger, embedding generator)
/// plug in via their dedicated packages.
/// <para>
/// Localization resources (<c>Localization/Indexing/{culture}.json</c>) are embedded
/// in this assembly and auto-discovered by <c>GranitLocalizationModule</c> via
/// <see cref="IndexingLocalizationResource"/>.
/// </para>
/// </remarks>
public sealed class GranitIndexingModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitIndexing();
}
