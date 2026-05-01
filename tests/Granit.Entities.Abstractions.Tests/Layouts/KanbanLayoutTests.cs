using Granit.Entities.Layouts;
using Shouldly;
using Xunit;

namespace Granit.Entities.Abstractions.Tests.Layouts;

public sealed class KanbanLayoutTests
{
    [Fact]
    public void Descriptor_carries_groupby_card_and_columns()
    {
        EntityDefinitionDescriptor d = new SampleTaskDefinition().Descriptor;

        d.ListLayouts.ShouldHaveSingleItem();
        KanbanLayoutDescriptor kanban = d.ListLayouts.Single().ShouldBeOfType<KanbanLayoutDescriptor>();

        kanban.Kind.ShouldBe(EntityListLayoutKind.Kanban);
        kanban.GroupByPropertyName.ShouldBe("Status");
        kanban.GroupByClrType.ShouldBe(typeof(SampleTaskStatus));
        kanban.Card.TitleProperty.ShouldBe("Title");
        kanban.Card.Fields.Select(f => f.PropertyName).ShouldBe(["AssignedTo", "DueDate"]);
        kanban.Columns.Select(c => c.Value).ShouldBe(["Todo", "InProgress", "Done", "Archived"]);
    }

    [Fact]
    public void Column_color_and_default_state_are_preserved()
    {
        EntityDefinitionDescriptor d = new SampleTaskDefinition().Descriptor;
        var kanban = (KanbanLayoutDescriptor)d.ListLayouts.Single();

        KanbanColumnDescriptor todo = kanban.Columns.Single(c => c.Value == "Todo");
        todo.Color.ShouldBe(KanbanColor.Gray);
        todo.DefaultState.ShouldBe(KanbanColumnState.Open);

        KanbanColumnDescriptor inProgress = kanban.Columns.Single(c => c.Value == "InProgress");
        inProgress.Color.ShouldBe(KanbanColor.Orange);

        KanbanColumnDescriptor done = kanban.Columns.Single(c => c.Value == "Done");
        done.Color.ShouldBe(KanbanColor.Green);

        KanbanColumnDescriptor archived = kanban.Columns.Single(c => c.Value == "Archived");
        archived.DefaultState.ShouldBe(KanbanColumnState.Hidden);
    }

    [Fact]
    public void IsDefault_and_RequiresPermission_round_trip()
    {
        EntityDefinitionDescriptor d = new SampleTaskDefinition().Descriptor;
        EntityListLayoutDescriptor kanban = d.ListLayouts.Single();

        kanban.IsDefault.ShouldBeTrue();
        kanban.RequiresPermission.ShouldBe("Tasks.Tasks.Kanban");
    }

    [Fact]
    public void Build_rejects_missing_groupby()
    {
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            _ = new MissingGroupByDefinition().Descriptor);

        ex.Message.ShouldContain("GroupBy");
    }

    [Fact]
    public void Build_rejects_non_property_groupby_lambda()
    {
        ArgumentException ex = Should.Throw<ArgumentException>(() =>
            _ = new BadGroupByLambdaDefinition().Descriptor);

        ex.Message.ShouldContain("GroupBy selector must be a direct property access");
    }

    [Fact]
    public void Build_rejects_non_property_title_lambda()
    {
        ArgumentException ex = Should.Throw<ArgumentException>(() =>
            _ = new BadTitleLambdaDefinition().Descriptor);

        ex.Message.ShouldContain("Title selector must be a direct property access");
    }

    [Fact]
    public void Build_rejects_two_default_layouts()
    {
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            _ = new TwoDefaultsDefinition().Descriptor);

        ex.Message.ShouldContain("At most one list-view layout may be marked IsDefault");
    }

    [Fact]
    public void Build_rejects_duplicate_layout_kinds()
    {
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            _ = new DuplicateKindsDefinition().Descriptor);

        ex.Message.ShouldContain("Duplicate list-view layout kind 'Kanban'");
    }

    [Fact]
    public void Card_field_options_round_trip()
    {
        EntityDefinitionDescriptor d = new SampleTaskDefinition().Descriptor;
        var kanban = (KanbanLayoutDescriptor)d.ListLayouts.Single();

        Forms.FieldDescriptor due = kanban.Card.Fields.Single(f => f.PropertyName == "DueDate");
        due.ReadOnly.ShouldBeTrue();
        due.LabelKey.ShouldBe("Tasks:Field.DueDate");
    }

    private enum SampleTaskStatus { Todo, InProgress, Done, Archived }

    private sealed class SampleTask
    {
        public string Title { get; set; } = string.Empty;
        public SampleTaskStatus Status { get; set; }
        public string AssignedTo { get; set; } = string.Empty;
        public DateOnly DueDate { get; set; }
    }

    private sealed class SampleTaskDefinition : EntityDefinition<SampleTask>
    {
        public override string Name => "Granit.Sample.Task";

        protected override void Configure(EntityDefinitionBuilder<SampleTask> builder) =>
            builder.KanbanView<SampleTaskStatus>(k => k
                .IsDefault()
                .RequiresPermission("Tasks.Tasks.Kanban")
                .GroupBy(t => t.Status)
                .Card(c => c
                    .Title(t => t.Title)
                    .Field(t => t.AssignedTo)
                    .Field(t => t.DueDate, f => f.ReadOnly().Label("Tasks:Field.DueDate")))
                .Column(SampleTaskStatus.Todo, c => c.Color(KanbanColor.Gray))
                .Column(SampleTaskStatus.InProgress, c => c.Color(KanbanColor.Orange))
                .Column(SampleTaskStatus.Done, c => c.Color(KanbanColor.Green))
                .Column(SampleTaskStatus.Archived, c => c.Color(KanbanColor.Neutral).Hidden()));
    }

    private sealed class MissingGroupByDefinition : EntityDefinition<SampleTask>
    {
        public override string Name => "Granit.Sample.MissingGroupBy";
        protected override void Configure(EntityDefinitionBuilder<SampleTask> builder) =>
            builder.KanbanView<SampleTaskStatus>(k => k.Card(c => c.Field(t => t.Title)));
    }

    private sealed class BadGroupByLambdaDefinition : EntityDefinition<SampleTask>
    {
        public override string Name => "Granit.Sample.BadGroupBy";
        protected override void Configure(EntityDefinitionBuilder<SampleTask> builder) =>
            builder.KanbanView<string>(k => k.GroupBy(t => t.Title.ToUpperInvariant()));
    }

    private sealed class BadTitleLambdaDefinition : EntityDefinition<SampleTask>
    {
        public override string Name => "Granit.Sample.BadTitle";
        protected override void Configure(EntityDefinitionBuilder<SampleTask> builder) =>
            builder.KanbanView<SampleTaskStatus>(k => k
                .GroupBy(t => t.Status)
                .Card(c => c.Title(t => t.Title.ToUpperInvariant())));
    }

    private sealed class TwoDefaultsDefinition : EntityDefinition<SampleTask>
    {
        public override string Name => "Granit.Sample.TwoDefaults";
        protected override void Configure(EntityDefinitionBuilder<SampleTask> builder) =>
            builder
                .KanbanView<SampleTaskStatus>(k => k.IsDefault().GroupBy(t => t.Status))
                .KanbanView<string>(k => k.IsDefault().GroupBy(t => t.AssignedTo));
    }

    private sealed class DuplicateKindsDefinition : EntityDefinition<SampleTask>
    {
        public override string Name => "Granit.Sample.DuplicateKinds";
        protected override void Configure(EntityDefinitionBuilder<SampleTask> builder) =>
            builder
                .KanbanView<SampleTaskStatus>(k => k.GroupBy(t => t.Status))
                .KanbanView<string>(k => k.GroupBy(t => t.AssignedTo));
    }
}
