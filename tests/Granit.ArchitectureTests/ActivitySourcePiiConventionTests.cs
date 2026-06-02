using Granit.ArchitectureTests.Abstractions.Rules;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates that ActivitySource tag constants do not expose PII-indicative names.
/// Logic lives in <c>Granit.ArchitectureTests.Abstractions</c> so downstream repos can reuse it.
/// </summary>
public sealed class ActivitySourcePiiConventionTests
{
    private static readonly string RepoRoot =
        OpenApiTagConventionRules.FindRepoRoot(typeof(ActivitySourcePiiConventionTests).Assembly);

    /// <summary>
    /// Known exemptions — tag constants confirmed safe because values are always redacted.
    /// Format: <c>"Tags.ConstantName"</c>. Each entry must carry an inline justification.
    /// </summary>
    private static readonly HashSet<string> Exemptions = new(StringComparer.Ordinal)
    {
        // identity.*.user_id tags carry GUIDs (pseudonymous, not direct PII).
        "Tags.UserId",
        // Notification tags — values are redacted at every call site via LogRedaction.*
        "Tags.To",        // acs.email.to — value is EmailDomain(recipient)
        "Tags.Recipient", // acs-sms.recipient / sns-sms.recipient — value is HashPrefix(phone)
    };

    [Fact]
    public void ActivitySource_tag_values_should_not_contain_pii_names() =>
        PiiConventionRules.ActivitySourceTagsShouldNotContainPiiNames(
            Path.Join(RepoRoot, "src"),
            RepoRoot,
            Exemptions);
}
