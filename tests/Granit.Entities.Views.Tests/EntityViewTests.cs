// =============================================================================
// Tests - EntityView aggregate (Domain)
// =============================================================================

using System.Text.Json.Nodes;
using Granit.Entities.Views.Domain;
using Shouldly;
using Xunit;

namespace Granit.Entities.Views.Tests;

public sealed class EntityViewTests
{
    private static readonly Guid OwnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly JsonObject EmptyState = new();

    [Fact]
    public void Create_BuildsAggregate_WithExpectedDefaults()
    {
        var view = EntityView.Create(
            entityName: "Granit.Sample.Item",
            basedOn: "default",
            kind: "list",
            name: "My filter",
            description: "Example",
            icon: "filter",
            state: EmptyState,
            ownerId: OwnerId,
            sortOrder: 5);

        view.EntityName.ShouldBe("Granit.Sample.Item");
        view.BasedOn.ShouldBe("default");
        view.Kind.ShouldBe("list");
        view.Name.ShouldBe("My filter");
        view.Visibility.ShouldBe(EntityViewVisibility.Personal);
        view.OwnerId.ShouldBe(OwnerId);
        view.SharedWith.ShouldBeNull();
        view.IsPinned.ShouldBeFalse();
        view.IsDefault.ShouldBeFalse();
        view.IsPersonalDefault.ShouldBeFalse();
        view.SortOrder.ShouldBe(5);
    }

    [Fact]
    public void Create_RejectsEmptyOwnerId()
    {
        Should.Throw<ArgumentException>(() =>
            EntityView.Create("e", "default", "list", "n", null, null, EmptyState, Guid.Empty));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsBlankRequiredStrings(string? blank)
    {
        Should.Throw<ArgumentException>(() =>
            EntityView.Create(blank!, "default", "list", "n", null, null, EmptyState, OwnerId));

        Should.Throw<ArgumentException>(() =>
            EntityView.Create("e", blank!, "list", "n", null, null, EmptyState, OwnerId));

        Should.Throw<ArgumentException>(() =>
            EntityView.Create("e", "default", blank!, "n", null, null, EmptyState, OwnerId));

        Should.Throw<ArgumentException>(() =>
            EntityView.Create("e", "default", "list", blank!, null, null, EmptyState, OwnerId));
    }

    [Fact]
    public void Rename_UpdatesNameDescriptionIcon()
    {
        EntityView view = NewPersonal();
        view.Rename("New name", "New description", "new-icon");

        view.Name.ShouldBe("New name");
        view.Description.ShouldBe("New description");
        view.Icon.ShouldBe("new-icon");
    }

    [Fact]
    public void Rename_RejectsBlankName()
    {
        EntityView view = NewPersonal();
        Should.Throw<ArgumentException>(() => view.Rename("", null, null));
    }

    [Fact]
    public void UpdateState_ReplacesStateNode()
    {
        EntityView view = NewPersonal();
        JsonObject newState = new() { ["filters"] = new JsonArray() };

        view.UpdateState(newState);
        view.State.ShouldBeSameAs(newState);
    }

    [Fact]
    public void ShareWith_PromotesPersonalToShared()
    {
        EntityView view = NewPersonal();
        EntityViewSharedWith audience = new(["Manager"], [Guid.NewGuid()]);

        view.ShareWith(audience);

        view.Visibility.ShouldBe(EntityViewVisibility.Shared);
        view.SharedWith.ShouldBeSameAs(audience);
    }

    [Fact]
    public void ShareWith_RejectsEmptyAudience()
    {
        EntityView view = NewPersonal();
        Should.Throw<InvalidOperationException>(() => view.ShareWith(EntityViewSharedWith.Empty));
    }

    [Fact]
    public void ShareWith_RejectsRePromotionFromTenant()
    {
        EntityView view = NewPersonal();
        view.PromoteToTenant();

        Should.Throw<InvalidOperationException>(() =>
            view.ShareWith(new EntityViewSharedWith(["X"], [])));
    }

    [Fact]
    public void PromoteToTenant_ClearsOwnerAndAudience()
    {
        EntityView view = NewPersonal();
        view.ShareWith(new EntityViewSharedWith(["X"], []));

        view.PromoteToTenant();

        view.Visibility.ShouldBe(EntityViewVisibility.Tenant);
        view.OwnerId.ShouldBeNull();
        view.SharedWith.ShouldBeNull();
    }

    [Fact]
    public void PromotionFlags_ToggleIndependently()
    {
        EntityView view = NewPersonal();

        view.SetPinned(true);
        view.SetTenantDefault(true);
        view.SetPersonalDefault(true);

        view.IsPinned.ShouldBeTrue();
        view.IsDefault.ShouldBeTrue();
        view.IsPersonalDefault.ShouldBeTrue();

        view.SetPinned(false);
        view.SetTenantDefault(false);
        view.SetPersonalDefault(false);

        view.IsPinned.ShouldBeFalse();
        view.IsDefault.ShouldBeFalse();
        view.IsPersonalDefault.ShouldBeFalse();
    }

    [Fact]
    public void IMultiTenant_TenantId_RoundTrips()
    {
        EntityView view = NewPersonal();
        var tenantId = Guid.NewGuid();

        ((Granit.Domain.IMultiTenant)view).TenantId = tenantId;
        view.TenantId.ShouldBe(tenantId);
    }

    private static EntityView NewPersonal() =>
        EntityView.Create(
            "Granit.Sample.Item", "default", "list",
            "Initial", null, null, new JsonObject(), OwnerId);
}
