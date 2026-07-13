using Granit.Notifications.AzureCommunicationServices.Sms.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.AzureCommunicationServices.Sms.Tests;

public sealed class AcsSmsOptionsValidatorTests
{
    private readonly AcsSmsOptionsValidator _validator = new();

    [Fact]
    public void Validate_ValidOptionsWithConnectionString_ReturnsSuccess()
    {
        AcsSmsOptions options = new()
        {
            ConnectionString = "endpoint=https://test.communication.azure.com/;accesskey=dGVzdA==",
            FromPhoneNumber = "+15551234567",
            TimeoutSeconds = 30,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ValidOptionsWithEndpoint_ReturnsSuccess()
    {
        AcsSmsOptions options = new()
        {
            Endpoint = "https://test.communication.azure.com",
            FromPhoneNumber = "+15551234567",
            TimeoutSeconds = 30,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyFromPhoneNumber_ReturnsFail(string? fromPhoneNumber)
    {
        AcsSmsOptions options = new()
        {
            ConnectionString = "endpoint=https://test.communication.azure.com/;accesskey=dGVzdA==",
            FromPhoneNumber = fromPhoneNumber!,
            TimeoutSeconds = 30,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("FromPhoneNumber");
    }

    [Fact]
    public void Validate_FromPhoneNumberWithoutPlus_ReturnsFail()
    {
        AcsSmsOptions options = new()
        {
            ConnectionString = "endpoint=https://test.communication.azure.com/;accesskey=dGVzdA==",
            FromPhoneNumber = "15551234567",
            TimeoutSeconds = 30,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("E.164");
    }

    [Fact]
    public void Validate_NeitherConnectionStringNorEndpoint_ReturnsFail()
    {
        AcsSmsOptions options = new()
        {
            ConnectionString = null,
            Endpoint = null,
            FromPhoneNumber = "+15551234567",
            TimeoutSeconds = 30,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("ConnectionString");
    }

    [Fact]
    public void Validate_TimeoutLessThanOne_ReturnsFail()
    {
        AcsSmsOptions options = new()
        {
            ConnectionString = "endpoint=https://test.communication.azure.com/;accesskey=dGVzdA==",
            FromPhoneNumber = "+15551234567",
            TimeoutSeconds = 0,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("TimeoutSeconds");
    }

    [Fact]
    public void Validate_BothConnectionStringAndEndpoint_ReturnsSuccess()
    {
        AcsSmsOptions options = new()
        {
            ConnectionString = "endpoint=https://test.communication.azure.com/;accesskey=dGVzdA==",
            Endpoint = "https://test.communication.azure.com",
            FromPhoneNumber = "+15551234567",
            TimeoutSeconds = 30,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }
}
