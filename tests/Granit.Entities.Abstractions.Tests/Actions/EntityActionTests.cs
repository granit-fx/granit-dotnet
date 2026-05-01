using Granit.Entities.Actions;
using Shouldly;
using Xunit;

namespace Granit.Entities.Abstractions.Tests.Actions;

public sealed class EntityActionTests
{
    [Fact]
    public void Action_apicall_records_method_url_and_kind()
    {
        EntityDefinitionDescriptor d = new SampleDefinition().Descriptor;

        EntityActionDescriptor finalize = d.Actions.Single(a => a.Name == "finalize");
        finalize.Kind.ShouldBe(EntityActionKind.ApiCall);
        finalize.HttpMethod.ShouldBe("POST");
        finalize.UrlTemplate.ShouldBe("/api/v1/orders/{id}/finalize");
        finalize.RequiresPermission.ShouldBe("Orders.Manage");
        finalize.ConfirmationKey.ShouldBe("Orders:Action.Finalize.Confirm");
        finalize.Icon.ShouldBe("check");
        finalize.Order.ShouldBe(10);
    }

    [Fact]
    public void Action_download_records_url_only_no_method()
    {
        EntityDefinitionDescriptor d = new SampleDefinition().Descriptor;

        EntityActionDescriptor pdf = d.Actions.Single(a => a.Name == "download-pdf");
        pdf.Kind.ShouldBe(EntityActionKind.Download);
        pdf.HttpMethod.ShouldBeNull();
        pdf.UrlTemplate.ShouldBe("/api/v1/orders/{id}/pdf");
    }

    [Fact]
    public void Action_navigate_records_url_with_navigate_kind()
    {
        EntityDefinitionDescriptor d = new SampleDefinition().Descriptor;

        EntityActionDescriptor merge = d.Actions.Single(a => a.Name == "merge");
        merge.Kind.ShouldBe(EntityActionKind.Navigate);
        merge.HttpMethod.ShouldBeNull();
        merge.UrlTemplate.ShouldBe("/w/orders/{id}/merge");
    }

    [Fact]
    public void Action_workflowtransition_records_target_state_no_url()
    {
        EntityDefinitionDescriptor d = new SampleDefinition().Descriptor;

        EntityActionDescriptor archive = d.Actions.Single(a => a.Name == "archive");
        archive.Kind.ShouldBe(EntityActionKind.WorkflowTransition);
        archive.UrlTemplate.ShouldBeNull();
        archive.HttpMethod.ShouldBeNull();
        archive.WorkflowTransitionName.ShouldBe("Archived");
    }

    [Fact]
    public void Actions_sorted_by_order_then_name()
    {
        EntityDefinitionDescriptor d = new SampleDefinition().Descriptor;

        d.Actions.Select(a => a.Name).ShouldBe(
            ["finalize", "archive", "download-pdf", "merge"]);
    }

    [Fact]
    public void Action_intramodule_has_no_contributor_assembly_name()
    {
        EntityDefinitionDescriptor d = new SampleDefinition().Descriptor;

        d.Actions.ShouldAllBe(a => a.ContributorAssemblyName == null);
    }

    [Fact]
    public void Build_rejects_action_without_kind_shortcut()
    {
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            _ = new BareActionDefinition().Descriptor);

        ex.Message.ShouldContain("must declare a URL via ApiCall");
    }

    [Fact]
    public void Build_rejects_duplicate_action_names()
    {
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            _ = new DuplicateActionNamesDefinition().Descriptor);

        ex.Message.ShouldContain("Duplicate action name 'finalize'");
    }

    private sealed class SampleEntity;

    private sealed class SampleDefinition : EntityDefinition<SampleEntity>
    {
        public override string Name => "Granit.Sample.Order";

        protected override void Configure(EntityDefinitionBuilder<SampleEntity> builder) =>
            builder
                .Action("finalize", a => a
                    .ApiCall("POST", "/api/v1/orders/{id}/finalize")
                    .DisplayKey("Orders:Action.Finalize")
                    .Icon("check")
                    .Order(10)
                    .RequiresPermission("Orders.Manage")
                    .Confirmation("Orders:Action.Finalize.Confirm"))
                .Action("download-pdf", a => a
                    .Download("/api/v1/orders/{id}/pdf")
                    .DisplayKey("Orders:Action.Pdf")
                    .Icon("file-down")
                    .Order(30))
                .Action("merge", a => a
                    .Navigate("/w/orders/{id}/merge")
                    .DisplayKey("Orders:Action.Merge")
                    .Icon("git-merge")
                    .Order(40))
                .Action("archive", a => a
                    .WorkflowTransition("Archived")
                    .DisplayKey("Orders:Action.Archive")
                    .Icon("archive")
                    .Order(20)
                    .RequiresPermission("Orders.Manage"));
    }

    private sealed class BareActionDefinition : EntityDefinition<SampleEntity>
    {
        public override string Name => "Granit.Sample.Bare";

        protected override void Configure(EntityDefinitionBuilder<SampleEntity> builder) =>
            builder.Action("naked", a => a.DisplayKey("X").Icon("y").Order(1));
    }

    private sealed class DuplicateActionNamesDefinition : EntityDefinition<SampleEntity>
    {
        public override string Name => "Granit.Sample.Duplicates";

        protected override void Configure(EntityDefinitionBuilder<SampleEntity> builder) =>
            builder
                .Action("finalize", a => a.ApiCall("POST", "/x"))
                .Action("finalize", a => a.ApiCall("POST", "/y"));
    }

    [Fact]
    public void OnKanbanCard_defaults_false_and_opt_in_sets_flag()
    {
        EntityDefinitionDescriptor d = new SampleDefinition().Descriptor;

        d.Actions.Single(a => a.Name == "finalize").ShowOnKanbanCard.ShouldBeFalse();

        EntityDefinitionDescriptor pinned = new KanbanPinnedDefinition().Descriptor;
        pinned.Actions.Single(a => a.Name == "quick-note").ShowOnKanbanCard.ShouldBeTrue();
        pinned.Actions.Single(a => a.Name == "archive").ShowOnKanbanCard.ShouldBeFalse();
    }

    private sealed class KanbanPinnedDefinition : EntityDefinition<SampleEntity>
    {
        public override string Name => "Granit.Sample.KanbanPinned";

        protected override void Configure(EntityDefinitionBuilder<SampleEntity> builder) =>
            builder
                .Action("quick-note", a => a
                    .ApiCall("POST", "/api/v1/orders/{id}/notes")
                    .Icon("note-plus")
                    .OnKanbanCard())
                .Action("archive", a => a
                    .WorkflowTransition("Archived")
                    .Icon("archive"));
    }
}
