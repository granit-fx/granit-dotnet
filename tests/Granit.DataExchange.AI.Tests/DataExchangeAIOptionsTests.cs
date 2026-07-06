using Granit.DataExchange.AI.Options;
using Shouldly;

namespace Granit.DataExchange.AI.Tests;

public sealed class DataExchangeAIOptionsTests
{
    [Fact]
    public void SectionName_IsCorrect() =>
        DataExchangeAIOptions.SectionName.ShouldBe("DataExchange:AI");

    [Fact]
    public void Default_WorkspaceName_IsDefault()
    {
        DataExchangeAIOptions options = new();

        options.WorkspaceName.ShouldBe("default");
    }

    [Fact]
    public void Default_TimeoutSeconds_Is10()
    {
        DataExchangeAIOptions options = new();

        options.TimeoutSeconds.ShouldBe(10);
    }

    [Fact]
    public void Default_MinConfidenceScore_Is0_6()
    {
        DataExchangeAIOptions options = new();

        options.MinConfidenceScore.ShouldBe(0.6);
    }

    [Fact]
    public void Default_IncludePreviewRows_IsFalse()
    {
        DataExchangeAIOptions options = new();

        options.IncludePreviewRows.ShouldBeFalse();
    }

    [Fact]
    public void Default_PreviewRowCount_Is5()
    {
        DataExchangeAIOptions options = new();

        options.PreviewRowCount.ShouldBe(5);
    }
}
