using Granit.DataLookup.Descriptors;
using Shouldly;
using Xunit;

namespace Granit.DataLookup.Tests.Descriptors;

public sealed class LookupDescriptorTests
{
    [Fact]
    public void Default_values_are_conservative()
    {
        LookupDescriptor descriptor = new(Name: "tenants");

        descriptor.Name.ShouldBe("tenants");
        descriptor.Endpoint.ShouldBeNull();
        descriptor.Kind.ShouldBe(LookupKind.QueryEngine);
        descriptor.RequiredPermission.ShouldBeNull();
        descriptor.SearchParam.ShouldBe("search");
        descriptor.ScopeKeys.ShouldBeNull();
    }

    [Fact]
    public void Records_with_same_values_are_equal()
    {
        LookupDescriptor a = new(Name: "tenants", Kind: LookupKind.QueryEngine, RequiredPermission: "MultiTenancy.Tenants.Read");
        LookupDescriptor b = new(Name: "tenants", Kind: LookupKind.QueryEngine, RequiredPermission: "MultiTenancy.Tenants.Read");

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void Endpoint_fallback_is_supported_without_name()
    {
        LookupDescriptor descriptor = new(
            Endpoint: "/api/external/stripe/customers",
            Kind: LookupKind.Simple,
            SearchParam: "q");

        descriptor.Name.ShouldBeNull();
        descriptor.Endpoint.ShouldBe("/api/external/stripe/customers");
        descriptor.Kind.ShouldBe(LookupKind.Simple);
        descriptor.SearchParam.ShouldBe("q");
    }
}

public sealed class LookupItemTests
{
    [Fact]
    public void Exposes_value_label_and_extra()
    {
        Dictionary<string, object?> extra = new(StringComparer.Ordinal) { ["active"] = true };
        LookupItem item = new(Value: Guid.Empty, Label: "Acme", Extra: extra);

        item.Value.ShouldBe(Guid.Empty);
        item.Label.ShouldBe("Acme");
        item.Extra.ShouldNotBeNull();
        item.Extra!["active"].ShouldBe(true);
    }
}

public sealed class LookupResultTests
{
    [Fact]
    public void Empty_result_has_empty_items()
    {
        LookupResult result = new([]);

        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBeNull();
        result.ContinuationToken.ShouldBeNull();
    }
}

public sealed class LookupKindTests
{
    [Theory]
    [InlineData(LookupKind.QueryEngine, 0)]
    [InlineData(LookupKind.Simple, 1)]
    [InlineData(LookupKind.ReferenceData, 2)]
    [InlineData(LookupKind.Enum, 3)]
    public void Numeric_values_are_stable(LookupKind kind, int expected) =>
        ((int)kind).ShouldBe(expected);
}
