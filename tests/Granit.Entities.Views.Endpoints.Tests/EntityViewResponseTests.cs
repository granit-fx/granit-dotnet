// =============================================================================
// Tests - EntityViewResponse projection from EntityViewDescriptor
// =============================================================================

using System.Text.Json.Nodes;
using Granit.Entities.Views.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.Entities.Views.Endpoints.Tests;

public sealed class EntityViewResponseTests
{
    [Fact]
    public void FromDescriptor_CopiesScalarFields()
    {
        EntityViewDescriptor descriptor = new(
            Id: Guid.NewGuid(),
            EntityName: "Granit.Sample.Item",
            BasedOn: "default",
            Kind: "list",
            Name: "My filter",
            Description: "demo",
            Icon: "filter",
            State: new JsonObject(),
            Visibility: EntityViewVisibility.Personal,
            OwnerId: Guid.NewGuid(),
            SharedWith: null,
            IsPinned: true,
            IsDefault: false,
            IsPersonalDefault: true,
            SortOrder: 7);

        var response = EntityViewResponse.FromDescriptor(descriptor);

        response.Id.ShouldBe(descriptor.Id);
        response.EntityName.ShouldBe(descriptor.EntityName);
        response.BasedOn.ShouldBe(descriptor.BasedOn);
        response.Kind.ShouldBe(descriptor.Kind);
        response.Name.ShouldBe(descriptor.Name);
        response.Description.ShouldBe(descriptor.Description);
        response.Icon.ShouldBe(descriptor.Icon);
        response.Visibility.ShouldBe(descriptor.Visibility);
        response.OwnerId.ShouldBe(descriptor.OwnerId);
        response.SharedWith.ShouldBeNull();
        response.IsPinned.ShouldBeTrue();
        response.IsDefault.ShouldBeFalse();
        response.IsPersonalDefault.ShouldBeTrue();
        response.SortOrder.ShouldBe(7);
    }

    [Fact]
    public void FromDescriptor_ProjectsSharedWith_WhenPresent()
    {
        EntityViewSharedWith audience = new(["Manager"], [Guid.NewGuid()]);
        EntityViewDescriptor descriptor = new(
            Id: Guid.NewGuid(),
            EntityName: "X",
            BasedOn: "b",
            Kind: "list",
            Name: "n",
            Description: null,
            Icon: null,
            State: new JsonObject(),
            Visibility: EntityViewVisibility.Shared,
            OwnerId: Guid.NewGuid(),
            SharedWith: audience,
            IsPinned: false,
            IsDefault: false,
            IsPersonalDefault: false,
            SortOrder: 0);

        var response = EntityViewResponse.FromDescriptor(descriptor);

        response.SharedWith.ShouldNotBeNull();
        response.SharedWith!.Roles.ShouldBe(audience.Roles);
        response.SharedWith.Users.ShouldBe(audience.Users);
    }
}
