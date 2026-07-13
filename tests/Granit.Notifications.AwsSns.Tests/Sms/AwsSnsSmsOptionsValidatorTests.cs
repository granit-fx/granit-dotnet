using Granit.Notifications.AwsSns.Sms.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.AwsSns.Sms.Tests;

public sealed class AwsSnsSmsOptionsValidatorTests
{
    private readonly AwsSnsSmsOptionsValidator _validator = new();

    [Fact]
    public void Validate_ValidOptions_ReturnsSuccess()
    {
        AwsSnsSmsOptions options = new()
        {
            Region = "eu-west-1",
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
        AwsSnsSmsOptions options = new()
        {
            Region = region!,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Region");
    }

    [Fact]
    public void Validate_InvalidSmsType_ReturnsFail()
    {
        AwsSnsSmsOptions options = new()
        {
            Region = "eu-west-1",
            SmsType = "Invalid",
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("SmsType");
    }

    [Theory]
    [InlineData("Transactional")]
    [InlineData("Promotional")]
    public void Validate_ValidSmsType_ReturnsSuccess(string smsType)
    {
        AwsSnsSmsOptions options = new()
        {
            Region = "eu-west-1",
            SmsType = smsType,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_AccessKeyWithoutSecret_ReturnsFail()
    {
        AwsSnsSmsOptions options = new()
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
        AwsSnsSmsOptions options = new()
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
    public void Validate_BothCredentials_ReturnsSuccess()
    {
        AwsSnsSmsOptions options = new()
        {
            Region = "eu-west-1",
            AccessKeyId = "AKID",
            SecretAccessKey = "secret",
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_TimeoutLessThanOne_ReturnsFail()
    {
        AwsSnsSmsOptions options = new()
        {
            Region = "eu-west-1",
            TimeoutSeconds = 0,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("TimeoutSeconds");
    }
}
