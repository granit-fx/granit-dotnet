using Granit.RateLimiting.Exceptions;
using Shouldly;
using Xunit;

namespace Granit.RateLimiting.Tests;

public sealed class RateLimitExceptionTests
{
    [Fact]
    public void RateLimitExceededException_SetsProperties()
    {
        var retryAfter = TimeSpan.FromSeconds(42);

        var ex = new RateLimitExceededException("api", retryAfter, 100, 0);

        ex.PolicyName.ShouldBe("api");
        ex.RetryAfter.ShouldBe(retryAfter);
        ex.Limit.ShouldBe(100);
        ex.Remaining.ShouldBe(0);
        ex.Message.ShouldContain("api");
        ex.Message.ShouldContain("42");
    }
}
