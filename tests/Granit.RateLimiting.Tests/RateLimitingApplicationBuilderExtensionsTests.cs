using Granit.RateLimiting.Extensions;
using Microsoft.AspNetCore.Builder;
using Shouldly;
using Xunit;

namespace Granit.RateLimiting.Tests;

public sealed class RateLimitingApplicationBuilderExtensionsTests
{
    [Fact]
    public void UseGranitRateLimiting_ReturnsSameBuilder()
    {
        WebApplicationBuilder webAppBuilder = WebApplication.CreateBuilder();
        WebApplication app = webAppBuilder.Build();

        IApplicationBuilder result = app.UseGranitRateLimiting();

        result.ShouldBeSameAs(app);
    }
}
