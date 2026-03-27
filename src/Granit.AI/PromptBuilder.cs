using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Granit.AI;

/// <summary>
/// Builds LLM prompts with input sanitization to mitigate prompt injection attacks.
/// Separates system instructions from user data using structured delimiters
/// and enforces input length limits.
/// </summary>
public sealed partial class PromptBuilder
{
    private const int DefaultMaxInputLength = 50_000;
    private const string DataBlockOpen = "<data>";
    private const string DataBlockClose = "</data>";

    private readonly StringBuilder _sb = new();
    private readonly int _maxInputLength;

    public PromptBuilder(int maxInputLength = DefaultMaxInputLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxInputLength);
        _maxInputLength = maxInputLength;
    }

    /// <summary>
    /// Appends a system instruction line. Not sanitized — must be developer-controlled.
    /// </summary>
    public PromptBuilder AppendInstruction(string instruction)
    {
        _sb.AppendLine(instruction);
        return this;
    }

    /// <summary>
    /// Appends user-controlled data inside a structured <c>&lt;data&gt;</c> block
    /// with sanitization and length truncation.
    /// </summary>
    /// <param name="label">A developer-controlled label for the data (e.g., "User ID", "Text to analyze").</param>
    /// <param name="value">The user-controlled value to include. Sanitized and truncated.</param>
    public PromptBuilder AppendUserData(string label, string? value)
    {
        _sb.Append(label).Append(": ");
        _sb.Append(DataBlockOpen);
        _sb.Append(SanitizeInput(value ?? string.Empty));
        _sb.AppendLine(DataBlockClose);
        return this;
    }

    /// <summary>
    /// Appends a block of user-controlled text (e.g., a document to analyze)
    /// inside a structured delimited block with sanitization.
    /// </summary>
    /// <param name="label">A developer-controlled label for the block.</param>
    /// <param name="text">The user-controlled text. Sanitized and truncated.</param>
    public PromptBuilder AppendUserTextBlock(string label, string? text)
    {
        _sb.AppendLine(label).Append(DataBlockOpen).AppendLine();
        _sb.AppendLine(SanitizeInput(text ?? string.Empty));
        _sb.AppendLine(DataBlockClose);
        return this;
    }

    /// <summary>
    /// Appends user-controlled data as a JSON-encoded value, preventing
    /// any content from breaking out of the data structure.
    /// </summary>
    /// <param name="label">A developer-controlled label.</param>
    /// <param name="value">The user-controlled value. JSON-encoded for safety.</param>
    public PromptBuilder AppendUserDataJson(string label, string? value)
    {
        _sb.Append(label).Append(": ");
        _sb.AppendLine(JsonSerializer.Serialize(Truncate(value ?? string.Empty)));
        return this;
    }

    /// <summary>
    /// Appends a set of user-controlled key-value pairs as a JSON object.
    /// </summary>
    public PromptBuilder AppendUserDataMap(string label, IEnumerable<KeyValuePair<string, string?>> pairs)
    {
        _sb.AppendLine(label);
        _sb.Append(DataBlockOpen).AppendLine();

        foreach (KeyValuePair<string, string?> pair in pairs)
        {
            _sb.Append("  ").Append(SanitizeInput(pair.Key)).Append(": ");
            _sb.AppendLine(SanitizeInput(pair.Value ?? string.Empty));
        }

        _sb.AppendLine(DataBlockClose);
        return this;
    }

    /// <summary>Builds the final prompt string.</summary>
    public string Build() => _sb.ToString();

    /// <inheritdoc />
    public override string ToString() => Build();

    /// <summary>
    /// Sanitizes user input to prevent prompt injection.
    /// Strips Unicode control characters (VULN-200), XML-like tags that could confuse
    /// delimiters, and truncates to the configured maximum length.
    /// </summary>
    internal string SanitizeInput(string input)
    {
        input = Truncate(input);
        input = StripControlCharacters(input);
        return StripDangerousPatterns(input);
    }

    private string Truncate(string input) =>
        input.Length > _maxInputLength
            ? $"{input[.._maxInputLength]}[TRUNCATED]"
            : input;

    /// <summary>
    /// Strips ALL XML/HTML-like tags from user input to prevent prompt injection
    /// via delimiter spoofing (e.g. &lt;data&gt;, &lt;system&gt;, &lt;tool&gt;,
    /// &lt;assistant&gt;, and any other tag an attacker might use).
    /// </summary>
    [GeneratedRegex(@"</?[a-zA-Z][a-zA-Z0-9]*[^>]*>", RegexOptions.None, 100)]
    private static partial Regex XmlLikeTagRegex();

    /// <summary>
    /// Strips Unicode control characters, zero-width chars, and bidirectional overrides
    /// that could be used to obfuscate prompt injection payloads (VULN-200).
    /// Preserves tab (0x09), LF (0x0A), and CR (0x0D).
    /// </summary>
    internal static string StripControlCharacters(string input) =>
        ControlCharacterRegex().Replace(input, string.Empty);

    [GeneratedRegex(@"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F-\x9F\u200B-\u200F\u202A-\u202E\uFEFF]")]
    private static partial Regex ControlCharacterRegex();

    private static string StripDangerousPatterns(string input) =>
        XmlLikeTagRegex().Replace(input, string.Empty);
}
