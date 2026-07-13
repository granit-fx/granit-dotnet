using Microsoft.Extensions.Options;

namespace Granit.Notifications.AzureCommunicationServices.Sms.Options;

/// <summary>Validates <see cref="AcsSmsOptions"/>.</summary>
internal sealed class AcsSmsOptionsValidator : IValidateOptions<AcsSmsOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, AcsSmsOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.FromPhoneNumber))
        {
            return ValidateOptionsResult.Fail("AcsSmsOptions.FromPhoneNumber is required.");
        }

        if (!options.FromPhoneNumber.StartsWith('+'))
        {
            return ValidateOptionsResult.Fail(
                "AcsSmsOptions.FromPhoneNumber must start with '+' (E.164 format).");
        }

        if (options.ConnectionString is null && options.Endpoint is null)
        {
            return ValidateOptionsResult.Fail(
                "Either AcsSmsOptions.ConnectionString or AcsSmsOptions.Endpoint must be provided.");
        }

        if (options.TimeoutSeconds < 1)
        {
            return ValidateOptionsResult.Fail("AcsSmsOptions.TimeoutSeconds must be at least 1.");
        }

        return ValidateOptionsResult.Success;
    }
}
