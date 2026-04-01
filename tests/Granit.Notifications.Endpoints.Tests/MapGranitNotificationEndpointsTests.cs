using Granit.Notifications.Endpoints.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Endpoints.Tests;

public sealed class MapGranitNotificationsTests
{
    [Fact]
    public void MapGranitNotifications_WithDefaultOptions_ReturnsEndpointRouteBuilder()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        WebApplication app = builder.Build();

        IEndpointRouteBuilder result = app.MapGranitNotifications();

        result.ShouldNotBeNull();
    }

    [Fact]
    public void MapGranitNotifications_WithCustomPrefix_ReturnsEndpointRouteBuilder()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        WebApplication app = builder.Build();

        IEndpointRouteBuilder result = app.MapGranitNotifications(
            opts => opts.RoutePrefix = "notif");

        result.ShouldNotBeNull();
    }

    [Fact]
    public void MapGranitNotifications_WithNullConfigure_DoesNotThrow()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        WebApplication app = builder.Build();

        Should.NotThrow(() => app.MapGranitNotifications(configure: null));
    }
}
