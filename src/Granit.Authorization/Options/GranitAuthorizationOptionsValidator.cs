using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.Authorization.Options;

/// <summary>
/// Validates <see cref="GranitAuthorizationOptions"/> at startup to catch
/// misconfigurations that would silently degrade authorization security.
/// </summary>
internal sealed class GranitAuthorizationOptionsValidator(
    IHostEnvironment environment) : IValidateOptions<GranitAuthorizationOptions>
{
    public ValidateOptionsResult Validate(string? name, GranitAuthorizationOptions options)
    {
        List<string>? failures = null;

        if (options.AlwaysAllow && environment.IsProduction())
        {
            (failures ??= []).Add(
                "AlwaysAllow must not be enabled in production — it bypasses all permission checks for authenticated users.");
        }

        if (options.AdminRoles.Count == 0)
        {
            (failures ??= []).Add(
                "AdminRoles must contain at least one role name.");
        }

        if (options.CacheDuration < TimeSpan.FromSeconds(10))
        {
            (failures ??= []).Add(
                $"CacheDuration must be at least 10 seconds, got {options.CacheDuration}.");
        }

        if (options.CacheDuration > TimeSpan.FromMinutes(30))
        {
            (failures ??= []).Add(
                $"CacheDuration must not exceed 30 minutes, got {options.CacheDuration}.");
        }

        return failures is null
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
