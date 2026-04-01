using Granit.Authorization.Endpoints.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Endpoints.Tests;

public sealed class MapGranitAuthorizationTests
{
    [Fact]
    public void MapGranitAuthorization_WithDefaultOptions_ReturnsRouteGroupBuilder()
    {
        // Arrange
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        WebApplication app = builder.Build();

        // Act
        RouteGroupBuilder group = app.MapGranitAuthorization();

        // Assert
        group.ShouldNotBeNull();
    }

    [Fact]
    public void MapGranitAuthorization_WithCustomPrefix_ReturnsRouteGroupBuilder()
    {
        // Arrange
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        WebApplication app = builder.Build();

        // Act
        RouteGroupBuilder group = app.MapGranitAuthorization(
            opts => opts.RoutePrefix = "authorization");

        // Assert
        group.ShouldNotBeNull();
    }

}
