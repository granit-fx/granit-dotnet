# Granit.LanguageDetection.Trigram

Default `ILanguageDetector` for the Granit framework. Pure-managed character-trigram detector with Unicode-script pre-filtering and embedded profiles for 390+ languages — deterministic, sub-millisecond, no native dependency.

## Algorithm

Two-stage detection, ported clean-room from Franc (Wormer 2014, building on Cavnar-Trenkle 1994):

1. **Unicode script detection.** Count BMP code-point occurrences against Latin / Cyrillic / Arabic / Devanagari / Hebrew / Ethiopic / Myanmar (multi-language) and Greek / Bengali / Thai / Hangul / Hiragana / Katakana / Han (single-language). Single-language scripts short-circuit immediately to their ISO 639-3 code.
2. **Trigram ranking inside the dominant script.** Extract input trigrams (lowercase, non-letter → space, prepend/append space, sliding 3-char window). Score against each language's trigram rank table; out-of-profile trigrams contribute a fixed `MAX_DIFFERENCE = 300` penalty.

Detection is deterministic — same input always yields the same answer. Suitable for CI fixtures and reproducible builds.

## Coverage

- 9 multi-language scripts: Latin (300+ langs), Cyrillic (35), Arabic (9), Devanagari (7), Myanmar (3), Ethiopic (2), Tibetan (2), Hebrew (2), Canadian_Aboriginal (3).
- 7 single-language script shortcuts: Greek (`el`), Bengali (`bn`), Thai (`th`), Hangul (`ko`), Hiragana/Katakana (`ja`), Han (`zh`).

The detector returns an ISO 639-1 (alpha-2) code so the result drops straight into `IndexingLanguageMap`. ISO 639-3 outputs without a 639-1 equivalent return `null`, letting the composite chain fall through to the next provider (typically AI-backed).

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

Register a higher-priority detector to override the default:

```csharp
public sealed class MetadataHintDetector : ILanguageDetector
{
    public int Priority => 1000;
    public Task<string?> DetectAsync(string content, CancellationToken ct) =>
        Task.FromResult(ExtractFromHeader(content));
}
```
