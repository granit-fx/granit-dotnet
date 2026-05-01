using Granit.Entities.Details;
using Granit.Entities.Endpoints.Dtos;
using Granit.Entities.Endpoints.Internal;
using Granit.Entities.Forms;
using Shouldly;
using Xunit;

namespace Granit.Entities.Endpoints.Tests;

public sealed class EntityManifestComposerTests
{
    private sealed class SampleEntity { }

    [Fact]
    public void Compose_drops_fields_user_lacks_permission_for()
    {
        EntityDefinitionDescriptor descriptor = BuildDescriptor(
            forms: [
                new FormDescriptor
                {
                    Name = "default",
                    Customizable = false,
                    Sections = [
                        new SectionDescriptor
                        {
                            Key = "general",
                            Order = 0,
                            Fields = [
                                new FieldDescriptor
                                {
                                    PropertyName = "Name",
                                    ClrType = typeof(string),
                                    Widget = "text",
                                    Order = 0,
                                },
                                new FieldDescriptor
                                {
                                    PropertyName = "Amount",
                                    ClrType = typeof(decimal),
                                    Widget = "money",
                                    Order = 1,
                                    RequiresPermission = "Invoicing.Invoices.Manage",
                                },
                            ],
                        },
                    ],
                },
            ]);

        EntityManifestResponse manifest = EntityManifestComposer.Compose(
            descriptor,
            EntityPermissionSnapshot.AllPublic,
            grantedPermissions: new HashSet<string>(StringComparer.Ordinal),
            EntityFacets.All,
            defaultViewId: null);

        manifest.Forms.ShouldNotBeNull();
        manifest.Forms!.Single().Sections.Single().Fields
            .Select(f => f.PropertyName)
            .ShouldBe(["Name"]);
    }

    [Fact]
    public void Compose_drops_section_when_every_field_is_filtered()
    {
        EntityDefinitionDescriptor descriptor = BuildDescriptor(
            forms: [
                new FormDescriptor
                {
                    Name = "default",
                    Customizable = false,
                    Sections = [
                        new SectionDescriptor
                        {
                            Key = "secret",
                            Order = 0,
                            Fields = [
                                new FieldDescriptor
                                {
                                    PropertyName = "Vault",
                                    ClrType = typeof(string),
                                    Widget = "text",
                                    Order = 0,
                                    RequiresPermission = "Secrets.Vault.Read",
                                },
                            ],
                        },
                    ],
                },
            ]);

        EntityManifestResponse manifest = EntityManifestComposer.Compose(
            descriptor,
            EntityPermissionSnapshot.AllPublic,
            grantedPermissions: new HashSet<string>(StringComparer.Ordinal),
            EntityFacets.All,
            defaultViewId: null);

        manifest.Forms!.Single().Sections.ShouldBeEmpty();
    }

    [Fact]
    public void Compose_drops_side_panel_user_lacks_permission_for()
    {
        EntityDefinitionDescriptor descriptor = BuildDescriptor(
            details: [
                new DetailDescriptor
                {
                    Name = "default",
                    Sections = [],
                    SidePanels = [
                        new SidePanelDescriptor { Kind = SidePanelKind.Audit, Order = 0 },
                        new SidePanelDescriptor { Kind = SidePanelKind.Documents, Order = 1, RequiresPermission = "Docs.Read" },
                    ],
                },
            ]);

        EntityManifestResponse manifest = EntityManifestComposer.Compose(
            descriptor,
            EntityPermissionSnapshot.AllPublic,
            grantedPermissions: new HashSet<string>(StringComparer.Ordinal),
            EntityFacets.All,
            defaultViewId: null);

        manifest.Details!.Single().SidePanels
            .Select(p => p.Kind)
            .ShouldBe([SidePanelKind.Audit]);
    }

    [Fact]
    public void Compose_omits_facets_not_requested()
    {
        EntityDefinitionDescriptor descriptor = BuildDescriptor();

        EntityManifestResponse manifest = EntityManifestComposer.Compose(
            descriptor,
            EntityPermissionSnapshot.AllPublic,
            grantedPermissions: new HashSet<string>(StringComparer.Ordinal),
            EntityFacets.Identity,
            defaultViewId: null);

        manifest.Identity.ShouldNotBeNull();
        manifest.Permissions.ShouldBeNull();
        manifest.Forms.ShouldBeNull();
        manifest.Details.ShouldBeNull();
        manifest.Collections.ShouldBeNull();
    }

    [Fact]
    public void Compose_carries_schema_version()
    {
        EntityManifestResponse manifest = EntityManifestComposer.Compose(
            BuildDescriptor(),
            EntityPermissionSnapshot.AllPublic,
            grantedPermissions: new HashSet<string>(StringComparer.Ordinal),
            EntityFacets.Identity,
            defaultViewId: null);

        manifest.SchemaVersion.ShouldBe(EntityManifestComposer.SchemaVersion);
    }

    [Fact]
    public void Compose_emits_owned_collection_section_with_item_fields_and_display_property()
    {
        EntityDefinitionDescriptor descriptor = BuildDescriptor(
            forms: [
                new FormDescriptor
                {
                    Name = "default",
                    Customizable = false,
                    Sections = [
                        new SectionDescriptor
                        {
                            Key = "addresses",
                            Order = 0,
                            Fields = [],
                            OwnedCollection = new OwnedCollectionDescriptor
                            {
                                PropertyName = "Addresses",
                                ItemType = typeof(SampleAddress),
                                ItemDisplayProperty = "Line1",
                                MaxRendered = 5,
                                ItemFields = [
                                    new FieldDescriptor
                                    {
                                        PropertyName = "Line1",
                                        ClrType = typeof(string),
                                        Widget = "text",
                                        Order = 0,
                                    },
                                    new FieldDescriptor
                                    {
                                        PropertyName = "City",
                                        ClrType = typeof(string),
                                        Widget = "text",
                                        Order = 1,
                                    },
                                ],
                            },
                        },
                    ],
                },
            ]);

        EntityManifestResponse manifest = EntityManifestComposer.Compose(
            descriptor,
            EntityPermissionSnapshot.AllPublic,
            grantedPermissions: new HashSet<string>(StringComparer.Ordinal),
            EntityFacets.Forms,
            defaultViewId: null);

        EntityFormSectionManifest section = manifest.Forms!.Single().Sections.Single();
        section.Key.ShouldBe("addresses");
        section.Fields.ShouldBeEmpty();
        section.OwnedCollection.ShouldNotBeNull();
        section.OwnedCollection!.PropertyName.ShouldBe("Addresses");
        section.OwnedCollection.ItemTypeName.ShouldBe(nameof(SampleAddress));
        section.OwnedCollection.ItemDisplayProperty.ShouldBe("Line1");
        section.OwnedCollection.MaxRendered.ShouldBe(5);
        section.OwnedCollection.ItemFields.Select(f => f.PropertyName).ShouldBe(["Line1", "City"]);
    }

    [Fact]
    public void Compose_filters_owned_collection_item_fields_by_permission()
    {
        EntityDefinitionDescriptor descriptor = BuildDescriptor(
            forms: [
                new FormDescriptor
                {
                    Name = "default",
                    Customizable = false,
                    Sections = [
                        new SectionDescriptor
                        {
                            Key = "addresses",
                            Order = 0,
                            Fields = [],
                            OwnedCollection = new OwnedCollectionDescriptor
                            {
                                PropertyName = "Addresses",
                                ItemType = typeof(SampleAddress),
                                ItemFields = [
                                    new FieldDescriptor
                                    {
                                        PropertyName = "Line1",
                                        ClrType = typeof(string),
                                        Widget = "text",
                                        Order = 0,
                                    },
                                    new FieldDescriptor
                                    {
                                        PropertyName = "Country",
                                        ClrType = typeof(string),
                                        Widget = "text",
                                        Order = 1,
                                        RequiresPermission = "Sample.Manage",
                                    },
                                ],
                            },
                        },
                    ],
                },
            ]);

        EntityManifestResponse manifest = EntityManifestComposer.Compose(
            descriptor,
            EntityPermissionSnapshot.AllPublic,
            grantedPermissions: new HashSet<string>(StringComparer.Ordinal),
            EntityFacets.Forms,
            defaultViewId: null);

        manifest.Forms!.Single().Sections.Single().OwnedCollection!.ItemFields
            .Select(f => f.PropertyName)
            .ShouldBe(["Line1"]);
    }

    [Fact]
    public void Compose_drops_owned_collection_section_when_every_item_field_is_filtered()
    {
        EntityDefinitionDescriptor descriptor = BuildDescriptor(
            forms: [
                new FormDescriptor
                {
                    Name = "default",
                    Customizable = false,
                    Sections = [
                        new SectionDescriptor
                        {
                            Key = "addresses",
                            Order = 0,
                            Fields = [],
                            OwnedCollection = new OwnedCollectionDescriptor
                            {
                                PropertyName = "Addresses",
                                ItemType = typeof(SampleAddress),
                                ItemFields = [
                                    new FieldDescriptor
                                    {
                                        PropertyName = "Vault",
                                        ClrType = typeof(string),
                                        Widget = "text",
                                        Order = 0,
                                        RequiresPermission = "Sample.Manage",
                                    },
                                ],
                            },
                        },
                    ],
                },
            ]);

        EntityManifestResponse manifest = EntityManifestComposer.Compose(
            descriptor,
            EntityPermissionSnapshot.AllPublic,
            grantedPermissions: new HashSet<string>(StringComparer.Ordinal),
            EntityFacets.Forms,
            defaultViewId: null);

        manifest.Forms!.Single().Sections.ShouldBeEmpty();
    }

    private sealed class SampleAddress
    {
        public string Line1 { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
    }

    private static EntityDefinitionDescriptor BuildDescriptor(
        IReadOnlyList<FormDescriptor>? forms = null,
        IReadOnlyList<DetailDescriptor>? details = null) => new()
        {
            Name = "Test.Sample",
            EntityType = typeof(SampleEntity),
            DisplayKey = "Entity:Sample",
            Icon = "box",
            PermissionGroup = "Test.Samples",
            DisplayProperty = "Name",
            QueryDefinitionType = null,
            ExportDefinitionType = null,
            MetricDefinitionTypes = [],
            DashboardDefinitionTypes = [],
            WorkflowDefinitionType = null,
            Forms = forms ?? [],
            Details = details ?? [],
        };
}
