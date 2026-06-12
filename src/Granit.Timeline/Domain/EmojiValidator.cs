using System.Text;

namespace Granit.Timeline.Domain;

/// <summary>
/// Validates and normalises Unicode emoji strings on the reactions wire.
/// Replaces the v1 closed catalog (<c>thumbs_up</c>, <c>heart</c>, …) with
/// a doctrine-aligned <i>closed by upstream standard</i> rule
/// (ADR-040 §5): any sequence formed of recognised emoji codepoints
/// (joined by ZWJ, optionally suffixed by VS-16, Fitzpatrick skin-tone
/// modifiers, the combining enclosing keycap, or a Unicode tag sequence
/// terminated by U+E007F — used by subdivision flags such as 🏴󠁧󠁢󠁥󠁮󠁧󠁿)
/// is accepted.
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
    private const int TagCharMin = 0xE0020;
    private const int TagCharMax = 0xE007E;
    private const int TagEnd = 0xE007F;

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
            if (kind == RuneKind.Invalid)
            {
                return false;
            }

            if (kind is RuneKind.Base or RuneKind.KeycapBase)
            {
                hasBase = true;
            }
            else if (!IsValidTransition(kind, previous))
            {
                return false;
            }

            previous = kind;
        }

        // Trailing ZWJ leaves a dangling joiner; trailing tag chars without
        // the cancel-tag leave the sequence open — reject both.
        return hasBase
            && previous != RuneKind.Zwj
            && previous != RuneKind.TagChar;
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

    // Validates that a combiner/joiner/tag rune may follow <paramref name="previous"/>.
    // Base / KeycapBase / Invalid are handled by the caller and never reach here.
    private static bool IsValidTransition(RuneKind kind, RuneKind previous) => kind switch
    {
        // Combiners and ZWJ need a preceding atom; ZWJ may not start or double.
        RuneKind.Modifier or RuneKind.Vs16 or RuneKind.KeycapCombiner or RuneKind.Zwj
            => previous is not (RuneKind.Start or RuneKind.Zwj),
        // Tag chars (U+E0020..U+E007E) form an Emoji_Tag_Sequence after a base —
        // e.g. the subdivision flags 🏴󠁧󠁢󠁥󠁮󠁧󠁿 (England). Never valid at start, after a
        // combiner, or after a ZWJ.
        RuneKind.TagChar => previous is RuneKind.Base or RuneKind.TagChar,
        // Cancel-tag (U+E007F) terminates an open tag sequence, valid only right
        // after at least one tag char.
        RuneKind.TagEnd => previous == RuneKind.TagChar,
        _ => true,
    };

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
        TagChar,
        TagEnd,
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
        if (cp == TagEnd)
        {
            return RuneKind.TagEnd;
        }
        if (cp >= TagCharMin && cp <= TagCharMax)
        {
            return RuneKind.TagChar;
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

    // Recognised emoji codepoint ranges, ordered by block. The set mirrors the
    // Unicode Extended_Pictographic property for blocks where emojis live; the
    // safety-net goal does not require the strict RGI subset. Single codepoints
    // are encoded as a one-element range (Min == Max). The Fitzpatrick range
    // (0x1F3FB..0x1F3FF) is intercepted earlier by Classify, so it never reaches
    // this table as a base even though it falls inside 0x1F000..0x1FAFF.
    private static readonly (int Min, int Max)[] EmojiBaseRanges =
    [
        (0x00A9, 0x00A9), (0x00AE, 0x00AE),
        (0x203C, 0x203C), (0x2049, 0x2049),
        (0x2122, 0x2122), (0x2139, 0x2139),
        (0x2194, 0x2199),
        (0x21A9, 0x21AA),
        (0x231A, 0x231B),
        (0x2328, 0x2328),
        (0x23CF, 0x23CF),
        (0x23E9, 0x23F3),
        (0x23F8, 0x23FA),
        (0x24C2, 0x24C2),
        (0x25AA, 0x25AB),
        (0x25B6, 0x25B6), (0x25C0, 0x25C0),
        (0x25FB, 0x25FE),
        (0x2600, 0x27BF),
        (0x2934, 0x2935),
        (0x2B00, 0x2BFF),
        (0x3030, 0x3030), (0x303D, 0x303D), (0x3297, 0x3297), (0x3299, 0x3299),
        (0x1F000, 0x1FAFF),
    ];

    private static bool IsEmojiBase(int cp) =>
        Array.Exists(EmojiBaseRanges, range => cp >= range.Min && cp <= range.Max);
}
