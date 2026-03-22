using System.Reflection;
using Granit.Bff.Endpoints.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Bff.Endpoints.Tests.Extensions;

public sealed class BffEndpointRouteBuilderExtensionsTests
{
    [Fact]
    public void MapGranitBffEndpoints_IsPublicExtensionMethod()
    {
        // Verify the extension method exists and is publicly accessible via reflection.
        // A full integration test requires a configured WebApplication with OIDC,
        // which is out of scope for unit tests. This validates the API surface.
        MethodInfo? method = typeof(BffEndpointRouteBuilderExtensions)
            .GetMethod(nameof(BffEndpointRouteBuilderExtensions.MapGranitBffEndpoints));

        method.ShouldNotBeNull();
        method.IsStatic.ShouldBeTrue();
        method.IsPublic.ShouldBeTrue();
    }

    [Fact]
    public void BffSecurityHeadersMiddleware_CanBeInstantiated()
    {
        // Verify the middleware can be instantiated with a RequestDelegate
        var middleware = new BffSecurityHeadersMiddleware(_ => Task.CompletedTask);

        middleware.ShouldNotBeNull();
    }
}
