using Microsoft.Extensions.Options;

namespace Granit.Notifications.AzureNotificationHubs.Options;

/// <summary>Validates <see cref="AzureNotificationHubsOptions"/>.</summary>
internal sealed class AzureNotificationHubsOptionsValidator : IValidateOptions<AzureNotificationHubsOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, AzureNotificationHubsOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return ValidateOptionsResult.Fail("AzureNotificationHubsOptions.ConnectionString is required.");
        }

        if (string.IsNullOrWhiteSpace(options.HubName))
        {
            return ValidateOptionsResult.Fail("AzureNotificationHubsOptions.HubName is required.");
        }

        if (options.TimeoutSeconds < 1)
        {
            return ValidateOptionsResult.Fail("AzureNotificationHubsOptions.TimeoutSeconds must be at least 1.");
        }

        return ValidateOptionsResult.Success;
    }
}
