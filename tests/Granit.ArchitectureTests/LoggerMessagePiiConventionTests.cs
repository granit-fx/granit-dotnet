using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates that <c>[LoggerMessage]</c> template parameters do not use PII-indicative
/// names unless the value is confirmed redacted (via <c>LogRedaction.*</c>).
/// Prevents sensitive data (email, phone, IP, username, device tokens) from
/// reaching observability backends (GDPR Art. 5, ISO 27001 A.5.34).
/// </summary>
/// <remarks>
/// Scans all <c>[LoggerMessage]</c> attributes in <c>src/</c> for template parameters
/// matching PII patterns. Exempted parameters have been reviewed and confirmed to
/// receive redacted values at every call site.
/// </remarks>
public sealed partial class LoggerMessagePiiConventionTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    /// <summary>
    /// Known exemptions — parameters whose names match PII heuristics but are
    /// confirmed to receive redacted values via <c>LogRedaction.*</c> at all call sites.
    /// Format: <c>"ClassName.ParameterName"</c>.
    /// </summary>
    /// <remarks>
    /// <b>Maintenance rule:</b> adding an exemption requires confirming that ALL call
    /// sites for the log method pass a <c>LogRedaction.*</c> call, not a raw value.
    /// Include the VULN reference or PR that introduced the redaction.
    /// </remarks>
    private static readonly HashSet<string> Exemptions = new(StringComparer.Ordinal)
    {
        // GUIDs — pseudonymous identifiers (GDPR Recital 26), standard OTel practice.
        // Acceptable in operational logs for debugging and forensics.
        "KeycloakIdentityProvider.UserId",
        "EntraIdIdentityProvider.UserId",
        "CognitoIdentityProvider.UserId",
        "GoogleCloudIdentityProvider.UserId",
        "NullPasswordResetNotifier.UserId",
        "AspNetImpersonationService.ImpersonatorId",
        "AspNetImpersonationService.TargetUserId",
        "MobilePushNotificationChannel.UserId",
        "BackChannelLogoutTokenValidator.SessionId",
        "BackChannelLogoutTokenValidator.SubjectId",
        "BffLoginEndpoints.SessionId",
        "BffSessionEndpoints.SessionId",
        "DefaultBffLogoutOrchestrator.SessionId",
        "BffBackChannelLogoutEndpoints.Subject",
        "ConnectAuthorizationEndpoints.Subject",
        "ConnectTokenEndpoints.Subject",
        "BffTokenInjectionTransform.SessionId",
        "BffTokenInjectionMiddleware.SessionId",
        "KeycloakUserTokenExchangeService.UserId",
        "BackgroundJobManager.UserId",

        // Session IDs — opaque internal identifiers (UUID or random), not direct PII.
        // Used for OIDC session management and back-channel logout correlation.
        "EntraIdIdentityProvider.SessionId",
        "KeycloakIdentityProvider.SessionId",
        "PkceState.SessionId",
        "DistributedCacheRevokedSessionStore.SessionId",

        // Infrastructure address — Vault server URL, not a personal address.
        "VaultClientFactory.Address",
    };

    /// <summary>
    /// LoggerMessage template parameters must not use PII-indicative names unless
    /// exempted (values confirmed redacted). Prevents email, phone, IP, username,
    /// and device token leakage to log backends.
    /// </summary>
    [Fact]
    public void LoggerMessage_parameters_should_not_contain_pii_names()
    {
        string srcDir = Path.Join(RepoRoot, "src");
        List<string> violations = [];

        foreach (string csFile in GetCsFiles(srcDir))
        {
            ScanFileForPiiViolations(csFile, violations);
        }

        violations.ShouldBeEmpty(
            "[LoggerMessage] template parameters must not use PII-indicative names " +
            "(GDPR Art. 5 — data minimization). Either redact the value with LogRedaction.* " +
            "and rename the parameter (e.g., {RedactedRecipient}), or add an exemption " +
            "in LoggerMessagePiiConventionTests.Exemptions with justification. " +
            $"Violators: {string.Join("; ", violations)}");
    }

    private static void ScanFileForPiiViolations(string csFile, List<string> violations)
    {
        string relativePath = Path.GetRelativePath(RepoRoot, csFile);
        string[] lines = File.ReadAllLines(csFile);
        string? currentClass = null;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];

            // Track current class name
            Match classMatch = ClassDeclaration().Match(line);
            if (classMatch.Success)
            {
                currentClass = classMatch.Groups[1].Value;
                continue;
            }

            if (currentClass is null)
            {
                continue;
            }

            // Check for [LoggerMessage] attribute with Message parameter
            Match loggerMatch = LoggerMessageAttribute().Match(line);
            if (!loggerMatch.Success)
            {
                // Handle multi-line attributes — check if previous lines started a LoggerMessage
                if (LoggerMessageContinuation().IsMatch(line))
                {
                    loggerMatch = MessageParameter().Match(line);
                }

                if (!loggerMatch.Success)
                {
                    continue;
                }
            }

            string template = loggerMatch.Groups.Count > 1
                ? loggerMatch.Groups[1].Value
                : "";

            // Also check continuation lines for the template
            if (string.IsNullOrEmpty(template))
            {
                for (int j = i + 1; j < Math.Min(lines.Length, i + 5); j++)
                {
                    Match msgMatch = MessageParameter().Match(lines[j]);
                    if (msgMatch.Success)
                    {
                        template = msgMatch.Groups[1].Value;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(template))
            {
                continue;
            }

            // Extract template parameters: {ParamName}
            foreach (Match paramMatch in TemplateParameter().Matches(template))
            {
                string paramName = paramMatch.Groups[1].Value;

                if (!PiiParameterName().IsMatch(paramName))
                {
                    continue;
                }

                string qualifiedName = $"{currentClass}.{paramName}";
                if (Exemptions.Contains(qualifiedName))
                {
                    continue;
                }

                violations.Add($"{relativePath}:{i + 1} {qualifiedName} in \"{template}\"");
            }
        }
    }

    private static IEnumerable<string> GetCsFiles(string srcDir)
    {
        foreach (string csFile in Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories))
        {
            if (csFile.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                || csFile.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            {
                continue;
            }

            yield return csFile;
        }
    }

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(LoggerMessagePiiConventionTests).Assembly.Location);
        while (dir is not null)
        {
            string gitPath = Path.Join(dir, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Could not find repository root (.git directory)");
    }

    /// <summary>
    /// Matches class/record/struct declarations. Group 1: type name.
    /// </summary>
    /// <remarks>
    /// Anchored to start-of-line and accepts only word-character modifier tokens before
    /// the keyword, so the keywords are not matched inside comments — comment lines
    /// start with <c>//</c> or contain <c>*</c>, neither of which is a <c>\w</c>
    /// character followed by whitespace. Without this guard, a comment like
    /// <c>// (authorization record lookup, ...)</c> would set <c>currentClass</c> to
    /// <c>"lookup"</c> and break the per-class exemption matching downstream.
    /// </remarks>
    [GeneratedRegex(@"^\s*(?:\w+\s+)*?(?:class|record|struct)\s+(\w+)", RegexOptions.None)]
    private static partial Regex ClassDeclaration();

    /// <summary>
    /// Matches a <c>[LoggerMessage]</c> attribute with an inline <c>Message = "..."</c>.
    /// Group 1: the template string.
    /// </summary>
    [GeneratedRegex(@"\[LoggerMessage\(.*Message\s*=\s*""([^""]+)""", RegexOptions.None)]
    private static partial Regex LoggerMessageAttribute();

    /// <summary>
    /// Matches continuation lines belonging to a multi-line LoggerMessage attribute.
    /// </summary>
    [GeneratedRegex(@"^\s*Message\s*=\s*""", RegexOptions.None)]
    private static partial Regex LoggerMessageContinuation();

    /// <summary>
    /// Extracts the Message string from a continuation line. Group 1: template.
    /// </summary>
    [GeneratedRegex(@"Message\s*=\s*""([^""]+)""", RegexOptions.None)]
    private static partial Regex MessageParameter();

    /// <summary>
    /// Extracts template parameter names: <c>{ParamName}</c>. Group 1: parameter name.
    /// </summary>
    [GeneratedRegex(@"\{(\w+)\}", RegexOptions.None)]
    private static partial Regex TemplateParameter();

    /// <summary>
    /// Parameter names that indicate PII — must be redacted or exempted.
    /// Matches raw names; "Redacted*" or "Masked*" prefixed names pass through.
    /// </summary>
    [GeneratedRegex(
        @"^(?!Redacted|Masked)(Email|Recipient|Phone|PhoneNumber|Mobile|IpAddress|Ip|Address|Username|UserName|DisplayName|FullName|FirstName|LastName|Token|DeviceToken|Subject|SessionId|Password|Secret|SocialSecurity|NationalId|PassportNumber|BankAccount|Iban)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex PiiParameterName();
}
