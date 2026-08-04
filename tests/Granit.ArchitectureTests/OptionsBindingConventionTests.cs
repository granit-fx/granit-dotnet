using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Every options class that declares a <c>const string SectionName</c> must actually be
/// bound to that section somewhere in <c>src/</c> (<c>BindConfiguration(...)</c> or an
/// equivalent <c>GetSection(...)</c> binding). The 2026-07 Http* audit found four
/// "decorative" SectionNames — declared, unit-tested, and never bound — meaning
/// <c>appsettings.json</c> configuration was silently ignored.
/// </summary>
public sealed partial class OptionsBindingConventionTests
{
    private static readonly string RepoRoot = ArchitectureTestHelpers.FindRepoRoot();

    /// <summary>
    /// Pre-existing violations outside the Http* family, enumerated when this test was
    /// introduced (ratchet: nothing may be added here without a linked issue explaining
    /// why the section is deliberately delegate-configured or bound by the host).
    /// Key: options type name. Value: rationale.
    /// </summary>
    private static readonly Dictionary<string, string> Exemptions = new(StringComparer.Ordinal)
    {
        // Pre-existing repo-wide debt, enumerated at introduction — tracked by #3010.
        ["AIEndpointsOptions"] = "#3010 — pre-existing unbound section 'AI:Endpoints'",
        ["AIQuotaOptions"] = "#3010 — pre-existing unbound section 'AI:Quota'",
        ["ApiKeysEndpointsOptions"] = "#3010 — pre-existing unbound section 'Authentication:ApiKeys:Endpoints'",
        ["AuthorizationEndpointsOptions"] = "#3010 — pre-existing unbound section 'Authorization:Endpoints'",
        ["AwsSesOptions"] = "#3010 — pre-existing unbound section 'Notifications:AwsSes'",
        ["AzureNotificationHubsOptions"] = "#3010 — pre-existing unbound section 'Notifications:AzureNotificationHubs'",
        ["BrevoOptions"] = "#3010 — pre-existing unbound section 'Notifications:Brevo'",
        ["DPoPValidationOptions"] = "#3010 — pre-existing unbound section 'Authentication:DPoP'",
        ["DataExchangeEndpointsOptions"] = "#3010 — pre-existing unbound section 'DataExchange:Endpoints'",
        ["DataLookupEndpointsOptions"] = "#3010 — pre-existing unbound section 'DataLookup:Endpoints'",
        ["DiagnosticsEndpointsOptions"] = "#3010 — pre-existing unbound section 'Diagnostics:Endpoints'",
        ["FeaturesEndpointsOptions"] = "#3010 — pre-existing unbound section 'Features:Endpoints'",
        ["GeocodingEndpointsOptions"] = "#3010 — pre-existing unbound section 'Geocoding:Endpoints'",
        ["GoogleFcmOptions"] = "#3010 — pre-existing unbound section 'Notifications:GoogleFcm'",
        ["HostnamesEndpointsOptions"] = "#3010 — pre-existing unbound section 'Hostnames:Endpoints'",
        ["IdentityEndpointsOptions"] = "#3010 — pre-existing unbound section 'Identity:Endpoints'",
        ["IdentityNotificationOptions"] = "#3010 — pre-existing unbound section 'Identity:Local:Notifications'",
        ["IdentityProviderEndpointsOptions"] = "#3010 — pre-existing unbound section 'Identity:Endpoints:Provider'",
        ["IdentityUserSessionNotificationOptions"] = "#3010 — pre-existing unbound section 'Identity:Notifications:UserSessions'",
        ["LocalizationEndpointsOptions"] = "#3010 — pre-existing unbound section 'Localization:Endpoints'",
        ["MultiTenancyEndpointsOptions"] = "#3010 — pre-existing unbound section 'MultiTenancy:Endpoints'",
        ["OpenIddictEndpointsOptions"] = "#3010 — pre-existing unbound section 'OpenIddict:Endpoints'",
        ["OpenIddictServerEndpointsOptions"] = "#3010 — pre-existing unbound section 'OpenIddict:Server:Endpoints'",
        ["PresenceEndpointsOptions"] = "#3010 — pre-existing unbound section 'Presence:Endpoints'",
        ["PrivacyEndpointsOptions"] = "#3010 — pre-existing unbound section 'Privacy:Endpoints'",
        ["QueryEndpointOptions"] = "#3010 — pre-existing unbound section 'QueryEngine:Endpoint'",
        ["ReEncryptionOptions"] = "#3010 — pre-existing unbound section 'Vault:ReEncryption'",
        ["RoleEndpointsOptions"] = "#3010 — pre-existing unbound section 'Identity:Local:Endpoints:Role'",
        ["ScalewayEmailOptions"] = "#3010 — pre-existing unbound section 'Notifications:Scaleway'",
        ["SchedulingEndpointsOptions"] = "#3010 — pre-existing unbound section 'Scheduling:Endpoints'",
        ["SendGridEmailOptions"] = "#3010 — pre-existing unbound section 'Notifications:SendGrid'",
        ["SettingsEndpointsOptions"] = "#3010 — pre-existing unbound section 'Settings:Endpoints'",
        ["SettingsOptions"] = "#3010 — pre-existing unbound section 'Settings'",
        ["SmtpOptions"] = "#3010 — pre-existing unbound section 'Notifications:Smtp'",
        ["SseRedisBackplaneOptions"] = "#3010 — pre-existing unbound section 'Notifications:Sse:StackExchangeRedis'",
        ["TemplatingEndpointsOptions"] = "#3010 — pre-existing unbound section 'Templating:Endpoints'",
        ["TimelineEndpointsOptions"] = "#3010 — pre-existing unbound section 'Timeline:Endpoints'",
        ["TokenManagementOptions"] = "#3010 — pre-existing unbound section 'Oidc:TokenManagement'",
        ["TwilioOptions"] = "#3010 — pre-existing unbound section 'Notifications:Twilio'",
        ["UserSessionsEndpointsOptions"] = "#3010 — pre-existing unbound section 'Identity:Endpoints:UserSessions'",
        ["ValidationEndpointsOptions"] = "#3010 — pre-existing unbound section 'Validation:Endpoints'",
        ["WebhooksEndpointsOptions"] = "#3010 — pre-existing unbound section 'Webhooks:Endpoints'",
        ["WorkflowEndpointsOptions"] = "#3010 — pre-existing unbound section 'Workflow:Endpoints'",
        ["ZulipBotOptions"] = "#3010 — pre-existing unbound section 'Notifications:Zulip:Bot'",
        ["ZulipChannelOptions"] = "#3010 — pre-existing unbound section 'Notifications:Zulip'",
    };

    [Fact]
    public void Every_SectionName_is_bound_somewhere_in_src()
    {
        string srcDir = Path.Join(RepoRoot, "src");
        List<string> violations = [];

        // Pass 1 — collect (file, typeName, sectionValue) for every SectionName const.
        List<(string File, string TypeName, string Value)> declarations = [];
        foreach (string csFile in ArchitectureTestHelpers.EnumerateSourceFiles(srcDir))
        {
            string content = File.ReadAllText(csFile);
            Match constMatch = SectionNameConstant().Match(content);
            if (!constMatch.Success)
            {
                continue;
            }

            Match typeMatch = TypeDeclaration().Match(content);
            string typeName = typeMatch.Success ? typeMatch.Groups[1].Value : Path.GetFileNameWithoutExtension(csFile);
            declarations.Add((csFile, typeName, constMatch.Groups[1].Value));
        }

        // Pass 2 — for each declaration, look for a binding site anywhere in src.
        // Accepted shapes: BindConfiguration(<Type>.SectionName), BindConfiguration("literal"),
        // GetSection(<Type>.SectionName), GetSection("literal"), GetRequiredSection(...).
        string allSources = string.Join('\n',
            ArchitectureTestHelpers.EnumerateSourceFiles(srcDir).Select(File.ReadAllText));

        foreach ((string file, string typeName, string value) in declarations)
        {
            if (Exemptions.ContainsKey(typeName))
            {
                continue;
            }

            bool bound =
                allSources.Contains($"BindConfiguration({typeName}.SectionName", StringComparison.Ordinal)
                || allSources.Contains($"BindConfiguration(\"{value}\"", StringComparison.Ordinal)
                || allSources.Contains($"BindConfiguration($\"{{{typeName}.SectionName}}", StringComparison.Ordinal)
                || allSources.Contains($"GetSection({typeName}.SectionName", StringComparison.Ordinal)
                || allSources.Contains($"GetSection(\"{value}\"", StringComparison.Ordinal)
                || allSources.Contains($"GetSection($\"{{{typeName}.SectionName}}", StringComparison.Ordinal)
                || allSources.Contains($"GetRequiredSection({typeName}.SectionName", StringComparison.Ordinal)
                || allSources.Contains($"GetRequiredSection(\"{value}\"", StringComparison.Ordinal);

            if (!bound)
            {
                violations.Add($"{Path.GetRelativePath(RepoRoot, file)} ({typeName}, \"{value}\")");
            }
        }

        violations.ShouldBeEmpty(
            "Every 'const string SectionName' must be bound via BindConfiguration/GetSection "
            + "somewhere in src/ — a declared-but-never-bound section silently ignores "
            + "appsettings.json. Bind it (see AuditingEndpointsOptions for the pattern) or add "
            + "a justified exemption. Violators: " + string.Join("; ", violations));
    }

    [GeneratedRegex(@"public\s+const\s+string\s+SectionName\s*=\s*""([^""]+)""")]
    private static partial Regex SectionNameConstant();

    [GeneratedRegex(@"(?:class|record)\s+([A-Za-z0-9_]+Options)\b")]
    private static partial Regex TypeDeclaration();
}
