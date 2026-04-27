using Granit.Domain;
using Granit.Mergeable;
using Granit.Mergeable.Exceptions;
using Granit.Parties.Domain;
using Granit.Parties.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Granit.Parties.Tests.Domain;

/// <summary>
/// Unit tests for Party.MergeFrom + GetConflicts. The orchestrator (#1284) is exercised
/// separately ; here we focus on per-field conflict resolution rules.
/// </summary>
public sealed class PartyMergeTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    [Fact]
    public void MergeFrom_DifferentTenants_Throws()
    {
        Party survivor = NewIndividual(Guid.NewGuid(), "Alice");
        Party loser = NewIndividual(Guid.NewGuid(), "Alice");

        Should.Throw<MergeException>(() => survivor.MergeFrom(loser, MergeFieldChoices.Empty))
            .Message.ShouldContain("Tenant mismatch");
    }

    [Fact]
    public void MergeFrom_DifferentKinds_Throws()
    {
        Party survivor = NewParty(PartyKind.Individual, "Alice");
        Party loser = NewParty(PartyKind.Company, "Acme");

        Should.Throw<MergeException>(() => survivor.MergeFrom(loser, MergeFieldChoices.Empty))
            .Message.ShouldContain("Kind mismatch");
    }

    [Fact]
    public void MergeFrom_DifferentCurrencies_Throws()
    {
        Party survivor = NewParty(PartyKind.Company, "Acme", currency: "EUR");
        Party loser = NewParty(PartyKind.Company, "Acme", currency: "USD");

        Should.Throw<MergeException>(() => survivor.MergeFrom(loser, MergeFieldChoices.Empty))
            .Message.ShouldContain("Currency mismatch");
    }

    [Fact]
    public void MergeFrom_SurvivorNotActive_Throws()
    {
        Party survivor = NewParty(PartyKind.Company, "Acme");
        survivor.Suspend();
        Party loser = NewParty(PartyKind.Company, "Acme dup");

        Should.Throw<MergeException>(() => survivor.MergeFrom(loser, MergeFieldChoices.Empty))
            .Message.ShouldContain("Survivor status must be Active");
    }

    [Fact]
    public void MergeFrom_LoserArchived_Throws()
    {
        Party survivor = NewParty(PartyKind.Company, "Acme");
        Party loser = NewParty(PartyKind.Company, "Acme dup");
        loser.Archive();

        Should.Throw<MergeException>(() => survivor.MergeFrom(loser, MergeFieldChoices.Empty))
            .Message.ShouldContain("Archived");
    }

    [Fact]
    public void MergeFrom_DefaultSurvivorWins_OnScalarFields()
    {
        Party survivor = NewParty(PartyKind.Company, "Acme Corp",
            website: "https://acme.com", taxId: "BE0123456789");
        Party loser = NewParty(PartyKind.Company, "Acme Corporation",
            website: "https://acmecorp.com", taxId: "BE9999999999");

        survivor.MergeFrom(loser, MergeFieldChoices.Empty);

        survivor.Name.ShouldBe("Acme Corp");
        survivor.Website.ShouldBe("https://acme.com");
        survivor.TaxId.ShouldBe("BE0123456789");
    }

    [Fact]
    public void MergeFrom_LoserOverride_FlipsScalarField()
    {
        Party survivor = NewParty(PartyKind.Company, "Old Acme");
        Party loser = NewParty(PartyKind.Company, "New Acme Corp");

        survivor.MergeFrom(
            loser,
            MergeFieldChoices.NewBuilder().With("Name", WinnerSide.Loser).Build());

        survivor.Name.ShouldBe("New Acme Corp");
    }

    [Fact]
    public void MergeFrom_RolesAlwaysUnion_NeverOverridable()
    {
        Party survivor = NewParty(PartyKind.Company, "Acme", roles: PartyRoles.Customer);
        Party loser = NewParty(PartyKind.Company, "Acme",
            roles: PartyRoles.Supplier | PartyRoles.Employee);

        // Even with explicit override to "loser", we still union.
        survivor.MergeFrom(
            loser,
            MergeFieldChoices.NewBuilder().With("Roles", WinnerSide.Loser).Build());

        survivor.Roles.HasFlag(PartyRoles.Customer).ShouldBeTrue();
        survivor.Roles.HasFlag(PartyRoles.Supplier).ShouldBeTrue();
        survivor.Roles.HasFlag(PartyRoles.Employee).ShouldBeTrue();
    }

    [Fact]
    public void MergeFrom_TaxStatus_PrefersNonStandard_WhenSurvivorIsStandard()
    {
        Party survivor = NewParty(PartyKind.Company, "Acme");
        Party loser = NewParty(PartyKind.Company, "Acme");
        loser.SetTaxStatus(TaxStatus.Create(reverseCharge: true, vatin: "BE0123456789"));

        survivor.MergeFrom(loser, MergeFieldChoices.Empty);

        survivor.TaxStatus.ReverseCharge.ShouldBeTrue();
    }

    [Fact]
    public void MergeFrom_TaxStatus_KeepsSurvivor_WhenLoserIsStandard()
    {
        Party survivor = NewParty(PartyKind.Company, "Acme");
        survivor.SetTaxStatus(TaxStatus.Create(isExempt: true));
        Party loser = NewParty(PartyKind.Company, "Acme");

        survivor.MergeFrom(loser, MergeFieldChoices.Empty);

        survivor.TaxStatus.IsExempt.ShouldBeTrue();
    }

    [Fact]
    public void MergeFrom_UserId_TransfersWhenSurvivorNullAndLoserHasOne()
    {
        Party survivor = NewParty(PartyKind.Individual, "Alice");
        Party loser = NewParty(PartyKind.Individual, "Alice");
        var userId = Guid.NewGuid();
        loser.LinkToUser(userId);

        survivor.MergeFrom(loser, MergeFieldChoices.Empty);

        survivor.UserId.ShouldBe(userId);
    }

    [Fact]
    public void MergeFrom_Metadata_MergesDictionaries_SurvivorWinsOnConflict()
    {
        Party survivor = NewParty(PartyKind.Company, "Acme");
        survivor.SetMetadataValue("segment", "enterprise");
        survivor.SetMetadataValue("source", "crm");
        Party loser = NewParty(PartyKind.Company, "Acme");
        loser.SetMetadataValue("segment", "smb");
        loser.SetMetadataValue("region", "EMEA");

        survivor.MergeFrom(loser, MergeFieldChoices.Empty);

        IReadOnlyDictionary<string, string> meta = survivor.GetMetadata();
        meta["segment"].ShouldBe("enterprise");
        meta["source"].ShouldBe("crm");
        meta["region"].ShouldBe("EMEA");
    }

    [Fact]
    public void MergeFrom_Metadata_PerKeyOverride_FlipsOneEntry()
    {
        Party survivor = NewParty(PartyKind.Company, "Acme");
        survivor.SetMetadataValue("segment", "enterprise");
        Party loser = NewParty(PartyKind.Company, "Acme");
        loser.SetMetadataValue("segment", "smb");

        survivor.MergeFrom(
            loser,
            MergeFieldChoices.NewBuilder()
                .With("Metadata.segment", WinnerSide.Loser)
                .Build());

        survivor.GetMetadataValue("segment").ShouldBe("smb");
    }

    [Fact]
    public void MergeFrom_InternalNotes_DefaultAppendsWithSeparator()
    {
        Party survivor = NewParty(PartyKind.Company, "Acme");
        survivor.SetInternalNotes("VIP customer");
        Party loser = NewParty(PartyKind.Company, "Acme");
        loser.SetInternalNotes("Created via Odoo sync 2026-01-15");

        survivor.MergeFrom(loser, MergeFieldChoices.Empty);

        survivor.InternalNotes.ShouldNotBeNull();
        survivor.InternalNotes!.ShouldContain("VIP customer");
        survivor.InternalNotes.ShouldContain("--- merged from");
        survivor.InternalNotes.ShouldContain("Created via Odoo sync 2026-01-15");
    }

    [Fact]
    public void MergeFrom_InternalNotes_LoserWinsOverride_ReplacesEntirely()
    {
        Party survivor = NewParty(PartyKind.Company, "Acme");
        survivor.SetInternalNotes("Outdated notes");
        Party loser = NewParty(PartyKind.Company, "Acme");
        loser.SetInternalNotes("Fresh authoritative notes");

        survivor.MergeFrom(
            loser,
            MergeFieldChoices.NewBuilder().With("InternalNotes", WinnerSide.Loser).Build());

        survivor.InternalNotes.ShouldBe("Fresh authoritative notes");
    }

    [Fact]
    public void GetConflicts_ScalarDifference_AppearsInList()
    {
        Party survivor = NewParty(PartyKind.Company, "Acme Corp");
        Party loser = NewParty(PartyKind.Company, "Acme Corporation");

        IReadOnlyList<FieldConflict> conflicts = survivor.GetConflicts(loser);

        conflicts.ShouldContain(c =>
            c.FieldPath == "Name"
            && (string?)c.SurvivorValue == "Acme Corp"
            && (string?)c.LoserValue == "Acme Corporation"
            && c.Default == WinnerSide.Survivor);
    }

    [Fact]
    public void GetConflicts_TaxStatus_DefaultIsLoser_WhenSurvivorIsStandard()
    {
        Party survivor = NewParty(PartyKind.Company, "Acme");
        Party loser = NewParty(PartyKind.Company, "Acme");
        loser.SetTaxStatus(TaxStatus.Create(reverseCharge: true, vatin: "BE0123456789"));

        IReadOnlyList<FieldConflict> conflicts = survivor.GetConflicts(loser);

        FieldConflict ts = conflicts.Single(c => c.FieldPath == "TaxStatus");
        ts.Default.ShouldBe(WinnerSide.Loser);
    }

    [Fact]
    public void GetConflicts_NoDifference_ReturnsEmpty()
    {
        Party survivor = NewParty(PartyKind.Company, "Same");
        Party loser = NewParty(PartyKind.Company, "Same");

        IReadOnlyList<FieldConflict> conflicts = survivor.GetConflicts(loser);

        conflicts.ShouldBeEmpty();
    }

    // ── helpers ───────────────────────────────────────────────────

    private static Party NewIndividual(Guid tenantId, string name) =>
        Party.Create(Guid.NewGuid(), tenantId, PartyKind.Individual, name, "EUR");

    private static Party NewParty(
        PartyKind kind,
        string name,
        string currency = "EUR",
        string? website = null,
        string? taxId = null,
        PartyRoles roles = PartyRoles.Customer) =>
        Party.Create(
            Guid.NewGuid(), Tenant, kind, name, currency,
            roles: roles,
            website: website,
            taxId: taxId);
}
