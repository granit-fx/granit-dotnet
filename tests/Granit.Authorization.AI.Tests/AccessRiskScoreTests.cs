using Shouldly;

namespace Granit.Authorization.AI.Tests;

public sealed class AccessRiskScoreTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        IReadOnlyList<string> riskFactors = ["off-hours access", "elevated permission"];

        AccessRiskScore score = new(0.85, "Suspicious access pattern", riskFactors);

        score.Score.ShouldBe(0.85);
        score.Reasoning.ShouldBe("Suspicious access pattern");
        score.RiskFactors.Count.ShouldBe(2);
        score.RiskFactors.ShouldContain("off-hours access");
    }

    [Fact]
    public void Constructor_ZeroScore_WithEmptyRiskFactors()
    {
        AccessRiskScore score = new(0.0, "No risk detected", []);

        score.Score.ShouldBe(0.0);
        score.RiskFactors.ShouldBeEmpty();
    }
}
