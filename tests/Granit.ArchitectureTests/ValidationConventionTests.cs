using Granit.ArchitectureTests.Abstractions.Rules;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates validation conventions for Granit endpoint packages.
/// Logic lives in <c>Granit.ArchitectureTests.Abstractions</c> so downstream repos
/// (granit-business, Showcase, …) can run the same checks against their own <c>src/</c>.
/// </summary>
public sealed class ValidationConventionTests
{
    private static readonly string RepoRoot =
        Granit.ArchitectureTests.Abstractions.Rules.OpenApiTagConventionRules
            .FindRepoRoot(typeof(ValidationConventionTests).Assembly);

    /// <summary>
    /// Known exemptions from the validator requirement.
    /// Each entry must carry an inline justification.
    /// </summary>
    private static readonly HashSet<string> ValidatorExemptions = new(StringComparer.Ordinal)
    {
        // Request with only optional fields validated in handler via IFeatureDefinitionStore
        "TemplatePreviewRequest",
        // Nested sub-type validated via ChildRules in AIChatRequestValidator
        "AIChatMessageRequest",
        // Nested sub-type validated via ChildRules + MeterEventRules
        "MeterEventRequest",
        // Nested sub-type validated via ChildRules in SendMessageRequestValidator
        "MentionRequest",
        // Nested sub-type validated via ChildRules in SendMessageRequestValidator
        "AttachmentRequest",
    };

    [Fact]
    public void Request_types_in_Endpoints_should_have_validators() =>
        ValidationConventionRules.RequestTypesShouldHaveValidators(
            typeof(ValidationConventionTests).Assembly,
            "Granit.*.Endpoints.dll",
            ValidatorExemptions);

    [Fact]
    public void Top_level_route_groups_should_use_MapGranitGroup() =>
        ValidationConventionRules.EndpointsShouldUseMapGranitGroup(
            Path.Join(RepoRoot, "src"),
            RepoRoot);

    [Fact]
    public void Validators_should_not_use_hardcoded_WithMessage() =>
        SourceCodeAntiPatternRules.NoValidatorFileShouldUseHardcodedWithMessage(
            Path.Join(RepoRoot, "src"),
            RepoRoot);
}
