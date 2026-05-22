using System.Text;

namespace Granit.Timeline.Domain;

/// <summary>
/// Validates and normalises Unicode emoji strings on the reactions wire.
/// Replaces the v1 closed catalog (<c>thumbs_up</c>, <c>heart</c>, …) with
/// a doctrine-aligned <i>closed by upstream standard</i> rule
/// (ADR-040 §5): any sequence formed of recognised emoji codepoints
/// (joined by ZWJ, optionally suffixed by VS-16, Fitzpatrick skin-tone
/// modifiers, or the combining enclosing keycap) is accepted.
/// </summary>
/// <remarks>
/// <para>
/// The accepted set matches the Unicode <i>production rule</i> for
/// emoji sequences, not the CLDR RGI catalog (<c>emoji-test.txt</c>). A
/// small number of exotic ZWJ assemblies that render as separate glyphs
/// on most platforms can pass — but the primary client-side source
/// (<c>@emoji-mart/data</c>) never produces them. The backend validator
/// is a safety net for non-picker clients (curl, third-party
/// integrations, mobile), not the picker's gatekeeper.
/// </para>
/// <para>
/// For aggregate counters, <see cref="NormalizeForAggregate"/> strips
/// Fitzpatrick skin-tone modifiers and the VS-16 emoji-presentation
/// selector so 👍 / 👍🏽 / 👍🏿 collapse under the base codepoint. Storage
/// keeps the original variant on each row.
/// </para>
/// </remarks>
public static class EmojiValidator
{
    /// <summary>
    /// Defensive upper bound — matches the EF column width
    /// (<c>HasMaxLength(64)</c>) and covers every current RGI family
    /// sequence with margin.
    /// </summary>
    public const int MaxLength = 64;

    private const int Zwj = 0x200D;
    private const int VariationSelector16 = 0xFE0F;
    private const int KeycapCombiner = 0x20E3;
    private const int FitzpatrickMin = 0x1F3FB;
    private const int FitzpatrickMax = 0x1F3FF;

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="emoji"/> is a
    /// well-formed Unicode emoji sequence per the production rule
    /// described on this class.
    /// </summary>
    public static bool IsValid(string? emoji)
    {
        if (string.IsNullOrEmpty(emoji) || emoji.Length > MaxLength)
        {
            return false;
        }

        bool hasBase = false;
        RuneKind previous = RuneKind.Start;

        foreach (Rune r in emoji.EnumerateRunes())
        {
            RuneKind kind = Classify(r.Value);
            switch (kind)
            {
                case RuneKind.Invalid:
                    return false;
                case RuneKind.Base:
                case RuneKind.KeycapBase:
                    hasBase = true;
                    break;
                case RuneKind.Modifier:
                case RuneKind.Vs16:
                case RuneKind.KeycapCombiner:
                    // Combiners need a preceding atom (base or keycap base).
                    if (previous is RuneKind.Start or RuneKind.Zwj)
                    {
                        return false;
                    }
                    break;
                case RuneKind.Zwj:
                    // ZWJ at start or doubled is not allowed.
                    if (previous is RuneKind.Start or RuneKind.Zwj)
                    {
                        return false;
                    }
                    break;
            }
            previous = kind;
        }

        // Trailing ZWJ leaves a dangling joiner — reject.
        return hasBase && previous != RuneKind.Zwj;
    }

    /// <summary>
    /// Collapses skin-tone variants and VS-16 selectors to a base
    /// codepoint sequence — used as the key of the per-emoji aggregate
    /// so 👍 + 👍🏽 + 👍🏿 share a single counter. Returns
    /// <paramref name="emoji"/> unchanged when there is nothing to strip.
    /// </summary>
    /// <remarks>
    /// Does NOT validate the input; callers should pair this with
    /// <see cref="IsValid"/> upstream. Returns an empty string for
    /// <see langword="null"/>/empty input.
    /// </remarks>
    public static string NormalizeForAggregate(string? emoji)
    {
        if (string.IsNullOrEmpty(emoji))
        {
            return string.Empty;
        }

        StringBuilder? buffer = null;
        SpanRuneEnumerator enumerator = emoji.AsSpan().EnumerateRunes();
        int copiedUpTo = 0;
        int index = 0;
        while (enumerator.MoveNext())
        {
            Rune r = enumerator.Current;
            int width = r.Utf16SequenceLength;
            int cp = r.Value;
            bool strip = cp == VariationSelector16
                || (cp >= FitzpatrickMin && cp <= FitzpatrickMax);
            if (strip)
            {
                buffer ??= new StringBuilder(emoji.Length);
                if (index > copiedUpTo)
                {
                    buffer.Append(emoji, copiedUpTo, index - copiedUpTo);
                }
                copiedUpTo = index + width;
            }
            index += width;
        }

        if (buffer is null)
        {
            return emoji;
        }
        if (copiedUpTo < emoji.Length)
        {
            buffer.Append(emoji, copiedUpTo, emoji.Length - copiedUpTo);
        }
        return buffer.ToString();
    }

    private enum RuneKind
    {
        Start,
        Invalid,
        Base,
        Modifier,
        Vs16,
        Zwj,
        KeycapBase,
        KeycapCombiner,
    }

    private static RuneKind Classify(int cp)
    {
        if (cp == Zwj)
        {
            return RuneKind.Zwj;
        }
        if (cp == VariationSelector16)
        {
            return RuneKind.Vs16;
        }
        if (cp == KeycapCombiner)
        {
            return RuneKind.KeycapCombiner;
        }
        if (cp >= FitzpatrickMin && cp <= FitzpatrickMax)
        {
            return RuneKind.Modifier;
        }
        if (IsKeycapBase(cp))
        {
            return RuneKind.KeycapBase;
        }
        if (IsEmojiBase(cp))
        {
            return RuneKind.Base;
        }
        return RuneKind.Invalid;
    }

    // Keycap base characters: '#', '*', '0'..'9'.
    private static bool IsKeycapBase(int cp) =>
        cp == 0x23 || cp == 0x2A || (cp >= 0x30 && cp <= 0x39);

    // Recognised emoji codepoints, ordered by block. The set mirrors the
    // Unicode Extended_Pictographic property for blocks where emojis live;
    // the safety-net goal does not require the strict RGI subset.
    private static bool IsEmojiBase(int cp) => cp switch
    {
        0x00A9 or 0x00AE => true,
        0x203C or 0x2049 => true,
        0x2122 or 0x2139 => true,
        >= 0x2194 and <= 0x2199 => true,
        >= 0x21A9 and <= 0x21AA => true,
        >= 0x231A and <= 0x231B => true,
        0x2328 => true,
        0x23CF => true,
        >= 0x23E9 and <= 0x23F3 => true,
        >= 0x23F8 and <= 0x23FA => true,
        0x24C2 => true,
        >= 0x25AA and <= 0x25AB => true,
        0x25B6 or 0x25C0 => true,
        >= 0x25FB and <= 0x25FE => true,
        >= 0x2600 and <= 0x27BF => true,
        >= 0x2934 and <= 0x2935 => true,
        >= 0x2B00 and <= 0x2BFF => true,
        0x3030 or 0x303D or 0x3297 or 0x3299 => true,
        // Supplementary Multilingual Plane emoji blocks. The Fitzpatrick
        // range (0x1F3FB..0x1F3FF) is intercepted earlier by Classify so
        // it never reaches this switch as a base.
        >= 0x1F000 and <= 0x1FAFF => true,
        _ => false,
    };
}
