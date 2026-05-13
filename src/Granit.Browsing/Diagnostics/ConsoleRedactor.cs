using System.Text.RegularExpressions;

namespace Granit.Browsing.Diagnostics;

/// <summary>
/// Best-effort redaction of well-known secret shapes in console output before it crosses
/// the trust boundary into application logs. Closes the bearer-token / Set-Cookie /
/// AWS-key leak from browser console messages into application logs.
/// </summary>
/// <remarks>
/// <para>
/// Redaction is intentionally regex-based and approximate — it favours false positives
/// (over-redaction) over false negatives. Anything the regex matches is replaced with
/// <c>***</c>.
/// </para>
/// <para>
/// Recognised patterns: HTTP <c>Bearer</c> tokens, JWT tokens, <c>Cookie</c> and
/// <c>Set-Cookie</c> headers, AWS access key IDs (<c>AKIA…</c>), AWS secret-key style
/// assignments (<c>aws_secret_access_key=…</c>), GCP API keys (<c>AIza…</c>), vendor
/// tokens (GitHub <c>ghp_</c>/<c>ghs_</c>/<c>gho_</c>/<c>ghu_</c>/<c>ghr_</c>, OpenAI
/// <c>sk-</c>, Slack <c>xox[abprs]-</c>, GitLab <c>glpat-</c>), generic credential
/// assignments (<c>password=…</c>, <c>token=…</c>, <c>api_key=…</c>, <c>secret=…</c>,
/// <c>access_token=…</c>), and basic-auth credentials in URLs.
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
        redacted = JwtRegex().Replace(redacted, RedactionMarker);
        redacted = SetCookieRegex().Replace(redacted, "Set-Cookie: " + RedactionMarker);
        redacted = CookieHeaderRegex().Replace(redacted, "Cookie: " + RedactionMarker);
        redacted = AwsAccessKeyRegex().Replace(redacted, RedactionMarker);
        redacted = AwsSecretAssignmentRegex().Replace(redacted, "aws_secret_access_key=" + RedactionMarker);
        redacted = GcpApiKeyRegex().Replace(redacted, RedactionMarker);
        redacted = VendorTokenRegex().Replace(redacted, RedactionMarker);
        redacted = CredentialAssignmentRegex().Replace(redacted, m => RedactCredentialAssignment(m.Value));
        redacted = BasicAuthInUrlRegex().Replace(redacted, "$1" + RedactionMarker + "@");
        return redacted;
    }

    private static string RedactCredentialAssignment(string match)
    {
        // Preserve the key portion (everything up to and including the = / :) and replace
        // the value with the redaction marker. Match is guaranteed to contain a = or :.
        int eq = match.IndexOfAny(['=', ':']);
        return eq < 0 ? RedactionMarker : match[..(eq + 1)] + RedactionMarker;
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

    [GeneratedRegex(@"eyJ[A-Za-z0-9_\-]{10,}\.[A-Za-z0-9_\-]{10,}\.[A-Za-z0-9_\-]{10,}")]
    private static partial Regex JwtRegex();

    [GeneratedRegex(@"Cookie:\s*[^\r\n]+", RegexOptions.IgnoreCase)]
    private static partial Regex CookieHeaderRegex();

    [GeneratedRegex(@"\b(gh[pousr]_[A-Za-z0-9]{36,}|sk-[A-Za-z0-9]{20,}|xox[abprs]-[A-Za-z0-9-]{10,}|glpat-[A-Za-z0-9_\-]{20,})\b")]
    private static partial Regex VendorTokenRegex();

    [GeneratedRegex(@"\b(password|token|api[_-]?key|secret|access[_-]?token)\s*[=:]\s*[""']?[^""'\s,&]+", RegexOptions.IgnoreCase)]
    private static partial Regex CredentialAssignmentRegex();
}
