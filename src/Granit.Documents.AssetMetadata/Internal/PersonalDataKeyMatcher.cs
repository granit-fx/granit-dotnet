using System;
using System.Collections.Generic;

namespace Granit.Documents.AssetMetadata.Internal;

/// <summary>
/// Curated, case-insensitive substring allow-list of raw-metadata keys that
/// carry personal data — author, owner, contact, serial-number-style PII —
/// stripped from the EXIF/IPTC/XMP/PDF/Office archive when
/// <see cref="Options.GranitAssetMetadataOptions.StripPersonalDataOnUpload"/>
/// is enabled (GDPR Art. 5(c) data-minimisation).
/// </summary>
internal static class PersonalDataKeyMatcher
{
    /// <summary>Case-insensitive substrings that mark a key as PII-bearing.</summary>
    internal static readonly string[] Substrings =
    [
        "author",
        "owner",
        "artist",
        "copyright",
        "creator",
        "contact",
        "byline",
        "credit",
        "serial",
        "cameraownername",
        "lastmodifiedby",
    ];

    /// <summary>Returns <c>true</c> when <paramref name="key"/> matches any PII substring.</summary>
    public static bool IsPersonalData(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }
        foreach (string needle in Substrings)
        {
            if (key.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Returns the keys from <paramref name="raw"/> that match the PII substring
    /// allow-list. Allocates a fresh list — callers iterate before mutating.
    /// </summary>
    public static List<string> FindPersonalDataKeys(IReadOnlyDictionary<string, string?> raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        List<string> hits = [];
        foreach (string key in raw.Keys)
        {
            if (IsPersonalData(key))
            {
                hits.Add(key);
            }
        }
        return hits;
    }
}
