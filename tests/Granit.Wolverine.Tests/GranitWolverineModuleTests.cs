// =============================================================================
// Tests - GranitWolverineModule
// =============================================================================
// Verifies module inheritance, DependsOn declarations, that AddGranitWolverine()
// registers Wolverine services without throwing, and that all DI registrations
// are present in the service collection.
// =============================================================================

using Granit.Modularity;
using Granit.MultiTenancy;
using Granit.Users;
using Granit.Validation;
using Granit.Wolverine.Extensions;
using Granit.Wolverine.Internal;
using Granit.Wolverine.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Wolverine;
using Xunit;

namespace Granit.Wolverine.Tests;

public sealed class GranitWolverineModuleTests
{
    [Fact]
    public void GranitWolverineModule_IsGranitModule() =>
        typeof(GranitWolverineModule).IsAssignableTo(typeof(GranitModule)).ShouldBeTrue();

    [Fact]
    public void GranitWolverineModule_DependsOn_ValidationModule()
    {
        var attributes = (DependsOnAttribute[])
            typeof(GranitWolverineModule).GetCustomAttributes(typeof(DependsOnAttribute), inherit: false);

        attributes.ShouldContain(a => a.DependedTypes.Contains(typeof(Validation.GranitValidationModule)));
    }

    [Fact]
    public void GranitWolverineModule_DoesNotDependOn_MultiTenancyModule()
    {
        var attributes = (DependsOnAttribute[])
            typeof(GranitWolverineModule).GetCustomAttributes(typeof(DependsOnAttribute), inherit: false);

        attributes.ShouldNotContain(a => a.DependedTypes.Contains(typeof(GranitMultiTenancyModule)),
            "ICurrentTenant is now sourced from Granit.MultiTenancy — Granit.MultiTenancy is a soft dependency");
    }

    [Fact]
    public void GranitWolverineModule_IsSealed() =>
        typeof(GranitWolverineModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void AddGranitWolverine_RegistersWolverineServices()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        Action act = () => builder.AddGranitWolverine();

        Should.NotThrow(act);
    }

    // -------------------------------------------------------------------------
    // DI registration verification (inspects IServiceCollection directly,
    // no host build needed — avoids starting Wolverine background services)
    // -------------------------------------------------------------------------

    [Fact]
    public void AddGranitWolverine_RegistersICurrentUserService_Scoped()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddGranitWolverine();

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(ICurrentUserService) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitWolverine_RegistersIWolverineUserContextSetter_Scoped()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddGranitWolverine();

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IWolverineUserContextSetter) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitWolverine_RegistersWolverineCurrentUserService_Scoped()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddGranitWolverine();

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(WolverineCurrentUserService) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitWolverine_RegistersHttpContextAccessor()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddGranitWolverine();

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IHttpContextAccessor));
    }

    [Fact]
    public void AddGranitWolverine_RegistersMessagingOptionsValidator()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddGranitWolverine();

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IValidateOptions<WolverineMessagingOptions>));
    }

    [Fact]
    public void AddGranitWolverine_WithConfigureCallback_InvokesCallback()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        bool callbackInvoked = false;

        builder.AddGranitWolverine(opts => { callbackInvoked = true; });

        callbackInvoked.ShouldBeTrue();
    }
}
