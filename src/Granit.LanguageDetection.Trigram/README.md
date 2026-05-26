# Granit.LanguageDetection.Trigram

Default `ILanguageDetectorProvider` for the Granit framework. Pure-managed character-trigram detector with Unicode-script pre-filtering and an embedded dataset — deterministic, sub-millisecond, no native dependency.

## Algorithm

Two-stage detection, ported clean-room from Franc (Wormer 2014, building on Cavnar-Trenkle 1994):

1. **Unicode script detection.** Count BMP code-point occurrences. Multi-language scripts (Latin, Cyrillic, Arabic, Devanagari, Hebrew, Ethiopic, Myanmar) trigger the trigram scoring stage. Single-language scripts (Greek, Bengali, Thai, Hangul, Hiragana/Katakana → Japanese, Han → Chinese) short-circuit to their ISO 639-3 directly — no trigram comparison needed because the script itself disambiguates.
2. **Trigram ranking inside the dominant script.** Extract input trigrams (lowercase, non-letter → space, prepend/append space, sliding 3-char window). Score against each language's trigram rank table; out-of-profile trigrams contribute a fixed `MAX_DIFFERENCE = 300` penalty.

Detection is deterministic — same input always yields the same answer. Suitable for CI fixtures and reproducible builds.

## Coverage

- 7 multi-language scripts wired into `ScriptDetector` and scored against the embedded
  trigram dataset: Latin (300+ langs), Cyrillic (35), Arabic (9), Devanagari (7),
  Myanmar (3), Ethiopic (2), Hebrew (2).
- 6 single-language script shortcuts: Greek (`el`), Bengali (`bn`), Thai (`th`),
  Hangul (`ko`), Hiragana/Katakana → Japanese (`ja`), Han → Chinese (`zh`).
- Detected and asserted on the **15 Granit base cultures** (`en`, `fr`, `nl`, `de`,
  `es`, `it`, `pt`, `zh`, `ja`, `pl`, `tr`, `ko`, `sv`, `cs`, `hi`) via the
  `KnownSamples` theory in `Granit.LanguageDetection.Trigram.Tests`.

The Franc dataset also bundles Tibetan and Canadian_Aboriginal multi-language
scripts; their rank tables ship in `Resources/profiles.json` but `ScriptDetector`
does not yet recognise those Unicode blocks, so input in them falls through to
`null`. Extend `ScriptDetector.ClassifyChar` to enable.

The detector returns an ISO 639-1 (alpha-2) code, ready for downstream consumers
that key off the alpha-2 form. Detected languages without a 639-1 equivalent return
`null`, letting the composite chain fall through to the next provider (typically
AI-backed).

## Profile data attribution

The embedded `Resources/profiles.json` is parsed from the [Franc](https://github.com/wooorm/franc) trigram dataset (Titus Wormer 2014+, MIT-licensed), itself trained on the Universal Declaration of Human Rights + Wikipedia corpora. The C# detector code is a clean-room implementation. See `THIRD-PARTY-NOTICES.md` at the repository root for the full attribution.

## Usage

```csharp
builder.Services.AddGranitLanguageDetection();
builder.Services.AddGranitLanguageDetectionTrigram();

ILanguageDetector detector = sp.GetRequiredService<ILanguageDetector>();
string? code = await detector.DetectAsync("Le renard brun saute sur le chien paresseux.");
// code => "fr"
```

To override at a higher priority, implement `ILanguageDetectorProvider` (NOT
`ILanguageDetector` — the consumer-facing facade resolves to a single composite,
provider implementations fan in via `IEnumerable<ILanguageDetectorProvider>`):

```csharp
public sealed class MetadataHintDetector : ILanguageDetectorProvider
{
    public int Priority => 1000;
    public Task<string?> DetectAsync(string content, CancellationToken ct) =>
        Task.FromResult(ExtractFromHeader(content));
}

// Then in composition root:
services.TryAddEnumerable(
    ServiceDescriptor.Singleton<ILanguageDetectorProvider, MetadataHintDetector>());
```
