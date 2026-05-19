using Granit.MultiTenancy.Authorization.Extensions;
using Granit.MultiTenancy.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.Authorization.Tests;

public sealed class MultiTenancyAuthorizationServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitHostImpersonationWithPermissions_ReplacesDefaultGate()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGranitMultiTenancy();

        services.AddGranitHostImpersonationWithPermissions();

        ServiceDescriptor descriptor = services
            .Last(d => d.ServiceType == typeof(IHostImpersonationGate));
        descriptor.ImplementationType.ShouldBe(typeof(PermissionBasedHostImpersonationGate));
    }
}
