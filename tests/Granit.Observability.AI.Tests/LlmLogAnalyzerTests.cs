using System.Diagnostics.Metrics;
using Granit.AI;
using Granit.Observability.AI.Diagnostics;
using Granit.Observability.AI.Internal;
using Granit.Observability.AI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Granit.Observability.AI.Tests;

public sealed class LlmLogAnalyzerTests : IDisposable
{
    private readonly IAIChatClientFactory _chatClientFactory = Substitute.For<IAIChatClientFactory>();
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();

    private readonly IOptions<ObservabilityAIOptions> _options =
        Microsoft.Extensions.Options.Options.Create(new ObservabilityAIOptions
        {
            WorkspaceName = "test-workspace",
            TimeoutSeconds = 30,
            MaxLogEntries = 500,
        });

    private readonly TestMeterFactory _meterFactory = new();

    private LlmLogAnalyzer CreateAnalyzer() =>
        new(_chatClientFactory, _options, new ObservabilityAIMetrics(_meterFactory), null, NullLogger<LlmLogAnalyzer>.Instance);

    public void Dispose() => _meterFactory.Dispose();

    private sealed class TestMeterFactory : IMeterFactory
    {
        public Meter Create(MeterOptions options) => new(options);
        public void Dispose() { }
    }

    private static List<LogEntry> CreateSampleEntries(int count = 3) =>
        Enumerable.Range(0, count)
            .Select(i => new LogEntry(
                DateTimeOffset.UtcNow.AddMinutes(-count + i),
                i % 3 == 0 ? "Error" : "Information",
                $"Log message {i}",
                i % 3 == 0 ? $"System.Exception: Error {i}" : null))
            .ToList();

    [Fact]
    public async Task AnalyzeAsync_EmptyEntries_ReturnsEmptyReport()
    {
        LlmLogAnalyzer analyzer = CreateAnalyzer();

        LogAnalysisReport report = await analyzer.AnalyzeAsync([], TestContext.Current.CancellationToken);

        report.Summary.ShouldBe("No log entries to analyze.");
        report.Insights.ShouldBeEmpty();
        report.TotalEntries.ShouldBe(0);
    }

    [Fact]
    public async Task AnalyzeAsync_NullEntries_ThrowsArgumentNullException()
    {
        LlmLogAnalyzer analyzer = CreateAnalyzer();

        await Should.ThrowAsync<ArgumentNullException>(
            () => analyzer.AnalyzeAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnalyzeAsync_ValidEntries_CallsChatClientAndReturnsReport()
    {
        const string llmResponse = """
            {
              "summary": "Found 1 error pattern",
              "insights": [
                {
                  "description": "Recurring NullReferenceException",
                  "severity": "High",
                  "category": "Pattern"
                }
              ]
            }
            """;

        _chatClientFactory
            .CreateAsync("test-workspace", Arg.Any<CancellationToken>())
            .Returns(_chatClient);

        _chatClient
            .GetResponseAsync(Arg.Any<IList<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, llmResponse)));

        LlmLogAnalyzer analyzer = CreateAnalyzer();
        List<LogEntry> entries = CreateSampleEntries();

        LogAnalysisReport report = await analyzer.AnalyzeAsync(entries, TestContext.Current.CancellationToken);

        report.Summary.ShouldBe("Found 1 error pattern");
        report.Insights.Count.ShouldBe(1);
        report.Insights[0].Description.ShouldBe("Recurring NullReferenceException");
        report.Insights[0].Severity.ShouldBe("High");
        report.Insights[0].Category.ShouldBe("Pattern");
        report.TotalEntries.ShouldBe(3);
    }

    [Fact]
    public async Task AnalyzeAsync_EntriesExceedMax_TruncatesToMaxLogEntries()
    {
        IOptions<ObservabilityAIOptions> smallOptions =
            Microsoft.Extensions.Options.Options.Create(new ObservabilityAIOptions
            {
                WorkspaceName = "test-workspace",
                TimeoutSeconds = 30,
                MaxLogEntries = 2,
            });

        const string llmResponse = """{"summary": "OK", "insights": []}""";

        _chatClientFactory
            .CreateAsync("test-workspace", Arg.Any<CancellationToken>())
            .Returns(_chatClient);

        _chatClient
            .GetResponseAsync(Arg.Any<IList<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, llmResponse)));

        var analyzer = new LlmLogAnalyzer(_chatClientFactory, smallOptions, new ObservabilityAIMetrics(_meterFactory), null, NullLogger<LlmLogAnalyzer>.Instance);
        List<LogEntry> entries = CreateSampleEntries(5);

        LogAnalysisReport report = await analyzer.AnalyzeAsync(entries, TestContext.Current.CancellationToken);

        report.TotalEntries.ShouldBe(2);
    }

    [Fact]
    public async Task AnalyzeAsync_InvalidJsonResponse_ReturnsFallbackReport()
    {
        const string invalidJson = "This is not JSON at all";

        _chatClientFactory
            .CreateAsync("test-workspace", Arg.Any<CancellationToken>())
            .Returns(_chatClient);

        _chatClient
            .GetResponseAsync(Arg.Any<IList<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, invalidJson)));

        LlmLogAnalyzer analyzer = CreateAnalyzer();
        List<LogEntry> entries = CreateSampleEntries();

        LogAnalysisReport report = await analyzer.AnalyzeAsync(entries, TestContext.Current.CancellationToken);

        report.Summary.ShouldBe("AI analysis returned a non-JSON response.");
        report.Insights.ShouldBeEmpty();
        report.TotalEntries.ShouldBe(3);
    }
}
