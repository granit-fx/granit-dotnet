using System.Reflection;
using Granit.MultiTenancy.Events;
using Granit.MultiTenancy.Wolverine.Handlers;
using Granit.Persistence.EntityFrameworkCore.Hosting;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.Wolverine.Tests;

public sealed class TenantProvisioningHandlerTests
{
    [Fact]
    public async Task HandleAsync_DelegatesToProvisioner()
    {
        // Arrange
        ITenantProvisioner provisioner = Substitute.For<ITenantProvisioner>();
        TenantCreatedEvent evt = new(Guid.NewGuid(), "Acme Corp", "acme");

        // Act
        await TenantProvisioningHandler.HandleAsync(evt, provisioner, CancellationToken.None);

        // Assert
        await provisioner.Received(1).ProvisionAsync(
            evt.TenantId, evt.Name, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Handler_ShouldBePublicNonStatic()
    {
        Type handlerType = typeof(TenantProvisioningHandler);

        handlerType.IsPublic.ShouldBeTrue("handler must be public for Wolverine discovery");
        handlerType.IsAbstract.ShouldBeFalse("handler must not be static for Wolverine discovery");
    }

    [Fact]
    public void HandleAsync_ShouldBePublicStatic()
    {
        MethodInfo? method = typeof(TenantProvisioningHandler)
            .GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Static);

        method.ShouldNotBeNull("HandleAsync must exist as a public static method");
        method.ReturnType.ShouldBe(typeof(Task));
    }

    [Fact]
    public void HandleAsync_FirstParameter_ShouldBeTenantCreatedEvent()
    {
        MethodInfo method = typeof(TenantProvisioningHandler)
            .GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Static)!;

        ParameterInfo[] parameters = method.GetParameters();
        parameters.Length.ShouldBeGreaterThanOrEqualTo(1);
        parameters[0].ParameterType.ShouldBe(typeof(TenantCreatedEvent));
    }
}
