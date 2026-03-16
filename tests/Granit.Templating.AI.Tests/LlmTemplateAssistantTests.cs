using Granit.AI;
using Granit.Templating.AI.Internal;
using Granit.Templating.AI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Granit.Templating.AI.Tests;

public sealed class LlmTemplateAssistantTests
{
    private readonly IAIChatClientFactory _chatClientFactory = Substitute.For<IAIChatClientFactory>();
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    private readonly ILogger<LlmTemplateAssistant> _logger = NullLogger<LlmTemplateAssistant>.Instance;

    private readonly TemplatingAIOptions _options = new()
    {
        WorkspaceName = "test-workspace",
        TimeoutSeconds = 10,
    };

    private LlmTemplateAssistant CreateAssistant() =>
        new(_chatClientFactory, Microsoft.Extensions.Options.Options.Create(_options), _logger);

    private void SetupChatClient(string responseText)
    {
        _chatClientFactory
            .CreateAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_chatClient);

        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText));
        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(response);
    }

    [Fact]
    public async Task GenerateDraftAsync_ValidResponse_ReturnsTemplate()
    {
        const string htmlTemplate = "<html><body><h1>{{ supplier }}</h1><p>{{ amount }}</p></body></html>";
        SetupChatClient(htmlTemplate);

        LlmTemplateAssistant assistant = CreateAssistant();

        string? result = await assistant.GenerateDraftAsync(
            "Generate an invoice email template",
            typeof(SampleInvoiceData),
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.ShouldContain("<html>");
        result.ShouldContain("{{ supplier }}");
    }

    [Fact]
    public async Task GenerateDraftAsync_LLMFailure_ReturnsNull()
    {
        _chatClientFactory
            .CreateAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("LLM provider unavailable"));

        LlmTemplateAssistant assistant = CreateAssistant();

        string? result = await assistant.GenerateDraftAsync(
            "Generate an invoice template",
            typeof(SampleInvoiceData),
            TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GenerateDraftAsync_StripsMarkdownFences()
    {
        const string wrappedResponse = """
            ```html
            <html><body><h1>Invoice</h1></body></html>
            ```
            """;
        SetupChatClient(wrappedResponse);

        LlmTemplateAssistant assistant = CreateAssistant();

        string? result = await assistant.GenerateDraftAsync(
            "Generate an invoice template",
            typeof(SampleInvoiceData),
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.ShouldNotContain("```");
        result.ShouldContain("<html>");
    }

    [Fact]
    public async Task GenerateDraftAsync_IncludesPropertyNames()
    {
        const string htmlTemplate = "<html><body>{{ supplier }} {{ amount }} {{ invoice_date }}</body></html>";
        SetupChatClient(htmlTemplate);

        LlmTemplateAssistant assistant = CreateAssistant();

        await assistant.GenerateDraftAsync(
            "Generate an invoice template",
            typeof(SampleInvoiceData),
            TestContext.Current.CancellationToken);

        // Verify the prompt sent to the LLM contains property names from the data type
        await _chatClient.Received(1).GetResponseAsync(
            Arg.Is<IEnumerable<ChatMessage>>(msgs =>
                string.Join("", msgs.Select(m => m.Text)).Contains("Supplier") &&
                string.Join("", msgs.Select(m => m.Text)).Contains("Amount") &&
                string.Join("", msgs.Select(m => m.Text)).Contains("InvoiceDate")),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void BuildPrompt_ContainsDescriptionAndProperties()
    {
        string prompt = LlmTemplateAssistant.BuildPrompt("Generate an invoice email", typeof(SampleInvoiceData));

        prompt.ShouldContain("Generate an invoice email");
        prompt.ShouldContain("Supplier");
        prompt.ShouldContain("String");
        prompt.ShouldContain("Amount");
        prompt.ShouldContain("Decimal");
        prompt.ShouldContain("InvoiceDate");
        prompt.ShouldContain("DateOnly");
        prompt.ShouldContain("snake_case");
    }

    [Fact]
    public void BuildPrompt_HandlesNullableProperties()
    {
        string prompt = LlmTemplateAssistant.BuildPrompt("Test", typeof(SampleNullableData));

        prompt.ShouldContain("Int32?");
        prompt.ShouldContain("String");
    }

    [Fact]
    public void StripMarkdownFences_WithFences_StripsCorrectly()
    {
        const string input = """
            ```html
            <div>Content</div>
            ```
            """;

        string result = LlmTemplateAssistant.StripMarkdownFences(input);

        result.ShouldNotContain("```");
        result.ShouldContain("<div>Content</div>");
    }

    [Fact]
    public void StripMarkdownFences_WithoutFences_ReturnsOriginal()
    {
        const string input = "<div>Content</div>";

        string result = LlmTemplateAssistant.StripMarkdownFences(input);

        result.ShouldBe("<div>Content</div>");
    }

    [Fact]
    public async Task GenerateDraftAsync_EmptyResponse_ReturnsNull()
    {
        SetupChatClient("   ");

        LlmTemplateAssistant assistant = CreateAssistant();

        string? result = await assistant.GenerateDraftAsync(
            "Generate a template",
            typeof(SampleInvoiceData),
            TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    /// <summary>
    /// Sample data type used in tests to simulate invoice template data.
    /// </summary>
    internal sealed class SampleInvoiceData
    {
        public string Supplier { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateOnly InvoiceDate { get; set; }
    }

    /// <summary>
    /// Sample data type with nullable properties for testing type name resolution.
    /// </summary>
    internal sealed class SampleNullableData
    {
        public string Name { get; set; } = string.Empty;
        public int? OptionalCount { get; set; }
    }
}
