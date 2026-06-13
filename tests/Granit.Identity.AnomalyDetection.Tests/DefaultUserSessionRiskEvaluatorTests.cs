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

        IUserSessionRiskStore store = Substitute.For<IUserSessionRiskStore>();
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
            detector, store, TimeProvider.System, tenant, options, bus);

        await evaluator.EvaluateAsync(Candidate(), [], Ct);

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
}
