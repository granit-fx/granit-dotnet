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
using Granit.Wolverine.Extensions;
using Granit.Wolverine.Internal;
using Granit.Wolverine.Options;
using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
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
        // WolverineCurrentUserService is no longer registered as its own concrete service:
        // it is bound directly to ICurrentUserService and IWolverineUserContextSetter via
        // AddScoped<IFoo, TConcrete> so both registrations stay inline-able under Wolverine
        // static codegen (ServiceLocationPolicy.NotAllowed). All AsyncLocal state is static,
        // so the two distinct scoped instances still share user context.
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddGranitWolverine();

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(ICurrentUserService) &&
            d.ImplementationType == typeof(WolverineCurrentUserService) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitWolverine_RegistersIWolverineUserContextSetter_Scoped()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddGranitWolverine();

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IWolverineUserContextSetter) &&
            d.ImplementationType == typeof(WolverineCurrentUserService) &&
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

        builder.AddGranitWolverine(configure: opts => callbackInvoked = true);

        callbackInvoked.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Code-generation mode wiring (config → opts.CodeGeneration.TypeLoadMode).
    // Inspects the captured WolverineOptions without building the host, so Static
    // never triggers the missing-pre-generated-types startup throw.
    // -------------------------------------------------------------------------

    [Fact]
    public void AddGranitWolverine_DefaultsCodeGenerationModeToDynamic()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddGranitWolverine();

        ResolveWolverineOptions(builder).CodeGeneration.TypeLoadMode.ShouldBe(TypeLoadMode.Dynamic);
    }

    [Fact]
    public void AddGranitWolverine_AppliesCodeGenerationModeFromConfig()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Wolverine:CodeGenerationMode"] = "Static",
        });

        builder.AddGranitWolverine();

        ResolveWolverineOptions(builder).CodeGeneration.TypeLoadMode.ShouldBe(TypeLoadMode.Static);
    }

    [Fact]
    public void AddGranitWolverine_SetsApplicationAssembly_ToEntryAssembly()
    {
        // UseWolverine() runs from inside Granit.Wolverine, so without the explicit override
        // Wolverine would infer ApplicationAssembly = Granit.Wolverine — and Static codegen
        // would look for the pre-generated registry in the wrong assembly.
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddGranitWolverine();

        ResolveWolverineOptions(builder).ApplicationAssembly
            .ShouldBe(System.Reflection.Assembly.GetEntryAssembly());
    }

    [Fact]
    public void AddGranitWolverine_SetsServiceLocationPolicy_NotAllowed()
    {
        // ServiceLocationPolicy.NotAllowed aborts `codegen write` when a chain dependency can't
        // be inlined. The three previously-problematic registrations are now codegen-clean:
        // ICurrentUserService + IWolverineUserContextSetter use direct AddScoped<IFoo, TConcrete>
        // (no lambda factory), and the internal INotificationPublisher impls are reachable via
        // InternalsVisibleTo "WolverineHandlers" — so NotAllowed is restored for fail-fast codegen.
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.AddGranitWolverine();

        ResolveWolverineOptions(builder).ServiceLocationPolicy
            .ShouldBe(ServiceLocationPolicy.NotAllowed);
    }

    private static global::Wolverine.WolverineOptions ResolveWolverineOptions(HostApplicationBuilder builder) =>
        builder.Services
            .Select(d => d.ImplementationInstance)
            .OfType<WolverineOptionsHolder>()
            .First()
            .Options;
}
