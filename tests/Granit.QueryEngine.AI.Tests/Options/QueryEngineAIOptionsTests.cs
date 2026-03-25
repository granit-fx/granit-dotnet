using Granit.QueryEngine.AI.Options;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.AI.Tests.Options;

public sealed class QueryEngineAIOptionsTests
{
    [Fact]
    public void SectionName_is_AI_QueryEngine() => QueryEngineAIOptions.SectionName.ShouldBe("AI:QueryEngine");

    [Fact]
    public void WorkspaceName_defaults_to_default()
    {
        QueryEngineAIOptions options = new();

        options.WorkspaceName.ShouldBe("default");
    }

    [Fact]
    public void TimeoutSeconds_defaults_to_5()
    {
        QueryEngineAIOptions options = new();

        options.TimeoutSeconds.ShouldBe(5);
    }

    [Fact]
    public void WorkspaceName_can_be_set()
    {
        QueryEngineAIOptions options = new() { WorkspaceName = "production" };

        options.WorkspaceName.ShouldBe("production");
    }

    [Fact]
    public void TimeoutSeconds_can_be_set()
    {
        QueryEngineAIOptions options = new() { TimeoutSeconds = 30 };

        options.TimeoutSeconds.ShouldBe(30);
    }
}
