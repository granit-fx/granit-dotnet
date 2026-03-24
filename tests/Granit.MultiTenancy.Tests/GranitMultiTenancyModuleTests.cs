// =============================================================================
// GranitMultiTenancyModuleTests - DI integration tests for the module
// =============================================================================
// Verifies the complete service wiring via AddGranit<T>(),
// in the style of AbpIntegratedTest<T> in ABP Framework.
//
// Each test bootstraps the MultiTenancy module (standalone, no Security dependency)
// and resolves services from the real DI container.
// =============================================================================

using Granit.Extensions;
using Granit.Modularity;
using Granit.MultiTenancy;
using Granit.MultiTenancy.Middleware;
using Granit.MultiTenancy.Pipeline;
using Granit.MultiTenancy.Resolvers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.Tests;

public sealed class GranitMultiTenancyModuleTests
{
    private static WebApplication BuildApp()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.AddGranit<GranitMultiTenancyModule>();
        return builder.Build();
    }

    // --- DI wiring ---

    [Fact]
    public void ICurrentTenant_Is_Resolvable_And_Singleton()
    {
        using WebApplication app = BuildApp();

        ICurrentTenant first = app.Services.GetRequiredService<ICurrentTenant>();
        ICurrentTenant second = app.Services.GetRequiredService<ICurrentTenant>();

        first.ShouldNotBeNull();
        first.ShouldBeOfType<CurrentTenant>();
        first.ShouldBeSameAs(second, "ICurrentTenant must be a singleton");
    }

    [Fact]
    public void TenantResolverPipeline_Is_Resolvable_And_Singleton()
    {
        using WebApplication app = BuildApp();

        TenantResolverPipeline first = app.Services.GetRequiredService<TenantResolverPipeline>();
        TenantResolverPipeline second = app.Services.GetRequiredService<TenantResolverPipeline>();

        first.ShouldNotBeNull();
        first.ShouldBeSameAs(second, "TenantResolverPipeline must be a singleton");
    }

    [Fact]
    public void Two_TenantResolvers_Are_Registered()
    {
        using WebApplication app = BuildApp();

        IEnumerable<ITenantResolver> resolvers = app.Services.GetRequiredService<IEnumerable<ITenantResolver>>();

        resolvers.Count().ShouldBe(2);
    }

    [Fact]
    public void Resolvers_Are_Ordered_Header_Before_Jwt()
    {
        using WebApplication app = BuildApp();

        var ordered = app.Services
            .GetRequiredService<IEnumerable<ITenantResolver>>()
            .OrderBy(r => r.Order)
            .ToList();

        ordered[0].ShouldBeOfType<HeaderTenantResolver>("Header (order=100) must precede JWT (order=200)");
        ordered[1].ShouldBeOfType<JwtClaimTenantResolver>();
    }

    [Fact]
    public void TenantResolutionMiddleware_Is_Resolvable_As_Scoped()
    {
        using WebApplication app = BuildApp();
        using IServiceScope scope = app.Services.CreateScope();

        TenantResolutionMiddleware middleware = scope.ServiceProvider.GetRequiredService<TenantResolutionMiddleware>();

        middleware.ShouldNotBeNull();
    }

    // --- Topological order ---

    [Fact]
    public void Module_Topological_Order_Contains_MultiTenancy()
    {
        using WebApplication app = BuildApp();

        GranitApplication granitApp = app.Services.GetRequiredService<GranitApplication>();

        granitApp.GetModuleTypes().ShouldContain(typeof(GranitMultiTenancyModule),
            "MultiTenancy module is standalone with no Security dependency");
    }

    // --- Functional test (AbpIntegratedTest style) ---

    [Fact]
    public void ICurrentTenant_Resolved_From_DI_Change_Works()
    {
        using WebApplication app = BuildApp();
        ICurrentTenant currentTenant = app.Services.GetRequiredService<ICurrentTenant>();
        var tenantId = Guid.NewGuid();

        currentTenant.IsAvailable.ShouldBeFalse("no active tenant at startup");

        using (currentTenant.Change(tenantId, "Acme"))
        {
            currentTenant.IsAvailable.ShouldBeTrue();
            currentTenant.Id.ShouldBe(tenantId);
            currentTenant.Name.ShouldBe("Acme");
        }

        currentTenant.IsAvailable.ShouldBeFalse("the scope must be restored after Dispose");
    }

    [Fact]
    public void ICurrentTenant_Resolved_From_DI_Nested_Scopes_Work()
    {
        using WebApplication app = BuildApp();
        ICurrentTenant currentTenant = app.Services.GetRequiredService<ICurrentTenant>();
        var outer = Guid.NewGuid();
        var inner = Guid.NewGuid();

        using (currentTenant.Change(outer, "Outer"))
        {
            currentTenant.Id.ShouldBe(outer);

            using (currentTenant.Change(inner, "Inner"))
            {
                currentTenant.Id.ShouldBe(inner);
            }

            currentTenant.Id.ShouldBe(outer, "the outer scope must be restored after the inner scope is disposed");
        }

        currentTenant.IsAvailable.ShouldBeFalse();
    }
}
