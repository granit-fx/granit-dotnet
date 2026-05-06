using Granit.MultiTenancy.Options;
using Granit.MultiTenancy.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.Tests.Options;

/// <summary>
/// Startup-time validations for <see cref="MultiTenancyOptions"/>.
/// </summary>
public sealed class MultiTenancyOptionsValidatorTests
{
    private static IHostEnvironment Env(string name)
    {
        IHostEnvironment env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(name);
        return env;
    }

    private static ServiceProvider WithMembershipReader(IUserTenantMembershipReader reader)
    {
        ServiceCollection services = new();
        services.AddScoped(_ => reader);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void QueryStringParam_NonEmpty_OutsideDevelopment_Fails()
    {
        IServiceProvider sp = WithMembershipReader(new NullUserTenantMembershipReader());
        MultiTenancyOptionsValidator validator = new(Env("Production"), sp);
        MultiTenancyOptions options = new() { QueryStringParamName = "__tenant" };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("QueryStringParamName");
    }

    [Fact]
    public void QueryStringParam_NonEmpty_InDevelopment_Succeeds()
    {
        IServiceProvider sp = WithMembershipReader(new NullUserTenantMembershipReader());
        MultiTenancyOptionsValidator validator = new(Env("Development"), sp);
        MultiTenancyOptions options = new() { QueryStringParamName = "__tenant" };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void RequireMembershipCheck_Without_Concrete_Reader_Fails()
    {
        // The fallback NullUserTenantMembershipReader returns true for every
        // membership check. Enabling the flag without a real reader registered
        // would silently accept any tenant claim — the validator must catch
        // this misconfiguration at startup.
        IServiceProvider sp = WithMembershipReader(new NullUserTenantMembershipReader());
        MultiTenancyOptionsValidator validator = new(Env("Production"), sp);
        MultiTenancyOptions options = new() { RequireMembershipCheck = true };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("IUserTenantMembershipReader");
    }

    [Fact]
    public void RequireMembershipCheck_With_Concrete_Reader_Succeeds()
    {
        IUserTenantMembershipReader concrete = Substitute.For<IUserTenantMembershipReader>();
        IServiceProvider sp = WithMembershipReader(concrete);
        MultiTenancyOptionsValidator validator = new(Env("Production"), sp);
        MultiTenancyOptions options = new() { RequireMembershipCheck = true };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void RequireMembershipCheck_Disabled_DoesNot_Inspect_Reader()
    {
        // Even with only the NullReader registered, the validator must succeed
        // when the flag is off — we don't inspect the reader at all.
        IServiceProvider sp = WithMembershipReader(new NullUserTenantMembershipReader());
        MultiTenancyOptionsValidator validator = new(Env("Production"), sp);
        MultiTenancyOptions options = new() { RequireMembershipCheck = false };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }
}
