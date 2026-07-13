using Granit.Notifications.AwsSns.MobilePush.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.AwsSns.MobilePush.Tests;

public sealed class AwsSnsMobilePushOptionsValidatorTests
{
    private readonly AwsSnsMobilePushOptionsValidator _validator = new();

    [Fact]
    public void Validate_ValidOptions_ReturnsSuccess()
    {
        AwsSnsMobilePushOptions options = new()
        {
            Region = "eu-west-1",
            PlatformApplicationArn = "arn:aws:sns:eu-west-1:123456789:app/GCM/MyApp",
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyRegion_ReturnsFail(string? region)
    {
        AwsSnsMobilePushOptions options = new()
        {
            Region = region!,
            PlatformApplicationArn = "arn:aws:sns:eu-west-1:123456789:app/GCM/MyApp",
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Region");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyPlatformApplicationArn_ReturnsFail(string? arn)
    {
        AwsSnsMobilePushOptions options = new()
        {
            Region = "eu-west-1",
            PlatformApplicationArn = arn!,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("PlatformApplicationArn");
    }

    [Fact]
    public void Validate_AccessKeyWithoutSecret_ReturnsFail()
    {
        AwsSnsMobilePushOptions options = new()
        {
            Region = "eu-west-1",
            PlatformApplicationArn = "arn:aws:sns:eu-west-1:123456789:app/GCM/MyApp",
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
        AwsSnsMobilePushOptions options = new()
        {
            Region = "eu-west-1",
            PlatformApplicationArn = "arn:aws:sns:eu-west-1:123456789:app/GCM/MyApp",
            AccessKeyId = null,
            SecretAccessKey = "secret",
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("AccessKeyId");
    }

    [Fact]
    public void Validate_TimeoutLessThanOne_ReturnsFail()
    {
        AwsSnsMobilePushOptions options = new()
        {
            Region = "eu-west-1",
            PlatformApplicationArn = "arn:aws:sns:eu-west-1:123456789:app/GCM/MyApp",
            TimeoutSeconds = 0,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("TimeoutSeconds");
    }
}
