using Granit.DataExchange.Import.Mapping;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import.Mapping;

public sealed class ImportColumnMappingTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        ImportColumnMapping mapping = new("Email", "Email", MappingConfidence.Exact);

        mapping.SourceColumn.ShouldBe("Email");
        mapping.TargetProperty.ShouldBe("Email");
        mapping.Confidence.ShouldBe(MappingConfidence.Exact);
    }

    [Fact]
    public void Constructor_WithNullTarget()
    {
        ImportColumnMapping mapping = new("UnknownColumn", null, MappingConfidence.Manual);

        mapping.TargetProperty.ShouldBeNull();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        ImportColumnMapping a = new("Col", "Prop", MappingConfidence.Fuzzy);
        ImportColumnMapping b = new("Col", "Prop", MappingConfidence.Fuzzy);

        a.ShouldBe(b);
    }

    [Theory]
    [InlineData(MappingConfidence.Manual, 0)]
    [InlineData(MappingConfidence.Saved, 1)]
    [InlineData(MappingConfidence.Exact, 2)]
    [InlineData(MappingConfidence.Fuzzy, 3)]
    [InlineData(MappingConfidence.Semantic, 4)]
    public void MappingConfidence_HasExpectedIntValues(MappingConfidence confidence, int expected) =>
        ((int)confidence).ShouldBe(expected);

    [Fact]
    public void MappingConfidence_Manual_HasHighestPriority()
    {
        // Lower values = higher confidence
        ((int)MappingConfidence.Manual).ShouldBeLessThan((int)MappingConfidence.Saved);
        ((int)MappingConfidence.Saved).ShouldBeLessThan((int)MappingConfidence.Exact);
        ((int)MappingConfidence.Exact).ShouldBeLessThan((int)MappingConfidence.Fuzzy);
        ((int)MappingConfidence.Fuzzy).ShouldBeLessThan((int)MappingConfidence.Semantic);
    }

    [Fact]
    public void MappingConfidence_AllValues_CoveredByEnum()
    {
        MappingConfidence[] values = Enum.GetValues<MappingConfidence>();
        values.Length.ShouldBe(5);
    }
}
