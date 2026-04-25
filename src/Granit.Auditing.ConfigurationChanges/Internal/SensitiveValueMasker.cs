using System.Text.RegularExpressions;

namespace Granit.Auditing.ConfigurationChanges.Internal;

/// <summary>
/// Masks setting values whose names match known sensitive patterns
/// (connection strings, passwords, API keys, tokens, secrets).
/// </summary>
/// <remarks>
/// Applied to <see cref="Domain.AuditPropertyChange.OriginalValue"/> /
/// <see cref="Domain.AuditPropertyChange.NewValue"/> in configuration change
/// audit handlers to prevent secrets from being persisted in the audit trail.
/// </remarks>
internal static partial class SensitiveValueMasker
{
    private const string Mask = "***";

    /// <summary>
    /// Returns <c>"***"</c> if the setting name matches a sensitive pattern
    /// or the value contains embedded credentials; otherwise returns the
    /// original value unchanged.
    /// </summary>
    public static string? MaskIfSensitive(string settingName, string? value)
    {
        if (value is null)
        {
            return null;
        }

        if (SensitiveNamePattern().IsMatch(settingName))
        {
            return Mask;
        }

        if (EmbeddedCredentialPattern().IsMatch(value))
        {
            return Mask;
        }

        return value;
    }

    // Negative lookahead `(?![a-z])` prevents over-masking on words that
    // happen to start with a sensitive token (e.g. "KeyboardLayout" — `Key`
    // followed by `b` rejects the match). PascalCase suffixes such as
    // "ClientSecret" or "ApiKey" still match because the trailing lookahead
    // is satisfied by end-of-string or by the next non-lowercase character.
    [GeneratedRegex(
        @"(Password|Pwd|Secret|Token|Credential|ConnectionString|ConnString|ApiKey|PrivateKey|PublicKey|SigningKey|HmacKey|EncryptionKey|Bearer|Authorization|AccessKey|SharedAccessKey|SasToken|Key)(?![a-z])",
        RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex SensitiveNamePattern();

    [GeneratedRegex(
        @"(://[^/\s]*:[^@/\s]+@|Password\s*=|Pwd\s*=|AccountKey\s*=|SharedAccessKey\s*=)",
        RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex EmbeddedCredentialPattern();
}
