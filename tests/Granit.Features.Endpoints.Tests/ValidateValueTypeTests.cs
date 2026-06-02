using Granit.Features.Definitions;
using Granit.Features.Endpoints.Internal;
using Granit.Features.Exceptions;
using Granit.Features.ValueTypes;
using Shouldly;
using Xunit;

namespace Granit.Features.Endpoints.Tests;

public sealed class ValidateValueTypeTests
{
    // -------------------------------------------------------------------------
    // Toggle validation
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    [InlineData("false")]
    [InlineData("False")]
    [InlineData("FALSE")]
    public void Toggle_ValidBooleanValues_DoNotThrow(string value)
    {
        FeatureDefinition definition = new("App.Feature", "false", FeatureValueType.Toggle);

        Action act = () => FeaturesResponseMapper.ValidateValueType(definition, value);

        Should.NotThrow(act);
    }

    [Theory]
    [InlineData("maybe")]
    [InlineData("1")]
    [InlineData("0")]
    [InlineData("yes")]
    [InlineData("no")]
    [InlineData("")]
    public void Toggle_InvalidValues_ThrowsFeatureValueValidationException(string value)
    {
        FeatureDefinition definition = new("App.Feature", "false", FeatureValueType.Toggle);

        Action act = () => FeaturesResponseMapper.ValidateValueType(definition, value);

        FeatureValueValidationException ex = Should.Throw<FeatureValueValidationException>(act);
        ex.FeatureName.ShouldBe("App.Feature");
        ex.InvalidValue.ShouldBe(value);
    }

    // -------------------------------------------------------------------------
    // Numeric validation — with constraint
    // -------------------------------------------------------------------------

    [Fact]
    public void Numeric_WithConstraint_ValidValue_DoesNotThrow()
    {
        FeatureDefinition definition = new("App.MaxUsers", "50", FeatureValueType.Numeric)
        {
            NumericConstraint = new NumericConstraint(1, 10_000),
        };

        Action act = () => FeaturesResponseMapper.ValidateValueType(definition, "500");

        Should.NotThrow(act);
    }

    [Fact]
    public void Numeric_WithConstraint_OutOfRange_Throws()
    {
        FeatureDefinition definition = new("App.MaxUsers", "50", FeatureValueType.Numeric)
        {
            NumericConstraint = new NumericConstraint(1, 100),
        };

        Action act = () => FeaturesResponseMapper.ValidateValueType(definition, "999");

        Should.Throw<FeatureValueValidationException>(act);
    }

    [Fact]
    public void Numeric_WithConstraint_NonInteger_Throws()
    {
        FeatureDefinition definition = new("App.MaxUsers", "50", FeatureValueType.Numeric)
        {
            NumericConstraint = new NumericConstraint(1, 100),
        };

        Action act = () => FeaturesResponseMapper.ValidateValueType(definition, "abc");

        Should.Throw<FeatureValueValidationException>(act);
    }

    // -------------------------------------------------------------------------
    // Numeric validation — without constraint
    // -------------------------------------------------------------------------

    [Fact]
    public void Numeric_WithoutConstraint_ValidInteger_DoesNotThrow()
    {
        FeatureDefinition definition = new("App.MaxUsers", "50", FeatureValueType.Numeric);

        Action act = () => FeaturesResponseMapper.ValidateValueType(definition, "999");

        Should.NotThrow(act);
    }

    [Fact]
    public void Numeric_WithoutConstraint_NonInteger_Throws()
    {
        FeatureDefinition definition = new("App.MaxUsers", "50", FeatureValueType.Numeric);

        Action act = () => FeaturesResponseMapper.ValidateValueType(definition, "abc");

        FeatureValueValidationException ex = Should.Throw<FeatureValueValidationException>(act);
        ex.FeatureName.ShouldBe("App.MaxUsers");
        ex.InvalidValue.ShouldBe("abc");
    }

    // -------------------------------------------------------------------------
    // Selection validation
    // -------------------------------------------------------------------------

    [Fact]
    public void Selection_WithSelectionValues_ValidValue_DoesNotThrow()
    {
        FeatureDefinition definition = new("App.Theme", "light", FeatureValueType.Selection)
        {
            SelectionValues = new SelectionValues(["light", "dark", "auto"]),
        };

        Action act = () => FeaturesResponseMapper.ValidateValueType(definition, "dark");

        Should.NotThrow(act);
    }

    [Fact]
    public void Selection_WithSelectionValues_InvalidValue_Throws()
    {
        FeatureDefinition definition = new("App.Theme", "light", FeatureValueType.Selection)
        {
            SelectionValues = new SelectionValues(["light", "dark", "auto"]),
        };

        Action act = () => FeaturesResponseMapper.ValidateValueType(definition, "neon");

        Should.Throw<FeatureValueValidationException>(act);
    }

    [Fact]
    public void Selection_WithoutSelectionValues_AnyValue_DoesNotThrow()
    {
        FeatureDefinition definition = new("App.Theme", "light", FeatureValueType.Selection);

        Action act = () => FeaturesResponseMapper.ValidateValueType(definition, "anything");

        Should.NotThrow(act);
    }

    // -------------------------------------------------------------------------
    // FeatureNotFound helper
    // -------------------------------------------------------------------------

    [Fact]
    public void FeatureNotFound_Returns_ProblemHttpResult_With404()
    {
        Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult result =
            FeaturesResponseMapper.FeatureNotFound();

        result.StatusCode.ShouldBe(404);
    }
}
