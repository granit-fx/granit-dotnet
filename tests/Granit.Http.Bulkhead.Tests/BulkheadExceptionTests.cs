using Granit.Http.Bulkhead.Exceptions;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace Granit.Http.Bulkhead.Tests;

public sealed class BulkheadExceptionTests
{
    // =========================================================================
    // BulkheadRejectedException
    // =========================================================================

    [Fact]
    public void BulkheadRejectedException_Properties()
    {
        var ex = new BulkheadRejectedException("api", permitLimit: 20, queueLimit: 10);

        ex.PolicyName.ShouldBe("api");
        ex.PermitLimit.ShouldBe(20);
        ex.QueueLimit.ShouldBe(10);
        ex.Message.ShouldContain("api");
        ex.Message.ShouldContain("20");
        ex.Message.ShouldContain("10");
    }

    // =========================================================================
    // Status code mapper
    // =========================================================================

    [Fact]
    public void StatusCodeMapper_BulkheadRejectedException_Returns503()
    {
        var mapper = new BulkheadExceptionStatusCodeMapper();
        var ex = new BulkheadRejectedException("api", 10, 5);

        int? statusCode = mapper.TryGetStatusCode(ex);

        statusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public void StatusCodeMapper_OtherException_ReturnsNull()
    {
        var mapper = new BulkheadExceptionStatusCodeMapper();

        int? statusCode = mapper.TryGetStatusCode(new InvalidOperationException());

        statusCode.ShouldBeNull();
    }
}
