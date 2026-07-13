using Microsoft.Extensions.Options;

namespace Granit.Notifications.AwsSns.Sms.Options;

/// <summary>Validates <see cref="AwsSnsSmsOptions"/>.</summary>
internal sealed class AwsSnsSmsOptionsValidator : IValidateOptions<AwsSnsSmsOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, AwsSnsSmsOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Region))
        {
            return ValidateOptionsResult.Fail("AwsSnsSmsOptions.Region is required.");
        }

        if (options.SmsType is not ("Transactional" or "Promotional"))
        {
            return ValidateOptionsResult.Fail("AwsSnsSmsOptions.SmsType must be 'Transactional' or 'Promotional'.");
        }

        if (!string.IsNullOrEmpty(options.AccessKeyId) && string.IsNullOrEmpty(options.SecretAccessKey))
        {
            return ValidateOptionsResult.Fail("AwsSnsSmsOptions.SecretAccessKey is required when AccessKeyId is set.");
        }

        if (!string.IsNullOrEmpty(options.SecretAccessKey) && string.IsNullOrEmpty(options.AccessKeyId))
        {
            return ValidateOptionsResult.Fail("AwsSnsSmsOptions.AccessKeyId is required when SecretAccessKey is set.");
        }

        if (options.TimeoutSeconds < 1)
        {
            return ValidateOptionsResult.Fail("AwsSnsSmsOptions.TimeoutSeconds must be at least 1.");
        }

        return ValidateOptionsResult.Success;
    }
}
