# Granit.LanguageDetection

Cross-cutting language-detection abstractions for the Granit framework.

## What's in this package

- `ILanguageDetector` — priority-chain port for detecting an ISO 639-1 language code from text.
- `CompositeLanguageDetector` — built-in chain that delegates to every registered detector in descending priority until one returns a non-null result.
- `GranitLanguageDetectionModule` — registers the composite via `AddGranitLanguageDetection()`.

## Why a dedicated module

Detection is useful well beyond indexing — notifications routing, content localization classification, AI prompt targeting, privacy data classification. Keeping the abstraction here lets each consumer depend on `Granit.LanguageDetection` without dragging in the larger indexing tree.

## Companion packages

| Package | Concern |
| --- | --- |
| `Granit.LanguageDetection.Lingua` | Default pure-managed trigram detector with embedded data (Franc dataset, MIT). |
| `Granit.LanguageDetection.AI.*` (future) | LLM-backed detectors for ambiguous corpora. |

## Usage

```csharp
builder.Services.AddGranitLanguageDetection();
builder.Services.AddGranitLanguageDetectionLingua();    // ships default at priority 100

ILanguageDetector detector = sp.GetRequiredService<ILanguageDetector>();
string? code = await detector.DetectAsync("La rapida volpe marrone…");
// code => "it"
```

Register a higher-priority detector to override the default:

```csharp
public sealed class MetadataHintDetector(IHttpContextAccessor http) : ILanguageDetector
{
    public int Priority => 1000;
    public Task<string?> DetectAsync(string _, CancellationToken __) =>
        Task.FromResult(http.HttpContext?.Request.Headers["X-Content-Language"].FirstOrDefault());
}
```
