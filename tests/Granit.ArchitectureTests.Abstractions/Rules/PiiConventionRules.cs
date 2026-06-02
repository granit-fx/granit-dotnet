using System.Text.RegularExpressions;
using Shouldly;

namespace Granit.ArchitectureTests.Abstractions.Rules;

/// <summary>
/// Reusable PII (Personally Identifiable Information) convention rules for telemetry.
/// Guards ActivitySource tag constants, [LoggerMessage] template parameters, and metric
/// tag keys against GDPR Art. 5 data-minimization violations.
/// </summary>
public static partial class PiiConventionRules
{
    // ── ActivitySource ────────────────────────────────────────────────────────────

    /// <summary>
    /// <c>*ActivitySource.cs</c> tag constant values inside nested <c>Tags</c> classes must not
    /// use PII-indicative names (e.g. <c>"email.to"</c>, <c>"recipient"</c>, <c>"ip_address"</c>).
    /// Such names signal that the tag value will contain PII — even if the call-site value
    /// is not inspected by this test.
    /// </summary>
    /// <param name="srcDir">Path to the <c>src/</c> directory.</param>
    /// <param name="repoRoot">Repo root for relative paths in violation messages.</param>
    /// <param name="exemptions">
    /// Qualified constant names (<c>"Tags.ConstantName"</c>) confirmed safe because the
    /// value is always redacted at every call site (e.g. via <c>LogRedaction.*</c>).
    /// </param>
    public static void ActivitySourceTagsShouldNotContainPiiNames(
        string srcDir,
        string repoRoot,
        IReadOnlySet<string>? exemptions = null)
    {
        exemptions ??= new HashSet<string>(StringComparer.Ordinal);
        List<string> violations = [];

        foreach (string csFile in GetActivitySourceFiles(srcDir))
        {
            ScanActivitySourceFile(csFile, repoRoot, exemptions, violations);
        }

        violations.ShouldBeEmpty(
            "ActivitySource tag constants must not define PII-indicative names " +
            "(GDPR Art. 5 — data minimization, trace span attribute privacy). " +
            "Rename the tag, or add an exemption with justification confirming redaction at every call site. " +
            $"Violators: {string.Join("; ", violations)}");
    }

    // ── LoggerMessage ─────────────────────────────────────────────────────────────

    /// <summary>
    /// <c>[LoggerMessage]</c> template parameters must not use PII-indicative names unless
    /// confirmed redacted. Prevents email, phone, IP, username, and device token leakage
    /// to log backends (GDPR Art. 5, ISO 27001 A.5.34).
    /// </summary>
    /// <param name="srcDir">Path to the <c>src/</c> directory.</param>
    /// <param name="repoRoot">Repo root for relative paths.</param>
    /// <param name="exemptions">
    /// Qualified parameter names (<c>"ClassName.ParameterName"</c>) confirmed to receive
    /// redacted values at all call sites.
    /// </param>
    public static void LoggerMessageParametersShouldNotContainPiiNames(
        string srcDir,
        string repoRoot,
        IReadOnlySet<string>? exemptions = null)
    {
        exemptions ??= new HashSet<string>(StringComparer.Ordinal);
        List<string> violations = [];

        foreach (string csFile in GetSrcCsFiles(srcDir))
        {
            ScanLoggerMessageFile(csFile, repoRoot, exemptions, violations);
        }

        violations.ShouldBeEmpty(
            "[LoggerMessage] template parameters must not use PII-indicative names (GDPR Art. 5). " +
            "Redact the value with a helper (e.g. LogRedaction.*) and rename the parameter, " +
            "or add an exemption with justification. " +
            $"Violators: {string.Join("; ", violations)}");
    }

    // ── Metrics ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Metric tag keys in <c>*Metrics.cs</c> files must not use PII-indicative names.
    /// PII in metric tags causes both a privacy leak and cardinality explosion (GDPR Art. 5,
    /// ISO 27001 A.8.2). Only bounded, low-cardinality values are safe.
    /// </summary>
    /// <param name="srcDir">Path to the <c>src/</c> directory.</param>
    /// <param name="repoRoot">Repo root for relative paths.</param>
    /// <param name="allowedTagNames">
    /// Explicit allowlist of tag names confirmed safe (bounded, low-cardinality):
    /// e.g. <c>tenant_id</c>, <c>status</c>, <c>operation</c>. Names not on this list
    /// are screened against PII patterns. Pass <c>null</c> to use the built-in defaults.
    /// </param>
    public static void MetricsTagsShouldNotContainPiiNames(
        string srcDir,
        string repoRoot,
        IReadOnlySet<string>? allowedTagNames = null)
    {
        allowedTagNames ??= DefaultAllowedMetricTagNames;
        List<string> violations = [];

        foreach (string csFile in GetMetricsFiles(srcDir))
        {
            string relativePath = Path.GetRelativePath(repoRoot, csFile);
            int lineNumber = 0;

            foreach (string line in File.ReadLines(csFile))
            {
                lineNumber++;

                foreach (Match match in TagKeyLiteral().Matches(line))
                {
                    string tagName = match.Groups[1].Value;

                    if (allowedTagNames.Contains(tagName))
                    {
                        continue;
                    }

                    if (PiiMetricTagName().IsMatch(tagName))
                    {
                        violations.Add($"{relativePath}:{lineNumber} tag \"{tagName}\"");
                    }
                }
            }
        }

        violations.ShouldBeEmpty(
            "Metric tags must not contain PII-indicative names (GDPR Art. 5 — data minimization, " +
            "cardinality explosion risk). Use bounded, low-cardinality values only. " +
            $"Violators: {string.Join("; ", violations)}");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static readonly IReadOnlySet<string> DefaultAllowedMetricTagNames =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "tenant_id", "category", "status", "operation", "grant_type",
            "reason", "provider", "action", "tool_name", "resource_name",
            "event_type", "entity_type", "change_type", "is_new_user",
            "keys_generated", "keys_retired", "keys_revoked", "channel",
            "format", "template_type", "culture", "data_type", "job_type",
            "direction", "method", "policy", "state", "module",
            "severity", "analysis_type", "feature_name",
        };

    private static void ScanActivitySourceFile(
        string csFile,
        string repoRoot,
        IReadOnlySet<string> exemptions,
        List<string> violations)
    {
        string relativePath = Path.GetRelativePath(repoRoot, csFile);
        string[] lines = File.ReadAllLines(csFile);
        string? currentClass = null;
        bool insideTagsClass = false;
        int braceDepth = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];

            Match classMatch = SimpleClassDeclaration().Match(line);
            if (classMatch.Success)
            {
                string name = classMatch.Groups[1].Value;
                if (name == "Tags")
                {
                    insideTagsClass = true;
                    braceDepth = 0;
                }
                currentClass = name;
                continue;
            }

            if (insideTagsClass)
            {
                foreach (char c in line)
                {
                    if (c == '{')
                    {
                        braceDepth++;
                    }
                    else if (c == '}')
                    {
                        braceDepth--;
                        if (braceDepth <= 0)
                        {
                            insideTagsClass = false;
                        }
                    }
                }
            }

            if (!insideTagsClass)
            {
                continue;
            }

            Match tagMatch = TagConstantDefinition().Match(line);
            if (!tagMatch.Success)
            {
                continue;
            }

            string constantName = tagMatch.Groups[1].Value;
            string tagValue = tagMatch.Groups[2].Value;

            if (!PiiActivityTagValue().IsMatch(tagValue))
            {
                continue;
            }

            string qualifiedName = $"{currentClass}.{constantName}";
            if (exemptions.Contains(qualifiedName))
            {
                continue;
            }

            violations.Add($"{relativePath}:{i + 1} {qualifiedName} = \"{tagValue}\"");
        }
    }

    private static void ScanLoggerMessageFile(
        string csFile,
        string repoRoot,
        IReadOnlySet<string> exemptions,
        List<string> violations)
    {
        string relativePath = Path.GetRelativePath(repoRoot, csFile);
        string[] lines = File.ReadAllLines(csFile);
        string? currentClass = null;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];

            Match classMatch = AnchoredClassDeclaration().Match(line);
            if (classMatch.Success)
            {
                currentClass = classMatch.Groups[1].Value;
                continue;
            }

            if (currentClass is null)
            {
                continue;
            }

            Match loggerMatch = LoggerMessageWithMessage().Match(line);
            string template;

            if (loggerMatch.Success)
            {
                template = loggerMatch.Groups[1].Value;
            }
            else if (LoggerMessageContinuation().IsMatch(line))
            {
                Match msgMatch = MessageParameter().Match(line);
                if (!msgMatch.Success)
                {
                    continue;
                }

                template = msgMatch.Groups[1].Value;
            }
            else
            {
                continue;
            }

            if (string.IsNullOrEmpty(template))
            {
                for (int j = i + 1; j < Math.Min(lines.Length, i + 5); j++)
                {
                    Match m = MessageParameter().Match(lines[j]);
                    if (m.Success) { template = m.Groups[1].Value; break; }
                }
            }

            if (string.IsNullOrEmpty(template))
            {
                continue;
            }

            foreach (Match paramMatch in TemplateParameter().Matches(template))
            {
                string paramName = paramMatch.Groups[1].Value;
                if (!PiiLoggerParamName().IsMatch(paramName))
                {
                    continue;
                }

                string qualifiedName = $"{currentClass}.{paramName}";
                if (exemptions.Contains(qualifiedName))
                {
                    continue;
                }

                violations.Add($"{relativePath}:{i + 1} {qualifiedName} in \"{template}\"");
            }
        }
    }

    private static IEnumerable<string> GetActivitySourceFiles(string srcDir) =>
        Directory.GetFiles(srcDir, "*ActivitySource.cs", SearchOption.AllDirectories)
            .Where(f => !IsInBuildOutput(f));

    private static IEnumerable<string> GetMetricsFiles(string srcDir) =>
        Directory.GetFiles(srcDir, "*Metrics.cs", SearchOption.AllDirectories)
            .Where(f => !IsInBuildOutput(f));

    private static IEnumerable<string> GetSrcCsFiles(string srcDir) =>
        Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsInBuildOutput(f));

    private static bool IsInBuildOutput(string path) =>
        path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
        || path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar);

    // ── Regex patterns ────────────────────────────────────────────────────────────

    [GeneratedRegex(@"(?:class|struct)\s+(\w+)")]
    private static partial Regex SimpleClassDeclaration();

    [GeneratedRegex(@"^\s*(?:\w+\s+)*?(?:class|record|struct)\s+(\w+)")]
    private static partial Regex AnchoredClassDeclaration();

    [GeneratedRegex(@"const\s+string\s+(\w+)\s*=\s*""([^""]+)""")]
    private static partial Regex TagConstantDefinition();

    [GeneratedRegex(
        @"(?i)\b(email\.to|recipient|phone|mobile|ip[_.]address|username|user[_.]name|display[_.]name|full[_.]name|first[_.]name|last[_.]name|device[_.]token|password|secret|ssn|national[_.]id|passport|bank[_.]account|iban)\b")]
    private static partial Regex PiiActivityTagValue();

    [GeneratedRegex(@"\[LoggerMessage\(.*Message\s*=\s*""([^""]+)""")]
    private static partial Regex LoggerMessageWithMessage();

    [GeneratedRegex(@"^\s*Message\s*=\s*""")]
    private static partial Regex LoggerMessageContinuation();

    [GeneratedRegex(@"Message\s*=\s*""([^""]+)""")]
    private static partial Regex MessageParameter();

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex TemplateParameter();

    [GeneratedRegex(
        @"^(?!Redacted|Masked)(Email|Recipient|Phone|PhoneNumber|Mobile|IpAddress|Ip|Address|Username|UserName|DisplayName|FullName|FirstName|LastName|Token|DeviceToken|Subject|SessionId|Password|Secret|SocialSecurity|NationalId|PassportNumber|BankAccount|Iban)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex PiiLoggerParamName();

    [GeneratedRegex(@"\{\s*""(\w+)""\s*,")]
    private static partial Regex TagKeyLiteral();

    [GeneratedRegex(
        @"(?i)(email|mail|phone|mobile|address|street|city|postal|zip|firstName|lastName|fullName|displayName|userName|username|ssn|nationalId|passport|birthDate|dateOfBirth|salary|income|bankAccount|iban|ipAddress|password|secret|token|avatar|photo)")]
    private static partial Regex PiiMetricTagName();
}
