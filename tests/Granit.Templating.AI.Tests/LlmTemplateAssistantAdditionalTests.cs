using Granit.AI;
using Granit.Templating.AI.Internal;
using Granit.Templating.AI.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace Granit.Templating.AI.Tests;

public sealed class LlmTemplateAssistantAdditionalTests
{
    [Fact]
    public async Task GenerateDraftAsync_NullDescription_ThrowsArgumentNullException()
    {
        IAIChatClientFactory factory = Substitute.For<IAIChatClientFactory>();
        ILogger<LlmTemplateAssistant> logger = NullLogger<LlmTemplateAssistant>.Instance;
        TemplatingAIOptions options = new();
        LlmTemplateAssistant assistant = new(factory, Microsoft.Extensions.Options.Options.Create(options), logger);

        Func<Task> act = () => assistant.GenerateDraftAsync(
            null!, typeof(string), TestContext.Current.CancellationToken);

        await Should.ThrowAsync<ArgumentNullException>(act);
    }

    [Fact]
    public async Task GenerateDraftAsync_NullDataType_ThrowsArgumentNullException()
    {
        IAIChatClientFactory factory = Substitute.For<IAIChatClientFactory>();
        ILogger<LlmTemplateAssistant> logger = NullLogger<LlmTemplateAssistant>.Instance;
        TemplatingAIOptions options = new();
        LlmTemplateAssistant assistant = new(factory, Microsoft.Extensions.Options.Options.Create(options), logger);

        Func<Task> act = () => assistant.GenerateDraftAsync(
            "Generate template", null!, TestContext.Current.CancellationToken);

        await Should.ThrowAsync<ArgumentNullException>(act);
    }

    [Fact]
    public void BuildPrompt_WithGenericListProperty_HandlesGenericType()
    {
        string prompt = LlmTemplateAssistant.BuildPrompt("Test", typeof(GenericData));

        prompt.ShouldContain("List<String>");
        prompt.ShouldContain("Items");
    }

    [Fact]
    public void BuildPrompt_WithAllBasicTypes_HandlesFriendlyNames()
    {
        string prompt = LlmTemplateAssistant.BuildPrompt("Test", typeof(AllTypesData));

        prompt.ShouldContain("String");
        prompt.ShouldContain("Int32");
        prompt.ShouldContain("Int64");
        prompt.ShouldContain("Decimal");
        prompt.ShouldContain("Double");
        prompt.ShouldContain("Single");
        prompt.ShouldContain("Boolean");
        prompt.ShouldContain("DateTime");
        prompt.ShouldContain("DateTimeOffset");
        prompt.ShouldContain("Guid");
        prompt.ShouldContain("TimeOnly");
    }

    [Fact]
    public void StripMarkdownFences_FencesWithoutNewline_HandlesGracefully()
    {
        // Edge case: ``` with no newline after it
        string result = LlmTemplateAssistant.StripMarkdownFences("```");

        // After removing opening ```, no newline found so entire string from index after end
        // The method handles this by checking firstNewline >= 0
        result.ShouldNotBeNull();
    }

    [Fact]
    public void StripMarkdownFences_EmptyString_ReturnsEmpty()
    {
        string result = LlmTemplateAssistant.StripMarkdownFences("");

        result.ShouldBe("");
    }

    internal sealed class GenericData
    {
        public List<string> Items { get; set; } = [];
    }

    internal sealed class AllTypesData
    {
        public string StringProp { get; set; } = string.Empty;
        public int IntProp { get; set; }
        public long LongProp { get; set; }
        public decimal DecimalProp { get; set; }
        public double DoubleProp { get; set; }
        public float FloatProp { get; set; }
        public bool BoolProp { get; set; }
        public DateTime DateTimeProp { get; set; }
        public DateTimeOffset DateTimeOffsetProp { get; set; }
        public Guid GuidProp { get; set; }
        public TimeOnly TimeOnlyProp { get; set; }
    }
}
