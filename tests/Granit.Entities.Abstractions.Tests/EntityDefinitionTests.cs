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
    public void FieldBuilder_ChoosesDefaultWidget_FromClrType()
    {
        EntityDefinitionDescriptor d = new SampleEntityDefinition().Descriptor;

        var fields = d.Forms[0].Sections.SelectMany(s => s.Fields).ToDictionary(f => f.PropertyName);

        fields["Title"].Widget.ShouldBe("text");
        fields["Active"].Widget.ShouldBe("boolean");
        fields["IssuedAt"].Widget.ShouldBe("datetime");
        // Amount is overridden to "money" in the fixture (covered by FieldBuilder_OverridesWidget_AndConfig).
    }

    [Fact]
    public void FieldBuilder_OverridesWidget_AndConfig()
    {
        EntityDefinitionDescriptor d = new SampleEntityDefinition().Descriptor;
        FieldDescriptor amount = d.Forms[0].Sections.SelectMany(s => s.Fields).First(f => f.PropertyName == "Amount");

        amount.Widget.ShouldBe("money");
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

        protected override void Configure(EntityDefinitionBuilder<SampleEntity> b)
        {
            b.DisplayKey("Entity:SampleEntity")
             .Icon("box")
             .PermissionGroup("Sample.SampleEntities")
             .DisplayProperty(s => s.Title);

            b.Query<SampleQueryDefinition>();
            b.Export<SampleExportDefinition>();
            b.Workflow<SampleWorkflowDefinition>();
            b.Metric<SampleMetricA>();
            b.Metric<SampleMetricB>();
            b.Dashboard<SampleDashboard>();

            b.Form("default", f => f
                .Section("general", s => s
                    .Field(x => x.Title)
                    .Field(x => x.Amount, fld => fld
                        .Widget("money", new Dictionary<string, object?>(StringComparer.Ordinal) { ["currencyCode"] = "EUR" })
                        .RequiresPermission("Sample.SampleEntities.Manage"))
                    .Field(x => x.Active)
                    .Field(x => x.IssuedAt)
                    .Field(x => x.Notes, fld => fld.VisibleIf("Status", FieldOp.Eq, "Draft")))
                .Customizable());

            b.Form("quick", f => f
                .Section("essentials", s => s
                    .Field(x => x.Title)
                    .Field(x => x.Amount)));

            b.Detail("default", d => d
                .SectionsFromForm()
                .SidePanel.Audit().Timeline());
        }
    }

    private sealed class DuplicateFormDefinition : EntityDefinition<SampleEntity>
    {
        public override string Name => "Granit.Sample.Dup";

        protected override void Configure(EntityDefinitionBuilder<SampleEntity> b)
        {
            b.Form("default", f => f.Section("a", s => s.Field(x => x.Title)));
            b.Form("default", f => f.Section("b", s => s.Field(x => x.Title)));
        }
    }

    private sealed class MixedDetailDefinition : EntityDefinition<SampleEntity>
    {
        public override string Name => "Granit.Sample.Mixed";

        protected override void Configure(EntityDefinitionBuilder<SampleEntity> b)
        {
            b.Detail("default", d => d.Section("mix", s => s
                .InheritsFromForm("default")
                .Field(x => x.Title)));
        }
    }
}
