using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Abstractions.Tests;

public sealed class QueryDefinitionLookupTests
{
    [Fact]
    public void AsLookup_stores_descriptor_with_name_type_permission_and_scope()
    {
        TenantLookupDefinition definition = new();

        LookupSourceDescriptor? descriptor = definition.GetLookupSource();

        descriptor.ShouldNotBeNull();
        descriptor!.Name.ShouldBe("tenants");
        descriptor.ValueType.ShouldBe(typeof(Guid));
        descriptor.RequiredPermission.ShouldBe("Platform.Tenants.Read");
        descriptor.ScopeKeys.ShouldBe(["region"]);
    }

    [Fact]
    public void AsLookup_boxed_value_selector_compiles_and_boxes_value()
    {
        TenantLookupDefinition definition = new();
        LookupSourceDescriptor descriptor = definition.GetLookupSource()!;
        var id = Guid.NewGuid();

        var boxed = (System.Linq.Expressions.Expression<Func<Tenant, object>>)descriptor.BoxedValueSelector;
        object value = boxed.Compile()(new Tenant(id, "Acme"));

        value.ShouldBeOfType<Guid>().ShouldBe(id);
    }

    [Fact]
    public void GetLookupSource_returns_null_when_not_declared()
    {
        NoLookupDefinition definition = new();

        definition.GetLookupSource().ShouldBeNull();
    }

    [Fact]
    public void AsLookup_blank_name_throws()
    {
        QueryDefinitionBuilder<Tenant> builder = new();

        Should.Throw<ArgumentException>(() => builder.AsLookup(" ", t => t.Id, t => t.Name));
    }

    public sealed record Tenant(Guid Id, string Name);

    private sealed class TenantLookupDefinition : QueryDefinition<Tenant>
    {
        public override string Name => "Test.Tenants";

        protected override void Configure(QueryDefinitionBuilder<Tenant> builder) =>
            builder
                .Column(t => t.Name, c => c.Sortable().Filterable())
                .GlobalSearch(t => t.Name!)
                .AsLookup("tenants", t => t.Id, t => t.Name,
                    requiredPermission: "Platform.Tenants.Read",
                    scopeKeys: ["region"]);
    }

    private sealed class NoLookupDefinition : QueryDefinition<Tenant>
    {
        public override string Name => "Test.NoLookup";

        protected override void Configure(QueryDefinitionBuilder<Tenant> builder) =>
            builder.Column(t => t.Name);
    }
}
