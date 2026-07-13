using Granit.Bulkhead.Exceptions;
using Shouldly;
using Xunit;

namespace Granit.Bulkhead.Tests;

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
}
