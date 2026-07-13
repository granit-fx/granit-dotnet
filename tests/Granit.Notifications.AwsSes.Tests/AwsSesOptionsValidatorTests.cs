using Granit.Notifications.AwsSes.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.AwsSes.Tests;

public sealed class AwsSesOptionsValidatorTests
{
    private readonly AwsSesOptionsValidator _validator = new();

    [Fact]
    public void Validate_ValidOptions_ReturnsSuccess()
    {
        AwsSesOptions options = new() { Region = "eu-west-1", TimeoutSeconds = 30 };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyRegion_ReturnsFail(string? region)
    {
        AwsSesOptions options = new() { Region = region!, TimeoutSeconds = 30 };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Region");
    }

    [Fact]
    public void Validate_TimeoutLessThanOne_ReturnsFail()
    {
        AwsSesOptions options = new() { Region = "eu-west-1", TimeoutSeconds = 0 };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("TimeoutSeconds");
    }

    [Fact]
    public void Validate_AccessKeyWithoutSecret_ReturnsFail()
    {
        AwsSesOptions options = new()
        {
            Region = "eu-west-1",
            AccessKeyId = "AKID",
            SecretAccessKey = null,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("SecretAccessKey");
    }

    [Fact]
    public void Validate_SecretWithoutAccessKey_ReturnsFail()
    {
        AwsSesOptions options = new()
        {
            Region = "eu-west-1",
            AccessKeyId = null,
            SecretAccessKey = "secret",
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("AccessKeyId");
    }

    [Fact]
    public void Validate_BothAccessKeyAndSecret_ReturnsSuccess()
    {
        AwsSesOptions options = new()
        {
            Region = "eu-west-1",
            AccessKeyId = "AKID",
            SecretAccessKey = "secret",
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_NeitherAccessKeyNorSecret_ReturnsSuccess()
    {
        AwsSesOptions options = new()
        {
            Region = "eu-west-1",
            AccessKeyId = null,
            SecretAccessKey = null,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }
}
