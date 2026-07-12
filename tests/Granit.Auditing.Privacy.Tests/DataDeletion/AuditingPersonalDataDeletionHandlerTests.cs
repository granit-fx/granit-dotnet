using Granit.Auditing.Options;
using Granit.Auditing.Privacy.DataDeletion;
using Granit.Auditing.Privacy.DataExport;
using Granit.Privacy.DataDeletion;
using Granit.Privacy.DataDeletion.Events;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Privacy.Tests.DataDeletion;

public sealed class AuditingPersonalDataDeletionHandlerTests
{
    private static readonly Guid User = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Tenant = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly IAuditingCleaner _cleaner = Substitute.For<IAuditingCleaner>();

    private static PersonalDataDeletionRequestedEto Eto(Guid? tenantId) =>
        new(Guid.NewGuid(), User, "admin", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), "account deletion", "GDPR", tenantId);

    [Fact]
    public async Task HandleAsync_default_pseudonymizes_and_acknowledges_with_the_registered_provider_name()
    {
        PersonalDataDeletionRequestedEto eto = Eto(Tenant);
        _cleaner.PseudonymizeByUserAsync(User.ToString(), Arg.Any<CancellationToken>()).Returns(7);

        PersonalDataDeletedEto ack = await AuditingPersonalDataDeletionHandler.HandleAsync(
            eto,
            _cleaner,
            Microsoft.Extensions.Options.Options.Create(new AuditingOptions()),
            TestContext.Current.CancellationToken);

        // Auditing is a registered privacy provider (for Art. 15 export) so the saga waits on its
        // acknowledgement. The audit events themselves are retained by law (Art. 17(3)(b)), but the
        // subject's direct identifiers are pseudonymized in place by default.
        ack.RequestId.ShouldBe(eto.RequestId);
        ack.ProviderName.ShouldBe(AuditingPrivacyDataProvider.ProviderName);
        ack.Action.ShouldBe(DeletionAction.Anonymized);
        ack.AffectedRecords.ShouldBe(7);
        ack.TenantId.ShouldBe(Tenant);
        await _cleaner.Received(1).PseudonymizeByUserAsync(User.ToString(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_pseudonymize_disabled_retains_trail_and_still_acknowledges()
    {
        PersonalDataDeletionRequestedEto eto = Eto(Tenant);
        AuditingOptions options = new() { PseudonymizeOnErasure = false };

        PersonalDataDeletedEto ack = await AuditingPersonalDataDeletionHandler.HandleAsync(
            eto,
            _cleaner,
            Microsoft.Extensions.Options.Options.Create(options),
            TestContext.Current.CancellationToken);

        // It must still ack (with Retained) to drain the saga's pending set, otherwise every
        // deletion would time out to PartiallyExecuted.
        ack.RequestId.ShouldBe(eto.RequestId);
        ack.ProviderName.ShouldBe(AuditingPrivacyDataProvider.ProviderName);
        ack.Action.ShouldBe(DeletionAction.Retained);
        ack.AffectedRecords.ShouldBe(0);
        ack.TenantId.ShouldBe(Tenant);
        await _cleaner.DidNotReceive().PseudonymizeByUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_propagates_the_events_tenant_when_absent()
    {
        PersonalDataDeletedEto ack = await AuditingPersonalDataDeletionHandler.HandleAsync(
            Eto(tenantId: null),
            _cleaner,
            Microsoft.Extensions.Options.Options.Create(new AuditingOptions()),
            TestContext.Current.CancellationToken);

        ack.TenantId.ShouldBeNull();
    }
}
