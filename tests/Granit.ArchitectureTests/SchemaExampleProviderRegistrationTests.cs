using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Every <c>ISchemaExampleProvider</c> implementation must be DI-registered by its own
/// project. The 2026-07 Http* audit found a provider that was implemented and unit-tested
/// but never registered — its OpenAPI examples never surfaced, and the green test hid it.
/// </summary>
public sealed partial class SchemaExampleProviderRegistrationTests
{
    private static readonly string RepoRoot = ArchitectureTestHelpers.FindRepoRoot();

    /// <summary>Ratchet list — nothing may be added without a linked issue.</summary>
    private static readonly Dictionary<string, string> Exemptions = new(StringComparer.Ordinal)
    {
        // Pre-existing repo-wide debt, enumerated at introduction — tracked by #3010.
        ["LocalizationSchemaExampleProvider"] = "#3010 — implemented but never DI-registered",
        ["ApiKeysSchemaExampleProvider"] = "#3010 — implemented but never DI-registered",
        ["NotificationsSchemaExampleProvider"] = "#3010 — implemented but never DI-registered",
        ["WebhooksSchemaExampleProvider"] = "#3010 — implemented but never DI-registered",
        ["IdentitySchemaExampleProvider"] = "#3010 — implemented but never DI-registered",
        ["AuthorizationSchemaExampleProvider"] = "#3010 — implemented but never DI-registered",
        ["DataExchangeSchemaExampleProvider"] = "#3010 — implemented but never DI-registered",
        ["SettingsSchemaExampleProvider"] = "#3010 — implemented but never DI-registered",
        ["PrivacySchemaExampleProvider"] = "#3010 — implemented but never DI-registered",
        ["TemplatingSchemaExampleProvider"] = "#3010 — implemented but never DI-registered",
        ["WorkflowSchemaExampleProvider"] = "#3010 — implemented but never DI-registered",
        ["TimelineSchemaExampleProvider"] = "#3010 — implemented but never DI-registered",
        ["AISchemaExampleProvider"] = "#3010 — implemented but never DI-registered",
    };

    [Fact]
    public void Every_schema_example_provider_is_registered_in_its_project()
    {
        string srcDir = Path.Join(RepoRoot, "src");
        List<string> violations = [];

        foreach (string csFile in ArchitectureTestHelpers.EnumerateSourceFiles(srcDir))
        {
            string content = File.ReadAllText(csFile);
            Match match = ProviderImplementation().Match(content);
            if (!match.Success)
            {
                continue;
            }

            string typeName = match.Groups[1].Value;
            if (Exemptions.ContainsKey(typeName))
            {
                continue;
            }

            string projectDir = ArchitectureTestHelpers.ProjectDirOf(srcDir, csFile);
            bool registered = ArchitectureTestHelpers.EnumerateSourceFiles(projectDir)
                .Select(File.ReadAllText)
                .Any(source =>
                    source.Contains($"ISchemaExampleProvider, {typeName}>", StringComparison.Ordinal));

            if (!registered)
            {
                violations.Add($"{Path.GetRelativePath(RepoRoot, csFile)} ({typeName})");
            }
        }

        violations.ShouldBeEmpty(
            "Every ISchemaExampleProvider implementation must be registered in its own module "
            + "(TryAddEnumerable(ServiceDescriptor.Singleton<ISchemaExampleProvider, X>()) — see "
            + "GranitAuditingEndpointsModule). An unregistered provider never surfaces its "
            + "OpenAPI examples. Violators: " + string.Join("; ", violations));
    }

    /// <summary>
    /// Matches <c>class Xxx : ... ISchemaExampleProvider</c> implementations — the interface
    /// must appear in the base list (after <c>:</c>, outside any parameter list), so a type
    /// merely injecting <c>IEnumerable&lt;ISchemaExampleProvider&gt;</c> through a primary
    /// constructor does not match.
    /// </summary>
    [GeneratedRegex(@"class\s+([A-Za-z0-9_]+)(?:\s*\([^)]*\))?\s*:\s*[^{(]*\bISchemaExampleProvider\b")]
    private static partial Regex ProviderImplementation();
}
