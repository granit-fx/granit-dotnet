using Granit.Notifications.AzureCommunicationServices.Email.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.AzureCommunicationServices.Email.Tests;

public sealed class AcsEmailOptionsValidatorTests
{
    private readonly AcsEmailOptionsValidator _validator = new();

    [Fact]
    public void Validate_ValidOptionsWithConnectionString_ReturnsSuccess()
    {
        AcsEmailOptions options = new()
        {
            ConnectionString = "endpoint=https://test.communication.azure.com/;accesskey=dGVzdA==",
            DefaultSenderEmail = "noreply@example.com",
            TimeoutSeconds = 30,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ValidOptionsWithEndpoint_ReturnsSuccess()
    {
        AcsEmailOptions options = new()
        {
            Endpoint = "https://test.communication.azure.com",
            DefaultSenderEmail = "noreply@example.com",
            TimeoutSeconds = 30,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyDefaultSenderEmail_ReturnsFail(string? defaultSenderEmail)
    {
        AcsEmailOptions options = new()
        {
            ConnectionString = "endpoint=https://test.communication.azure.com/;accesskey=dGVzdA==",
            DefaultSenderEmail = defaultSenderEmail!,
            TimeoutSeconds = 30,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("DefaultSenderEmail");
    }

    [Fact]
    public void Validate_NeitherConnectionStringNorEndpoint_ReturnsFail()
    {
        AcsEmailOptions options = new()
        {
            ConnectionString = null,
            Endpoint = null,
            DefaultSenderEmail = "noreply@example.com",
            TimeoutSeconds = 30,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("ConnectionString");
    }

    [Fact]
    public void Validate_TimeoutLessThanOne_ReturnsFail()
    {
        AcsEmailOptions options = new()
        {
            ConnectionString = "endpoint=https://test.communication.azure.com/;accesskey=dGVzdA==",
            DefaultSenderEmail = "noreply@example.com",
            TimeoutSeconds = 0,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("TimeoutSeconds");
    }

    [Fact]
    public void Validate_BothConnectionStringAndEndpoint_ReturnsSuccess()
    {
        AcsEmailOptions options = new()
        {
            ConnectionString = "endpoint=https://test.communication.azure.com/;accesskey=dGVzdA==",
            Endpoint = "https://test.communication.azure.com",
            DefaultSenderEmail = "noreply@example.com",
            TimeoutSeconds = 30,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }
}
