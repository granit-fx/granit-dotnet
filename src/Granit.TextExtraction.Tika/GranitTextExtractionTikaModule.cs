using Granit.Modularity;

namespace Granit.TextExtraction.Tika;

/// <summary>
/// Granit module that registers the Apache Tika sidecar text extractor (opt-in).
/// Hosts wire the extractor via
/// <see cref="Extensions.ServiceCollectionExtensions.AddTikaSidecarExtractor"/>; the
/// module itself does not auto-register anything because the host must declare its
/// <c>TikaSidecarOptions</c> (specifically the <see cref="Options.TikaSidecarOptions.AllowedHosts"/>
/// allowlist) before the extractor is usable.
/// </summary>
[DependsOn(typeof(GranitTextExtractionModule))]
public sealed class GranitTextExtractionTikaModule : GranitModule;
