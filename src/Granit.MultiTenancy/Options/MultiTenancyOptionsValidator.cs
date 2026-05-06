using Granit.MultiTenancy.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.MultiTenancy.Options;

/// <summary>
/// Production-time validator for <see cref="MultiTenancyOptions"/>.
/// Refuses to start when development-only resolvers are enabled outside Development,
/// or when <see cref="MultiTenancyOptions.RequireMembershipCheck"/> is enabled but
/// no concrete <see cref="IUserTenantMembershipReader"/> is registered.
/// </summary>
internal sealed class MultiTenancyOptionsValidator(
    IHostEnvironment environment,
    IServiceProvider serviceProvider)
    : IValidateOptions<MultiTenancyOptions>
{
    public ValidateOptionsResult Validate(string? name, MultiTenancyOptions options)
    {
        if (!environment.IsDevelopment()
            && !string.IsNullOrEmpty(options.QueryStringParamName))
        {
            return ValidateOptionsResult.Fail(
                $"MultiTenancy:QueryStringParamName must be empty outside the Development "
                + $"environment (current: '{environment.EnvironmentName}'). The query-string "
                + "tenant resolver allows any caller to select an arbitrary tenant context "
                + "and is forbidden in production.");
        }

        if (options.RequireMembershipCheck)
        {
            using IServiceScope scope = serviceProvider.CreateScope();
            IUserTenantMembershipReader? reader = scope.ServiceProvider
                .GetService<IUserTenantMembershipReader>();
            if (reader is null or NullUserTenantMembershipReader)
            {
                return ValidateOptionsResult.Fail(
                    "MultiTenancy:RequireMembershipCheck is enabled but no concrete "
                    + "IUserTenantMembershipReader is registered. The fallback "
                    + "NullUserTenantMembershipReader returns true for every check, "
                    + "which would silently accept any tenant claim — defeating the "
                    + "purpose of the flag. Register a real reader or disable the flag.");
            }
        }

        return ValidateOptionsResult.Success;
    }
}
