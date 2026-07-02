using Granit.Auditing.Privacy.DataDeletion;
using Granit.Auditing.Privacy.DataExport;
using Granit.Privacy.DataDeletion;
using Granit.Privacy.DataDeletion.Events;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Privacy.Tests.DataDeletion;

public sealed class AuditingPersonalDataDeletionHandlerTests
{
    private static readonly Guid User = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Tenant = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static PersonalDataDeletionRequestedEto Eto(Guid? tenantId) =>
        new(Guid.NewGuid(), User, "admin", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), "account deletion", "GDPR", tenantId);

    [Fact]
    public void Handle_acknowledges_retention_with_the_registered_provider_name()
    {
        PersonalDataDeletionRequestedEto eto = Eto(Tenant);

        PersonalDataDeletedEto ack = AuditingPersonalDataDeletionHandler.Handle(eto);

        // Auditing is a registered privacy provider (for Art. 15 export) so the saga waits on its
        // acknowledgement. It never erases — the immutable audit trail is retained by law — so it
        // must still ack (with Retained) to drain the saga's pending set, otherwise every deletion
        // would time out to PartiallyExecuted.
        ack.RequestId.ShouldBe(eto.RequestId);
        ack.ProviderName.ShouldBe(AuditingPrivacyDataProvider.ProviderName);
        ack.Action.ShouldBe(DeletionAction.Retained);
        ack.AffectedRecords.ShouldBe(0);
        ack.TenantId.ShouldBe(Tenant);
    }

    [Fact]
    public void Handle_propagates_the_events_tenant_when_absent()
    {
        PersonalDataDeletedEto ack = AuditingPersonalDataDeletionHandler.Handle(Eto(tenantId: null));

        ack.TenantId.ShouldBeNull();
    }
}
