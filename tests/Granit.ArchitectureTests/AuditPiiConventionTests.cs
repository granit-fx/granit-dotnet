using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates that entity properties with PII-indicative names are annotated with
/// <c>[SensitiveData]</c> or <c>[AuditIgnore]</c> to prevent sensitive data from
/// being recorded in cleartext in the audit trail (GDPR Art. 25, ISO 27001 A.5.34).
/// </summary>
/// <remarks>
/// Scans all <c>.cs</c> files in <c>src/</c> for classes inheriting from
/// <c>Entity</c>, <c>AggregateRoot</c>, or audited variants, then checks
/// their properties against known PII name patterns.
/// </remarks>
public sealed partial class AuditPiiConventionTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    /// <summary>
    /// Known exemptions — properties whose names match PII heuristics but are
    /// intentionally unprotected (with documented justification).
    /// Format: <c>"ClassName.PropertyName"</c>.
    /// </summary>
    private static readonly HashSet<string> Exemptions = new(StringComparer.Ordinal)
    {
        // AuditEntry.IpAddress is the audit record itself — masking it would defeat the purpose.
        "AuditEntry.IpAddress",
        // AuditEntry.UserId/UserName are the audit actor — required for ISO 27001 A.12.4.1.
        "AuditEntry.UserId",
        "AuditEntry.UserName",
    };

    /// <summary>
    /// Entity properties with PII-indicative names must have <c>[SensitiveData]</c>
    /// or <c>[AuditIgnore]</c> to prevent sensitive data in the audit trail.
    /// </summary>
    [Fact]
    public void Pii_properties_on_entities_should_have_audit_attribute()
    {
        string srcDir = Path.Join(RepoRoot, "src");
        List<string> violations = [];

        foreach (string csFile in GetEntityFiles(srcDir))
        {
            ScanFileForPiiViolations(csFile, violations);
        }

        violations.ShouldBeEmpty(
            "Entity properties with PII-indicative names must be annotated with " +
            "[SensitiveData] or [AuditIgnore] (GDPR Art. 25 — data minimization). " +
            "Add the attribute or add an exemption in AuditPiiConventionTests.Exemptions with justification. " +
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
            Match classMatch = EntityClassDeclaration().Match(line);
            if (classMatch.Success)
            {
                currentClass = classMatch.Groups[1].Value;
                continue;
            }

            if (currentClass is null)
            {
                continue;
            }

            // Check for PII property
            Match propMatch = PublicPropertyDeclaration().Match(line);
            if (!propMatch.Success)
            {
                continue;
            }

            string propName = propMatch.Groups[1].Value;

            if (!PiiPropertyName().IsMatch(propName))
            {
                continue;
            }

            string qualifiedName = $"{currentClass}.{propName}";
            if (Exemptions.Contains(qualifiedName))
            {
                continue;
            }

            // Check preceding lines for [SensitiveData] or [AuditIgnore]
            bool hasAuditAttribute = HasAuditAttributeAbove(lines, i);
            if (!hasAuditAttribute)
            {
                violations.Add($"{relativePath}:{i + 1} {qualifiedName}");
            }
        }
    }

    private static bool HasAuditAttributeAbove(string[] lines, int propertyLineIndex)
    {
        // Scan up to 5 lines above (attribute, summary, remarks, etc.)
        for (int j = propertyLineIndex - 1; j >= Math.Max(0, propertyLineIndex - 5); j--)
        {
            string above = lines[j];
            if (AuditAttributePattern().IsMatch(above))
            {
                return true;
            }

            // Stop scanning if we hit a non-attribute, non-comment, non-blank line
            if (!above.TrimStart().StartsWith('/')
                && !above.TrimStart().StartsWith('[')

                && !string.IsNullOrWhiteSpace(above))
            {
                break;
            }
        }

        return false;
    }

    private static IEnumerable<string> GetEntityFiles(string srcDir)
    {
        foreach (string csFile in Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories))
        {
            if (csFile.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                || csFile.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            {
                continue;
            }

            // Only scan files that define entity classes
            string content = File.ReadAllText(csFile);
            if (EntityClassDeclaration().IsMatch(content))
            {
                yield return csFile;
            }
        }
    }

    private static string FindRepoRoot()
    {
        string? dir = Path.GetDirectoryName(typeof(AuditPiiConventionTests).Assembly.Location);
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
    /// Matches class declarations inheriting from Entity, AggregateRoot, or audited variants.
    /// Group 1: class name.
    /// </summary>
    [GeneratedRegex(
        @"class\s+(\w+)\s*(?:<[^>]+>)?\s*:\s*(?:[\w.]+\s*,\s*)*(?:(?:Creation)?Audited(?:Entity|AggregateRoot)|Full(?:Audited)?(?:Entity|AggregateRoot)|Entity|AggregateRoot)",
        RegexOptions.None)]
    private static partial Regex EntityClassDeclaration();

    /// <summary>
    /// Matches public property declarations. Group 1: property name.
    /// </summary>
    [GeneratedRegex(@"public\s+\w+[\w<>?,\s]*\s+(\w+)\s*\{", RegexOptions.None)]
    private static partial Regex PublicPropertyDeclaration();

    /// <summary>
    /// Property names that indicate PII content.
    /// </summary>
    [GeneratedRegex(
        @"^(Email|Mail|Phone|PhoneNumber|Mobile|Telephone|Address|Street|City|PostalCode|ZipCode|FirstName|LastName|FullName|DisplayName|UserName|Username|SocialSecurity|NationalId|IdNumber|PassportNumber|DateOfBirth|BirthDate|Salary|Income|BankAccount|Iban|IpAddress|ProfilePicture|Avatar)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex PiiPropertyName();

    /// <summary>
    /// Matches [SensitiveData] or [AuditIgnore] attribute declarations.
    /// </summary>
    [GeneratedRegex(@"\[(AuditIgnore|SensitiveData)(\(.*\))?\]")]
    private static partial Regex AuditAttributePattern();
}
