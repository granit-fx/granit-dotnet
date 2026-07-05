// =============================================================================
// Tests - EntityDefinition + EntityDefinitionBuilder
// =============================================================================

using Granit.Entities.Details;
using Granit.Entities.Forms;
using Granit.Entities.Visibility;
using Shouldly;
using Xunit;

namespace Granit.Entities.Abstractions.Tests;

public sealed class EntityDefinitionTests
{
    [Fact]
    public void Descriptor_IsCached_AndConfiguresOnFirstAccess()
    {
        var definition = new SampleEntityDefinition();

        EntityDefinitionDescriptor first = definition.Descriptor;
        EntityDefinitionDescriptor second = definition.Descriptor;

        first.ShouldBeSameAs(second);
        first.Name.ShouldBe("Granit.Sample.SampleEntity");
        first.EntityType.ShouldBe(typeof(SampleEntity));
    }

    [Fact]
    public void Builder_CapturesAllReferences()
    {
        var definition = new SampleEntityDefinition();
        EntityDefinitionDescriptor d = definition.Descriptor;

        d.QueryDefinitionType.ShouldBe(typeof(SampleQueryDefinition));
        d.ExportDefinitionType.ShouldBe(typeof(SampleExportDefinition));
        d.WorkflowDefinitionType.ShouldBe(typeof(SampleWorkflowDefinition));
        d.MetricDefinitionTypes.ShouldBe([typeof(SampleMetricA), typeof(SampleMetricB)]);
        d.DashboardDefinitionTypes.ShouldBe([typeof(SampleDashboard)]);
    }

    [Fact]
    public void Builder_CapturesIdentityFields()
    {
        EntityDefinitionDescriptor d = new SampleEntityDefinition().Descriptor;

        d.DisplayKey.ShouldBe("Entity:SampleEntity");
        d.Icon.ShouldBe("box");
        d.PermissionGroup.ShouldBe("Sample.SampleEntities");
        d.DisplayProperty.ShouldBe("Title");
        d.SubtitleProperty.ShouldBe("Status");
    }

    [Fact]
    public void SubtitleProperty_DefaultsToNull_WhenNotConfigured()
    {
        // SubtitleProperty is optional — entities with only DisplayProperty
        // surface no subtitle, and the renderer falls back to a single label.
        EntityDefinitionDescriptor d = new MinimalEntityDefinition().Descriptor;

        d.DisplayProperty.ShouldBe("Title");
        d.SubtitleProperty.ShouldBeNull();
    }

    [Fact]
    public void SubtitleProperty_RejectsNonDirectPropertyAccess()
    {
        // Mirror of DisplayProperty's contract: only direct property access
        // expressions are accepted (no method calls, no compound expressions).
        EntityDefinitionBuilder<SampleEntity> builder = new();

        Should.Throw<ArgumentException>(() =>
            builder.SubtitleProperty(x => x.Title.ToUpperInvariant()));
    }

    [Fact]
    public void Builder_CapturesMultipleFormVariants()
    {
        EntityDefinitionDescriptor d = new SampleEntityDefinition().Descriptor;

        d.Forms.Count.ShouldBe(2);
        d.Forms.Select(f => f.Name).ShouldBe(["default", "quick"]);
    }

    [Fact]
    public void Builder_CapturesMultipleDetailVariants()
    {
        EntityDefinitionDescriptor d = new SampleEntityDefinition().Descriptor;

        d.Details.Count.ShouldBe(1);
        d.Details[0].Name.ShouldBe("default");
    }

    [Fact]
    public void Builder_RejectsDuplicateFormVariantName()
    {
        var def = new DuplicateFormDefinition();
        Should.Throw<InvalidOperationException>(() => _ = def.Descriptor)
            .Message.ShouldContain("Duplicate Form variant name 'default'");
    }

    [Fact]
    public void FieldBuilder_ChoosesDefaultComponent_FromClrType()
    {
        EntityDefinitionDescriptor d = new SampleEntityDefinition().Descriptor;

        var fields = d.Forms[0].Sections.SelectMany(s => s.Fields).ToDictionary(f => f.PropertyName);

        fields["Title"].Component.ShouldBe("text");
        fields["Active"].Component.ShouldBe("boolean");
        fields["IssuedAt"].Component.ShouldBe("datetime");
        // Amount is overridden to "money" in the fixture (covered by FieldBuilder_OverridesComponent_AndConfig).
    }

    [Fact]
    public void FieldBuilder_EnumField_DefaultsToSelect_WithAutoOptions()
    {
        EntityDefinitionDescriptor d = new SampleEntityDefinition().Descriptor;
        FieldDescriptor kind = d.Forms[0].Sections.SelectMany(s => s.Fields).First(f => f.PropertyName == "Kind");

        kind.Component.ShouldBe("select");
        kind.Config.ShouldNotBeNull();
        IReadOnlyList<FieldSelectOption> options = kind.Config!["options"].ShouldBeAssignableTo<IReadOnlyList<FieldSelectOption>>()!;
        options.Select(o => o.Value).ShouldBe(["Individual", "Company"]);
        options.Select(o => o.LabelKey).ShouldBe(["Enum:SampleKind.Individual", "Enum:SampleKind.Company"]);
    }

    [Fact]
    public void FieldBuilder_FlagsEnumField_DefaultsToMultiselect_DroppingZeroMember()
    {
        EntityDefinitionDescriptor d = new SampleEntityDefinition().Descriptor;
        FieldDescriptor roles = d.Forms[0].Sections.SelectMany(s => s.Fields).First(f => f.PropertyName == "Roles");

        roles.Component.ShouldBe("multiselect");
        roles.Config.ShouldNotBeNull();
        IReadOnlyList<FieldSelectOption> options = roles.Config!["options"].ShouldBeAssignableTo<IReadOnlyList<FieldSelectOption>>()!;
        // None (0) is dropped — the empty selection already represents it.
        options.Select(o => o.Value).ShouldBe(["Customer", "Supplier"]);
    }

    [Fact]
    public void FieldBuilder_OverridesComponent_AndConfig()
    {
        EntityDefinitionDescriptor d = new SampleEntityDefinition().Descriptor;
        FieldDescriptor amount = d.Forms[0].Sections.SelectMany(s => s.Fields).First(f => f.PropertyName == "Amount");

        amount.Component.ShouldBe("money");
        amount.Config.ShouldNotBeNull();
        amount.Config!["currencyCode"].ShouldBe("EUR");
    }

    [Fact]
    public void FieldBuilder_CapturesRequiresPermission()
    {
        EntityDefinitionDescriptor d = new SampleEntityDefinition().Descriptor;
        FieldDescriptor amount = d.Forms[0].Sections.SelectMany(s => s.Fields).First(f => f.PropertyName == "Amount");

        amount.RequiresPermission.ShouldBe("Sample.SampleEntities.Manage");
    }

    [Fact]
    public void FieldBuilder_CapturesVisibleIf()
    {
        EntityDefinitionDescriptor d = new SampleEntityDefinition().Descriptor;
        FieldDescriptor notes = d.Forms[0].Sections.SelectMany(s => s.Fields).First(f => f.PropertyName == "Notes");

        notes.VisibleIf.ShouldBe(new VisibilityCondition("Status", FieldOp.Eq, "Draft"));
    }

    [Fact]
    public void DetailBuilder_SectionsFromForm_WiresUpInherits()
    {
        EntityDefinitionDescriptor d = new SampleEntityDefinition().Descriptor;
        DetailDescriptor detail = d.Details[0];

        detail.Sections.ShouldHaveSingleItem();
        detail.Sections[0].InheritsFromFormVariant.ShouldBe("default");
    }

    [Fact]
    public void DetailBuilder_SidePanels_InOrder()
    {
        EntityDefinitionDescriptor d = new SampleEntityDefinition().Descriptor;
        DetailDescriptor detail = d.Details[0];

        detail.SidePanels.Select(p => p.Kind).ShouldBe([SidePanelKind.Audit, SidePanelKind.Timeline]);
    }

    [Fact]
    public void DetailSectionBuilder_RejectsMixingInheritsAndExplicitFields()
    {
        var def = new MixedDetailDefinition();
        Should.Throw<InvalidOperationException>(() => _ = def.Descriptor)
            .Message.ShouldContain("InheritsFromForm");
    }

    [Fact]
    public void Customizable_FormVariantFlagsCustomizable()
    {
        EntityDefinitionDescriptor d = new SampleEntityDefinition().Descriptor;
        d.Forms[0].Customizable.ShouldBeTrue();
        d.Forms[1].Customizable.ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // Test fixtures
    // -------------------------------------------------------------------------

    private sealed class SampleEntity
    {
        public string Title { get; set; } = "";
        public decimal Amount { get; set; }
        public bool Active { get; set; }
        public DateTimeOffset IssuedAt { get; set; }
        public string Status { get; set; } = "";
        public string? Notes { get; set; }
        public SampleKind Kind { get; set; }
        public SampleRoles Roles { get; set; }
    }

    private enum SampleKind
    {
        Individual,
        Company,
    }

    [Flags]
    private enum SampleRoles
    {
        None = 0,
        Customer = 1 << 0,
        Supplier = 1 << 1,
    }

    private sealed class SampleQueryDefinition;
    private sealed class SampleExportDefinition;
    private sealed class SampleMetricA;
    private sealed class SampleMetricB;
    private sealed class SampleDashboard;
    private sealed class SampleWorkflowDefinition;

    private sealed class SampleEntityDefinition : EntityDefinition<SampleEntity>
    {
        public override string Name => "Granit.Sample.SampleEntity";

        protected override void Configure(EntityDefinitionBuilder<SampleEntity> builder)
        {
            builder.DisplayKey("Entity:SampleEntity")
             .Icon("box")
             .PermissionGroup("Sample.SampleEntities")
             .DisplayProperty(s => s.Title)
             .SubtitleProperty(s => s.Status);

            builder.Query<SampleQueryDefinition>();
            builder.Export<SampleExportDefinition>();
            builder.Workflow<SampleWorkflowDefinition>();
            builder.Metric<SampleMetricA>();
            builder.Metric<SampleMetricB>();
            builder.Dashboard<SampleDashboard>();

            builder.Form("default", f => f
                .Section("general", s => s
                    .Field(x => x.Title)
                    .Field(x => x.Amount, fld => fld
                        .Component("money", new Dictionary<string, object?>(StringComparer.Ordinal) { ["currencyCode"] = "EUR" })
                        .RequiresPermission("Sample.SampleEntities.Manage"))
                    .Field(x => x.Active)
                    .Field(x => x.IssuedAt)
                    .Field(x => x.Kind)
                    .Field(x => x.Roles)
                    .Field(x => x.Notes, fld => fld.VisibleIf("Status", FieldOp.Eq, "Draft")))
                .Customizable());

            builder.Form("quick", f => f
                .Section("essentials", s => s
                    .Field(x => x.Title)
                    .Field(x => x.Amount)));

            builder.Detail("default", d => d
                .SectionsFromForm()
                .SidePanel.Audit().Timeline());
        }
    }

    private sealed class MinimalEntityDefinition : EntityDefinition<SampleEntity>
    {
        public override string Name => "Granit.Sample.Minimal";

        protected override void Configure(EntityDefinitionBuilder<SampleEntity> builder) =>
            builder.DisplayProperty(s => s.Title);
    }

    private sealed class DuplicateFormDefinition : EntityDefinition<SampleEntity>
    {
        public override string Name => "Granit.Sample.Dup";

        protected override void Configure(EntityDefinitionBuilder<SampleEntity> builder)
        {
            builder.Form("default", f => f.Section("a", s => s.Field(x => x.Title)));
            builder.Form("default", f => f.Section("b", s => s.Field(x => x.Title)));
        }
    }

    private sealed class MixedDetailDefinition : EntityDefinition<SampleEntity>
    {
        public override string Name => "Granit.Sample.Mixed";

        protected override void Configure(EntityDefinitionBuilder<SampleEntity> builder)
        {
            builder.Detail("default", d => d.Section("mix", s => s
                .InheritsFromForm("default")
                .Field(x => x.Title)));
        }
    }
}
