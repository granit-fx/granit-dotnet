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

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        List<string> factors = ["factor1"];
        AccessRiskScore first = new(0.5, "Medium risk", factors);
        AccessRiskScore second = new(0.5, "Medium risk", factors);

        first.ShouldBe(second);
    }

    [Fact]
    public void Equality_DifferentScore_AreNotEqual()
    {
        AccessRiskScore first = new(0.5, "Risk", []);
        AccessRiskScore second = new(0.7, "Risk", []);

        first.ShouldNotBe(second);
    }
}
