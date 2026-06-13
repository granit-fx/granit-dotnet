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
    private readonly IUserBehavioralProfileStore _profileStore = Substitute.For<IUserBehavioralProfileStore>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();

    public UserSessionAnomalyDetectorTests()
    {
        _timeProvider.GetUtcNow().Returns(Now);
        // Default: empty durable profile, so existing scenarios behave exactly as before (no suppression).
        _profileStore.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(UserBehavioralProfile.Empty);
    }

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

    // ── Durable habitual profile suppresses familiar country/device (the second-residence fix) ──

    [Fact]
    public async Task AssessAsync_HabitualCountry_SuppressesNewCountry()
    {
        // BE is habitual in the durable profile (seen 5×), even though the only active session is in FR.
        _profileStore.GetAsync("user-1", Arg.Any<CancellationToken>()).Returns(new UserBehavioralProfile(
            [new BehavioralObservation(BehavioralObservationKind.Country, "BE", 5, Now.AddDays(-90), Now.AddDays(-5))]));

        UserSessionAnomalyDetector sut = CreateDetector();
        UserSessionDescriptor candidate = Session("s2", Brussels, Desktop, Now);
        UserSessionDescriptor prior = Session("s1", Paris, Desktop, Now.AddHours(-10));

        UserSessionRiskAssessment result = await sut.AssessAsync(candidate, [prior], Ct);

        result.Reasons.ShouldNotContain("new_country");
        result.Level.ShouldBe(UserSessionRiskLevel.None);
    }

    [Fact]
    public async Task AssessAsync_HabitualCountryAndDevice_SuppressesBoth()
    {
        string mobileFamily = DeviceFingerprint.Family(Mobile);
        _profileStore.GetAsync("user-1", Arg.Any<CancellationToken>()).Returns(new UserBehavioralProfile(
        [
            new BehavioralObservation(BehavioralObservationKind.Country, "BE", 5, Now.AddDays(-90), Now.AddDays(-5)),
            new BehavioralObservation(BehavioralObservationKind.DeviceFamily, mobileFamily, 5, Now.AddDays(-90), Now.AddDays(-5)),
        ]));

        UserSessionAnomalyDetector sut = CreateDetector();
        UserSessionDescriptor candidate = Session("s2", Brussels, Mobile, Now);
        UserSessionDescriptor prior = Session("s1", Paris, Desktop, Now.AddHours(-10));

        UserSessionRiskAssessment result = await sut.AssessAsync(candidate, [prior], Ct);

        result.Reasons.ShouldNotContain("new_country");
        result.Reasons.ShouldNotContain("new_device");
        result.Level.ShouldBe(UserSessionRiskLevel.None);
    }

    [Fact]
    public async Task AssessAsync_RecentlySeenCountryBelowFrequency_StillSuppressed()
    {
        // Seen once (< MinObservationsForHabitual = 3) but only 3 days ago (< 30-day recency window) — "you were
        // just here", so it must not re-flag.
        _profileStore.GetAsync("user-1", Arg.Any<CancellationToken>()).Returns(new UserBehavioralProfile(
            [new BehavioralObservation(BehavioralObservationKind.Country, "BE", 1, Now.AddDays(-3), Now.AddDays(-3))]));

        UserSessionAnomalyDetector sut = CreateDetector();
        UserSessionDescriptor candidate = Session("s2", Brussels, Desktop, Now);
        UserSessionDescriptor prior = Session("s1", Paris, Desktop, Now.AddHours(-10));

        UserSessionRiskAssessment result = await sut.AssessAsync(candidate, [prior], Ct);

        result.Reasons.ShouldNotContain("new_country");
    }

    [Fact]
    public async Task AssessAsync_StaleCountryBeyondRetention_StillFlagged()
    {
        // Frequent long ago but last seen beyond the 180-day retention window — no longer counts, so it flags.
        _profileStore.GetAsync("user-1", Arg.Any<CancellationToken>()).Returns(new UserBehavioralProfile(
            [new BehavioralObservation(BehavioralObservationKind.Country, "BE", 5, Now.AddDays(-400), Now.AddDays(-200))]));

        UserSessionAnomalyDetector sut = CreateDetector();
        UserSessionDescriptor candidate = Session("s2", Brussels, Desktop, Now);
        UserSessionDescriptor prior = Session("s1", Paris, Desktop, Now.AddHours(-10));

        UserSessionRiskAssessment result = await sut.AssessAsync(candidate, [prior], Ct);

        result.Reasons.ShouldContain("new_country");
        result.Level.ShouldBe(UserSessionRiskLevel.Low);
    }

    // ── Geo confidence: a low-confidence / anonymising fix must not raise a hard-locked High travel alert ──

    [Fact]
    public async Task AssessAsync_ImpossibleTravelButCoarseFix_SuppressedAsLowGeoConfidence()
    {
        UserSessionAnomalyDetector sut = CreateDetector();
        // Same jump as the High impossible-travel test, but the candidate fix is coarse (500 km > 200 km).
        UserSessionDescriptor candidate = Session("s2", Brussels with { AccuracyRadiusKm = 500 }, Desktop, Now);
        UserSessionDescriptor prior = Session("s1", Sydney, Desktop, Now.AddHours(-1));

        UserSessionRiskAssessment result = await sut.AssessAsync(candidate, [prior], Ct);

        result.Reasons.ShouldNotContain("impossible_travel");
        result.Reasons.ShouldContain("low_geo_confidence");
        // new_country still fires (BE vs AU) → Low; low_geo_confidence must NOT inflate it to Medium/High.
        result.Level.ShouldBe(UserSessionRiskLevel.Low);
    }

    [Fact]
    public async Task AssessAsync_ImpossibleTravelFromVpn_SuppressedAsLowGeoConfidence()
    {
        UserSessionAnomalyDetector sut = CreateDetector();
        UserSessionDescriptor candidate = Session("s2", Brussels with { IsVpn = true }, Desktop, Now);
        UserSessionDescriptor prior = Session("s1", Sydney, Desktop, Now.AddHours(-1));

        UserSessionRiskAssessment result = await sut.AssessAsync(candidate, [prior], Ct);

        result.Reasons.ShouldNotContain("impossible_travel");
        result.Reasons.ShouldContain("low_geo_confidence");
    }

    [Fact]
    public async Task AssessAsync_HighConfidenceJump_StillImpossibleTravel()
    {
        UserSessionAnomalyDetector sut = CreateDetector();
        // A precise fix (small radius, no anonymising flags) must keep firing the High verdict.
        UserSessionDescriptor candidate = Session("s2", Brussels with { AccuracyRadiusKm = 10 }, Desktop, Now);
        UserSessionDescriptor prior = Session("s1", Sydney with { AccuracyRadiusKm = 10 }, Desktop, Now.AddHours(-1));

        UserSessionRiskAssessment result = await sut.AssessAsync(candidate, [prior], Ct);

        result.Reasons.ShouldContain("impossible_travel");
        result.Level.ShouldBe(UserSessionRiskLevel.High);
    }

    [Fact]
    public async Task AssessAsync_AnonymizedIp_NotSuppressed_WhenOptionDisabled()
    {
        UserSessionAnomalyDetector sut = CreateDetector(suppressAnonymizedIp: false);
        UserSessionDescriptor candidate = Session("s2", Brussels with { IsVpn = true }, Desktop, Now);
        UserSessionDescriptor prior = Session("s1", Sydney, Desktop, Now.AddHours(-1));

        UserSessionRiskAssessment result = await sut.AssessAsync(candidate, [prior], Ct);

        result.Reasons.ShouldContain("impossible_travel");
        result.Level.ShouldBe(UserSessionRiskLevel.High);
    }

    private UserSessionAnomalyDetector CreateDetector(bool useAi = false, bool suppressAnonymizedIp = true) =>
        new(
            _structuredCompletion,
            _rateLimiter,
            Microsoft.Extensions.Options.Options.Create(new IdentityAnomalyDetectionOptions
            {
                UseAi = useAi,
                SuppressTravelForAnonymizedIp = suppressAnonymizedIp,
            }),
            _currentTenant,
            _profileStore,
            _timeProvider,
            CreateMetrics());

    private static UserSessionDescriptor Session(string id, GeoLocation location, string userAgent, DateTimeOffset createdAt) =>
        new(id, "user-1", IsCurrent: false, createdAt, LastAccessedAt: createdAt, userAgent, IpAddress: "203.0.113.7", location);

    private static IdentityAnomalyDetectionMetrics CreateMetrics()
    {
        IMeterFactory factory = Substitute.For<IMeterFactory>();
        factory.Create(Arg.Any<MeterOptions>()).Returns(call => new Meter(call.Arg<MeterOptions>().Name));
        return new IdentityAnomalyDetectionMetrics(factory);
    }
}
