using System.Text.Json;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Abstractions.Tests;

/// <summary>
/// Locks the public shape and JSON wire format of <see cref="EntityAlias"/> +
/// the five <see cref="EntityAliasResolver"/> derived kinds (P2.3). Pinning the
/// kebab discriminators is the load-bearing assertion — the frontend's
/// TypeScript discriminated union mirrors them 1:1.
/// </summary>
public sealed class EntityAliasTests
{
    [Fact]
    public void RouteParamResolver_SerializesWithKebabDiscriminator()
    {
        EntityAliasResolver resolver = new RouteParamResolver("customerId");
        string json = JsonSerializer.Serialize(resolver);

        json.ShouldContain("\"kind\":\"route-param\"");
        json.ShouldNotContain("$type");

        EntityAliasResolver? decoded = JsonSerializer.Deserialize<EntityAliasResolver>(json);
        decoded.ShouldBeOfType<RouteParamResolver>();
        ((RouteParamResolver)decoded!).ParamName.ShouldBe("customerId");
    }

    [Fact]
    public void ViewEntityResolver_DefaultsParamNameToEntityId()
    {
        ViewEntityResolver resolver = new();

        resolver.ParamName.ShouldBe("entityId");

        string json = JsonSerializer.Serialize<EntityAliasResolver>(resolver);
        json.ShouldContain("\"kind\":\"view-entity\"");
    }

    [Fact]
    public void ViewEntityResolver_OverridesParamName()
    {
        EntityAliasResolver resolver = new ViewEntityResolver("deviceId");

        EntityAliasResolver? decoded = JsonSerializer.Deserialize<EntityAliasResolver>(
            JsonSerializer.Serialize(resolver));

        decoded.ShouldBeOfType<ViewEntityResolver>();
        ((ViewEntityResolver)decoded!).ParamName.ShouldBe("deviceId");
    }

    [Fact]
    public void TenantContextResolver_CarriesNoPayload()
    {
        EntityAliasResolver resolver = new TenantContextResolver();
        string json = JsonSerializer.Serialize(resolver);

        json.ShouldContain("\"kind\":\"tenant-context\"");

        EntityAliasResolver? decoded = JsonSerializer.Deserialize<EntityAliasResolver>(json);
        decoded.ShouldBeOfType<TenantContextResolver>();
    }

    [Fact]
    public void UserSelectionResolver_CarriesLookupNameAndMultiSelectFlag()
    {
        EntityAliasResolver resolver = new UserSelectionResolver("Granit.Customers", MultiSelect: true);

        string json = JsonSerializer.Serialize(resolver);
        json.ShouldContain("\"kind\":\"user-selection\"");

        EntityAliasResolver? decoded = JsonSerializer.Deserialize<EntityAliasResolver>(json);
        UserSelectionResolver typed = decoded.ShouldBeOfType<UserSelectionResolver>();
        typed.LookupName.ShouldBe("Granit.Customers");
        typed.MultiSelect.ShouldBeTrue();
    }

    [Fact]
    public void StaticEntityResolver_CarriesEntityId()
    {
        EntityAliasResolver resolver = new StaticEntityResolver("global-tenant-id");

        string json = JsonSerializer.Serialize(resolver);
        json.ShouldContain("\"kind\":\"static\"");

        EntityAliasResolver? decoded = JsonSerializer.Deserialize<EntityAliasResolver>(json);
        ((StaticEntityResolver)decoded!).EntityId.ShouldBe("global-tenant-id");
    }

    [Fact]
    public void Alias_CarriesNameAndEntityTypeAlongsideResolver()
    {
        EntityAlias alias = new(
            Name: "currentDevice",
            EntityType: "Device",
            Resolver: new RouteParamResolver("deviceId"));

        alias.Name.ShouldBe("currentDevice");
        alias.EntityType.ShouldBe("Device");
        alias.Resolver.ShouldBeOfType<RouteParamResolver>();
    }

    [Fact]
    public void Alias_RoundTripsThroughJson_PreservingResolverKind()
    {
        EntityAlias[] originals =
        [
            new("currentCustomer", "Customer", new RouteParamResolver("customerId")),
            new("activeDevice",   "Device",   new ViewEntityResolver()),
            new("tenant",         "Tenant",   new TenantContextResolver()),
            new("targetCustomer", "Customer", new UserSelectionResolver("Granit.Customers")),
            new("hardCoded",      "Site",     new StaticEntityResolver("plant-3")),
        ];

        foreach (EntityAlias alias in originals)
        {
            string json = JsonSerializer.Serialize(alias);
            EntityAlias? decoded = JsonSerializer.Deserialize<EntityAlias>(json);

            decoded.ShouldNotBeNull();
            decoded.Resolver.GetType().ShouldBe(alias.Resolver.GetType());
            decoded.Name.ShouldBe(alias.Name);
            decoded.EntityType.ShouldBe(alias.EntityType);
        }
    }
}
