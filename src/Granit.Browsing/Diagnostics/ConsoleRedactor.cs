using System.Text.RegularExpressions;

namespace Granit.Browsing.Diagnostics;

/// <summary>
/// Best-effort redaction of well-known secret shapes in console output before it crosses
/// the trust boundary into application logs. Targets bearer tokens, Set-Cookie headers
/// and AWS access keys that page scripts might print.
/// </summary>
/// <remarks>
/// <para>
/// Redaction is intentionally regex-based and approximate — it favours false positives
/// (over-redaction) over false negatives. Anything the regex matches is replaced with
/// <c>***</c>.
/// </para>
/// <para>
/// Recognised patterns: HTTP <c>Bearer</c> tokens, <c>Set-Cookie</c> lines, AWS access
/// key IDs (<c>AKIA…</c>), AWS secret-key style assignments
/// (<c>aws_secret_access_key=…</c>), GCP API keys (<c>AIza…</c>), and basic-auth credentials
/// in URLs.
/// </para>
/// </remarks>
public static partial class ConsoleRedactor
{
    /// <summary>The replacement token used for every redacted span.</summary>
    public const string RedactionMarker = "***";

    /// <summary>Runs every redactor against <paramref name="input"/> and returns the masked text.</summary>
    public static string Redact(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input ?? string.Empty;
        }

        string redacted = BearerTokenRegex().Replace(input, "Bearer " + RedactionMarker);
        redacted = SetCookieRegex().Replace(redacted, "Set-Cookie: " + RedactionMarker);
        redacted = AwsAccessKeyRegex().Replace(redacted, RedactionMarker);
        redacted = AwsSecretAssignmentRegex().Replace(redacted, "aws_secret_access_key=" + RedactionMarker);
        redacted = GcpApiKeyRegex().Replace(redacted, RedactionMarker);
        redacted = BasicAuthInUrlRegex().Replace(redacted, "$1" + RedactionMarker + "@");
        return redacted;
    }

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9._\-+/=]+", RegexOptions.IgnoreCase)]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex(@"Set-Cookie:\s*[^\r\n]+", RegexOptions.IgnoreCase)]
    private static partial Regex SetCookieRegex();

    [GeneratedRegex(@"AKIA[0-9A-Z]{16}")]
    private static partial Regex AwsAccessKeyRegex();

    [GeneratedRegex(@"aws_secret_access_key\s*=\s*[A-Za-z0-9/+=]+", RegexOptions.IgnoreCase)]
    private static partial Regex AwsSecretAssignmentRegex();

    [GeneratedRegex(@"AIza[0-9A-Za-z_\-]{35}")]
    private static partial Regex GcpApiKeyRegex();

    [GeneratedRegex(@"(https?://)[^:/@\s]+:[^@\s]+@")]
    private static partial Regex BasicAuthInUrlRegex();
}
