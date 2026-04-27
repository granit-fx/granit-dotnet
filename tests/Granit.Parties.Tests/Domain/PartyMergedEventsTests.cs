using System.Text.Json;
using Granit.Mergeable;
using Granit.Parties.Domain;
using Granit.Parties.Domain.ValueObjects;
using Granit.Parties.Events;
using Shouldly;
using Xunit;

namespace Granit.Parties.Tests.Domain;

public sealed class PartyMergedEventsTests
{
    private static readonly DateTimeOffset MergedAt = new(2026, 4, 27, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RaiseMergedEvents_EmitsDomainAndDistributedEvents_OnSurvivor()
    {
        Party survivor = NewSurvivor();
        var loserId = PartyId.Create(Guid.NewGuid());
        Dictionary<string, WinnerSide> resolved = new(StringComparer.Ordinal)
        {
            ["Name"] = WinnerSide.Survivor,
            ["TaxStatus"] = WinnerSide.Loser,
        };
        Dictionary<string, int> rewriteCounts = new(StringComparer.Ordinal)
        {
            ["Invoice.PartyId"] = 17,
            ["Subscription.PartyId"] = 3,
        };

        survivor.RaiseMergedEvents(loserId, MergedAt, resolved, rewriteCounts, reparentedChildrenCount: 0);

        PartyMergedEvent merged = survivor.DomainEvents.OfType<PartyMergedEvent>().ShouldHaveSingleItem();
        merged.SurvivorId.Value.ShouldBe(survivor.Id);
        merged.LoserId.ShouldBe(loserId);
        merged.TenantId.ShouldBe(survivor.TenantId);
        merged.ResolvedChoices["Name"].ShouldBe(WinnerSide.Survivor);
        merged.ResolvedChoices["TaxStatus"].ShouldBe(WinnerSide.Loser);
        merged.RewriteCounts["Invoice.PartyId"].ShouldBe(17);

        PartyMergedEto eto = survivor.IntegrationEvents.OfType<PartyMergedEto>().ShouldHaveSingleItem();
        eto.SurvivorId.Value.ShouldBe(survivor.Id);
        eto.LoserId.ShouldBe(loserId);
        eto.MergedAt.ShouldBe(MergedAt);
        eto.ResolvedChoices["Name"].ShouldBe("Survivor");
        eto.ResolvedChoices["TaxStatus"].ShouldBe("Loser");
        eto.RewriteCounts["Subscription.PartyId"].ShouldBe(3);
    }

    [Fact]
    public void RaiseMergedEvents_EmitsChildrenReparented_WhenCountIsPositive()
    {
        Party survivor = NewSurvivor();
        var loserId = PartyId.Create(Guid.NewGuid());

        survivor.RaiseMergedEvents(
            loserId,
            MergedAt,
            new Dictionary<string, WinnerSide>(StringComparer.Ordinal),
            new Dictionary<string, int>(StringComparer.Ordinal),
            reparentedChildrenCount: 5);

        PartyChildrenReparentedEvent reparented = survivor.DomainEvents
            .OfType<PartyChildrenReparentedEvent>()
            .ShouldHaveSingleItem();
        reparented.SurvivorId.Value.ShouldBe(survivor.Id);
        reparented.LoserId.ShouldBe(loserId);
        reparented.TenantId.ShouldBe(survivor.TenantId);
        reparented.Count.ShouldBe(5);
    }

    [Fact]
    public void RaiseMergedEvents_DoesNotEmitChildrenReparented_WhenCountIsZero()
    {
        Party survivor = NewSurvivor();
        var loserId = PartyId.Create(Guid.NewGuid());

        survivor.RaiseMergedEvents(
            loserId,
            MergedAt,
            new Dictionary<string, WinnerSide>(StringComparer.Ordinal),
            new Dictionary<string, int>(StringComparer.Ordinal),
            reparentedChildrenCount: 0);

        survivor.DomainEvents.OfType<PartyChildrenReparentedEvent>().ShouldBeEmpty();
    }

    [Fact]
    public void RaiseMergedEvents_RejectsNegativeChildCount()
    {
        Party survivor = NewSurvivor();
        var loserId = PartyId.Create(Guid.NewGuid());

        Should.Throw<ArgumentOutOfRangeException>(() => survivor.RaiseMergedEvents(
            loserId,
            MergedAt,
            new Dictionary<string, WinnerSide>(StringComparer.Ordinal),
            new Dictionary<string, int>(StringComparer.Ordinal),
            reparentedChildrenCount: -1));
    }

    [Fact]
    public void PartyMergedEto_RoundTripsThroughJson()
    {
        PartyMergedEto original = new(
            SurvivorId: PartyId.Create(Guid.NewGuid()),
            LoserId: PartyId.Create(Guid.NewGuid()),
            TenantId: Guid.NewGuid(),
            MergedAt: MergedAt,
            ResolvedChoices: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Name"] = "Survivor",
                ["Metadata.segment"] = "Loser",
            },
            RewriteCounts: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["Invoice.PartyId"] = 42,
            });

        string json = JsonSerializer.Serialize(original);
        PartyMergedEto? deserialised = JsonSerializer.Deserialize<PartyMergedEto>(json);

        deserialised.ShouldNotBeNull();
        deserialised!.SurvivorId.ShouldBe(original.SurvivorId);
        deserialised.LoserId.ShouldBe(original.LoserId);
        deserialised.TenantId.ShouldBe(original.TenantId);
        deserialised.MergedAt.ShouldBe(original.MergedAt);
        deserialised.ResolvedChoices["Name"].ShouldBe("Survivor");
        deserialised.ResolvedChoices["Metadata.segment"].ShouldBe("Loser");
        deserialised.RewriteCounts["Invoice.PartyId"].ShouldBe(42);
    }

    [Fact]
    public void PartyChildrenReparentedEvent_RoundTripsThroughJson()
    {
        PartyChildrenReparentedEvent original = new(
            SurvivorId: PartyId.Create(Guid.NewGuid()),
            LoserId: PartyId.Create(Guid.NewGuid()),
            TenantId: Guid.NewGuid(),
            Count: 7);

        string json = JsonSerializer.Serialize(original);
        PartyChildrenReparentedEvent? deserialised = JsonSerializer.Deserialize<PartyChildrenReparentedEvent>(json);

        deserialised.ShouldNotBeNull();
        deserialised!.SurvivorId.ShouldBe(original.SurvivorId);
        deserialised.LoserId.ShouldBe(original.LoserId);
        deserialised.TenantId.ShouldBe(original.TenantId);
        deserialised.Count.ShouldBe(7);
    }

    private static Party NewSurvivor()
    {
        var party = Party.Create(
            id: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            kind: PartyKind.Company,
            name: "Acme Survivor",
            defaultCurrency: "EUR");
        party.ClearDomainEvents();
        party.ClearIntegrationEvents();
        return party;
    }
}
