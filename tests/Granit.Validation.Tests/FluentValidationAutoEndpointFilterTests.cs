// =============================================================================
// Tests - FluentValidationAutoEndpointFilter
// =============================================================================
// Verifies:
//   - Valid request passes through to next delegate
//   - Invalid request returns 422 with ValidationProblemDetails
//   - No validator registered → passes through (graceful degradation)
//   - Primitive / string arguments are skipped
//   - [SkipAutoValidation] metadata skips validation
//   - Multiple errors returned (CascadeMode.Continue via GranitValidator)
//   - Null arguments are skipped
// =============================================================================

// Results.Ok() is used as a mock return value in EndpointFilterDelegate — not an endpoint.
#pragma warning disable GRAPI001

using FluentValidation;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Validation.Tests;

public sealed class FluentValidationAutoEndpointFilterTests
{
    // -------------------------------------------------------------------------
    // Valid request → passes through
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InvokeAsync_ValidRequest_CallsNext()
    {
        TestRequest request = new("Alice", 25);
        FluentValidationAutoEndpointFilter filter = new();

        bool nextCalled = false;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        };

        DefaultEndpointFilterInvocationContext context =
            CreateContext(request, withValidator: true);

        await filter.InvokeAsync(context, next);

        nextCalled.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Invalid request → 422
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InvokeAsync_InvalidRequest_Returns422()
    {
        TestRequest request = new("", -1);
        FluentValidationAutoEndpointFilter filter = new();

        bool nextCalled = false;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        };

        DefaultEndpointFilterInvocationContext context =
            CreateContext(request, withValidator: true);

        object? result = await filter.InvokeAsync(context, next);

        nextCalled.ShouldBeFalse();
        ProblemHttpResult pr = result.ShouldBeOfType<ProblemHttpResult>();
        pr.StatusCode.ShouldBe(StatusCodes.Status422UnprocessableEntity);
        HttpValidationProblemDetails vpd = pr.ProblemDetails.ShouldBeOfType<HttpValidationProblemDetails>();
        vpd.Errors.ShouldContainKey(nameof(TestRequest.Name));
        vpd.Errors.ShouldContainKey(nameof(TestRequest.Age));
    }

    // -------------------------------------------------------------------------
    // No validator registered → passes through
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InvokeAsync_NoValidator_CallsNext()
    {
        TestRequest request = new("", -1);
        FluentValidationAutoEndpointFilter filter = new();

        bool nextCalled = false;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        };

        DefaultEndpointFilterInvocationContext context =
            CreateContext(request, withValidator: false);

        await filter.InvokeAsync(context, next);

        nextCalled.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Primitive arguments are skipped
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InvokeAsync_PrimitiveArgument_CallsNext()
    {
        FluentValidationAutoEndpointFilter filter = new();

        bool nextCalled = false;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        };

        DefaultEndpointFilterInvocationContext context =
            CreateContext(argument: 42, withValidator: false);

        await filter.InvokeAsync(context, next);

        nextCalled.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // String arguments are skipped
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InvokeAsync_StringArgument_CallsNext()
    {
        FluentValidationAutoEndpointFilter filter = new();

        bool nextCalled = false;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        };

        DefaultEndpointFilterInvocationContext context =
            CreateContext(argument: "some string", withValidator: false);

        await filter.InvokeAsync(context, next);

        nextCalled.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Guid arguments are skipped
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InvokeAsync_GuidArgument_CallsNext()
    {
        FluentValidationAutoEndpointFilter filter = new();

        bool nextCalled = false;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        };

        DefaultEndpointFilterInvocationContext context =
            CreateContext(argument: Guid.NewGuid(), withValidator: false);

        await filter.InvokeAsync(context, next);

        nextCalled.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Null arguments are skipped
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InvokeAsync_NullArgument_CallsNext()
    {
        FluentValidationAutoEndpointFilter filter = new();

        bool nextCalled = false;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        };

        DefaultEndpointFilterInvocationContext context =
            CreateContext(argument: null!, withValidator: true);

        await filter.InvokeAsync(context, next);

        nextCalled.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // [SkipAutoValidation] → skips validation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InvokeAsync_SkipAutoValidation_CallsNext()
    {
        TestRequest request = new("", -1);
        FluentValidationAutoEndpointFilter filter = new();

        bool nextCalled = false;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        };

        DefaultEndpointFilterInvocationContext context =
            CreateContext(request, withValidator: true, skipAutoValidation: true);

        await filter.InvokeAsync(context, next);

        nextCalled.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Multiple errors returned (CascadeMode.Continue via GranitValidator)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InvokeAsync_MultipleErrors_ReturnsAllErrors()
    {
        TestRequest request = new("", -1);
        FluentValidationAutoEndpointFilter filter = new();

        EndpointFilterDelegate next = _ => ValueTask.FromResult<object?>(Results.Ok());

        DefaultEndpointFilterInvocationContext context =
            CreateContext(request, withValidator: true);

        object? result = await filter.InvokeAsync(context, next);

        ProblemHttpResult pr = result.ShouldBeOfType<ProblemHttpResult>();
        HttpValidationProblemDetails vpd = pr.ProblemDetails.ShouldBeOfType<HttpValidationProblemDetails>();
        vpd.Errors.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    // -------------------------------------------------------------------------
    // CancellationToken argument is skipped
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InvokeAsync_CancellationTokenArgument_CallsNext()
    {
        FluentValidationAutoEndpointFilter filter = new();

        bool nextCalled = false;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        };

        DefaultEndpointFilterInvocationContext context =
            CreateContext(argument: CancellationToken.None, withValidator: false);

        await filter.InvokeAsync(context, next);

        nextCalled.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static DefaultEndpointFilterInvocationContext CreateContext(
        object argument, bool withValidator, bool skipAutoValidation = false)
    {
        ServiceCollection services = new();

        if (withValidator)
        {
            services.AddSingleton<IValidator<TestRequest>, TestRequestValidator>();
        }

        ServiceProvider serviceProvider = services.BuildServiceProvider();

        DefaultHttpContext httpContext = new()
        {
            RequestServices = serviceProvider,
        };

        if (skipAutoValidation)
        {
            Endpoint endpoint = new(
                _ => Task.CompletedTask,
                new EndpointMetadataCollection(new SkipAutoValidationAttribute()),
                "TestEndpoint");
            httpContext.SetEndpoint(endpoint);
        }

        return new DefaultEndpointFilterInvocationContext(httpContext, argument);
    }

    private sealed record TestRequest(string Name, int Age);

    private sealed class TestRequestValidator : GranitValidator<TestRequest>
    {
        public TestRequestValidator()
        {
            RuleFor(x => x.Name).NotEmpty();
            RuleFor(x => x.Age).GreaterThan(0);
        }
    }
}
