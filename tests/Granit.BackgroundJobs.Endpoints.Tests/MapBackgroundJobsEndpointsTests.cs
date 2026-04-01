using Granit.BackgroundJobs.Endpoints.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.Endpoints.Tests;

public sealed class MapGranitBackgroundJobsTests
{
    [Fact]
    public void MapGranitBackgroundJobs_WithDefaultOptions_ReturnsRouteGroupBuilder()
    {
        // Arrange
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        WebApplication app = builder.Build();

        // Act
        RouteGroupBuilder group = app.MapGranitBackgroundJobs();

        // Assert
        group.ShouldNotBeNull();
    }

    [Fact]
    public void MapGranitBackgroundJobs_WithCustomPrefix_ReturnsRouteGroupBuilder()
    {
        // Arrange
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        WebApplication app = builder.Build();

        // Act
        RouteGroupBuilder group = app.MapGranitBackgroundJobs(
            opts => opts.RoutePrefix = "admin/jobs");

        // Assert
        group.ShouldNotBeNull();
    }

}
