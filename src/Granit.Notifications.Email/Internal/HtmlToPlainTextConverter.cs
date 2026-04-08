using System.Text;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;

namespace Granit.Notifications.Email.Internal;

/// <summary>
/// Converts rendered HTML email content to a clean plain-text representation
/// suitable for the <c>text/plain</c> part of a <c>multipart/alternative</c> email.
/// </summary>
/// <remarks>
/// <para>
/// Uses a reusable <see cref="IBrowsingContext"/> (thread-safe, no CSS/JS engine)
/// to avoid per-call parser allocation overhead in high-throughput scenarios.
/// </para>
/// <para>
/// The converter is optimized for Granit email templates: it recognizes layout tables
/// (<c>role="presentation"</c>), MSO conditional comments, and common email patterns
/// (buttons, dividers, unsubscribe links).
/// </para>
/// </remarks>
internal static class HtmlToPlainTextConverter
{
    // Thread-safe: AngleSharp's IBrowsingContext with Configuration.Default (no CSS/JS engine)
    // is safe for concurrent OpenAsync calls. Reused to avoid per-call allocation overhead.
    private static readonly IBrowsingContext Context = BrowsingContext.New(Configuration.Default);

    private static readonly HashSet<string> BlockElements =
    [
        "p", "div", "tr", "blockquote", "section", "article", "aside", "nav",
        "header", "footer", "main", "figure", "figcaption", "address", "pre",
    ];

    private static readonly HashSet<string> HeadingElements = ["h1", "h2", "h3", "h4", "h5", "h6"];

    private static readonly HashSet<string> SkipElements = ["style", "script", "head"];

    public static async Task<string> ConvertAsync(string html, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        using IDocument document = await Context.OpenAsync(req => req.Content(html), ct).ConfigureAwait(false);

        IHtmlElement? body = document.Body;
        if (body is null)
        {
            return string.Empty;
        }

        StringBuilder sb = new();
        WalkNode(body, sb, listDepth: 0, orderedIndex: 0);

        return NormalizeWhitespace(sb.ToString());
    }

    private static void WalkNode(INode node, StringBuilder sb, int listDepth, int orderedIndex)
    {
        int currentIndex = orderedIndex;
        foreach (INode child in node.ChildNodes)
        {
            switch (child)
            {
                case IText textNode:
                    AppendText(textNode, sb);
                    break;

                case IComment:
                    // Strip MSO conditional comments and all HTML comments
                    break;

                case IHtmlElement element:
                    ProcessElement(element, sb, listDepth, currentIndex);
                    if (orderedIndex > 0 && element.LocalName == "li")
                    {
                        currentIndex++;
                    }
                    break;
            }
        }
    }

    private static void ProcessElement(IHtmlElement element, StringBuilder sb, int listDepth, int orderedIndex)
    {
        string tag = element.LocalName;

        // Skip invisible elements
        if (SkipElements.Contains(tag))
        {
            return;
        }

        // Headings: UPPERCASE + newline
        if (HeadingElements.Contains(tag))
        {
            EnsureNewline(sb);
            sb.Append('\n');
            StringBuilder headingSb = new();
            WalkNode(element, headingSb, listDepth, 0);
            sb.Append(headingSb.ToString().Trim().ToUpperInvariant());
            sb.Append('\n');
            sb.Append('\n');
            return;
        }

        switch (tag)
        {
            case "br":
                sb.Append('\n');
                return;

            case "hr":
                EnsureNewline(sb);
                sb.Append("---\n\n");
                return;

            case "img":
                string? alt = element.GetAttribute("alt");
                if (!string.IsNullOrWhiteSpace(alt))
                {
                    sb.Append('[');
                    sb.Append(alt.Trim());
                    sb.Append(']');
                }
                return;

            case "a":
                ProcessLink(element, sb, listDepth);
                return;

            case "strong" or "b":
                sb.Append('*');
                WalkNode(element, sb, listDepth, 0);
                sb.Append('*');
                return;

            case "em" or "i":
                sb.Append('_');
                WalkNode(element, sb, listDepth, 0);
                sb.Append('_');
                return;

            case "ul":
                EnsureNewline(sb);
                WalkNode(element, sb, listDepth + 1, 0);
                sb.Append('\n');
                return;

            case "ol":
                EnsureNewline(sb);
                WalkNode(element, sb, listDepth + 1, orderedIndex: 1);
                sb.Append('\n');
                return;

            case "li":
                string indent = new(' ', listDepth * 2);
                if (orderedIndex > 0)
                {
                    sb.Append(indent);
                    sb.Append(orderedIndex);
                    sb.Append(". ");
                }
                else
                {
                    sb.Append(indent);
                    sb.Append("\u2022 ");
                }
                WalkNode(element, sb, listDepth, 0);
                sb.Append('\n');
                return;

            case "table":
                ProcessTable(element, sb, listDepth);
                return;

            default:
                if (BlockElements.Contains(tag))
                {
                    EnsureNewline(sb);
                    WalkNode(element, sb, listDepth, 0);
                    sb.Append('\n');
                    sb.Append('\n');
                }
                else
                {
                    // Inline elements: just recurse
                    WalkNode(element, sb, listDepth, 0);
                }
                return;
        }
    }

    private static void ProcessLink(IHtmlElement element, StringBuilder sb, int listDepth)
    {
        string? href = element.GetAttribute("href");
        StringBuilder linkTextSb = new();
        WalkNode(element, linkTextSb, listDepth, 0);
        string linkText = linkTextSb.ToString().Trim();

        if (string.IsNullOrEmpty(href) || string.IsNullOrEmpty(linkText))
        {
            sb.Append(linkText);
            return;
        }

        // Skip redundant URL display for mailto links where text = email
        if (href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
            && href["mailto:".Length..].Equals(linkText, StringComparison.OrdinalIgnoreCase))
        {
            sb.Append(linkText);
            return;
        }

        // Skip if link text IS the URL (no need to duplicate)
        if (linkText.Equals(href, StringComparison.OrdinalIgnoreCase))
        {
            sb.Append(linkText);
            return;
        }

        sb.Append(linkText);
        sb.Append(" (");
        sb.Append(href);
        sb.Append(')');
    }

    private static void ProcessTable(IHtmlElement element, StringBuilder sb, int listDepth)
    {
        // Divider table pattern: single-cell table with only border-top styling
        // Check before presentation role — divider tables inside layout tables are common
        if (IsDividerTable(element))
        {
            EnsureNewline(sb);
            sb.Append("---\n\n");
            return;
        }

        string? role = element.GetAttribute("role");
        if (string.Equals(role, "presentation", StringComparison.OrdinalIgnoreCase))
        {
            // Layout table: just traverse children, extract text
            WalkNode(element, sb, listDepth, 0);
            return;
        }

        // Content table: render as rows
        WalkNode(element, sb, listDepth, 0);
    }

    private static bool IsDividerTable(IHtmlElement table)
    {
        IHtmlCollection<IElement> cells = table.QuerySelectorAll("td");
        if (cells.Length != 1)
        {
            return false;
        }

        string? style = cells[0].GetAttribute("style");
        return style is not null
            && style.Contains("border-top", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(cells[0].TextContent);
    }

    private static void AppendText(IText textNode, StringBuilder sb)
    {
        string text = textNode.Data;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        // Collapse whitespace (like browser rendering)
        StringBuilder collapsed = new();
        bool lastWasSpace = false;
        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace)
                {
                    collapsed.Append(' ');
                    lastWasSpace = true;
                }
            }
            else
            {
                collapsed.Append(c);
                lastWasSpace = false;
            }
        }

        sb.Append(collapsed);
    }

    private static void EnsureNewline(StringBuilder sb)
    {
        if (sb.Length > 0 && sb[^1] != '\n')
        {
            sb.Append('\n');
        }
    }

    private static string NormalizeWhitespace(string text)
    {
        // Collapse 3+ consecutive newlines to 2
        StringBuilder result = new(text.Length);
        int consecutiveNewlines = 0;

        foreach (char c in text)
        {
            if (c == '\n')
            {
                consecutiveNewlines++;
                if (consecutiveNewlines <= 2)
                {
                    result.Append(c);
                }
            }
            else
            {
                consecutiveNewlines = 0;
                result.Append(c);
            }
        }

        return result.ToString().Trim();
    }
}
