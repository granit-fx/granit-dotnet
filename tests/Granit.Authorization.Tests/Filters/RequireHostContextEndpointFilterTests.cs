// =============================================================================
// Tests - RequireHostContextEndpointFilter
// =============================================================================
// Verifies that the filter (a) authenticates host-mode callers, (b) signals the
// data layer via IHostAccessFeature so EfStoreBase can distinguish a deliberate
// .AllowHostAccess() bypass from an accidental tenant-context loss.
// =============================================================================

using System.Security.Claims;
using Granit.Authorization.Filters;
using Granit.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Tests.Filters;

public sealed class RequireHostContextEndpointFilterTests
{
    private readonly RequireHostContextEndpointFilter _filter = new();

    [Fact]
    public async Task PassesThrough_WhenTenantIsActive_NoFeatureSet()
    {
        // Tenant active = normal request, filter is a no-op. The feature must NOT
        // be set so EfStoreBase doesn't mis-tag the metric on a tenant-scoped read.
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(true);
        EndpointFilterInvocationContext context = BuildContext(tenant, authenticated: true);

        bool nextCalled = false;
        await _filter.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return new ValueTask<object?>("ok");
        });

        nextCalled.ShouldBeTrue();
        context.HttpContext.Features.Get<IHostAccessFeature>().ShouldBeNull();
    }

    [Fact]
    public async Task PassesThrough_WhenNoMultiTenancyModule_NoFeatureSet()
    {
        // No ICurrentTenant in DI = single-tenant deployment, filter is a no-op.
        EndpointFilterInvocationContext context = BuildContext(currentTenant: null, authenticated: true);

        await _filter.InvokeAsync(context, _ => new ValueTask<object?>("ok"));

        context.HttpContext.Features.Get<IHostAccessFeature>().ShouldBeNull();
    }

    [Fact]
    public async Task Returns401_WhenHostMode_AndUnauthenticated()
    {
        // Host mode without auth must be rejected — anonymous callers cannot
        // reach cross-tenant data even on .AllowHostAccess() endpoints.
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);
        EndpointFilterInvocationContext context = BuildContext(tenant, authenticated: false);

        object? result = await _filter.InvokeAsync(context, _ => new ValueTask<object?>("should-not-run"));

        result.ShouldBeOfType<ProblemHttpResult>()
            .StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        context.HttpContext.Features.Get<IHostAccessFeature>().ShouldBeNull();
    }

    [Fact]
    public async Task SetsHostAccessFeature_WhenHostMode_AndAuthenticated()
    {
        // The signal contract: host mode + authenticated = IHostAccessFeature
        // is set on the request feature collection so EfStoreBase tags the
        // metric origin=host_endpoint instead of implicit_unsignaled.
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);
        EndpointFilterInvocationContext context = BuildContext(tenant, authenticated: true);

        await _filter.InvokeAsync(context, _ => new ValueTask<object?>("ok"));

        IHostAccessFeature? feature = context.HttpContext.Features.Get<IHostAccessFeature>();
        feature.ShouldNotBeNull();
        feature.IsHostAccess.ShouldBeTrue();
    }

    private static DefaultEndpointFilterInvocationContext BuildContext(
        ICurrentTenant? currentTenant,
        bool authenticated)
    {
        ServiceCollection services = new();
        if (currentTenant is not null)
        {
            services.AddSingleton(currentTenant);
        }
        ServiceProvider sp = services.BuildServiceProvider();

        DefaultHttpContext httpContext = new() { RequestServices = sp };
        if (authenticated)
        {
            httpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "u1")], "test"));
        }

        return new DefaultEndpointFilterInvocationContext(httpContext);
    }
}
