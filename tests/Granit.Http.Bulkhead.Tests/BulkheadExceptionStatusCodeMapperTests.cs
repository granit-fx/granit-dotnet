using Granit.Bulkhead.Exceptions;
using Granit.Http.Bulkhead.Exceptions;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace Granit.Http.Bulkhead.Tests;

public sealed class BulkheadExceptionStatusCodeMapperTests
{
    [Fact]
    public void TryGetStatusCode_BulkheadRejectedException_Returns503()
    {
        var mapper = new BulkheadExceptionStatusCodeMapper();
        var ex = new BulkheadRejectedException("api", 10, 5);

        int? statusCode = mapper.TryGetStatusCode(ex);

        statusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public void TryGetStatusCode_OtherException_ReturnsNull()
    {
        var mapper = new BulkheadExceptionStatusCodeMapper();

        int? statusCode = mapper.TryGetStatusCode(new InvalidOperationException());

        statusCode.ShouldBeNull();
    }
}
