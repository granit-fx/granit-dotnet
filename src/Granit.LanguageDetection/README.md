# Granit.LanguageDetection

Cross-cutting language-detection abstractions for the Granit framework.

## What's in this package

- `ILanguageDetector` — consumer-facing facade returning an ISO 639-1 language code from text. The DI container resolves this to a single `CompositeLanguageDetector`.
- `ILanguageDetectorProvider` — marker interface for concrete detector implementations that plug into the priority chain. Concrete providers (trigram, AI-backed, metadata-hint) implement this; the composite consumes `IEnumerable<ILanguageDetectorProvider>`.
- `CompositeLanguageDetector` — priority-chain implementation. Delegates to each registered provider in descending priority until one returns a non-null result.
- `GranitLanguageDetectionModule` — registers the composite via `AddGranitLanguageDetection()`.

## Why a dedicated module

Detection is useful well beyond indexing — notifications routing, content localization classification, AI prompt targeting, privacy data classification. Keeping the abstraction here lets each consumer depend on `Granit.LanguageDetection` without dragging in the larger indexing tree.

## Companion packages

| Package | Concern |
| --- | --- |
| `Granit.LanguageDetection.Trigram` | Default pure-managed trigram detector with embedded data (Franc dataset, MIT). |
| `Granit.LanguageDetection.AI.*` (future) | LLM-backed detectors for ambiguous corpora. |

## Usage

```csharp
builder.Services.AddGranitLanguageDetection();
builder.Services.AddGranitLanguageDetectionTrigram();    // ships default provider at priority 100

ILanguageDetector detector = sp.GetRequiredService<ILanguageDetector>();
string? code = await detector.DetectAsync("La rapida volpe marrone…");
// code => "it"
```

Register a higher-priority provider to override the default — implementations declare
`ILanguageDetectorProvider`, not `ILanguageDetector` (so the consumer-facing
`ILanguageDetector` resolution stays unambiguous):

```csharp
public sealed class MetadataHintDetector(IHttpContextAccessor http) : ILanguageDetectorProvider
{
    public int Priority => 1000;
    public Task<string?> DetectAsync(string _, CancellationToken __) =>
        Task.FromResult(http.HttpContext?.Request.Headers["X-Content-Language"].FirstOrDefault());
}

// Then in composition root:
services.TryAddEnumerable(
    ServiceDescriptor.Singleton<ILanguageDetectorProvider, MetadataHintDetector>());
```
