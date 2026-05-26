using Granit.AI.Extraction.Redaction;
using Granit.LanguageDetection.AI.Internal;
using Granit.LanguageDetection.AI.Options;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

// Granit.LanguageDetection.AI.Options namespace shadows Microsoft.Extensions.Options
// in this assembly, so Options.Create() resolves to the wrong symbol. Alias the static
// helper class explicitly.
using MEOptions = Microsoft.Extensions.Options.Options;

namespace Granit.LanguageDetection.AI.Tests;

public sealed class RedactionConfigurationStartupCheckTests
{
    [Fact]
    public async Task Emits_warning_when_RedactPIIBeforeLLMCall_is_true_and_redactor_is_NoOp()
    {
        // The dangerous combination the framework cannot prevent at compile time: the
        // option flag suggests masking is on, the registered redactor is the identity
        // default. Operators only learn about it on incident review unless we warn at
        // boot.
        var logger = new ListLogger<RedactionConfigurationStartupCheck>();
        LanguageDetectionAIOptions options = new() { RedactPIIBeforeLLMCall = true };
        var check = new RedactionConfigurationStartupCheck(
            MEOptions.Create(options), new NoOpAIContentRedactor(), logger);

        await check.StartAsync(TestContext.Current.CancellationToken);

        logger.Entries.ShouldContain(e =>
            e.Level == LogLevel.Warning
            && e.Message.Contains("NoOpAIContentRedactor", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Stays_silent_when_RedactPIIBeforeLLMCall_is_false()
    {
        // If the operator opted out of redaction explicitly, the NoOp redactor is
        // expected — no warning, no noise.
        var logger = new ListLogger<RedactionConfigurationStartupCheck>();
        LanguageDetectionAIOptions options = new() { RedactPIIBeforeLLMCall = false };
        var check = new RedactionConfigurationStartupCheck(
            MEOptions.Create(options), new NoOpAIContentRedactor(), logger);

        await check.StartAsync(TestContext.Current.CancellationToken);

        logger.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task Stays_silent_when_redactor_is_NOT_the_NoOp_default()
    {
        // Custom redactor wired in — the configuration is coherent; no warning.
        var logger = new ListLogger<RedactionConfigurationStartupCheck>();
        LanguageDetectionAIOptions options = new() { RedactPIIBeforeLLMCall = true };
        var check = new RedactionConfigurationStartupCheck(
            MEOptions.Create(options), new FakeRedactor(), logger);

        await check.StartAsync(TestContext.Current.CancellationToken);

        logger.Entries.ShouldBeEmpty();
    }

    private sealed class FakeRedactor : IAIContentRedactor
    {
        public string Redact(string content) => content;
    }

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
    }
}
