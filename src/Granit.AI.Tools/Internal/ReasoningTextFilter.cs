using System.Text;

namespace Granit.AI.Tools.Internal;

/// <summary>
/// Strips reasoning-model think blocks (<c>&lt;think&gt;…&lt;/think&gt;</c>, as emitted inline by
/// DeepSeek-R1 and similar models served over Ollama) from assistant text before it is streamed to the
/// client or persisted. Reasoning is the model's internal scratch space, not an answer: leaking it
/// pollutes the visible reply (the stray leading blank lines users see) and, once persisted, the
/// conversation history replayed to the model on later turns.
/// </summary>
/// <remarks>
/// The filter is streaming-aware: a block split across several streamed deltas is still removed because
/// the instance carries state between <see cref="Process"/> calls, holding back only the minimal tail
/// that could begin a tag. Leading whitespace is trimmed until the first real character is surfaced, so
/// the blank gap a removed opening block leaves behind never reaches the client. Models that emit no
/// think block (OpenAI, Anthropic, …) pass through unchanged apart from that leading trim. A model whose
/// adapter classifies reasoning as <c>TextReasoningContent</c> is already excluded upstream; this covers
/// the inline-text case those adapters miss.
/// </remarks>
internal sealed class ReasoningTextFilter
{
    private const string OpenTag = "<think>";
    private const string CloseTag = "</think>";

    private readonly StringBuilder _pending = new();
    private bool _insideThink;
    private bool _emittedContent;

    /// <summary>
    /// Feeds the next text fragment and returns the portion safe to surface now. Text inside a think
    /// block is dropped; a fragment that might be the start of a tag is held back until the next call
    /// (or <see cref="Flush"/>) resolves it.
    /// </summary>
    public string Process(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        _pending.Append(text);
        StringBuilder? visible = null;

        while (true)
        {
            string buffer = _pending.ToString();
            if (!_insideThink)
            {
                int open = buffer.IndexOf(OpenTag, StringComparison.OrdinalIgnoreCase);
                if (open >= 0)
                {
                    (visible ??= new StringBuilder()).Append(buffer, 0, open);
                    ResetPendingFrom(buffer, open + OpenTag.Length);
                    _insideThink = true;
                    continue;
                }

                int hold = PartialTagSuffixLength(buffer, OpenTag);
                (visible ??= new StringBuilder()).Append(buffer, 0, buffer.Length - hold);
                ResetPendingTail(buffer, hold);
                break;
            }

            int close = buffer.IndexOf(CloseTag, StringComparison.OrdinalIgnoreCase);
            if (close >= 0)
            {
                ResetPendingFrom(buffer, close + CloseTag.Length);
                _insideThink = false;
                continue;
            }

            ResetPendingTail(buffer, PartialTagSuffixLength(buffer, CloseTag));
            break;
        }

        return TrimLeadingOnce(visible);
    }

    /// <summary>
    /// Flushes any held-back tail at end of stream. Outside a block the tail is real trailing content;
    /// an unterminated block is reasoning that never closed and is dropped.
    /// </summary>
    public string Flush()
    {
        string remainder = _insideThink ? string.Empty : _pending.ToString();
        _pending.Clear();
        _insideThink = false;
        return TrimLeadingOnce(remainder.Length == 0 ? null : new StringBuilder(remainder));
    }

    /// <summary>
    /// One-shot strip for fully-settled text (the persistence path), with the same semantics as the
    /// streaming path so a persisted message matches what was streamed.
    /// </summary>
    public static string StripReasoning(string text)
    {
        ReasoningTextFilter filter = new();
        return filter.Process(text) + filter.Flush();
    }

    private void ResetPendingFrom(string buffer, int start)
    {
        _pending.Clear();
        _pending.Append(buffer, start, buffer.Length - start);
    }

    private void ResetPendingTail(string buffer, int tailLength)
    {
        _pending.Clear();
        _pending.Append(buffer, buffer.Length - tailLength, tailLength);
    }

    private string TrimLeadingOnce(StringBuilder? visible)
    {
        if (visible is null || visible.Length == 0)
        {
            return string.Empty;
        }

        string text = visible.ToString();
        if (_emittedContent)
        {
            return text;
        }

        text = text.TrimStart();
        if (text.Length == 0)
        {
            return string.Empty;
        }

        _emittedContent = true;
        return text;
    }

    // Longest suffix of buffer that is a (case-insensitive) prefix of tag — text held back because the
    // next fragment could complete the tag. Capped at tag.Length - 1 (a full match is handled as a hit).
    private static int PartialTagSuffixLength(string buffer, string tag)
    {
        int max = Math.Min(buffer.Length, tag.Length - 1);
        for (int length = max; length > 0; length--)
        {
            if (string.Compare(buffer, buffer.Length - length, tag, 0, length, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return length;
            }
        }

        return 0;
    }
}
