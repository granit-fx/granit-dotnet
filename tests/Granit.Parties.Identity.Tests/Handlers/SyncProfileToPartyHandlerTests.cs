using Granit.Identity.Events;
using Granit.Parties.Domain;
using Granit.Parties.Identity.Handlers;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Parties.Identity.Tests.Handlers;

/// <summary>
/// Locks the contract of <see cref="SyncProfileToPartyHandler"/> per
/// ADR-051 B-step 5.
/// </summary>
public sealed class SyncProfileToPartyHandlerTests
{
    private readonly IPartyReader _reader = Substitute.For<IPartyReader>();
    private readonly IPartyWriter _writer = Substitute.For<IPartyWriter>();

    [Fact]
    public async Task HandleAsync_PropagatesIdentityFieldsToParty()
    {
        var userId = Guid.NewGuid();
        var party = Party.Create(
            id: Guid.NewGuid(),
            tenantId: null,
            kind: PartyKind.Individual,
            name: "Old Name",
            defaultCurrency: "EUR");
        party.LinkToUser(userId);

        _reader.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(party);

        UserProfileChangedEto evt = new(
            UserId: userId,
            DisplayName: "New Name",
            Email: "new@example.com",
            FirstName: "New",
            LastName: "Name",
            PhoneNumber: "+1 555 0100",
            PreferredLocale: "en-GB",
            Timezone: "Europe/London",
            TenantId: null);

        await SyncProfileToPartyHandler.HandleAsync(evt, _reader, _writer, TestContext.Current.CancellationToken);

        party.Name.ShouldBe("New Name");
        party.Language.ShouldBe("en-GB");
        party.Timezone.ShouldBe("Europe/London");
        await _writer.Received(1).UpdateAsync(party, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NoOps_WhenNoPartyLinkedYet()
    {
        // Race window: User exists, EnsurePartyForUserHandler hasn't
        // run yet. The sync handler must not throw — the next profile
        // change after the Party exists picks the changes up.
        var userId = Guid.NewGuid();
        _reader.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((Party?)null);

        UserProfileChangedEto evt = new(
            UserId: userId,
            DisplayName: "Name",
            Email: "user@example.com",
            FirstName: null, LastName: null, PhoneNumber: null,
            PreferredLocale: null, Timezone: null,
            TenantId: null);

        await SyncProfileToPartyHandler.HandleAsync(evt, _reader, _writer, TestContext.Current.CancellationToken);

        await _writer.DidNotReceiveWithAnyArgs().UpdateAsync(default!, CancellationToken.None);
    }
}
