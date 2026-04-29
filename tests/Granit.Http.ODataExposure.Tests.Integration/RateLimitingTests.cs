using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Http.ODataExposure.Tests.Integration;

/// <summary>
/// C3b (#1392) — rate limiting acceptance tests. The OData route group is
/// gated by the <c>"granit-odata"</c> policy registered in
/// <see cref="ODataTestApp"/>; a suite-specific tight cap exercises the
/// 429 path without slowing the rest of the test matrix.
/// </summary>
public sealed class RateLimitingTests(PostgresFixture postgres)
    : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres = postgres;
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private ODataTestApp _app = null!;

    public async ValueTask InitializeAsync()
    {
        // PermitLimit = 5 over a 1-minute window — small enough to trigger
        // 429 deterministically inside the test, large enough that the
        // surrounding HTTP plumbing doesn't hit it.
        _app = await ODataTestApp.CreateAsync(
            _postgres.ConnectionString,
            configureEntitySet: null,
            rateLimitPermitLimit: 5);

        using IServiceScope scope = _app.CreateScope();
        TestDbContext db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        db.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = TenantA,
            Number = "RL-001",
            Amount = 100m,
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task SixthRequest_InASingleMinute_Returns429_WithRetryAfter()
    {
        // Acceptance criterion #5 from #1392 — past the cap, the response is
        // 429 + Retry-After. The five preceding requests succeed because
        // they're under the limit.
        for (int i = 0; i < 5; i++)
        {
            HttpResponseMessage allowed = await SendAsync();
            allowed.StatusCode.ShouldBe(HttpStatusCode.OK,
                $"request {i + 1} (under cap) must be accepted");
        }

        HttpResponseMessage denied = await SendAsync();

        denied.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        denied.Headers.Contains("Retry-After").ShouldBeTrue(
            "the Granit rate limiter sets Retry-After on 429 responses");
    }

    [Fact]
    public async Task RateLimitHeaders_PresentOnAcceptedRequests()
    {
        // Acceptance criterion: every accepted request carries
        // X-RateLimit-Limit and X-RateLimit-Remaining so the BI tool can
        // see how much budget it has left without hitting the cap.
        HttpResponseMessage response = await SendAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Contains("X-RateLimit-Limit").ShouldBeTrue();
        response.Headers.Contains("X-RateLimit-Remaining").ShouldBeTrue();
    }

    private async Task<HttpResponseMessage> SendAsync()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/granit/odata/Invoices");
        request.Headers.Add("X-Test-Tenant", TenantA.ToString());
        return await _app.Client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
