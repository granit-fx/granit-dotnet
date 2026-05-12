using System.Globalization;
using Scriban.Parsing;

namespace Granit.Templating.Scriban.Exceptions;

/// <summary>
/// Exception thrown when a Scriban template source contains syntax errors.
/// </summary>
public sealed class TemplateParseException : Exception
{
    /// <summary>List of parse errors reported by Scriban.</summary>
    public IReadOnlyList<LogMessage> Errors { get; }

    /// <summary>
    /// Initializes a new <see cref="TemplateParseException"/> from Scriban parse messages.
    /// </summary>
    /// <param name="errors">The list of error messages from <c>Template.Messages</c>.</param>
    public TemplateParseException(IReadOnlyList<LogMessage> errors)
        : base(BuildMessage(errors))
    {
        Errors = errors;
    }

    private static string BuildMessage(IReadOnlyList<LogMessage> errors)
    {
        System.Text.StringBuilder sb = new();
        sb.AppendLine("Failed to parse Scriban template. Errors:");
        foreach (LogMessage error in errors)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"  [{error.Type}] {error.Span}: {error.Message}");
        }
        return sb.ToString();
    }
}
