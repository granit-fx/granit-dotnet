using Granit.MultiTenancy.Options;
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

    [Fact]
    public void QueryStringParam_NonEmpty_OutsideDevelopment_Fails()
    {
        MultiTenancyOptionsValidator validator = new(Env("Production"));
        MultiTenancyOptions options = new() { QueryStringParamName = "__tenant" };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("QueryStringParamName");
    }

    [Fact]
    public void QueryStringParam_NonEmpty_InDevelopment_Succeeds()
    {
        MultiTenancyOptionsValidator validator = new(Env("Development"));
        MultiTenancyOptions options = new() { QueryStringParamName = "__tenant" };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }
}
