using Granit.Http.ExceptionHandling;
using Granit.Http.RateLimiting.Extensions;
using Granit.Http.RateLimiting.Internal;
using Granit.RateLimiting.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Http.RateLimiting.Tests;

public sealed class RateLimitExceptionStatusCodeMapperTests
{
    [Fact]
    public void StatusCodeMapper_MapsRateLimitExceededException_To429()
    {
        var mapper = new RateLimitExceptionStatusCodeMapper();
        var ex = new RateLimitExceededException("api", TimeSpan.FromSeconds(1), 100, 0);

        int? statusCode = mapper.TryGetStatusCode(ex);

        statusCode.ShouldBe(StatusCodes.Status429TooManyRequests);
    }

    [Fact]
    public void StatusCodeMapper_ReturnsNull_ForOtherExceptions()
    {
        var mapper = new RateLimitExceptionStatusCodeMapper();

        int? statusCode = mapper.TryGetStatusCode(new InvalidOperationException());

        statusCode.ShouldBeNull();
    }

    [Fact]
    public void AddGranitHttpRateLimiting_RegistersExceptionStatusCodeMapper()
    {
        ServiceCollection services = [];

        services.AddGranitHttpRateLimiting();

        ServiceProvider sp = services.BuildServiceProvider();

        IEnumerable<IExceptionStatusCodeMapper> mappers = sp.GetServices<IExceptionStatusCodeMapper>();
        mappers.ShouldContain(m => m is RateLimitExceptionStatusCodeMapper);
    }
}
