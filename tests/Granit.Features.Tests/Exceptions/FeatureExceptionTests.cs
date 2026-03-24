using Granit.Exceptions;
using Granit.Features.Exceptions;
using Shouldly;
using Xunit;

namespace Granit.Features.Tests.Exceptions;

public sealed class FeatureExceptionTests
{
    // -------------------------------------------------------------------------
    // FeatureNotFoundException
    // -------------------------------------------------------------------------

    [Fact]
    public void FeatureNotFoundException_SetsFeatureName()
    {
        FeatureNotFoundException ex = new("App.Missing");

        ex.FeatureName.ShouldBe("App.Missing");
    }

    [Fact]
    public void FeatureNotFoundException_MessageContainsFeatureName()
    {
        FeatureNotFoundException ex = new("App.Missing");

        ex.Message.ShouldContain("App.Missing");
    }

    [Fact]
    public void FeatureNotFoundException_InheritsFromException()
    {
        FeatureNotFoundException ex = new("App.Missing");

        ex.ShouldBeAssignableTo<Exception>();
    }

    // -------------------------------------------------------------------------
    // FeatureNotEnabledException
    // -------------------------------------------------------------------------

    [Fact]
    public void FeatureNotEnabledException_SetsFeatureName()
    {
        FeatureNotEnabledException ex = new("App.Video");

        ex.FeatureName.ShouldBe("App.Video");
    }

    [Fact]
    public void FeatureNotEnabledException_ErrorCode_Is_FeaturesNotEnabled()
    {
        FeatureNotEnabledException ex = new("App.Video");

        ex.ErrorCode.ShouldBe("Features:NotEnabled");
    }

    [Fact]
    public void FeatureNotEnabledException_MessageContainsFeatureName()
    {
        FeatureNotEnabledException ex = new("App.Video");

        ex.Message.ShouldContain("App.Video");
    }

    [Fact]
    public void FeatureNotEnabledException_InheritsFromForbiddenException()
    {
        FeatureNotEnabledException ex = new("App.Video");

        ex.ShouldBeAssignableTo<ForbiddenException>();
    }

    [Fact]
    public void FeatureNotEnabledException_ImplementsIHasErrorCode()
    {
        FeatureNotEnabledException ex = new("App.Video");

        ex.ShouldBeAssignableTo<IHasErrorCode>();
    }

    // -------------------------------------------------------------------------
    // FeatureLimitExceededException
    // -------------------------------------------------------------------------

    [Fact]
    public void FeatureLimitExceededException_SetsAllProperties()
    {
        FeatureLimitExceededException ex = new("App.MaxPatients", 50, 50);

        ex.FeatureName.ShouldBe("App.MaxPatients");
        ex.Current.ShouldBe(50);
        ex.Limit.ShouldBe(50);
    }

    [Fact]
    public void FeatureLimitExceededException_ErrorCode_Is_FeaturesLimitExceeded()
    {
        FeatureLimitExceededException ex = new("App.MaxPatients", 10, 5);

        ex.ErrorCode.ShouldBe("Features:LimitExceeded");
    }

    [Fact]
    public void FeatureLimitExceededException_MessageContainsContextInfo()
    {
        FeatureLimitExceededException ex = new("App.MaxPatients", 50, 50);

        ex.Message.ShouldContain("App.MaxPatients");
        ex.Message.ShouldContain("50/50");
    }

    [Fact]
    public void FeatureLimitExceededException_InheritsFromForbiddenException()
    {
        FeatureLimitExceededException ex = new("App.MaxPatients", 1, 1);

        ex.ShouldBeAssignableTo<ForbiddenException>();
    }

    // -------------------------------------------------------------------------
    // FeatureValueValidationException
    // -------------------------------------------------------------------------

    [Fact]
    public void FeatureValueValidationException_SetsAllProperties()
    {
        FeatureValueValidationException ex = new("App.MaxPatients", "abc", "must be integer");

        ex.FeatureName.ShouldBe("App.MaxPatients");
        ex.InvalidValue.ShouldBe("abc");
    }

    [Fact]
    public void FeatureValueValidationException_MessageContainsContext()
    {
        FeatureValueValidationException ex = new("App.MaxPatients", "abc", "must be integer");

        ex.Message.ShouldContain("App.MaxPatients");
        ex.Message.ShouldContain("abc");
        ex.Message.ShouldContain("must be integer");
    }

    [Fact]
    public void FeatureValueValidationException_InheritsFromBusinessException()
    {
        FeatureValueValidationException ex = new("App.Plan", "ultimate", "not in allowed list");

        ex.ShouldBeAssignableTo<BusinessException>();
    }
}
