using Granit.Modularity;

namespace Granit.TextExtraction.Ocr.AI;

/// <summary>
/// Granit module that registers the AI vision OCR extractor (opt-in). Hosts wire the
/// extractor via
/// <see cref="Extensions.ServiceCollectionExtensions.AddAiVisionOcrExtractor"/>;
/// the module itself does not auto-register anything because enabling vision OCR sends
/// document bytes to a third-party LLM provider (GDPR Article 28 disclosure required).
/// </summary>
[DependsOn(typeof(GranitTextExtractionModule))]
public sealed class GranitTextExtractionOcrAIModule : GranitModule;
