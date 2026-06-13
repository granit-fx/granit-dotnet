using System.Text.Json;
using Granit.AI.Tools.Internal;
using Microsoft.Extensions.AI;
using Shouldly;

namespace Granit.AI.Tools.Tests;

public sealed class AIToolProjectorTests
{
    [Fact]
    public void Projects_each_tool_to_an_AIFunction_with_matching_declaration()
    {
        FakeAITool tool = new(name: "query_data", description: "Queries data.");
        AIToolProjector projector = new(new AIToolRegistry([tool]));

        IReadOnlyList<AITool> projected = projector.ProjectAll();

        AITool only = projected.ShouldHaveSingleItem();
        AIFunction function = only.ShouldBeOfType<GranitAIToolFunction>();
        function.Name.ShouldBe("query_data");
        function.Description.ShouldBe("Queries data.");
        function.JsonSchema.GetProperty("type").GetString().ShouldBe("object");
    }

    [Fact]
    public async Task Invoking_the_projected_function_forwards_arguments_and_returns_content()
    {
        FakeAITool tool = new(name: "echo", result: "done");
        AIToolProjector projector = new(new AIToolRegistry([tool]));
        var function = (AIFunction)projector.ProjectAll()[0];

        object? result = await function.InvokeAsync(
            new AIFunctionArguments { ["query"] = "hello" },
            TestContext.Current.CancellationToken);

        result.ShouldBe("done");

        using var forwarded = JsonDocument.Parse(tool.LastArgumentsJson!);
        forwarded.RootElement.GetProperty("query").GetString().ShouldBe("hello");
    }

    [Fact]
    public void Project_maps_an_explicit_set()
    {
        AIToolProjector projector = new(new AIToolRegistry([]));

        IReadOnlyList<AITool> projected = projector.Project(
            [new FakeAITool(name: "a"), new FakeAITool(name: "b")]);

        projected.Select(t => t.Name).ShouldBe(["a", "b"]);
    }
}
