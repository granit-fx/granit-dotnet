using Granit.Documents.Endpoints.Quotas.Endpoints;
using Shouldly;
using Xunit;

namespace Granit.Documents.Endpoints.Tests.Quotas;

/// <summary>
/// Unit tests for the F7.3 quota endpoint helper. The handler itself is exercised by
/// the Postgres integration test in
/// <c>Granit.Documents.EntityFrameworkCore.Tests.Integration</c>; this file pins the
/// percent-used calculation that the response DTO carries.
/// </summary>
public sealed class QuotaEndpointsTests
{
    [Theory]
    [InlineData(0L, 1000L, 0d)]
    [InlineData(500L, 1000L, 50d)]
    [InlineData(995L, 1000L, 99.5d)]
    [InlineData(1000L, 1000L, 100d)]
    [InlineData(1500L, 1000L, 100d)] // clamped at 100% on overshoot
    [InlineData(0L, 0L, 0d)] // pathological: zero limit → zero, no DivideByZero
    public void ComputePercentUsed_RoundsTwoDecimals_ClampsAt100(long usage, long limit, double expected)
    {
        QuotaEndpoints.ComputePercentUsed(usage, limit).ShouldBe(expected);
    }
}
