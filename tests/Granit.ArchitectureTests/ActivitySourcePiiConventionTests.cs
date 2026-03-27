using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates that <c>*ActivitySource.cs</c> tag constants do not define PII-indicative
/// names that would cause sensitive data to flow into OpenTelemetry trace spans.
/// Tags like <c>"email.to"</c> or <c>"sms.recipient"</c> signal that the tag value
/// will contain PII — even if the architecture test cannot verify the actual value.
/// </summary>
/// <remarks>
/// Complements <see cref="MetricsPiiConventionTests"/> (which guards metric tags) by
/// extending coverage to distributed trace span attributes.
/// </remarks>
public sealed partial class ActivitySourcePiiConventionTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    /// <summary>
    /// Known exemptions — tag constant values that match PII heuristics but are
    /// confirmed safe (e.g., the tag value is always redacted at the call site).
    /// Format: <c>"Tags.ConstantName"</c>.
    /// </summary>
    /// <remarks>
    /// <b>Maintenance rule:</b> adding an exemption requires confirming that ALL call
    /// sites for <c>SetTag(Tags.Xxx, value)</c> pass a <c>LogRedaction.*</c> call.
    /// </remarks>
    private static readonly HashSet<string> Exemptions = new(StringComparer.Ordinal)
    {
        // identity.*.user_id tags carry GUIDs (pseudonymous, not direct PII).
        // Standard OpenTelemetry semantic convention for user identification.
        "Tags.UserId",

        // Notification tags — values are redacted at every call site via
        // LogRedaction.EmailDomain() / LogRedaction.HashPrefix(). The tag key
        // names remain for dashboard backward compatibility.
        "Tags.To",        // acs.email.to — value is EmailDomain(recipient)
        "Tags.Recipient", // acs-sms.recipient / sns-sms.recipient — value is HashPrefix(phone)
    };

    /// <summary>
    /// ActivitySource tag constant values must not use PII-indicative names.
    /// Tags named <c>email</c>, <c>recipient</c>, <c>phone</c>, <c>ip_address</c>,
    /// <c>username</c>, or <c>token</c> signal that the tag value will contain PII.
    /// </summary>
    [Fact]
    public void ActivitySource_tag_values_should_not_contain_pii_names()
    {
        string srcDir = Path.Join(RepoRoot, "src");
        List<string> violations = [];

        foreach (string csFile in GetActivitySourceFiles(srcDir))
        {
            ScanFileForPiiTags(csFile, violations);
        }

        violations.ShouldBeEmpty(
            "ActivitySource tag constants must not define PII-indicative names " +
            "(GDPR Art. 5 — data minimization, trace span attribute privacy). " +
            "Rename the tag or add an exemption in ActivitySourcePiiConventionTests.Exemptions " +
            $"with justification. Violators: {string.Join("; ", violations)}");
    }

    private static void ScanFileForPiiTags(string csFile, List<string> violations)
    {
        string relativePath = Path.GetRelativePath(RepoRoot, csFile);
        string[] lines = File.ReadAllLines(csFile);
        string? currentClass = null;
        bool insideTagsClass = false;
        int braceDepth = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];

            // Track current class/struct name
            Match classMatch = ClassDeclaration().Match(line);
            if (classMatch.Success)
            {
                string name = classMatch.Groups[1].Value;

                // Only scan constants inside "Tags" nested classes/structs.
                // "Operations" constants are span names, not tag keys — they don't carry PII values.
                if (name == "Tags")
                {
                    insideTagsClass = true;
                    braceDepth = 0;
                }

                currentClass = name;
                continue;
            }

            // Track brace depth to know when we leave the Tags class
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

            // Match tag constant definitions: public/internal const string Xxx = "tag.name";
            Match tagMatch = TagConstantDefinition().Match(line);
            if (!tagMatch.Success)
            {
                continue;
            }

            string constantName = tagMatch.Groups[1].Value;
            string tagValue = tagMatch.Groups[2].Value;

            // Check the tag VALUE (the string literal) for PII patterns
            if (!PiiTagValue().IsMatch(tagValue))
            {
                continue;
            }

            string qualifiedName = $"{currentClass}.{constantName}";
            if (Exemptions.Contains(qualifiedName))
            {
                continue;
            }

            violations.Add($"{relativePath}:{i + 1} {qualifiedName} = \"{tagValue}\"");
        }
    }

    private static IEnumerable<string> GetActivitySourceFiles(string srcDir)
    {
        foreach (string csFile in Directory.GetFiles(srcDir, "*ActivitySource.cs", SearchOption.AllDirectories))
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
        string? dir = Path.GetDirectoryName(typeof(ActivitySourcePiiConventionTests).Assembly.Location);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Join(dir, ".git")))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Could not find repository root (.git directory)");
    }

    /// <summary>
    /// Matches class/struct declarations. Group 1: type name.
    /// </summary>
    [GeneratedRegex(@"(?:class|struct)\s+(\w+)", RegexOptions.None)]
    private static partial Regex ClassDeclaration();

    /// <summary>
    /// Matches tag constant definitions: <c>const string Xxx = "value";</c>.
    /// Group 1: constant name, Group 2: string value.
    /// </summary>
    [GeneratedRegex(@"const\s+string\s+(\w+)\s*=\s*""([^""]+)""", RegexOptions.None)]
    private static partial Regex TagConstantDefinition();

    /// <summary>
    /// Tag string values that indicate PII content.
    /// Matches tag names like <c>email.to</c>, <c>sms.recipient</c>, <c>user.email</c>,
    /// <c>ip_address</c>, <c>phone</c>, <c>device_token</c>, etc.
    /// </summary>
    [GeneratedRegex(
        @"(?i)\b(email\.to|recipient|phone|mobile|ip[_.]address|username|user[_.]name|display[_.]name|full[_.]name|first[_.]name|last[_.]name|device[_.]token|password|secret|ssn|national[_.]id|passport|bank[_.]account|iban)\b")]
    private static partial Regex PiiTagValue();
}
