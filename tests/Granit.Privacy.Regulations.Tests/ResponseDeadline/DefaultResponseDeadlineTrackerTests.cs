using Granit.Privacy.Regulations.ResponseDeadline;
using Granit.Privacy.Regulations.ResponseDeadline.Internal;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Regulations.Tests.ResponseDeadline;

public sealed class DefaultResponseDeadlineTrackerTests
{
    private readonly DefaultResponseDeadlineTracker _sut = new();

    private static PrivacyRegulationProfile CreateProfile(
        int sarDays = 30, int? deletionDays = null, int? rectificationDays = null) => new()
        {
            Regulation = PrivacyRegulation.EuGdpr,
            DisplayName = "Test",
            JurisdictionCode = "XX",
            ConsentModel = ConsentModel.OptIn,
            AvailableLegalBases = [],
            SubjectAccessRequestDays = sarDays,
            DeletionRequestDays = deletionDays,
            RectificationRequestDays = rectificationDays,
            DefaultDeletionGracePeriodDays = 30,
            MaxDeletionGracePeriodDays = 90,
            CookieConsentModel = ConsentModel.OptIn,
        };

    [Fact]
    public async Task Sar_GdprProfile_Returns30Days()
    {
        DateTimeOffset requestedAt = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

        DateTimeOffset deadline = await _sut.CalculateDeadlineAsync(
            PrivacyRequestType.SubjectAccessRequest, CreateProfile(sarDays: 30), requestedAt, TestContext.Current.CancellationToken);

        deadline.ShouldBe(requestedAt.AddDays(30));
    }

    [Fact]
    public async Task Sar_LgpdProfile_Returns15Days()
    {
        DateTimeOffset requestedAt = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

        DateTimeOffset deadline = await _sut.CalculateDeadlineAsync(
            PrivacyRequestType.SubjectAccessRequest, CreateProfile(sarDays: 15), requestedAt, TestContext.Current.CancellationToken);

        deadline.ShouldBe(requestedAt.AddDays(15));
    }

    [Fact]
    public async Task Deletion_UsesSpecificDays_WhenSet()
    {
        DateTimeOffset requestedAt = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

        DateTimeOffset deadline = await _sut.CalculateDeadlineAsync(
            PrivacyRequestType.DeletionRequest, CreateProfile(sarDays: 30, deletionDays: 45), requestedAt, TestContext.Current.CancellationToken);

        deadline.ShouldBe(requestedAt.AddDays(45));
    }

    [Fact]
    public async Task Deletion_FallsBackToSar_WhenNull()
    {
        DateTimeOffset requestedAt = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

        DateTimeOffset deadline = await _sut.CalculateDeadlineAsync(
            PrivacyRequestType.DeletionRequest, CreateProfile(sarDays: 30, deletionDays: null), requestedAt, TestContext.Current.CancellationToken);

        deadline.ShouldBe(requestedAt.AddDays(30));
    }

    [Fact]
    public async Task Rectification_UsesSpecificDays_WhenSet()
    {
        DateTimeOffset requestedAt = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

        DateTimeOffset deadline = await _sut.CalculateDeadlineAsync(
            PrivacyRequestType.RectificationRequest, CreateProfile(sarDays: 30, rectificationDays: 15), requestedAt, TestContext.Current.CancellationToken);

        deadline.ShouldBe(requestedAt.AddDays(15));
    }

    [Fact]
    public async Task OptOut_FallsBackToSar_WhenNoDeletionDays()
    {
        DateTimeOffset requestedAt = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

        DateTimeOffset deadline = await _sut.CalculateDeadlineAsync(
            PrivacyRequestType.OptOut, CreateProfile(sarDays: 45), requestedAt, TestContext.Current.CancellationToken);

        deadline.ShouldBe(requestedAt.AddDays(45));
    }
}
