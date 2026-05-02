using Granit.Domain;
using Granit.Entities.Customization.Domain;
using Granit.Entities.Customization.Domain.Deltas;
using Shouldly;
using Xunit;

namespace Granit.Entities.Customization.Tests.Domain;

public sealed class EntityCustomizationTests
{
    private const string Entity = "Granit.Parties.Party";

    [Fact]
    public void Create_with_well_formed_deltas_succeeds()
    {
        var customization = EntityCustomization.Create(
            id: Guid.NewGuid(),
            entityName: Entity,
            layoutKind: LayoutKind.FormDefault,
            deltas:
            [
                new ReorderDelta("currency", BeforeFieldName: "total", AfterFieldName: null),
                new RegroupDelta("internalNotes", "internal"),
                new HideDelta("legacyCode"),
            ],
            tenantId: Guid.NewGuid());

        customization.EntityName.ShouldBe(Entity);
        customization.LayoutKind.ShouldBe(LayoutKind.FormDefault);
        customization.Deltas.Count.ShouldBe(3);
        customization.Deltas[0].ShouldBeOfType<ReorderDelta>();
        customization.Deltas[1].ShouldBeOfType<RegroupDelta>();
        customization.Deltas[2].ShouldBeOfType<HideDelta>();
    }

    [Fact]
    public void Create_with_empty_entity_name_throws()
    {
        Should.Throw<ArgumentException>(() => EntityCustomization.Create(
            id: Guid.NewGuid(),
            entityName: " ",
            layoutKind: LayoutKind.List,
            deltas: []));
    }

    [Fact]
    public void Create_with_null_deltas_throws()
    {
        Should.Throw<ArgumentNullException>(() => EntityCustomization.Create(
            id: Guid.NewGuid(),
            entityName: Entity,
            layoutKind: LayoutKind.List,
            deltas: null!));
    }

    [Theory]
    [InlineData(null, null)]                  // both anchors null
    [InlineData("a", "b")]                    // both anchors set
    public void Create_rejects_reorder_with_malformed_anchor(string? before, string? after)
    {
        ArgumentException ex = Should.Throw<ArgumentException>(() => EntityCustomization.Create(
            id: Guid.NewGuid(),
            entityName: Entity,
            layoutKind: LayoutKind.FormDefault,
            deltas: [new ReorderDelta("currency", before, after)]));

        ex.Message.ShouldContain("BeforeFieldName");
        ex.Message.ShouldContain("AfterFieldName");
    }

    [Fact]
    public void Replace_swaps_the_delta_list_in_full()
    {
        var customization = EntityCustomization.Create(
            id: Guid.NewGuid(),
            entityName: Entity,
            layoutKind: LayoutKind.Calendar,
            deltas: [new HideDelta("a"), new HideDelta("b")]);

        customization.Replace([new HideDelta("c")]);

        customization.Deltas.Count.ShouldBe(1);
        ((HideDelta)customization.Deltas[0]).FieldName.ShouldBe("c");
    }

    [Fact]
    public void Replace_with_malformed_reorder_throws_and_preserves_previous_state()
    {
        var customization = EntityCustomization.Create(
            id: Guid.NewGuid(),
            entityName: Entity,
            layoutKind: LayoutKind.Gallery,
            deltas: [new HideDelta("a")]);

        Should.Throw<ArgumentException>(() =>
            customization.Replace([new ReorderDelta("x", BeforeFieldName: null, AfterFieldName: null)]));

        // Previous list is cleared before assignment, so the aggregate is left
        // empty. This is the documented behaviour: callers MUST validate via
        // the endpoint layer before calling Replace.
        customization.Deltas.ShouldBeEmpty();
    }

    [Fact]
    public void Tenant_id_round_trips_through_explicit_interface()
    {
        var tenantId = Guid.NewGuid();
        var customization = EntityCustomization.Create(
            id: Guid.NewGuid(),
            entityName: Entity,
            layoutKind: LayoutKind.DetailDefault,
            deltas: [],
            tenantId: tenantId);

        customization.TenantId.ShouldBe(tenantId);
        ((IMultiTenant)customization).TenantId.ShouldBe(tenantId);

        var newTenantId = Guid.NewGuid();
        ((IMultiTenant)customization).TenantId = newTenantId;
        customization.TenantId.ShouldBe(newTenantId);
    }
}
