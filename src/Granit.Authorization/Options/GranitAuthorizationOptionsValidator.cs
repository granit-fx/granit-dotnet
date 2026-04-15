using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.Authorization.Options;

/// <summary>Validates <see cref="GranitAuthorizationOptions"/>.</summary>
internal sealed class GranitAuthorizationOptionsValidator(
    IHostEnvironment hostEnvironment) : IValidateOptions<GranitAuthorizationOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, GranitAuthorizationOptions options)
    {
        if (options.AdminRoles is null || options.AdminRoles.Count == 0)
        {
            return ValidateOptionsResult.Fail("Authorization.AdminRoles must contain at least one role.");
        }

        if (options.CacheDuration < TimeSpan.FromSeconds(10) || options.CacheDuration > TimeSpan.FromMinutes(30))
        {
            return ValidateOptionsResult.Fail("Authorization.CacheDuration must be between 10 seconds and 30 minutes.");
        }

        if (options.AlwaysAllow && hostEnvironment.IsProduction())
        {
            return ValidateOptionsResult.Fail(
                "Authorization.AlwaysAllow MUST NOT be enabled in production. " +
                "This setting bypasses all permission checks and is intended for development/testing only.");
        }

        return ValidateOptionsResult.Success;
    }
}
