using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.MultiTenancy.Options;

/// <summary>
/// Production-time validator for <see cref="MultiTenancyOptions"/>.
/// Refuses to start when development-only resolvers are enabled outside Development.
/// </summary>
internal sealed class MultiTenancyOptionsValidator(IHostEnvironment environment)
    : IValidateOptions<MultiTenancyOptions>
{
    public ValidateOptionsResult Validate(string? name, MultiTenancyOptions options)
    {
        if (!environment.IsDevelopment()
            && !string.IsNullOrEmpty(options.QueryStringParamName))
        {
            return ValidateOptionsResult.Fail(
                "MultiTenancy:QueryStringParamName must be empty outside the Development "
                + $"environment (current: '{environment.EnvironmentName}'). The query-string "
                + "tenant resolver allows any caller to select an arbitrary tenant context "
                + "and is forbidden in production.");
        }

        return ValidateOptionsResult.Success;
    }
}
