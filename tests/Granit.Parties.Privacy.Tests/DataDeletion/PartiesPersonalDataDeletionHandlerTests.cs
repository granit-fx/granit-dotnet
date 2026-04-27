using Granit.DataFiltering;
using Granit.Domain;
using Granit.Events;
using Granit.Parties;
using Granit.Parties.Domain;
using Granit.Parties.Domain.ValueObjects;
using Granit.Parties.Privacy.DataDeletion;
using Granit.Privacy.DataDeletion;
using Granit.Privacy.DataDeletion.Events;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Parties.Privacy.Tests.DataDeletion;

public sealed class ContactsPersonalDataDeletionHandlerTests
{
    private readonly IPartyReader _reader = Substitute.For<IPartyReader>();
    private readonly IPartyWriter _writer = Substitute.For<IPartyWriter>();
    private readonly IDataFilter _dataFilter = Substitute.For<IDataFilter>();
    private readonly IDistributedEventBus _bus = Substitute.For<IDistributedEventBus>();

    public ContactsPersonalDataDeletionHandlerTests()
    {
        _dataFilter.Disable<IMultiTenant>().Returns(Substitute.For<IDisposable>());
    }

    private static PersonalDataDeletionRequestedEto Request(Guid? userId = null) => new(
        RequestId: Guid.NewGuid(),
        UserId: userId ?? Guid.NewGuid(),
        RequestedBy: "tester@example.com",
        RequestedAt: DateTimeOffset.UtcNow,
        Reason: "test",
        Regulation: "GDPR",
        TenantId: null);

    [Fact]
    public async Task HandleAsync_NoLinkedContact_PublishesRetainedAudit()
    {
        PersonalDataDeletionRequestedEto request = Request();
        _reader.GetByUserIdAsync(request.UserId, Arg.Any<CancellationToken>())
            .Returns((Party?)null);

        await PartiesPersonalDataDeletionHandler.HandleAsync(
            request, _reader, _writer, _dataFilter, _bus, TestContext.Current.CancellationToken);

        await _writer.DidNotReceive().UpdateAsync(Arg.Any<Party>(), Arg.Any<CancellationToken>());
        await _bus.Received(1).PublishAsync(
            Arg.Is<PersonalDataDeletedEto>(e =>
                e.RequestId == request.RequestId
                && e.ProviderName == "parties"
                && e.Action == DeletionAction.Retained
                && e.AffectedRecords == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithLinkedContact_PseudonymisesAndPublishesAnonymised()
    {
        PersonalDataDeletionRequestedEto request = Request();
        var contact = Party.Create(
            Guid.NewGuid(), null, PartyKind.Individual, "Jean Dupont", "EUR");
        contact.AddEmail(Guid.NewGuid(), "jean@example.com");
        contact.LinkToUser(request.UserId);

        _reader.GetByUserIdAsync(request.UserId, Arg.Any<CancellationToken>())
            .Returns(contact);

        await PartiesPersonalDataDeletionHandler.HandleAsync(
            request, _reader, _writer, _dataFilter, _bus, TestContext.Current.CancellationToken);

        contact.Name.ShouldBe("[deleted]");
        contact.Emails.ShouldBeEmpty();
        contact.UserId.ShouldBeNull();
        await _writer.Received(1).UpdateAsync(contact, Arg.Any<CancellationToken>());
        await _bus.Received(1).PublishAsync(
            Arg.Is<PersonalDataDeletedEto>(e =>
                e.Action == DeletionAction.Anonymized
                && e.AffectedRecords == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_AlreadyPseudonymised_PublishesRetainedNoUpdate()
    {
        PersonalDataDeletionRequestedEto request = Request();
        var contact = Party.Create(
            Guid.NewGuid(), null, PartyKind.Company, "[deleted]", "EUR");
        // already pseudonymised: no email, no phone, no address, no website, no user link

        _reader.GetByUserIdAsync(request.UserId, Arg.Any<CancellationToken>())
            .Returns(contact);

        await PartiesPersonalDataDeletionHandler.HandleAsync(
            request, _reader, _writer, _dataFilter, _bus, TestContext.Current.CancellationToken);

        await _writer.DidNotReceive().UpdateAsync(Arg.Any<Party>(), Arg.Any<CancellationToken>());
        await _bus.Received(1).PublishAsync(
            Arg.Is<PersonalDataDeletedEto>(e =>
                e.Action == DeletionAction.Retained
                && e.AffectedRecords == 0),
            Arg.Any<CancellationToken>());
    }
}
