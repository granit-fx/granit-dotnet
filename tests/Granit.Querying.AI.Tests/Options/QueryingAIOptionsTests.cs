using Granit.Querying.AI.Options;
using Shouldly;
using Xunit;

namespace Granit.Querying.AI.Tests.Options;

public sealed class QueryingAIOptionsTests
{
    [Fact]
    public void SectionName_is_AI_Querying() => QueryingAIOptions.SectionName.ShouldBe("AI:Querying");

    [Fact]
    public void WorkspaceName_defaults_to_default()
    {
        QueryingAIOptions options = new();

        options.WorkspaceName.ShouldBe("default");
    }

    [Fact]
    public void TimeoutSeconds_defaults_to_5()
    {
        QueryingAIOptions options = new();

        options.TimeoutSeconds.ShouldBe(5);
    }

    [Fact]
    public void WorkspaceName_can_be_set()
    {
        QueryingAIOptions options = new() { WorkspaceName = "production" };

        options.WorkspaceName.ShouldBe("production");
    }

    [Fact]
    public void TimeoutSeconds_can_be_set()
    {
        QueryingAIOptions options = new() { TimeoutSeconds = 30 };

        options.TimeoutSeconds.ShouldBe(30);
    }
}
