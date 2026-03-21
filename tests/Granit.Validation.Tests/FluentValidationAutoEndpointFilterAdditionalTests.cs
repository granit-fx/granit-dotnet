// Results.Ok() is used as a mock return value in EndpointFilterDelegate — not an endpoint.
#pragma warning disable GRAPI001

using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Validation.Tests;

public sealed class FluentValidationAutoEndpointFilterAdditionalTests
{
    // -------------------------------------------------------------------------
    // DateTime argument is skipped
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InvokeAsync_DateTimeArgument_CallsNext()
    {
        FluentValidationAutoEndpointFilter filter = new();

        bool nextCalled = false;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        };

        DefaultEndpointFilterInvocationContext context =
            CreateContext(argument: DateTime.UtcNow);

        await filter.InvokeAsync(context, next);

        nextCalled.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // DateTimeOffset argument is skipped
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InvokeAsync_DateTimeOffsetArgument_CallsNext()
    {
        FluentValidationAutoEndpointFilter filter = new();

        bool nextCalled = false;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        };

        DefaultEndpointFilterInvocationContext context =
            CreateContext(argument: DateTimeOffset.UtcNow);

        await filter.InvokeAsync(context, next);

        nextCalled.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // DateOnly argument is skipped
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InvokeAsync_DateOnlyArgument_CallsNext()
    {
        FluentValidationAutoEndpointFilter filter = new();

        bool nextCalled = false;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        };

        DefaultEndpointFilterInvocationContext context =
            CreateContext(argument: DateOnly.FromDateTime(DateTime.UtcNow));

        await filter.InvokeAsync(context, next);

        nextCalled.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // TimeOnly argument is skipped
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InvokeAsync_TimeOnlyArgument_CallsNext()
    {
        FluentValidationAutoEndpointFilter filter = new();

        bool nextCalled = false;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        };

        DefaultEndpointFilterInvocationContext context =
            CreateContext(argument: TimeOnly.FromDateTime(DateTime.UtcNow));

        await filter.InvokeAsync(context, next);

        nextCalled.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Decimal argument is skipped
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InvokeAsync_DecimalArgument_CallsNext()
    {
        FluentValidationAutoEndpointFilter filter = new();

        bool nextCalled = false;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        };

        DefaultEndpointFilterInvocationContext context =
            CreateContext(argument: 42.5m);

        await filter.InvokeAsync(context, next);

        nextCalled.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Enum argument is skipped
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InvokeAsync_EnumArgument_CallsNext()
    {
        FluentValidationAutoEndpointFilter filter = new();

        bool nextCalled = false;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        };

        DefaultEndpointFilterInvocationContext context =
            CreateContext(argument: DayOfWeek.Monday);

        await filter.InvokeAsync(context, next);

        nextCalled.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Boolean (primitive) argument is skipped
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InvokeAsync_BoolArgument_CallsNext()
    {
        FluentValidationAutoEndpointFilter filter = new();

        bool nextCalled = false;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        };

        DefaultEndpointFilterInvocationContext context =
            CreateContext(argument: true);

        await filter.InvokeAsync(context, next);

        nextCalled.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static DefaultEndpointFilterInvocationContext CreateContext(object argument)
    {
        ServiceCollection services = new();
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        DefaultHttpContext httpContext = new()
        {
            RequestServices = serviceProvider,
        };

        return new DefaultEndpointFilterInvocationContext(httpContext, argument);
    }
}
