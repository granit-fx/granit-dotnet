using Granit.Events;
using Granit.Identity.AnomalyDetection.Internal;
using Granit.Identity.AnomalyDetection.Options;
using Granit.IpGeolocation;
using Granit.MultiTenancy;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.AnomalyDetection.Tests;

/// <summary>
/// Verifies the raw-IP opt-in gate on the published <see cref="SuspiciousUserSessionDetectedEto"/>:
/// location-only by default, raw IP carried only when <see cref="IdentityAnomalyDetectionOptions.IncludeClientIpInAlert"/>
/// is enabled.
/// </summary>
public sealed class DefaultUserSessionRiskEvaluatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 10, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static UserSessionDescriptor Candidate() =>
        new("s-new", "user-1", IsCurrent: true, Now, null, "ua", "203.0.113.7",
            new GeoLocation { City = "Brussels", CountryCode = "BE" });

    private static async Task<SuspiciousUserSessionDetectedEto?> EvaluateAndCaptureAsync(bool includeIp)
    {
        IUserSessionAnomalyDetector detector = Substitute.For<IUserSessionAnomalyDetector>();
        detector.AssessAsync(Arg.Any<UserSessionDescriptor>(), Arg.Any<IReadOnlyList<UserSessionDescriptor>>(), Arg.Any<CancellationToken>())
            .Returns(new UserSessionRiskAssessment(UserSessionRiskLevel.High, 0.9, ["impossible_travel"]));

        IIdentitySecurityStateStore securityState = Substitute.For<IIdentitySecurityStateStore>();
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);

        IDistributedEventBus bus = Substitute.For<IDistributedEventBus>();
        SuspiciousUserSessionDetectedEto? captured = null;
        await bus.PublishAsync(
            Arg.Do<SuspiciousUserSessionDetectedEto>(e => captured = e),
            Arg.Any<CancellationToken>());

        IOptions<IdentityAnomalyDetectionOptions> options =
            Microsoft.Extensions.Options.Options.Create(
                new IdentityAnomalyDetectionOptions { IncludeClientIpInAlert = includeIp });

        var evaluator = new DefaultUserSessionRiskEvaluator(
            detector, securityState, TimeProvider.System, tenant, options, bus);

        await evaluator.EvaluateAsync(Candidate(), [], cancellationToken: Ct);

        return captured;
    }

    [Fact]
    public async Task EvaluateAsync_WhenIpOptInDisabled_PublishesEtoWithoutRawIp()
    {
        SuspiciousUserSessionDetectedEto? eto = await EvaluateAndCaptureAsync(includeIp: false);

        eto.ShouldNotBeNull();
        eto.IpAddress.ShouldBeNull();
        eto.City.ShouldBe("Brussels");
        eto.CountryCode.ShouldBe("BE");
    }

    [Fact]
    public async Task EvaluateAsync_WhenIpOptInEnabled_PublishesEtoWithRawIp()
    {
        SuspiciousUserSessionDetectedEto? eto = await EvaluateAndCaptureAsync(includeIp: true);

        eto.ShouldNotBeNull();
        eto.IpAddress.ShouldBe("203.0.113.7");
    }

    [Fact]
    public async Task EvaluateAsync_WhenDeviceTrusted_DowngradesMediumToLowAndSkipsAlert()
    {
        IUserSessionAnomalyDetector detector = Substitute.For<IUserSessionAnomalyDetector>();
        detector.AssessAsync(Arg.Any<UserSessionDescriptor>(), Arg.Any<IReadOnlyList<UserSessionDescriptor>>(), Arg.Any<CancellationToken>())
            .Returns(new UserSessionRiskAssessment(UserSessionRiskLevel.Medium, 0.5, ["new_country"]));

        IIdentitySecurityStateStore securityState = Substitute.For<IIdentitySecurityStateStore>();
        UserSessionRiskVerdict? storedVerdict = null;
        await securityState.SetSessionRiskAsync("user-1", "s-new", Arg.Do<UserSessionRiskVerdict>(v => storedVerdict = v), Arg.Any<CancellationToken>());
        securityState.GetDeviceTrustAsync("user-1", "trusted-device", Arg.Any<CancellationToken>())
            .Returns(new DeviceTrustVerdict(DeviceTrustLevel.Remembered, Now, DateTimeOffset.MaxValue, "user_marked"));

        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);
        IDistributedEventBus bus = Substitute.For<IDistributedEventBus>();
        IOptions<IdentityAnomalyDetectionOptions> options =
            Microsoft.Extensions.Options.Options.Create(new IdentityAnomalyDetectionOptions());

        var evaluator = new DefaultUserSessionRiskEvaluator(
            detector, securityState, TimeProvider.System, tenant, options, bus);

        UserSessionRiskAssessment result = await evaluator.EvaluateAsync(Candidate(), [], "trusted-device", Ct);

        // Medium downgraded to Low for the trusted device, with the reason recorded...
        result.Level.ShouldBe(UserSessionRiskLevel.Low);
        result.Reasons.ShouldContain("trusted_device");
        storedVerdict.ShouldNotBeNull();
        storedVerdict.Level.ShouldBe(UserSessionRiskLevel.Low);
        // ...and no suspicious-session alert raised (Low is below the Medium alert threshold).
        await bus.DidNotReceive().PublishAsync(Arg.Any<SuspiciousUserSessionDetectedEto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EvaluateAsync_WhenDeviceTrusted_PreservesHighRisk()
    {
        IUserSessionAnomalyDetector detector = Substitute.For<IUserSessionAnomalyDetector>();
        detector.AssessAsync(Arg.Any<UserSessionDescriptor>(), Arg.Any<IReadOnlyList<UserSessionDescriptor>>(), Arg.Any<CancellationToken>())
            .Returns(new UserSessionRiskAssessment(UserSessionRiskLevel.High, 0.95, ["impossible_travel"]));

        IIdentitySecurityStateStore securityState = Substitute.For<IIdentitySecurityStateStore>();
        securityState.GetDeviceTrustAsync("user-1", "trusted-device", Arg.Any<CancellationToken>())
            .Returns(new DeviceTrustVerdict(DeviceTrustLevel.Strong, Now, DateTimeOffset.MaxValue, "passkey"));

        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);
        IDistributedEventBus bus = Substitute.For<IDistributedEventBus>();
        var evaluator = new DefaultUserSessionRiskEvaluator(
            detector, securityState, TimeProvider.System, tenant,
            Microsoft.Extensions.Options.Options.Create(new IdentityAnomalyDetectionOptions()), bus);

        UserSessionRiskAssessment result = await evaluator.EvaluateAsync(Candidate(), [], "trusted-device", Ct);

        // A trusted device can still be compromised — a strong anomaly is never downgraded.
        result.Level.ShouldBe(UserSessionRiskLevel.High);
        result.Reasons.ShouldNotContain("trusted_device");
    }
}
