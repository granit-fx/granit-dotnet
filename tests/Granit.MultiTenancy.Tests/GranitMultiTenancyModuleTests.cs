// =============================================================================
// GranitMultiTenancyModuleTests - DI integration tests for the module
// =============================================================================
// Verifies the complete service wiring via AddGranit<T>().
//
// Each test bootstraps the MultiTenancy module (standalone, no Security dependency)
// and resolves services from the real DI container.
// =============================================================================

using Granit.Extensions;
using Granit.Modularity;
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
    public void TenantResolverPipeline_Is_Resolvable_As_Scoped()
    {
        using WebApplication app = BuildApp();
        using IServiceScope scope = app.Services.CreateScope();

        TenantResolverPipeline pipeline = scope.ServiceProvider.GetRequiredService<TenantResolverPipeline>();

        pipeline.ShouldNotBeNull();
    }

    [Fact]
    public void Five_TenantResolvers_Are_Registered()
    {
        using WebApplication app = BuildApp();
        using IServiceScope scope = app.Services.CreateScope();

        IEnumerable<ITenantResolver> resolvers = scope.ServiceProvider.GetRequiredService<IEnumerable<ITenantResolver>>();

        resolvers.Count().ShouldBe(5);
    }

    [Fact]
    public void Resolvers_Are_Ordered_CustomDomain_Domain_Header_Jwt_QueryString()
    {
        using WebApplication app = BuildApp();
        using IServiceScope scope = app.Services.CreateScope();

        var ordered = scope.ServiceProvider
            .GetRequiredService<IEnumerable<ITenantResolver>>()
            .OrderBy(r => r.Order)
            .ToList();

        ordered[0].ShouldBeOfType<CustomDomainTenantResolver>("CustomDomain (order=25)");
        ordered[1].ShouldBeOfType<DomainTenantResolver>("Domain (order=50)");
        ordered[2].ShouldBeOfType<HeaderTenantResolver>("Header (order=100)");
        ordered[3].ShouldBeOfType<JwtClaimTenantResolver>("JWT (order=200)");
        ordered[4].ShouldBeOfType<QueryStringTenantResolver>("QueryString (order=300)");
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

    // --- Functional test ---

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
