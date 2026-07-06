using Granit.Features.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.Features.Endpoints.Tests.Dtos;

public sealed class FeatureDtoTests
{
    // -------------------------------------------------------------------------
    // FeatureDefinitionResponse
    // -------------------------------------------------------------------------

    [Fact]
    public void FeatureDefinitionResponse_NullableProperties_CanBeNull()
    {
        FeatureDefinitionResponse response = new(
            "App.Feature", "true", "Toggle", null, null, null, null);

        response.NumericConstraint.ShouldBeNull();
        response.SelectionValues.ShouldBeNull();
        response.DisplayName.ShouldBeNull();
        response.Description.ShouldBeNull();
    }

    [Fact]
    public void FeatureDefinitionResponse_RecordEquality()
    {
        FeatureDefinitionResponse a = new("App.Feature", "true", "Toggle", null, null, null, null);
        FeatureDefinitionResponse b = new("App.Feature", "true", "Toggle", null, null, null, null);

        a.ShouldBe(b);
    }

    // -------------------------------------------------------------------------
    // FeatureNumericConstraintResponse
    // -------------------------------------------------------------------------

    [Fact]
    public void FeatureNumericConstraintResponse_SetsMinAndMax()
    {
        FeatureNumericConstraintResponse constraint = new(1, 10_000);

        constraint.Min.ShouldBe(1);
        constraint.Max.ShouldBe(10_000);
    }

    [Fact]
    public void FeatureNumericConstraintResponse_RecordEquality()
    {
        FeatureNumericConstraintResponse a = new(0, 100);
        FeatureNumericConstraintResponse b = new(0, 100);

        a.ShouldBe(b);
    }

    // -------------------------------------------------------------------------
    // FeatureValueResponse
    // -------------------------------------------------------------------------

    [Fact]
    public void FeatureValueResponse_SetsNameAndValue()
    {
        FeatureValueResponse response = new("App.Feature", "true");

        response.Name.ShouldBe("App.Feature");
        response.Value.ShouldBe("true");
    }

    [Fact]
    public void FeatureValueResponse_RecordEquality()
    {
        FeatureValueResponse a = new("App.Feature", "true");
        FeatureValueResponse b = new("App.Feature", "true");

        a.ShouldBe(b);
    }

    [Fact]
    public void FeatureValueResponse_DifferentValues_NotEqual()
    {
        FeatureValueResponse a = new("App.Feature", "true");
        FeatureValueResponse b = new("App.Feature", "false");

        a.ShouldNotBe(b);
    }

    // -------------------------------------------------------------------------
    // FeatureGroupResponse
    // -------------------------------------------------------------------------

    [Fact]
    public void FeatureGroupResponse_SetsProperties()
    {
        List<FeatureDefinitionResponse> features =
        [
            new("App.Feature", "true", "Toggle", null, null, null, null),
        ];

        FeatureGroupResponse group = new("App", "Application", features);

        group.Name.ShouldBe("App");
        group.DisplayName.ShouldBe("Application");
        group.Features.Count.ShouldBe(1);
    }

    [Fact]
    public void FeatureGroupResponse_NullDisplayName_IsAllowed()
    {
        FeatureGroupResponse group = new("App", null, []);

        group.DisplayName.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // SetFeatureOverrideRequest
    // -------------------------------------------------------------------------

    [Fact]
    public void SetFeatureOverrideRequest_SetsValue()
    {
        SetFeatureOverrideRequest request = new("true");

        request.Value.ShouldBe("true");
    }

    [Fact]
    public void SetFeatureOverrideRequest_RecordEquality()
    {
        SetFeatureOverrideRequest a = new("true");
        SetFeatureOverrideRequest b = new("true");

        a.ShouldBe(b);
    }
}
