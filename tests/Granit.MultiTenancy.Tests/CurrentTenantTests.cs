// =============================================================================
// CurrentTenantTests - Unit tests for ICurrentTenant / CurrentTenant
// =============================================================================

using Granit.MultiTenancy;
using Granit.MultiTenancy.Internal;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.Tests;

public sealed class CurrentTenantTests
{
    /// <summary>
    /// Default factory: no HttpContextAccessor, so all reads/writes go through the
    /// AsyncLocal fallback (the legacy non-HTTP code path used by Wolverine,
    /// background jobs, migrations).
    /// </summary>
    private static CurrentTenant Create() => new();

    [Fact]
    public void Initially_IsAvailable_False_And_Id_Null()
    {
        CurrentTenant tenant = Create();

        tenant.IsAvailable.ShouldBeFalse();
        tenant.Id.ShouldBeNull();
        tenant.Name.ShouldBeNull();
    }

    [Fact]
    public void Change_Sets_Tenant_Context()
    {
        CurrentTenant tenant = Create();
        var id = Guid.NewGuid();

        using IDisposable _ = tenant.Change(id, "Acme");

        tenant.IsAvailable.ShouldBeTrue();
        tenant.Id.ShouldBe(id);
        tenant.Name.ShouldBe("Acme");
    }

    [Fact]
    public void Change_WithNull_Clears_Context()
    {
        CurrentTenant tenant = Create();

        using IDisposable _ = tenant.Change(null);

        tenant.IsAvailable.ShouldBeFalse();
        tenant.Id.ShouldBeNull();
    }

    [Fact]
    public void Dispose_Restores_Previous_Context()
    {
        CurrentTenant tenant = Create();
        var id = Guid.NewGuid();

        IDisposable scope = tenant.Change(id, "Acme");
        scope.Dispose();

        tenant.IsAvailable.ShouldBeFalse();
        tenant.Id.ShouldBeNull();
    }

    [Fact]
    public void Nested_Change_Restores_Outer_Context_On_Dispose()
    {
        CurrentTenant tenant = Create();
        var outerTenant = Guid.NewGuid();
        var innerTenant = Guid.NewGuid();

        using IDisposable outer = tenant.Change(outerTenant, "Outer");

        using (IDisposable inner = tenant.Change(innerTenant, "Inner"))
        {
            tenant.Id.ShouldBe(innerTenant);
            tenant.Name.ShouldBe("Inner");
        }

        // After disposing the inner scope, the outer scope is restored
        tenant.Id.ShouldBe(outerTenant);
        tenant.Name.ShouldBe("Outer");
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        CurrentTenant tenant = Create();
        var id = Guid.NewGuid();

        IDisposable scope = tenant.Change(id);
        scope.Dispose();
        scope.Dispose(); // second dispose must not throw or alter the context

        tenant.IsAvailable.ShouldBeFalse();
    }

    [Fact]
    public async Task AsyncLocal_Isolates_Tasks()
    {
        // Two parallel tasks with different tenants
        // must not interfere with each other (AsyncLocal)
        CurrentTenant tenant = Create();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var taskA = Task.Run(async () =>
        {
            using IDisposable _ = tenant.Change(tenantA, "A");
            await Task.Delay(10, TestContext.Current.CancellationToken);
            tenant.Id.ShouldBe(tenantA, "task A must see its own tenant");
        }, TestContext.Current.CancellationToken);

        var taskB = Task.Run(async () =>
        {
            using IDisposable _ = tenant.Change(tenantB, "B");
            await Task.Delay(10, TestContext.Current.CancellationToken);
            tenant.Id.ShouldBe(tenantB, "task B must see its own tenant");
        }, TestContext.Current.CancellationToken);

        await Task.WhenAll(taskA, taskB);

        // The root context is intact
        tenant.IsAvailable.ShouldBeFalse();
    }

    [Fact]
    public void Change_WithoutName_Sets_Id_Only()
    {
        CurrentTenant tenant = Create();
        var id = Guid.NewGuid();

        using IDisposable _ = tenant.Change(id);

        tenant.Id.ShouldBe(id);
        tenant.Name.ShouldBeNull();
        tenant.IsAvailable.ShouldBeTrue();
    }

    // ====================================================================
    // HTTP backend — when an HttpContext is active, the tenant lives on
    // HttpContext.Features (not on the AsyncLocal). The lifetime is the
    // request itself, so there is no execution-context state to leak
    // across requests on thread-pool reuse.
    // ====================================================================

    [Fact]
    public void HttpContextActive_Change_Stores_Tenant_On_Features()
    {
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        DefaultHttpContext httpContext = new();
        accessor.HttpContext.Returns(httpContext);
        CurrentTenant tenant = new(accessor);
        var id = Guid.NewGuid();

        using IDisposable _ = tenant.Change(id, "Acme");

        tenant.Id.ShouldBe(id);
        tenant.Name.ShouldBe("Acme");
        // The feature has been written to the request's feature collection,
        // not to the framework's AsyncLocal.
        httpContext.Features.Get<ITenantContextFeature>().ShouldNotBeNull();
        httpContext.Features.Get<ITenantContextFeature>()!.Tenant!.Id.ShouldBe(id);
    }

    [Fact]
    public void HttpContextActive_Dispose_Restores_Previous_Feature()
    {
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        DefaultHttpContext httpContext = new();
        accessor.HttpContext.Returns(httpContext);
        CurrentTenant tenant = new(accessor);

        IDisposable scope = tenant.Change(Guid.NewGuid(), "Acme");
        scope.Dispose();

        tenant.IsAvailable.ShouldBeFalse();
        httpContext.Features.Get<ITenantContextFeature>().ShouldBeNull();
    }

    [Fact]
    public void HttpContextActive_Two_Parallel_Requests_Are_Isolated()
    {
        // Core safety property: the tenant value lives on the request's
        // FeatureCollection, so two parallel HTTP requests cannot read each
        // other's tenant — by construction, no shared static state.
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        CurrentTenant tenant = new(accessor);
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        DefaultHttpContext requestA = new();
        DefaultHttpContext requestB = new();
        requestA.Features.Set<ITenantContextFeature>(new TenantContextFeature(new TenantInfo(tenantA, "A")));
        requestB.Features.Set<ITenantContextFeature>(new TenantContextFeature(new TenantInfo(tenantB, "B")));

        accessor.HttpContext.Returns(requestA);
        tenant.Id.ShouldBe(tenantA);

        accessor.HttpContext.Returns(requestB);
        tenant.Id.ShouldBe(tenantB);

        accessor.HttpContext.Returns((HttpContext?)null);
        tenant.IsAvailable.ShouldBeFalse();
    }

    [Fact]
    public void HttpContextActive_AsyncLocal_Fallback_Is_Not_Consulted()
    {
        // Defense-in-depth: even if the legacy AsyncLocal is set (legitimately
        // by some deeper non-HTTP code path), the HTTP context wins. The two
        // backends never alias.
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        DefaultHttpContext httpContext = new();
        accessor.HttpContext.Returns(httpContext);
        CurrentTenant httpTenant = new(accessor);
        CurrentTenant asyncLocalTenant = new();

        var asyncLocalId = Guid.NewGuid();
        var httpId = Guid.NewGuid();

        using IDisposable _outer = asyncLocalTenant.Change(asyncLocalId, "AsyncLocal");
        using IDisposable _inner = httpTenant.Change(httpId, "Http");

        httpTenant.Id.ShouldBe(httpId, "HTTP backend reads from HttpContext.Features");
        asyncLocalTenant.Id.ShouldBe(asyncLocalId, "non-HTTP instance still sees its AsyncLocal value");
    }

    [Fact]
    public void HttpContextNull_Falls_Back_To_AsyncLocal()
    {
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);
        CurrentTenant tenant = new(accessor);
        var id = Guid.NewGuid();

        using IDisposable _ = tenant.Change(id, "Job");

        tenant.Id.ShouldBe(id, "no HttpContext active → AsyncLocal fallback used");
    }
}
