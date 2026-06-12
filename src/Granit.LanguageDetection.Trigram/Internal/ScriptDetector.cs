namespace Granit.LanguageDetection.Trigram.Internal;

/// <summary>
/// Detects the dominant Unicode script of a text sample by counting code-point
/// occurrences against a small set of BMP-only ranges. Matches the script naming
/// used by the upstream Franc dataset.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why script detection first.</b> The Franc approach (Wormer 2014, on top of
/// Cavnar-Trenkle) narrows the candidate-language space from ~400 to ~10-30 BEFORE
/// trigram scoring kicks in. A Cyrillic document only needs to be scored against the
/// ~35 Cyrillic-script languages, not against the 300+ Latin-script ones.
/// </para>
/// <para>
/// <b>BMP-only.</b> The ranges below cover the Basic Multilingual Plane only — code
/// points above U+FFFF are ignored. Indexing payloads are dominated by BMP characters
/// in practice; SMP coverage can be added later without breaking the API.
/// </para>
/// </remarks>
internal static class ScriptDetector
{
    /// <summary>
    /// Returns the dominant script for <paramref name="sample"/>. <c>null</c> when
    /// no script accounts for any character (digits-/punctuation-only input).
    /// </summary>
    public static ScriptDetectionResult? Detect(ReadOnlySpan<char> sample)
    {
        Span<int> counts = stackalloc int[(int)ScriptId.MaxId + 1];
        foreach (char c in sample)
        {
            ScriptId id = ClassifyChar(c);
            if (id != ScriptId.Other)
            {
                counts[(int)id]++;
            }
        }

        int bestCount = 0;
        ScriptId bestId = ScriptId.Other;
        for (int i = 0; i < counts.Length; i++)
        {
            if (counts[i] > bestCount)
            {
                bestCount = counts[i];
                bestId = (ScriptId)i;
            }
        }

        return bestCount == 0 ? null : MapScriptId(bestId);
    }

    private enum ScriptId
    {
        Latin = 0,
        Cyrillic = 1,
        Greek = 2,         // single-lang: ell
        Arabic = 3,
        Hebrew = 4,
        Devanagari = 5,
        Bengali = 6,       // single-lang: ben
        Thai = 7,          // single-lang: tha
        Hangul = 8,        // single-lang: kor
        Hiragana = 9,      // jpn marker
        Katakana = 10,     // jpn marker
        Han = 11,          // cmn / shared with jpn (kanji)
        Ethiopic = 12,
        Myanmar = 13,
        MaxId = Myanmar,
        Other = -1,
    }

    private static ScriptId ClassifyChar(char c)
    {
        ScriptId id = ClassifyWesternChar(c);
        return id != ScriptId.Other ? id : ClassifyAsianChar(c);
    }

    // Latin / Greek / Cyrillic / Hebrew / Arabic block ranges.
    private static ScriptId ClassifyWesternChar(char c) => c switch
    {
        // Latin (Basic + Latin-1 + Latin Extended A/B + IPA + Latin Extended Additional)
        >= 'A' and <= 'Z' => ScriptId.Latin,
        >= 'a' and <= 'z' => ScriptId.Latin,
        >= 'À' and <= 'ɏ' => ScriptId.Latin,
        >= 'Ḁ' and <= 'ỿ' => ScriptId.Latin,
        // Greek (Greek and Coptic)
        >= 'Ͱ' and <= 'Ͽ' => ScriptId.Greek,
        // Cyrillic
        >= 'Ѐ' and <= 'ӿ' => ScriptId.Cyrillic,
        >= 'Ԁ' and <= 'ԯ' => ScriptId.Cyrillic,
        // Hebrew
        >= '֐' and <= '׿' => ScriptId.Hebrew,
        // Arabic (main + Arabic Presentation Forms-A/B)
        >= '؀' and <= 'ۿ' => ScriptId.Arabic,
        >= 'ﭐ' and <= '﷿' => ScriptId.Arabic,
        >= 'ﹰ' and <= '﻿' => ScriptId.Arabic,
        _ => ScriptId.Other,
    };

    // South-Asian / South-East-Asian / Ethiopic / CJK block ranges.
    private static ScriptId ClassifyAsianChar(char c) => c switch
    {
        // Devanagari
        >= 'ऀ' and <= 'ॿ' => ScriptId.Devanagari,
        // Bengali
        >= 'ঀ' and <= '৿' => ScriptId.Bengali,
        // Thai
        >= '฀' and <= '๿' => ScriptId.Thai,
        // Myanmar
        >= 'က' and <= '႟' => ScriptId.Myanmar,
        // Ethiopic
        >= 'ሀ' and <= '፿' => ScriptId.Ethiopic,
        // Hiragana / Katakana (Japanese kana)
        >= '぀' and <= 'ゟ' => ScriptId.Hiragana,
        >= '゠' and <= 'ヿ' => ScriptId.Katakana,
        // Hangul (Korean)
        >= '가' and <= '힯' => ScriptId.Hangul,
        >= 'ᄀ' and <= 'ᇿ' => ScriptId.Hangul,
        // CJK Unified Ideographs (Han: Chinese, also Japanese kanji)
        >= '㐀' and <= '䶿' => ScriptId.Han,
        >= '一' and <= '鿿' => ScriptId.Han,
        _ => ScriptId.Other,
    };

    private static ScriptDetectionResult? MapScriptId(ScriptId id) => id switch
    {
        ScriptId.Latin => new ScriptDetectionResult("Latin", null),
        ScriptId.Cyrillic => new ScriptDetectionResult("Cyrillic", null),
        ScriptId.Arabic => new ScriptDetectionResult("Arabic", null),
        ScriptId.Devanagari => new ScriptDetectionResult("Devanagari", null),
        ScriptId.Hebrew => new ScriptDetectionResult("Hebrew", null),
        ScriptId.Ethiopic => new ScriptDetectionResult("Ethiopic", null),
        ScriptId.Myanmar => new ScriptDetectionResult("Myanmar", null),
        // Single-language scripts: short-circuit on script alone.
        ScriptId.Greek => new ScriptDetectionResult(null, "ell"),
        ScriptId.Bengali => new ScriptDetectionResult(null, "ben"),
        ScriptId.Thai => new ScriptDetectionResult(null, "tha"),
        ScriptId.Hangul => new ScriptDetectionResult(null, "kor"),
        ScriptId.Hiragana => new ScriptDetectionResult(null, "jpn"),
        ScriptId.Katakana => new ScriptDetectionResult(null, "jpn"),
        ScriptId.Han => new ScriptDetectionResult(null, "cmn"),
        _ => null,
    };
}

/// <summary>
/// Outcome of <see cref="ScriptDetector.Detect"/>.
/// </summary>
/// <param name="MultiLanguageScript">
/// Name of a multi-language script (matches the keys in the embedded Franc dataset),
/// or <c>null</c> when the dominant script is a single-language one.
/// </param>
/// <param name="SingleLanguageIso3">
/// ISO 639-3 code of the single language that uses the dominant script (Korean,
/// Japanese kana, Mandarin/Han, …), or <c>null</c> when the script is shared.
/// </param>
internal sealed record ScriptDetectionResult(string? MultiLanguageScript, string? SingleLanguageIso3);
