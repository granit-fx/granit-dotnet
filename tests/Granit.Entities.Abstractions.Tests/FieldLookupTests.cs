using Granit.DataLookup.Descriptors;
using Granit.Entities.Forms;
using Shouldly;
using Xunit;

namespace Granit.Entities.Abstractions.Tests;

public sealed class FieldLookupTests
{
    [Fact]
    public void Field_without_lookup_has_null_lookup()
    {
        FieldDescriptor title = FieldsOf(new SampleEntityDefinition())["Title"];

        title.Lookup.ShouldBeNull();
    }

    [Fact]
    public void Lookup_by_name_captures_descriptor_with_defaults()
    {
        FieldDescriptor tenant = FieldsOf(new SampleEntityDefinition())["TenantId"];

        tenant.Lookup.ShouldNotBeNull();
        tenant.Lookup!.Name.ShouldBe("tenants");
        tenant.Lookup.Kind.ShouldBe(LookupKind.QueryEngine);
        tenant.Lookup.RequiredPermission.ShouldBe("Platform.Tenants.Read");
        tenant.Lookup.ScopeKeys.ShouldBe(["region"]);
    }

    [Fact]
    public void Lookup_by_descriptor_is_stored_verbatim()
    {
        FieldDescriptor owner = FieldsOf(new SampleEntityDefinition())["OwnerId"];

        owner.Lookup.ShouldNotBeNull();
        owner.Lookup!.Kind.ShouldBe(LookupKind.Simple);
        owner.Lookup.Endpoint.ShouldBe("/custom/owners");
    }

    [Fact]
    public void Lookup_blank_name_throws()
    {
        EntityDefinitionBuilder<SampleEntity> builder = new();

        Should.Throw<ArgumentException>(() =>
            builder.Form("f", f => f.Section("s", s => s.Field(x => x.TenantId, fld => fld.Lookup(" ")))));
    }

    private static Dictionary<string, FieldDescriptor> FieldsOf(SampleEntityDefinition definition) =>
        definition.Descriptor.Forms[0].Sections
            .SelectMany(s => s.Fields)
            .ToDictionary(f => f.PropertyName);

    private sealed class SampleEntity
    {
        public string Title { get; set; } = "";
        public Guid TenantId { get; set; }
        public Guid OwnerId { get; set; }
    }

    private sealed class SampleEntityDefinition : EntityDefinition<SampleEntity>
    {
        public override string Name => "Granit.Sample.LookupEntity";

        protected override void Configure(EntityDefinitionBuilder<SampleEntity> builder) =>
            builder.Form("default", f => f
                .Section("general", s => s
                    .Field(x => x.Title)
                    .Field(x => x.TenantId, fld => fld
                        .Lookup("tenants", requiredPermission: "Platform.Tenants.Read", scopeKeys: ["region"]))
                    .Field(x => x.OwnerId, fld => fld
                        .Lookup(new LookupDescriptor(Endpoint: "/custom/owners", Kind: LookupKind.Simple)))));
    }
}
