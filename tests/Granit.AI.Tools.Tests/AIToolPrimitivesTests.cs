using System.Text.Json;
using Shouldly;

namespace Granit.AI.Tools.Tests;

public sealed class AIToolPrimitivesTests
{
    [Fact]
    public void Success_result_is_not_an_error()
    {
        var result = AIToolResult.Success("payload");

        result.Content.ShouldBe("payload");
        result.IsError.ShouldBeFalse();
    }

    [Fact]
    public void Error_result_is_flagged()
    {
        var result = AIToolResult.Error("nope");

        result.Content.ShouldBe("nope");
        result.IsError.ShouldBeTrue();
    }

    [Fact]
    public void Empty_schema_is_a_parameterless_object()
    {
        JsonElement schema = AIToolSchema.Empty;

        schema.GetProperty("type").GetString().ShouldBe("object");
        schema.GetProperty("properties").EnumerateObject().ShouldBeEmpty();
    }
}
