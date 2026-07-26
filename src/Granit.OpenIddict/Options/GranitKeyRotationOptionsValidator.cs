using Microsoft.Extensions.Options;

namespace Granit.OpenIddict.Options;

/// <summary>
/// Validates <see cref="GranitKeyRotationOptions"/> at startup when rotation is enabled.
/// </summary>
/// <remarks>
/// The knobs are inert unless <see cref="GranitKeyRotationOptions.Enabled"/> is set (rotation off
/// means ephemeral/configured keys), so validation is skipped in that case rather than blocking
/// startup for an unused feature. Runs after post-configuration, so the FAPI 2.0 PS256 override on
/// <see cref="GranitKeyRotationOptions.SigningAlgorithm"/> has already been applied.
/// </remarks>
internal sealed class GranitKeyRotationOptionsValidator : IValidateOptions<GranitKeyRotationOptions>
{
    private const int MinRsaKeySize = 2048;
    private const int MaxRsaKeySize = 4096;

    public ValidateOptionsResult Validate(string? name, GranitKeyRotationOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        List<string> failures = [];

        if (options.RsaKeySize is < MinRsaKeySize or > MaxRsaKeySize)
        {
            failures.Add(
                $"{nameof(options.RsaKeySize)} must be between {MinRsaKeySize} and {MaxRsaKeySize} bits.");
        }

        if (options.KeyLifetime <= TimeSpan.Zero)
        {
            failures.Add($"{nameof(options.KeyLifetime)} must be a positive duration.");
        }

        if (options.GracePeriod <= TimeSpan.Zero)
        {
            failures.Add($"{nameof(options.GracePeriod)} must be a positive duration.");
        }

        if (options.RotationLeadTime <= TimeSpan.Zero)
        {
            failures.Add($"{nameof(options.RotationLeadTime)} must be a positive duration.");
        }
        else if (options.RotationLeadTime >= options.KeyLifetime)
        {
            failures.Add(
                $"{nameof(options.RotationLeadTime)} must be shorter than {nameof(options.KeyLifetime)} "
                + "so a new key is minted before the active one expires.");
        }

        if (options.RefreshCheckInterval <= TimeSpan.Zero)
        {
            failures.Add($"{nameof(options.RefreshCheckInterval)} must be a positive duration.");
        }
        else if (options.RefreshCheckInterval >= options.GracePeriod)
        {
            failures.Add(
                $"{nameof(options.RefreshCheckInterval)} must be well below {nameof(options.GracePeriod)} "
                + "so a rotated-in key is picked up before the retired key is revoked.");
        }

        if (string.IsNullOrWhiteSpace(options.SigningAlgorithm))
        {
            failures.Add($"{nameof(options.SigningAlgorithm)} must be non-empty.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
