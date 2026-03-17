using Granit.Http.Cookies.Endpoints.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Shouldly;
using Xunit;

namespace Granit.Http.Cookies.Endpoints.Tests;

public sealed class MapGranitCookieConsentTests
{
    [Fact]
    public void MapGranitCookieConsent_WithDefaultOptions_ReturnsEndpointRouteBuilder()
    {
        // Arrange
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        WebApplication app = builder.Build();

        // Act
        IEndpointRouteBuilder result = app.MapGranitCookieConsent();

        // Assert
        result.ShouldNotBeNull();
    }

    [Fact]
    public void MapGranitCookieConsent_WithCustomPrefix_ReturnsEndpointRouteBuilder()
    {
        // Arrange
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        WebApplication app = builder.Build();

        // Act
        IEndpointRouteBuilder result = app.MapGranitCookieConsent(
            opts => opts.RoutePrefix = "api/consent");

        // Assert
        result.ShouldNotBeNull();
    }
}
