using System.Diagnostics.Metrics;
using Granit.AI;
using Granit.Observability.AI.Diagnostics;
using Granit.Observability.AI.Internal;
using Granit.Observability.AI.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using LlmAnalysisResponse = Granit.Observability.AI.Internal.LlmLogAnalyzer.LlmAnalysisResponse;
using LlmInsightResponse = Granit.Observability.AI.Internal.LlmLogAnalyzer.LlmInsightResponse;

namespace Granit.Observability.AI.Tests;

public sealed class LlmLogAnalyzerTests : IDisposable
{
    private readonly IStructuredCompletion _structured = Substitute.For<IStructuredCompletion>();
    private readonly IOptions<ObservabilityAIOptions> _options =
        Microsoft.Extensions.Options.Options.Create(new ObservabilityAIOptions
        {
            WorkspaceName = "test-workspace",
            TimeoutSeconds = 30,
            MaxLogEntries = 500,
        });

    private readonly TestMeterFactory _meterFactory = new();

    private LlmLogAnalyzer CreateAnalyzer(IOptions<ObservabilityAIOptions>? opts = null) =>
        new(_structured, opts ?? _options, new ObservabilityAIMetrics(_meterFactory), null, NullLogger<LlmLogAnalyzer>.Instance);

    public void Dispose() => _meterFactory.Dispose();

    private sealed class TestMeterFactory : IMeterFactory
    {
        public Meter Create(MeterOptions options) => new(options);
        public void Dispose() { }
    }

    private void Completes(StructuredCompletionStatus status, LlmAnalysisResponse? value = null) =>
        _structured
            .CompleteAsync<LlmAnalysisResponse>(Arg.Any<StructuredCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new StructuredCompletionResult<LlmAnalysisResponse> { Status = status, Value = value });

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
        LogAnalysisReport report = await CreateAnalyzer().AnalyzeAsync([], TestContext.Current.CancellationToken);

        report.Summary.ShouldBe("No log entries to analyze.");
        report.Insights.ShouldBeEmpty();
        report.TotalEntries.ShouldBe(0);
    }

    [Fact]
    public async Task AnalyzeAsync_NullEntries_ThrowsArgumentNullException() =>
        await Should.ThrowAsync<ArgumentNullException>(
            () => CreateAnalyzer().AnalyzeAsync(null!, TestContext.Current.CancellationToken));

    [Fact]
    public async Task AnalyzeAsync_ValidEntries_ReturnsReport()
    {
        Completes(StructuredCompletionStatus.Succeeded, new LlmAnalysisResponse
        {
            Summary = "Found 1 error pattern",
            Insights = [new LlmInsightResponse { Description = "Recurring NullReferenceException", Severity = "High", Category = "Pattern" }],
        });

        LogAnalysisReport report = await CreateAnalyzer().AnalyzeAsync(CreateSampleEntries(), TestContext.Current.CancellationToken);

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
        Completes(StructuredCompletionStatus.Succeeded, new LlmAnalysisResponse { Summary = "OK", Insights = [] });

        IOptions<ObservabilityAIOptions> smallOptions = Microsoft.Extensions.Options.Options.Create(
            new ObservabilityAIOptions { WorkspaceName = "test-workspace", TimeoutSeconds = 30, MaxLogEntries = 2 });

        LogAnalysisReport report = await CreateAnalyzer(smallOptions).AnalyzeAsync(CreateSampleEntries(5), TestContext.Current.CancellationToken);

        report.TotalEntries.ShouldBe(2);
    }

    [Fact]
    public async Task AnalyzeAsync_SchemaViolation_ReturnsFallbackReport()
    {
        Completes(StructuredCompletionStatus.SchemaViolation);

        LogAnalysisReport report = await CreateAnalyzer().AnalyzeAsync(CreateSampleEntries(), TestContext.Current.CancellationToken);

        report.Summary.ShouldBe("AI analysis returned a non-JSON response.");
        report.Insights.ShouldBeEmpty();
        report.TotalEntries.ShouldBe(3);
    }

    [Fact]
    public async Task AnalyzeAsync_TransportFailure_Throws()
    {
        Completes(StructuredCompletionStatus.TransportFailure);

        await Should.ThrowAsync<InvalidOperationException>(
            () => CreateAnalyzer().AnalyzeAsync(CreateSampleEntries(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnalyzeAsync_PassesEntriesAsContent()
    {
        StructuredCompletionRequest? captured = null;
        _structured
            .CompleteAsync<LlmAnalysisResponse>(Arg.Do<StructuredCompletionRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(new StructuredCompletionResult<LlmAnalysisResponse>
            {
                Status = StructuredCompletionStatus.Succeeded,
                Value = new LlmAnalysisResponse { Summary = "ok", Insights = [] },
            });

        await CreateAnalyzer().AnalyzeAsync(
            [new LogEntry(DateTimeOffset.UtcNow, "Error", "boom happened", null)], TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Content.ShouldNotBeNull();
        captured.Content.ShouldContain("boom happened");
        captured.WorkspaceName.ShouldBe("test-workspace");
    }
}
