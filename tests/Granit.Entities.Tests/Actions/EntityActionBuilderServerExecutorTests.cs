using System.Text.Json;
using Granit.Entities.Actions;
using Granit.Entities.Actions.Execution;
using Shouldly;
using Xunit;

namespace Granit.Entities.Tests.Actions;

public sealed class EntityActionBuilderServerExecutorTests
{
    private sealed class Widget { }

    private sealed class WidgetExecutor : IEntityActionExecutor<Widget>
    {
        public Task<EntityActionExecutionResult<Widget>> ExecuteAsync(
            Guid id, JsonElement payload, CancellationToken cancellationToken) =>
            Task.FromResult(new EntityActionExecutionResult<Widget>(new Widget(), null));
    }

    [Fact]
    public void ServerExecutor_flags_descriptor_and_captures_type()
    {
        var b = new EntityDefinitionBuilder<Widget>();
        b.RouteBase("/api/widgets");
        b.Action("archive", a => a
            .Post("archive")
            .RequiresPermission("Widgets.Widgets.Manage")
            .ServerExecutor<WidgetExecutor>());

        EntityDefinitionDescriptor d = b.Build("Test.Widget");
        EntityActionDescriptor action = d.Actions.ShouldHaveSingleItem();
        action.RequiresServerExecution.ShouldBeTrue();
        action.ServerExecutorType.ShouldBe(typeof(WidgetExecutor));
    }

    [Fact]
    public void Descriptor_defaults_to_declarative_action()
    {
        var b = new EntityDefinitionBuilder<Widget>();
        b.RouteBase("/api/widgets");
        b.Action("legacy", a => a.Post("legacy"));

        EntityActionDescriptor action = b.Build("Test.Widget").Actions.ShouldHaveSingleItem();
        action.RequiresServerExecution.ShouldBeFalse();
        action.ServerExecutorType.ShouldBeNull();
    }
}
