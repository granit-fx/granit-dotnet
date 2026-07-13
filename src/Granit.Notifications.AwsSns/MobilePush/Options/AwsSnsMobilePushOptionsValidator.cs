using Microsoft.Extensions.Options;

namespace Granit.Notifications.AwsSns.MobilePush.Options;

/// <summary>Validates <see cref="AwsSnsMobilePushOptions"/>.</summary>
internal sealed class AwsSnsMobilePushOptionsValidator : IValidateOptions<AwsSnsMobilePushOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, AwsSnsMobilePushOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Region))
        {
            return ValidateOptionsResult.Fail("AwsSnsMobilePushOptions.Region is required.");
        }

        if (string.IsNullOrWhiteSpace(options.PlatformApplicationArn))
        {
            return ValidateOptionsResult.Fail("AwsSnsMobilePushOptions.PlatformApplicationArn is required.");
        }

        if (!string.IsNullOrEmpty(options.AccessKeyId) && string.IsNullOrEmpty(options.SecretAccessKey))
        {
            return ValidateOptionsResult.Fail("AwsSnsMobilePushOptions.SecretAccessKey is required when AccessKeyId is set.");
        }

        if (!string.IsNullOrEmpty(options.SecretAccessKey) && string.IsNullOrEmpty(options.AccessKeyId))
        {
            return ValidateOptionsResult.Fail("AwsSnsMobilePushOptions.AccessKeyId is required when SecretAccessKey is set.");
        }

        if (options.TimeoutSeconds < 1)
        {
            return ValidateOptionsResult.Fail("AwsSnsMobilePushOptions.TimeoutSeconds must be at least 1.");
        }

        return ValidateOptionsResult.Success;
    }
}
