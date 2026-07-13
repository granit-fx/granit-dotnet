// =============================================================================
// Tests - AzureNotificationHubsOptionsValidator
// =============================================================================
// Verifies that options validation catches missing/invalid configuration
// and accepts valid configuration.
// =============================================================================

using Granit.Notifications.AzureNotificationHubs.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.AzureNotificationHubs.Tests;

public sealed class AzureNotificationHubsOptionsValidatorTests
{
    private readonly AzureNotificationHubsOptionsValidator _validator = new();

    private static AzureNotificationHubsOptions ValidOptions() => new()
    {
        ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=DefaultFullSharedAccessSignature;SharedAccessKey=abc123=",
        HubName = "my-hub",
        TimeoutSeconds = 30,
    };

    // -------------------------------------------------------------------------
    // Valid options
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_ValidOptions_ReturnsSuccess()
    {
        ValidateOptionsResult result = _validator.Validate(null, ValidOptions());
        result.Succeeded.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // ConnectionString
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingConnectionString_ReturnsFail(string? connectionString)
    {
        AzureNotificationHubsOptions options = ValidOptions();
        options.ConnectionString = connectionString!;

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("ConnectionString");
    }

    // -------------------------------------------------------------------------
    // HubName
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingHubName_ReturnsFail(string? hubName)
    {
        AzureNotificationHubsOptions options = ValidOptions();
        options.HubName = hubName!;

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("HubName");
    }

    // -------------------------------------------------------------------------
    // TimeoutSeconds
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_TimeoutSecondsZero_ReturnsFail()
    {
        AzureNotificationHubsOptions options = ValidOptions();
        options.TimeoutSeconds = 0;

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("TimeoutSeconds");
    }

    [Fact]
    public void Validate_TimeoutSecondsNegative_ReturnsFail()
    {
        AzureNotificationHubsOptions options = ValidOptions();
        options.TimeoutSeconds = -5;

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("TimeoutSeconds");
    }

    [Fact]
    public void Validate_TimeoutSecondsOne_ReturnsSuccess()
    {
        AzureNotificationHubsOptions options = ValidOptions();
        options.TimeoutSeconds = 1;

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Options defaults
    // -------------------------------------------------------------------------

    [Fact]
    public void SectionName_IsNotificationsAzureNotificationHubs() =>
        AzureNotificationHubsOptions.SectionName.ShouldBe("Notifications:AzureNotificationHubs");

    [Fact]
    public void ConnectionString_Default_IsEmpty() =>
        new AzureNotificationHubsOptions().ConnectionString.ShouldBe(string.Empty);

    [Fact]
    public void HubName_Default_IsEmpty() =>
        new AzureNotificationHubsOptions().HubName.ShouldBe(string.Empty);

    [Fact]
    public void TimeoutSeconds_Default_Is30() =>
        new AzureNotificationHubsOptions().TimeoutSeconds.ShouldBe(30);
}
