using Microsoft.Extensions.Options;

namespace Granit.Notifications.AwsSes.Options;

/// <summary>Validates <see cref="AwsSesOptions"/>.</summary>
internal sealed class AwsSesOptionsValidator : IValidateOptions<AwsSesOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, AwsSesOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Region))
        {
            return ValidateOptionsResult.Fail("AwsSesOptions.Region is required.");
        }

        if (options.TimeoutSeconds < 1)
        {
            return ValidateOptionsResult.Fail("AwsSesOptions.TimeoutSeconds must be at least 1.");
        }

        if (options.AccessKeyId is not null && options.SecretAccessKey is null)
        {
            return ValidateOptionsResult.Fail(
                "AwsSesOptions.SecretAccessKey is required when AccessKeyId is provided.");
        }

        if (options.SecretAccessKey is not null && options.AccessKeyId is null)
        {
            return ValidateOptionsResult.Fail(
                "AwsSesOptions.AccessKeyId is required when SecretAccessKey is provided.");
        }

        return ValidateOptionsResult.Success;
    }
}
