using System.Diagnostics.Metrics;
using Granit.AI;
using Granit.AI.RateLimiting;
using Granit.Identity.AnomalyDetection.Diagnostics;
using Granit.Identity.AnomalyDetection.Internal;
using Granit.Identity.AnomalyDetection.Options;
using Granit.IpGeolocation;
using Granit.MultiTenancy;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.AnomalyDetection.Tests;

public sealed class UserSessionAnomalyDetectorTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);

    // Brussels and Sydney — ~16,000 km apart.
    private static readonly GeoLocation Brussels = new() { City = "Brussels", CountryCode = "BE", Latitude = 50.85, Longitude = 4.35 };
    private static readonly GeoLocation Paris = new() { City = "Paris", CountryCode = "FR", Latitude = 48.85, Longitude = 2.35 };
    private static readonly GeoLocation Sydney = new() { City = "Sydney", CountryCode = "AU", Latitude = -33.87, Longitude = 151.2 };

    private const string Desktop = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/140";
    private const string Mobile = "Mozilla/5.0 (iPhone; CPU iPhone OS 19_0) Safari/605";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly IStructuredCompletion _structuredCompletion = Substitute.For<IStructuredCompletion>();
    private readonly IAICallRateLimiter _rateLimiter = Substitute.For<IAICallRateLimiter>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();

    [Fact]
    public async Task AssessAsync_NoHistory_ReturnsNone()
    {
        UserSessionAnomalyDetector sut = CreateDetector();
        UserSessionRiskAssessment result = await sut.AssessAsync(Session("s1", Brussels, Desktop, Now), [], Ct);

        result.Level.ShouldBe(UserSessionRiskLevel.None);
    }

    [Fact]
    public async Task AssessAsync_ImpossibleTravel_ReturnsHigh()
    {
        UserSessionAnomalyDetector sut = CreateDetector();
        UserSessionDescriptor candidate = Session("s2", Brussels, Desktop, Now);
        UserSessionDescriptor prior = Session("s1", Sydney, Desktop, Now.AddHours(-1));

        UserSessionRiskAssessment result = await sut.AssessAsync(candidate, [prior], Ct);

        result.Level.ShouldBe(UserSessionRiskLevel.High);
        result.Reasons.ShouldContain("impossible_travel");
    }

    [Fact]
    public async Task AssessAsync_NewCountrySameDevice_ReturnsLow()
    {
        UserSessionAnomalyDetector sut = CreateDetector();
        UserSessionDescriptor candidate = Session("s2", Brussels, Desktop, Now);
        UserSessionDescriptor prior = Session("s1", Paris, Desktop, Now.AddHours(-10)); // plausible travel

        UserSessionRiskAssessment result = await sut.AssessAsync(candidate, [prior], Ct);

        result.Level.ShouldBe(UserSessionRiskLevel.Low);
        result.Reasons.ShouldContain("new_country");
    }

    [Fact]
    public async Task AssessAsync_NewCountryAndNewDevice_ReturnsMedium()
    {
        UserSessionAnomalyDetector sut = CreateDetector();
        UserSessionDescriptor candidate = Session("s2", Brussels, Mobile, Now);
        UserSessionDescriptor prior = Session("s1", Paris, Desktop, Now.AddHours(-10));

        UserSessionRiskAssessment result = await sut.AssessAsync(candidate, [prior], Ct);

        result.Level.ShouldBe(UserSessionRiskLevel.Medium);
        result.Reasons.ShouldContain("new_country");
        result.Reasons.ShouldContain("new_device");
    }

    [Fact]
    public async Task AssessAsync_AiEnabled_MergesWithHeuristicTakingHigher()
    {
        _currentTenant.IsAvailable.Returns(false);
        _rateLimiter.TryAcquireAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(true));
        _structuredCompletion
            .CompleteAsync<UserSessionRiskResponse>(Arg.Any<StructuredCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new StructuredCompletionResult<UserSessionRiskResponse>
            {
                Status = StructuredCompletionStatus.Succeeded,
                Value = new UserSessionRiskResponse { Level = "High", Score = 0.95, Reasons = ["ai_signal"] },
            }));

        UserSessionAnomalyDetector sut = CreateDetector(useAi: true);
        // Heuristic alone would be None (no history), AI says High -> combined High.
        UserSessionRiskAssessment result = await sut.AssessAsync(Session("s1", Brussels, Desktop, Now), [], Ct);

        result.Level.ShouldBe(UserSessionRiskLevel.High);
        result.Reasons.ShouldContain("ai_signal");
    }

    [Fact]
    public async Task AssessAsync_AiTransportFailure_FallsBackToHeuristic()
    {
        _currentTenant.IsAvailable.Returns(false);
        _rateLimiter.TryAcquireAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(true));
        _structuredCompletion
            .CompleteAsync<UserSessionRiskResponse>(Arg.Any<StructuredCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new StructuredCompletionResult<UserSessionRiskResponse>
            {
                Status = StructuredCompletionStatus.TransportFailure,
            }));

        UserSessionAnomalyDetector sut = CreateDetector(useAi: true);
        UserSessionDescriptor candidate = Session("s2", Brussels, Desktop, Now);
        UserSessionDescriptor prior = Session("s1", Sydney, Desktop, Now.AddHours(-1));

        UserSessionRiskAssessment result = await sut.AssessAsync(candidate, [prior], Ct);

        // AI failed, heuristic impossible-travel verdict survives.
        result.Level.ShouldBe(UserSessionRiskLevel.High);
    }

    [Fact]
    public async Task AssessAsync_PerUserBudgetExhausted_SkipsAiAndKeepsHeuristic()
    {
        _currentTenant.IsAvailable.Returns(false);

        // Per-user bucket (checked first) is exhausted; the tenant bucket would still admit. AI must be skipped.
        _rateLimiter.TryAcquireAsync(
                Arg.Is<string>(k => k.StartsWith("user_sessions_anomaly:user:", StringComparison.Ordinal)),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(false));
        _rateLimiter.TryAcquireAsync(
                Arg.Is<string>(k => !k.StartsWith("user_sessions_anomaly:user:", StringComparison.Ordinal)),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(true));

        UserSessionAnomalyDetector sut = CreateDetector(useAi: true);
        UserSessionDescriptor candidate = Session("s2", Brussels, Desktop, Now);
        UserSessionDescriptor prior = Session("s1", Sydney, Desktop, Now.AddHours(-1));

        UserSessionRiskAssessment result = await sut.AssessAsync(candidate, [prior], Ct);

        // The AI layer never ran; the deterministic impossible-travel verdict survives.
        result.Level.ShouldBe(UserSessionRiskLevel.High);
        await _structuredCompletion.DidNotReceive().CompleteAsync<UserSessionRiskResponse>(
            Arg.Any<StructuredCompletionRequest>(), Arg.Any<CancellationToken>());
    }

    private UserSessionAnomalyDetector CreateDetector(bool useAi = false) =>
        new(
            _structuredCompletion,
            _rateLimiter,
            Microsoft.Extensions.Options.Options.Create(new UserSessionsAnomalyDetectionOptions { UseAi = useAi }),
            _currentTenant,
            CreateMetrics());

    private static UserSessionDescriptor Session(string id, GeoLocation location, string userAgent, DateTimeOffset createdAt) =>
        new(id, "user-1", IsCurrent: false, createdAt, LastAccessedAt: createdAt, userAgent, IpAddress: "203.0.113.7", location);

    private static UserSessionsAnomalyDetectionMetrics CreateMetrics()
    {
        IMeterFactory factory = Substitute.For<IMeterFactory>();
        factory.Create(Arg.Any<MeterOptions>()).Returns(call => new Meter(call.Arg<MeterOptions>().Name));
        return new UserSessionsAnomalyDetectionMetrics(factory);
    }
}
