using Granit.Features;
using Granit.Features.Exceptions;
using Granit.Http.Features.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.Http.Features.Tests.AspNetCore;

public sealed class RequiresFeatureEndpointFilterTests
{
    private sealed class FakeEndpointFilterInvocationContext(HttpContext httpContext)
        : EndpointFilterInvocationContext
    {
        public override HttpContext HttpContext { get; } = httpContext;
        public override IList<object?> Arguments => [];
        public override T GetArgument<T>(int index) => throw new NotSupportedException();
    }

    private static FakeEndpointFilterInvocationContext BuildContext(IFeatureChecker checker)
    {
        ServiceCollection services = new();
        services.AddSingleton(checker);
        ServiceProvider sp = services.BuildServiceProvider();

        DefaultHttpContext httpContext = new() { RequestServices = sp };
        return new FakeEndpointFilterInvocationContext(httpContext);
    }

    [Fact]
    public async Task InvokeAsync_FeatureEnabled_CallsNext()
    {
        IFeatureChecker checker = Substitute.For<IFeatureChecker>();
        checker.RequireEnabledAsync("App.Feature", Arg.Any<CancellationToken>())
               .Returns(Task.CompletedTask);

        RequiresFeatureEndpointFilter filter = new("App.Feature");
        EndpointFilterInvocationContext context = BuildContext(checker);
        bool nextCalled = false;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(TypedResults.Ok());
        };

        await filter.InvokeAsync(context, next);

        nextCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task InvokeAsync_FeatureDisabled_ThrowsAndDoesNotCallNext()
    {
        IFeatureChecker checker = Substitute.For<IFeatureChecker>();
        checker.RequireEnabledAsync("App.Feature", Arg.Any<CancellationToken>())
               .ThrowsAsync(new FeatureNotEnabledException("App.Feature"));

        RequiresFeatureEndpointFilter filter = new("App.Feature");
        EndpointFilterInvocationContext context = BuildContext(checker);
        bool nextCalled = false;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(TypedResults.Ok());
        };

        Func<Task> act = async () => await filter.InvokeAsync(context, next);

        await Should.ThrowAsync<FeatureNotEnabledException>(act);
        nextCalled.ShouldBeFalse();
    }
}
