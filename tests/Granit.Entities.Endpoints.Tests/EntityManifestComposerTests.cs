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
        IReadOnlyList<DetailDescriptor>? details = null,
        IReadOnlyList<Granit.Entities.Layouts.EntityListLayoutDescriptor>? listLayouts = null) => new()
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
            ListLayouts = listLayouts ?? [],
        };

    [Fact]
    public void Compose_emits_kanban_layout_with_card_and_columns()
    {
        Granit.Entities.Layouts.KanbanLayoutDescriptor kanban = new()
        {
            Kind = Granit.Entities.Layouts.EntityListLayoutKind.Kanban,
            IsDefault = true,
            GroupByPropertyName = "Status",
            GroupByClrType = typeof(SampleStatus),
            Card = new Granit.Entities.Layouts.KanbanCardDescriptor
            {
                TitleProperty = "Title",
                Fields = [
                    new FieldDescriptor { PropertyName = "Owner", ClrType = typeof(string), Widget = "text", Order = 0 },
                ],
            },
            Columns = [
                new Granit.Entities.Layouts.KanbanColumnDescriptor
                {
                    Value = "Open",
                    Color = Granit.Entities.Layouts.KanbanColor.Orange,
                    DefaultState = Granit.Entities.Layouts.KanbanColumnState.Open,
                },
                new Granit.Entities.Layouts.KanbanColumnDescriptor
                {
                    Value = "Done",
                    Color = Granit.Entities.Layouts.KanbanColor.Green,
                    DefaultState = Granit.Entities.Layouts.KanbanColumnState.Collapsed,
                },
            ],
        };

        EntityDefinitionDescriptor descriptor = BuildDescriptor(listLayouts: [kanban]);

        EntityManifestResponse manifest = EntityManifestComposer.Compose(
            descriptor,
            EntityPermissionSnapshot.AllPublic,
            grantedPermissions: new HashSet<string>(StringComparer.Ordinal),
            EntityFacets.Collections,
            defaultViewId: null);

        EntityListLayoutManifest layout = manifest.Collections!.ListLayouts.ShouldHaveSingleItem();
        layout.Kind.ShouldBe(Granit.Entities.Layouts.EntityListLayoutKind.Kanban);
        layout.IsDefault.ShouldBeTrue();
        layout.Kanban.ShouldNotBeNull();
        layout.Kanban!.GroupByPropertyName.ShouldBe("Status");
        layout.Kanban.Card.TitleProperty.ShouldBe("Title");
        layout.Kanban.Card.Fields.Select(f => f.PropertyName).ShouldBe(["Owner"]);
        layout.Kanban.Columns.Select(c => c.Value).ShouldBe(["Open", "Done"]);
        layout.Kanban.Columns.Single(c => c.Value == "Done").DefaultState
            .ShouldBe(Granit.Entities.Layouts.KanbanColumnState.Collapsed);
    }

    [Fact]
    public void Compose_drops_layout_when_RequiresPermission_not_granted()
    {
        Granit.Entities.Layouts.KanbanLayoutDescriptor kanban = new()
        {
            Kind = Granit.Entities.Layouts.EntityListLayoutKind.Kanban,
            RequiresPermission = "Tasks.Tasks.Kanban",
            GroupByPropertyName = "Status",
            GroupByClrType = typeof(SampleStatus),
            Card = new Granit.Entities.Layouts.KanbanCardDescriptor { Fields = [] },
            Columns = [],
        };

        EntityManifestResponse manifest = EntityManifestComposer.Compose(
            BuildDescriptor(listLayouts: [kanban]),
            EntityPermissionSnapshot.AllPublic,
            grantedPermissions: new HashSet<string>(StringComparer.Ordinal),
            EntityFacets.Collections,
            defaultViewId: null);

        manifest.Collections!.ListLayouts.ShouldBeEmpty();
    }

    [Fact]
    public void Compose_filters_kanban_card_fields_by_permission()
    {
        Granit.Entities.Layouts.KanbanLayoutDescriptor kanban = new()
        {
            Kind = Granit.Entities.Layouts.EntityListLayoutKind.Kanban,
            GroupByPropertyName = "Status",
            GroupByClrType = typeof(SampleStatus),
            Card = new Granit.Entities.Layouts.KanbanCardDescriptor
            {
                Fields = [
                    new FieldDescriptor { PropertyName = "Owner", ClrType = typeof(string), Widget = "text", Order = 0 },
                    new FieldDescriptor { PropertyName = "Salary", ClrType = typeof(decimal), Widget = "money", Order = 1, RequiresPermission = "Tasks.Sensitive.Read" },
                ],
            },
            Columns = [],
        };

        EntityManifestResponse manifest = EntityManifestComposer.Compose(
            BuildDescriptor(listLayouts: [kanban]),
            EntityPermissionSnapshot.AllPublic,
            grantedPermissions: new HashSet<string>(StringComparer.Ordinal),
            EntityFacets.Collections,
            defaultViewId: null);

        manifest.Collections!.ListLayouts.ShouldHaveSingleItem()
            .Kanban!.Card.Fields.Select(f => f.PropertyName).ShouldBe(["Owner"]);
    }

    [Fact]
    public void Compose_emits_calendar_layout_with_start_end_title_and_color_by()
    {
        Granit.Entities.Layouts.CalendarLayoutDescriptor calendar = new()
        {
            Kind = Granit.Entities.Layouts.EntityListLayoutKind.Calendar,
            IsDefault = true,
            StartPropertyName = "StartsAt",
            EndPropertyName = "EndsAt",
            TitlePropertyName = "Subject",
            ColorByPropertyName = "Status",
        };

        EntityManifestResponse manifest = EntityManifestComposer.Compose(
            BuildDescriptor(listLayouts: [calendar]),
            EntityPermissionSnapshot.AllPublic,
            grantedPermissions: new HashSet<string>(StringComparer.Ordinal),
            EntityFacets.Collections,
            defaultViewId: null);

        EntityListLayoutManifest layout = manifest.Collections!.ListLayouts.ShouldHaveSingleItem();
        layout.Kind.ShouldBe(Granit.Entities.Layouts.EntityListLayoutKind.Calendar);
        layout.IsDefault.ShouldBeTrue();
        layout.Kanban.ShouldBeNull();
        layout.Calendar.ShouldNotBeNull();
        layout.Calendar!.StartPropertyName.ShouldBe("StartsAt");
        layout.Calendar.EndPropertyName.ShouldBe("EndsAt");
        layout.Calendar.TitlePropertyName.ShouldBe("Subject");
        layout.Calendar.ColorByPropertyName.ShouldBe("Status");
    }

    [Fact]
    public void Compose_emits_minimal_calendar_with_only_start_field()
    {
        Granit.Entities.Layouts.CalendarLayoutDescriptor calendar = new()
        {
            Kind = Granit.Entities.Layouts.EntityListLayoutKind.Calendar,
            StartPropertyName = "OccurredAt",
        };

        EntityManifestResponse manifest = EntityManifestComposer.Compose(
            BuildDescriptor(listLayouts: [calendar]),
            EntityPermissionSnapshot.AllPublic,
            grantedPermissions: new HashSet<string>(StringComparer.Ordinal),
            EntityFacets.Collections,
            defaultViewId: null);

        EntityCalendarLayoutManifest emitted = manifest.Collections!.ListLayouts.ShouldHaveSingleItem().Calendar!;
        emitted.StartPropertyName.ShouldBe("OccurredAt");
        emitted.EndPropertyName.ShouldBeNull();
        emitted.TitlePropertyName.ShouldBeNull();
        emitted.ColorByPropertyName.ShouldBeNull();
    }

    [Fact]
    public void Compose_drops_calendar_layout_when_RequiresPermission_not_granted()
    {
        Granit.Entities.Layouts.CalendarLayoutDescriptor calendar = new()
        {
            Kind = Granit.Entities.Layouts.EntityListLayoutKind.Calendar,
            RequiresPermission = "Meetings.Meetings.Calendar",
            StartPropertyName = "StartsAt",
        };

        EntityManifestResponse manifest = EntityManifestComposer.Compose(
            BuildDescriptor(listLayouts: [calendar]),
            EntityPermissionSnapshot.AllPublic,
            grantedPermissions: new HashSet<string>(StringComparer.Ordinal),
            EntityFacets.Collections,
            defaultViewId: null);

        manifest.Collections!.ListLayouts.ShouldBeEmpty();
    }

    private enum SampleStatus { Open, Done }

    [Fact]
    public void Compose_emits_actions_when_facet_requested()
    {
        Granit.Entities.Actions.EntityActionDescriptor finalize = new(
            Name: "finalize",
            Kind: Granit.Entities.Actions.EntityActionKind.ApiCall,
            DisplayKey: "Test:Action.Finalize",
            Icon: "check",
            Order: 10,
            RequiresPermission: null,
            UrlTemplate: "/api/{id}/finalize",
            HttpMethod: "POST",
            ConfirmationKey: "Test:Action.Finalize.Confirm",
            WorkflowTransitionName: null,
            ContributorAssemblyName: null);

        EntityManifestResponse manifest = EntityManifestComposer.Compose(
            BuildDescriptor() with { Actions = [finalize] },
            EntityPermissionSnapshot.AllPublic,
            grantedPermissions: new HashSet<string>(StringComparer.Ordinal),
            EntityFacets.Actions,
            defaultViewId: null);

        EntityActionManifest only = manifest.Actions!.ShouldHaveSingleItem();
        only.Name.ShouldBe("finalize");
        only.Kind.ShouldBe(Granit.Entities.Actions.EntityActionKind.ApiCall);
        only.HttpMethod.ShouldBe("POST");
        only.UrlTemplate.ShouldBe("/api/{id}/finalize");
        only.ConfirmationKey.ShouldBe("Test:Action.Finalize.Confirm");
    }

    [Fact]
    public void Compose_drops_action_when_RequiresPermission_not_granted()
    {
        Granit.Entities.Actions.EntityActionDescriptor gated = new(
            Name: "void",
            Kind: Granit.Entities.Actions.EntityActionKind.ApiCall,
            DisplayKey: null, Icon: null, Order: 0,
            RequiresPermission: "Invoicing.Invoices.Manage",
            UrlTemplate: "/api/{id}/void", HttpMethod: "POST",
            ConfirmationKey: null, WorkflowTransitionName: null,
            ContributorAssemblyName: null);

        EntityManifestResponse manifest = EntityManifestComposer.Compose(
            BuildDescriptor() with { Actions = [gated] },
            EntityPermissionSnapshot.AllPublic,
            grantedPermissions: new HashSet<string>(StringComparer.Ordinal),
            EntityFacets.Actions,
            defaultViewId: null);

        manifest.Actions!.ShouldBeEmpty();
    }

    [Fact]
    public void Compose_omits_actions_when_facet_not_requested()
    {
        Granit.Entities.Actions.EntityActionDescriptor finalize = new(
            "finalize", Granit.Entities.Actions.EntityActionKind.ApiCall,
            null, null, 0, null, "/x", "POST", null, null, null);

        EntityManifestResponse manifest = EntityManifestComposer.Compose(
            BuildDescriptor() with { Actions = [finalize] },
            EntityPermissionSnapshot.AllPublic,
            grantedPermissions: new HashSet<string>(StringComparer.Ordinal),
            EntityFacets.Identity,
            defaultViewId: null);

        manifest.Actions.ShouldBeNull();
    }

    [Fact]
    public void Compose_kanban_card_pins_relations_that_opted_in()
    {
        Granit.Entities.Layouts.KanbanLayoutDescriptor kanban = new()
        {
            Kind = Granit.Entities.Layouts.EntityListLayoutKind.Kanban,
            GroupByPropertyName = "Status",
            GroupByClrType = typeof(SampleStatus),
            Card = new Granit.Entities.Layouts.KanbanCardDescriptor
            {
                Fields = [new FieldDescriptor { PropertyName = "Owner", ClrType = typeof(string), Widget = "text", Order = 0 }],
            },
            Columns = [],
        };

        Granit.Entities.Relations.RelationDescriptor pinned = new(
            Name: "tasks",
            Cardinality: Granit.Entities.Relations.RelationCardinality.Many,
            Display: Granit.Entities.Relations.RelationDisplay.SmartButton,
            TargetEntityName: "Granit.Tasks.Task",
            TargetEntityClrType: typeof(SampleEntity),
            DisplayKey: "Tasks:Relation.Tasks",
            Icon: "checklist",
            Order: 10,
            RequiresPermission: null,
            ForeignKeyExpression: null,
            Aggregates: [],
            QueryDefinitionName: null,
            ContributorAssemblyName: "Granit.Tasks",
            ShowOnKanbanCard: true);

        Granit.Entities.Relations.RelationDescriptor unpinned = pinned with { Name = "notes", ShowOnKanbanCard = false };

        EntityDefinitionDescriptor descriptor = BuildDescriptor(listLayouts: [kanban])
            with
        { Relations = [pinned, unpinned] };

        EntityManifestResponse manifest = EntityManifestComposer.Compose(
            descriptor,
            EntityPermissionSnapshot.AllPublic,
            grantedPermissions: new HashSet<string>(StringComparer.Ordinal),
            EntityFacets.Collections,
            defaultViewId: null);

        EntityKanbanCardRelationManifest only = manifest.Collections!
            .ListLayouts.Single().Kanban!.Card.Relations.ShouldHaveSingleItem();
        only.Name.ShouldBe("tasks");
        only.Icon.ShouldBe("checklist");
        only.ContributorAssemblyName.ShouldBe("Granit.Tasks");
    }

    [Fact]
    public void Compose_kanban_card_drops_pinned_relation_when_RequiresPermission_not_granted()
    {
        Granit.Entities.Layouts.KanbanLayoutDescriptor kanban = new()
        {
            Kind = Granit.Entities.Layouts.EntityListLayoutKind.Kanban,
            GroupByPropertyName = "Status",
            GroupByClrType = typeof(SampleStatus),
            Card = new Granit.Entities.Layouts.KanbanCardDescriptor
            {
                Fields = [new FieldDescriptor { PropertyName = "Owner", ClrType = typeof(string), Widget = "text", Order = 0 }],
            },
            Columns = [],
        };

        Granit.Entities.Relations.RelationDescriptor gated = new(
            Name: "tasks",
            Cardinality: Granit.Entities.Relations.RelationCardinality.Many,
            Display: Granit.Entities.Relations.RelationDisplay.SmartButton,
            TargetEntityName: "Granit.Tasks.Task",
            TargetEntityClrType: typeof(SampleEntity),
            DisplayKey: null, Icon: null, Order: 10,
            RequiresPermission: "Tasks.Tasks.Read",
            ForeignKeyExpression: null,
            Aggregates: [],
            QueryDefinitionName: null,
            ContributorAssemblyName: null,
            ShowOnKanbanCard: true);

        EntityDefinitionDescriptor descriptor = BuildDescriptor(listLayouts: [kanban])
            with
        { Relations = [gated] };

        EntityManifestResponse manifest = EntityManifestComposer.Compose(
            descriptor,
            EntityPermissionSnapshot.AllPublic,
            grantedPermissions: new HashSet<string>(StringComparer.Ordinal),
            EntityFacets.Collections,
            defaultViewId: null);

        manifest.Collections!.ListLayouts.Single().Kanban!.Card.Relations.ShouldBeEmpty();
    }

    [Fact]
    public void Compose_kanban_card_pins_actions_that_opted_in()
    {
        Granit.Entities.Layouts.KanbanLayoutDescriptor kanban = new()
        {
            Kind = Granit.Entities.Layouts.EntityListLayoutKind.Kanban,
            GroupByPropertyName = "Status",
            GroupByClrType = typeof(SampleStatus),
            Card = new Granit.Entities.Layouts.KanbanCardDescriptor
            {
                Fields = [new FieldDescriptor { PropertyName = "Owner", ClrType = typeof(string), Widget = "text", Order = 0 }],
            },
            Columns = [],
        };

        Granit.Entities.Actions.EntityActionDescriptor pinned = new(
            Name: "quick-note",
            Kind: Granit.Entities.Actions.EntityActionKind.ApiCall,
            DisplayKey: "Notes:Action.Add",
            Icon: "note-plus",
            Order: 5,
            RequiresPermission: null,
            UrlTemplate: "/api/{id}/notes",
            HttpMethod: "POST",
            ConfirmationKey: null,
            WorkflowTransitionName: null,
            ContributorAssemblyName: "Granit.Notes",
            ShowOnKanbanCard: true);

        Granit.Entities.Actions.EntityActionDescriptor unpinned = pinned with { Name = "void", ShowOnKanbanCard = false };

        EntityDefinitionDescriptor descriptor = BuildDescriptor(listLayouts: [kanban])
            with
        { Actions = [pinned, unpinned] };

        EntityManifestResponse manifest = EntityManifestComposer.Compose(
            descriptor,
            EntityPermissionSnapshot.AllPublic,
            grantedPermissions: new HashSet<string>(StringComparer.Ordinal),
            EntityFacets.Collections,
            defaultViewId: null);

        EntityKanbanCardActionManifest only = manifest.Collections!
            .ListLayouts.Single().Kanban!.Card.Actions.ShouldHaveSingleItem();
        only.Name.ShouldBe("quick-note");
        only.Icon.ShouldBe("note-plus");
        only.ContributorAssemblyName.ShouldBe("Granit.Notes");
    }

    [Fact]
    public void Compose_kanban_card_drops_pinned_action_when_RequiresPermission_not_granted()
    {
        Granit.Entities.Layouts.KanbanLayoutDescriptor kanban = new()
        {
            Kind = Granit.Entities.Layouts.EntityListLayoutKind.Kanban,
            GroupByPropertyName = "Status",
            GroupByClrType = typeof(SampleStatus),
            Card = new Granit.Entities.Layouts.KanbanCardDescriptor
            {
                Fields = [new FieldDescriptor { PropertyName = "Owner", ClrType = typeof(string), Widget = "text", Order = 0 }],
            },
            Columns = [],
        };

        Granit.Entities.Actions.EntityActionDescriptor gated = new(
            Name: "void",
            Kind: Granit.Entities.Actions.EntityActionKind.ApiCall,
            DisplayKey: null, Icon: "ban", Order: 0,
            RequiresPermission: "Invoicing.Invoices.Manage",
            UrlTemplate: "/api/{id}/void", HttpMethod: "POST",
            ConfirmationKey: null, WorkflowTransitionName: null,
            ContributorAssemblyName: null,
            ShowOnKanbanCard: true);

        EntityDefinitionDescriptor descriptor = BuildDescriptor(listLayouts: [kanban])
            with
        { Actions = [gated] };

        EntityManifestResponse manifest = EntityManifestComposer.Compose(
            descriptor,
            EntityPermissionSnapshot.AllPublic,
            grantedPermissions: new HashSet<string>(StringComparer.Ordinal),
            EntityFacets.Collections,
            defaultViewId: null);

        manifest.Collections!.ListLayouts.Single().Kanban!.Card.Actions.ShouldBeEmpty();
    }
}
