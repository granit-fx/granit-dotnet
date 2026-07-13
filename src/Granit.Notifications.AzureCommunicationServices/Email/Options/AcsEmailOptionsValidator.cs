using Microsoft.Extensions.Options;

namespace Granit.Notifications.AzureCommunicationServices.Email.Options;

/// <summary>Validates <see cref="AcsEmailOptions"/>.</summary>
internal sealed class AcsEmailOptionsValidator : IValidateOptions<AcsEmailOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, AcsEmailOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DefaultSenderEmail))
        {
            return ValidateOptionsResult.Fail("AcsEmailOptions.DefaultSenderEmail is required.");
        }

        if (string.IsNullOrWhiteSpace(options.ConnectionString) &&
            string.IsNullOrWhiteSpace(options.Endpoint))
        {
            return ValidateOptionsResult.Fail(
                "AcsEmailOptions requires either ConnectionString or Endpoint to be provided.");
        }

        if (options.TimeoutSeconds < 1)
        {
            return ValidateOptionsResult.Fail("AcsEmailOptions.TimeoutSeconds must be at least 1.");
        }

        return ValidateOptionsResult.Success;
    }
}
