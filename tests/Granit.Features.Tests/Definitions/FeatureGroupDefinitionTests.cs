using Granit.Features.Definitions;
using Granit.Features.ValueTypes;
using Shouldly;
using Xunit;

namespace Granit.Features.Tests.Definitions;

public sealed class FeatureGroupDefinitionTests
{
    private static FeatureGroupDefinition MakeGroup(string name = "TestApp", string? displayName = null)
    {
        // FeatureGroupDefinition constructor is internal — use IFeatureDefinitionContext
        // AddGroup returns the group directly, so capture it via a closure.
        FeatureGroupDefinition? group = null;
        FakeContextProvider provider = new(ctx => group = ctx.AddGroup(name, displayName));
        FeatureDefinitionContext context = new();
        provider.Define(context);
        return group!;
    }

    private sealed class FakeContextProvider(Action<IFeatureDefinitionContext> define)
        : IFeatureDefinitionProvider
    {
        public void Define(IFeatureDefinitionContext context) => define(context);
    }

    [Fact]
    public void AddToggle_DefaultFalse_AddsDefinitionWithFalseDefault()
    {
        FeatureGroupDefinition group = MakeGroup();
        group.AddToggle("Acme.VideoConference");

        FeatureDefinition feature = group.Features.ShouldHaveSingleItem();
        feature.Name.ShouldBe("Acme.VideoConference");
        feature.DefaultValue.ShouldBe("false");
        feature.ValueType.ShouldBe(FeatureValueType.Toggle);
    }

    [Fact]
    public void AddToggle_DefaultTrue_AddsDefinitionWithTrueDefault()
    {
        FeatureGroupDefinition group = MakeGroup();
        group.AddToggle("Acme.VideoConference", defaultValue: true, displayName: "Video");

        FeatureDefinition feature = group.Features.Single();
        feature.DefaultValue.ShouldBe("true");
        feature.DisplayName.ShouldBe("Video");
    }

    [Fact]
    public void AddNumeric_AddsDefinitionWithNumericConstraint()
    {
        FeatureGroupDefinition group = MakeGroup();
        group.AddNumeric("Acme.MaxUsers", defaultValue: 200, min: 0, max: 10000);

        FeatureDefinition feature = group.Features.Single();
        feature.DefaultValue.ShouldBe("200");
        feature.ValueType.ShouldBe(FeatureValueType.Numeric);
        feature.NumericConstraint.ShouldNotBeNull();
        feature.NumericConstraint!.Min.ShouldBe(0);
        feature.NumericConstraint.Max.ShouldBe(10000);
    }

    [Fact]
    public void AddSelection_AddsDefinitionWithSelectionValues()
    {
        FeatureGroupDefinition group = MakeGroup();
        group.AddSelection(
            "Acme.StorageTier",
            defaultValue: "standard",
            allowedValues: ["standard", "premium"],
            displayName: "Storage");

        FeatureDefinition feature = group.Features.Single();
        feature.DefaultValue.ShouldBe("standard");
        feature.ValueType.ShouldBe(FeatureValueType.Selection);
        feature.DisplayName.ShouldBe("Storage");
        feature.SelectionValues.ShouldNotBeNull();
        feature.SelectionValues!.AllowedValues.ShouldBe(["standard", "premium"]);
    }

    [Fact]
    public void AddToggle_IsChainable_ReturnsGroup()
    {
        FeatureGroupDefinition group = MakeGroup();

        FeatureGroupDefinition returned = group
            .AddToggle("Acme.FeatureA")
            .AddToggle("Acme.FeatureB");

        returned.ShouldBeSameAs(group);
        group.Features.Count.ShouldBe(2);
    }

    [Fact]
    public void Group_DisplayName_IsPreserved()
    {
        FeatureGroupDefinition group = MakeGroup("TestApp", "Acme Platform");

        group.Name.ShouldBe("TestApp");
        group.DisplayName.ShouldBe("Acme Platform");
    }

    // -------------------------------------------------------------------------
    // Constructor validation — null/whitespace name
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullOrWhitespaceName_Throws(string? name)
    {
        // FeatureGroupDefinition constructor is internal — exercise via context.AddGroup
        FeatureDefinitionContext context = new();

        Action act = () => context.AddGroup(name!);

        Should.Throw<ArgumentException>(act);
    }

    [Fact]
    public void Constructor_DisplayName_NullIsAllowed()
    {
        FeatureGroupDefinition group = MakeGroup("Test", displayName: null);

        group.DisplayName.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Features — starts empty
    // -------------------------------------------------------------------------

    [Fact]
    public void Features_InitiallyEmpty()
    {
        FeatureGroupDefinition group = MakeGroup();

        group.Features.ShouldBeEmpty();
    }

    // -------------------------------------------------------------------------
    // AddNumeric — chaining
    // -------------------------------------------------------------------------

    [Fact]
    public void AddNumeric_IsChainable_ReturnsGroup()
    {
        FeatureGroupDefinition group = MakeGroup();

        FeatureGroupDefinition returned = group
            .AddNumeric("App.MaxA", 10)
            .AddNumeric("App.MaxB", 20);

        returned.ShouldBeSameAs(group);
        group.Features.Count.ShouldBe(2);
    }

    // -------------------------------------------------------------------------
    // AddSelection — chaining
    // -------------------------------------------------------------------------

    [Fact]
    public void AddSelection_IsChainable_ReturnsGroup()
    {
        FeatureGroupDefinition group = MakeGroup();

        FeatureGroupDefinition returned = group
            .AddSelection("App.TierA", "a", ["a", "b"])
            .AddSelection("App.TierB", "x", ["x", "y"]);

        returned.ShouldBeSameAs(group);
        group.Features.Count.ShouldBe(2);
    }

    // -------------------------------------------------------------------------
    // Mixed chaining
    // -------------------------------------------------------------------------

    [Fact]
    public void MixedChaining_AddsAllFeatureTypes()
    {
        FeatureGroupDefinition group = MakeGroup();

        group.AddToggle("App.Toggle1")
             .AddNumeric("App.Num1", 100)
             .AddSelection("App.Sel1", "a", ["a", "b"]);

        group.Features.Count.ShouldBe(3);
    }

    // -------------------------------------------------------------------------
    // AddNumeric — default min/max
    // -------------------------------------------------------------------------

    [Fact]
    public void AddNumeric_DefaultMinIsZero_DefaultMaxIsLongMaxValue()
    {
        FeatureGroupDefinition group = MakeGroup();
        group.AddNumeric("App.Count", 50);

        FeatureDefinition feature = group.Features.Single();
        feature.NumericConstraint.ShouldNotBeNull();
        feature.NumericConstraint!.Min.ShouldBe(0);
        feature.NumericConstraint.Max.ShouldBe(long.MaxValue);
    }

    // -------------------------------------------------------------------------
    // AddToggle — displayName is null by default
    // -------------------------------------------------------------------------

    [Fact]
    public void AddToggle_DisplayName_NullByDefault()
    {
        FeatureGroupDefinition group = MakeGroup();
        group.AddToggle("App.Feature");

        FeatureDefinition feature = group.Features.Single();
        feature.DisplayName.ShouldBeNull();
    }
}
