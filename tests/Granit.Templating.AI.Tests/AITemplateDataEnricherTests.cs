using Granit.AI;
using Granit.Templating.AI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Granit.Templating.AI.Tests;

public sealed class AITemplateDataEnricherTests
{
    private sealed record TestData(string Name, string? Summary = null);

    private sealed class TestEnricher(
        IAIChatClientFactory chatClientFactory,
        IOptions<TemplatingAIOptions> options,
        ILogger logger)
        : AITemplateDataEnricher<TestData>(chatClientFactory, options, logger)
    {
        public override int Order => 10;

        protected override string BuildPrompt(TestData data) =>
            $"Summarize: {data.Name}";

        protected override TestData ApplyEnrichment(TestData data, string llmResponse) =>
            data with { Summary = llmResponse };
    }

    private readonly IAIChatClientFactory _chatClientFactory = Substitute.For<IAIChatClientFactory>();
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    private readonly ILogger _logger = NullLogger.Instance;

    private readonly TemplatingAIOptions _options = new()
    {
        WorkspaceName = "test",
        TimeoutSeconds = 5,
    };

    private TestEnricher CreateEnricher() =>
        new(_chatClientFactory, Microsoft.Extensions.Options.Options.Create(_options), _logger);

    private void SetupChatClient(string responseText)
    {
        _chatClientFactory
            .CreateAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_chatClient);

        ChatResponse response = new(new ChatMessage(ChatRole.Assistant, responseText));
        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(response);
    }

    [Fact]
    public void Order_ReturnsConfiguredValue()
    {
        TestEnricher enricher = CreateEnricher();

        enricher.Order.ShouldBe(10);
    }

    [Fact]
    public async Task EnrichAsync_ValidResponse_AppliesEnrichment()
    {
        SetupChatClient("A great summary");
        TestEnricher enricher = CreateEnricher();
        TestData data = new("Test Name");

        TestData result = await enricher.EnrichAsync(data, TestContext.Current.CancellationToken);

        result.Summary.ShouldBe("A great summary");
        result.Name.ShouldBe("Test Name");
    }

    [Fact]
    public async Task EnrichAsync_EmptyResponse_ReturnsOriginalData()
    {
        SetupChatClient("   ");
        TestEnricher enricher = CreateEnricher();
        TestData data = new("Test Name");

        TestData result = await enricher.EnrichAsync(data, TestContext.Current.CancellationToken);

        result.Summary.ShouldBeNull();
        result.ShouldBe(data);
    }

    [Fact]
    public async Task EnrichAsync_LLMFailure_ReturnsOriginalData()
    {
        _chatClientFactory
            .CreateAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("LLM unavailable"));

        TestEnricher enricher = CreateEnricher();
        TestData data = new("Test Name");

        TestData result = await enricher.EnrichAsync(data, TestContext.Current.CancellationToken);

        result.ShouldBe(data);
    }

    [Fact]
    public async Task EnrichAsync_NullData_ThrowsArgumentNullException()
    {
        TestEnricher enricher = CreateEnricher();

        Func<Task> act = () => enricher.EnrichAsync(null!, TestContext.Current.CancellationToken);

        await Should.ThrowAsync<ArgumentNullException>(act);
    }
}
