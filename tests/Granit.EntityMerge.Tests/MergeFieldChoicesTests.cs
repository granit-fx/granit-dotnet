using Shouldly;
using Xunit;

namespace Granit.EntityMerge.Tests;

public sealed class MergeFieldChoicesTests
{
    [Fact]
    public void Empty_HasNoChoices() =>
        MergeFieldChoices.Empty.Choices.ShouldBeEmpty();

    [Fact]
    public void ResolveOrDefault_FallsBackToDefault_WhenFieldNotInChoices() =>
        MergeFieldChoices.Empty.ResolveOrDefault("Name", WinnerSide.Survivor)
            .ShouldBe(WinnerSide.Survivor);

    [Fact]
    public void ResolveOrDefault_ReturnsExplicitChoice_WhenPresent()
    {
        MergeFieldChoices choices = MergeFieldChoices.NewBuilder()
            .With("Name", WinnerSide.Loser)
            .Build();

        choices.ResolveOrDefault("Name", WinnerSide.Survivor).ShouldBe(WinnerSide.Loser);
    }

    [Fact]
    public void Builder_PreservesEntries()
    {
        MergeFieldChoices choices = MergeFieldChoices.NewBuilder()
            .With("Name", WinnerSide.Loser)
            .With("TaxStatus", WinnerSide.Survivor)
            .With("Metadata.segment", WinnerSide.Loser)
            .Build();

        choices.Choices.Count.ShouldBe(3);
        choices.Choices["Name"].ShouldBe(WinnerSide.Loser);
        choices.Choices["TaxStatus"].ShouldBe(WinnerSide.Survivor);
        choices.Choices["Metadata.segment"].ShouldBe(WinnerSide.Loser);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Builder_RejectsBlankFieldPath(string blank) =>
        Should.Throw<ArgumentException>(() =>
            MergeFieldChoices.NewBuilder().With(blank, WinnerSide.Survivor));

    [Fact]
    public void Constructor_RejectsNullDictionary() =>
        Should.Throw<ArgumentNullException>(() => new MergeFieldChoices(null!));
}
