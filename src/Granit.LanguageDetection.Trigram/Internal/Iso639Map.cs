namespace Granit.LanguageDetection.Trigram.Internal;

/// <summary>
/// ISO 639-3 → ISO 639-1 mapping for the language codes returned by the detector.
/// Limited to languages that have an ISO 639-1 alpha-2 code — codes outside the list
/// trigger a <c>null</c> return from <see cref="TrigramLanguageDetector"/> so the
/// composite detector chain falls through to the next provider (typically AI-backed).
/// </summary>
/// <remarks>
/// Covers the languages the Granit framework recognises plus every language whose
/// stemmer ships with a vanilla Postgres install (the common consumer surface).
/// Adding a language requires an ISO 639-3 to ISO 639-1 entry here; downstream
/// language-to-resource mappings (Postgres dictionaries, locale resource bundles, …)
/// are the consumer's responsibility.
/// </remarks>
internal static class Iso639Map
{
    private static readonly Dictionary<string, string> Iso3ToIso1 = new(StringComparer.Ordinal)
    {
        // Western European
        ["eng"] = "en",
        ["fra"] = "fr",
        ["spa"] = "es",
        ["deu"] = "de",
        ["ita"] = "it",
        ["nld"] = "nl",
        ["por"] = "pt",
        ["dan"] = "da",
        ["nor"] = "no",
        ["nob"] = "no",
        ["nno"] = "no",
        ["swe"] = "sv",
        ["fin"] = "fi",
        ["isl"] = "is",
        ["gle"] = "ga",
        ["cym"] = "cy",
        ["cat"] = "ca",
        ["eus"] = "eu",
        ["glg"] = "gl",
        // Central / Eastern European
        ["pol"] = "pl",
        ["ces"] = "cs",
        ["slk"] = "sk",
        ["hun"] = "hu",
        ["ron"] = "ro",
        ["bul"] = "bg",
        ["hrv"] = "hr",
        ["srp"] = "sr",
        ["slv"] = "sl",
        ["lit"] = "lt",
        ["lav"] = "lv",
        ["est"] = "et",
        ["ell"] = "el",
        // Cyrillic
        ["rus"] = "ru",
        ["ukr"] = "uk",
        ["bel"] = "be",
        ["mkd"] = "mk",
        // Middle East
        ["ara"] = "ar",
        ["heb"] = "he",
        ["tur"] = "tr",
        ["fas"] = "fa",
        ["pes"] = "fa",
        ["urd"] = "ur",
        ["aze"] = "az",
        ["kur"] = "ku",
        // South / South-East Asia
        ["hin"] = "hi",
        // Hindi-belt (Bihari) macro-language folding: Magahi, Bhojpuri and Maithili
        // are listed by Franc as siblings of Hindi inside the Devanagari profile set
        // but have no ISO 639-1 code of their own. They share heavy vocabulary overlap
        // with Hindi, and Postgres ships only the Hindi stemmer for Devanagari, so
        // folding them to "hi" matches both the Granit base-culture surface (15 cultures)
        // and the downstream stemming/indexing reality. Without this, formal Hindi prose
        // can score closest to "mag" or "bho" and return null.
        ["mag"] = "hi",
        ["bho"] = "hi",
        ["mai"] = "hi",
        ["ben"] = "bn",
        ["pan"] = "pa",
        ["mar"] = "mr",
        ["tam"] = "ta",
        ["tel"] = "te",
        ["guj"] = "gu",
        ["kan"] = "kn",
        ["mal"] = "ml",
        ["nep"] = "ne",
        ["sin"] = "si",
        ["tha"] = "th",
        ["vie"] = "vi",
        ["ind"] = "id",
        ["msa"] = "ms",
        ["zsm"] = "ms",
        ["tgl"] = "tl",
        ["khm"] = "km",
        ["mya"] = "my",
        ["lao"] = "lo",
        // CJK
        ["cmn"] = "zh",
        ["zho"] = "zh",
        ["jpn"] = "ja",
        ["kor"] = "ko",
        // Africa
        ["swa"] = "sw",
        ["amh"] = "am",
        ["som"] = "so",
        ["hau"] = "ha",
        ["yor"] = "yo",
        ["zul"] = "zu",
        ["afr"] = "af",
        ["mlg"] = "mg",
        // Other
        ["lat"] = "la",
        ["epo"] = "eo",
        ["hye"] = "hy",
        ["kat"] = "ka",
        ["kaz"] = "kk",
        ["uzb"] = "uz",
    };

    /// <summary>
    /// Returns the ISO 639-1 code for the given ISO 639-3 code, or <c>null</c> when
    /// no mapping exists.
    /// </summary>
    public static string? ToIso639_1(string iso639_3) =>
        Iso3ToIso1.TryGetValue(iso639_3, out string? iso1) ? iso1 : null;
}
