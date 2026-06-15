using Granit.AI.Tools.Exceptions;
using Granit.AI.Tools.Internal;
using Shouldly;

namespace Granit.AI.Tools.Tests;

public sealed class AIToolRegistryTests
{
    [Fact]
    public void Tools_are_returned_in_registration_order()
    {
        FakeAITool first = new(name: "first");
        FakeAITool second = new(name: "second");

        AIToolRegistry registry = new([first, second]);

        registry.Tools.ShouldBe([first, second]);
    }

    [Fact]
    public void TryGet_returns_a_registered_tool()
    {
        FakeAITool tool = new(name: "query_data");
        AIToolRegistry registry = new([tool]);

        registry.TryGet("query_data", out IAITool? resolved).ShouldBeTrue();
        resolved.ShouldBeSameAs(tool);
    }

    [Fact]
    public void TryGet_returns_false_for_an_unregistered_tool()
    {
        AIToolRegistry registry = new([new FakeAITool(name: "query_data")]);

        registry.TryGet("search", out IAITool? resolved).ShouldBeFalse();
        resolved.ShouldBeNull();
    }

    [Fact]
    public void Duplicate_tool_names_throw()
    {
        DuplicateAIToolException ex = Should.Throw<DuplicateAIToolException>(
            () => new AIToolRegistry([new FakeAITool(name: "dup"), new FakeAITool(name: "dup")]));

        ex.ToolName.ShouldBe("dup");
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("bad!name")]
    [InlineData("dotted.name")]
    public void Invalid_tool_names_throw(string name) =>
        Should.Throw<InvalidAIToolNameException>(() => new AIToolRegistry([new FakeAITool(name: name)]));

    [Theory]
    [InlineData("query_data")]
    [InlineData("search")]
    [InlineData("extract-text")]
    [InlineData("Tool123")]
    public void Valid_tool_names_are_accepted(string name)
    {
        AIToolRegistry registry = new([new FakeAITool(name: name)]);

        registry.TryGet(name, out _).ShouldBeTrue();
    }
}
