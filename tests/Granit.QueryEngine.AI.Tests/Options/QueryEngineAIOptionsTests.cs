using Granit.QueryEngine.AI.Options;
using Shouldly;

namespace Granit.QueryEngine.AI.Tests.Options;

public sealed class QueryEngineAIOptionsTests
{
    [Fact]
    public void SectionName_is_QueryEngine_AI() => QueryEngineAIOptions.SectionName.ShouldBe("QueryEngine:AI");

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

}
