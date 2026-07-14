using System.Net;
using System.Net.Http.Json;
using Granit.Http.Cookies.Endpoints.Dtos;
using Granit.Http.Cookies.Endpoints.Extensions;
using Granit.Http.Cookies.Endpoints.Validators;
using Granit.Testing.Endpoints;

namespace Granit.Http.Cookies.Endpoints.Tests;

/// <summary>
/// HTTP-level tests for <c>POST /cookies/consent</c> driven through
/// <see cref="GranitEndpointTestHost"/>: anonymous 204 happy path, capture semantics
/// (ledger record contents, deterministic clock), 422 validation paths, and the
/// rate-limiting guard on this pre-auth endpoint.
/// </summary>
public sealed class ConsentDecisionEndpointsTests
{
    private const string Route = "/cookies/consent";
    private static readonly DateTimeOffset Now = new(2026, 7, 14, 9, 30, 0, TimeSpan.Zero);

    private readonly IConsentLedger _ledger = Substitute.For<IConsentLedger>();

    private Task<GranitEndpointTestHost> StartAsync(int? permitLimit = null) =>
        GranitEndpointTestHost.StartAsync(
            configureServices: services =>
            {
                services.AddAuthorizationBuilder();
                services.AddSingleton(_ledger);
                services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now));
                services.AddScoped<IValidator<ConsentDecisionRequest>, ConsentDecisionRequestValidator>();

                // The endpoint carries .RequireGranitRateLimiting; its filter resolves the
                // TenantPartitionedRateLimiter, so the service must be registered even when
                // no policy is configured (CheckAsync then no-ops).
                services.AddMetrics();
                services.AddSingleton(Substitute.For<ICurrentTenant>());
                services.AddSingleton(Substitute.For<ICurrentUserService>());
                services.AddGranitRateLimiting(options =>
                {
                    if (permitLimit is int limit)
                    {
                        options.Policies[CookieConsentRateLimitPolicies.Record] =
                            new RateLimitPolicyOptions { PermitLimit = limit };
                    }
                });
            },
            configureEndpoints: app => app.MapGranitCookieConsent());

    [Fact]
    public async Task Post_ValidDecision_Returns204_AndAppendsLedgerRecord()
    {
        // Arrange
        await using GranitEndpointTestHost host = await StartAsync();
        HttpClient client = host.CreateAnonymousClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");
        CookieConsentRecord? recorded = null;
        await _ledger.RecordAsync(
            Arg.Do<CookieConsentRecord>(r => recorded = r), Arg.Any<CancellationToken>());

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(
            Route,
            new ConsentDecisionRequest(
                GrantedCategories: ["strictly_necessary", "analytics"],
                DeniedCategories: ["marketing"]),
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        recorded.ShouldNotBeNull();
        recorded!.GrantedCategories.ShouldBe(["strictly_necessary", "analytics"]);
        recorded.DeniedCategories.ShouldBe(["marketing"]);
        recorded.Mode.ShouldBe(CookieConsentMode.OptIn); // GDPR-safe default when omitted
        recorded.CmpSource.ShouldBe("cookieconsent");
        recorded.DecidedAt.ShouldBe(Now);
        recorded.UserAgent.ShouldBe("Mozilla/5.0");
    }

    [Fact]
    public async Task Post_WithExplicitModeAndCmpSource_CapturesThem()
    {
        // Arrange
        await using GranitEndpointTestHost host = await StartAsync();
        HttpClient client = host.CreateAnonymousClient();
        CookieConsentRecord? recorded = null;
        await _ledger.RecordAsync(
            Arg.Do<CookieConsentRecord>(r => recorded = r), Arg.Any<CancellationToken>());

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(
            Route,
            new ConsentDecisionRequest(
                DeniedCategories: ["sale_or_sharing"],
                Mode: CookieConsentMode.OptOut,
                CmpSource: "axeptio"),
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        recorded.ShouldNotBeNull();
        recorded!.Mode.ShouldBe(CookieConsentMode.OptOut);
        recorded.CmpSource.ShouldBe("axeptio");
        recorded.GrantedCategories.ShouldBeEmpty();
    }

    [Fact]
    public async Task Post_UnknownCategory_Returns422_AndRecordsNothing()
    {
        // Arrange
        await using GranitEndpointTestHost host = await StartAsync();
        HttpClient client = host.CreateAnonymousClient();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(
            Route,
            new ConsentDecisionRequest(GrantedCategories: ["tracking"]),
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("Cookies:Validation:UnknownCategory");
        await _ledger.DidNotReceiveWithAnyArgs()
            .RecordAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Post_OverlappingCategories_Returns422()
    {
        // Arrange
        await using GranitEndpointTestHost host = await StartAsync();
        HttpClient client = host.CreateAnonymousClient();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(
            Route,
            new ConsentDecisionRequest(
                GrantedCategories: ["analytics"],
                DeniedCategories: ["analytics"]),
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Post_EmptyDecision_Returns422()
    {
        // Arrange
        await using GranitEndpointTestHost host = await StartAsync();
        HttpClient client = host.CreateAnonymousClient();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(
            Route, new ConsentDecisionRequest(), TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Post_OverThePolicyLimit_Returns429()
    {
        // Arrange — one permit: the second decision in the window is throttled.
        await using GranitEndpointTestHost host = await StartAsync(permitLimit: 1);
        HttpClient client = host.CreateAnonymousClient();
        ConsentDecisionRequest request = new(GrantedCategories: ["analytics"]);

        // Act
        HttpResponseMessage first = await client.PostAsJsonAsync(
            Route, request, TestContext.Current.CancellationToken);
        HttpResponseMessage second = await client.PostAsJsonAsync(
            Route, request, TestContext.Current.CancellationToken);

        // Assert
        first.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        second.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        second.Headers.RetryAfter.ShouldNotBeNull();
    }
}
