using Granit.Http.ApiVersioning.Deprecation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Http.ApiVersioning.Tests.Deprecation;

public sealed class DeprecationEndpointFilterTests
{
    private readonly DeprecationEndpointFilter _filter = new(
        NullLoggerFactory.Instance.CreateLogger<DeprecationEndpointFilter>());

    [Fact]
    public async Task No_metadata_skips_headers()
    {
        // Arrange — endpoint without DeprecatedAttribute
        DefaultHttpContext httpContext = CreateHttpContext();
        EndpointFilterInvocationContext context = CreateContext(httpContext);
        EndpointFilterDelegate next = _ => ValueTask.FromResult<object?>("ok");

        // Act
        object? result = await _filter.InvokeAsync(context, next);

        // Assert
        result.ShouldBe("ok");
        httpContext.Response.Headers.ContainsKey("Deprecation").ShouldBeFalse();
    }

    [Fact]
    public async Task Deprecated_attribute_emits_deprecation_header()
    {
        // Arrange
        DefaultHttpContext httpContext = CreateHttpContext(new DeprecatedAttribute());
        EndpointFilterInvocationContext context = CreateContext(httpContext);
        EndpointFilterDelegate next = _ => ValueTask.FromResult<object?>("ok");

        // Act
        await _filter.InvokeAsync(context, next);

        // Assert
        httpContext.Response.Headers["Deprecation"].ToString().ShouldBe("true");
        httpContext.Response.Headers.ContainsKey("Sunset").ShouldBeFalse();
        httpContext.Response.Headers.ContainsKey("Link").ShouldBeFalse();
    }

    [Fact]
    public async Task SunsetDate_emits_sunset_header_in_rfc7231_format()
    {
        // Arrange
        var attr = new DeprecatedAttribute { SunsetDate = "2025-11-01" };
        DefaultHttpContext httpContext = CreateHttpContext(attr);
        EndpointFilterInvocationContext context = CreateContext(httpContext);
        EndpointFilterDelegate next = _ => ValueTask.FromResult<object?>("ok");

        // Act
        await _filter.InvokeAsync(context, next);

        // Assert
        httpContext.Response.Headers["Deprecation"].ToString().ShouldBe("true");
        httpContext.Response.Headers["Sunset"].ToString().ShouldBe("Sat, 01 Nov 2025 00:00:00 GMT");
    }

    [Fact]
    public async Task Link_emits_link_header_with_deprecation_rel()
    {
        // Arrange
        var attr = new DeprecatedAttribute { Link = "https://docs.example.com/migration/v1-to-v2" };
        DefaultHttpContext httpContext = CreateHttpContext(attr);
        EndpointFilterInvocationContext context = CreateContext(httpContext);
        EndpointFilterDelegate next = _ => ValueTask.FromResult<object?>("ok");

        // Act
        await _filter.InvokeAsync(context, next);

        // Assert
        httpContext.Response.Headers.Link.ToString()
            .ShouldBe("<https://docs.example.com/migration/v1-to-v2>; rel=\"deprecation\"");
    }

    [Fact]
    public async Task All_headers_emitted_together()
    {
        // Arrange
        var attr = new DeprecatedAttribute
        {
            SunsetDate = "2025-06-15",
            Link = "https://docs.example.com/migration",
        };
        DefaultHttpContext httpContext = CreateHttpContext(attr);
        EndpointFilterInvocationContext context = CreateContext(httpContext);
        EndpointFilterDelegate next = _ => ValueTask.FromResult<object?>("ok");

        // Act
        await _filter.InvokeAsync(context, next);

        // Assert
        httpContext.Response.Headers["Deprecation"].ToString().ShouldBe("true");
        httpContext.Response.Headers["Sunset"].ToString().ShouldBe("Sun, 15 Jun 2025 00:00:00 GMT");
        httpContext.Response.Headers.Link.ToString()
            .ShouldBe("<https://docs.example.com/migration>; rel=\"deprecation\"");
    }

    [Fact]
    public async Task Invalid_sunset_date_skips_sunset_header()
    {
        // Arrange
        var attr = new DeprecatedAttribute { SunsetDate = "not-a-date" };
        DefaultHttpContext httpContext = CreateHttpContext(attr);
        EndpointFilterInvocationContext context = CreateContext(httpContext);
        EndpointFilterDelegate next = _ => ValueTask.FromResult<object?>("ok");

        // Act
        await _filter.InvokeAsync(context, next);

        // Assert
        httpContext.Response.Headers["Deprecation"].ToString().ShouldBe("true");
        httpContext.Response.Headers.ContainsKey("Sunset").ShouldBeFalse();
    }

    [Fact]
    public async Task Next_delegate_is_always_called()
    {
        // Arrange
        var attr = new DeprecatedAttribute { SunsetDate = "2025-12-31" };
        DefaultHttpContext httpContext = CreateHttpContext(attr);
        EndpointFilterInvocationContext context = CreateContext(httpContext);
        bool nextCalled = false;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>("result");
        };

        // Act
        object? result = await _filter.InvokeAsync(context, next);

        // Assert
        nextCalled.ShouldBeTrue();
        result.ShouldBe("result");
    }

    private static DefaultHttpContext CreateHttpContext(DeprecatedAttribute? deprecated = null)
    {
        DefaultHttpContext httpContext = new();

        EndpointMetadataCollection metadata = deprecated is not null
            ? new EndpointMetadataCollection(deprecated)
            : new EndpointMetadataCollection();

        Endpoint endpoint = new(null, metadata, "TestEndpoint");
        httpContext.SetEndpoint(endpoint);
        httpContext.Request.Path = "/api/v1/test";

        return httpContext;
    }

    private static DefaultEndpointFilterInvocationContext CreateContext(DefaultHttpContext httpContext) =>
        new(httpContext);
}
