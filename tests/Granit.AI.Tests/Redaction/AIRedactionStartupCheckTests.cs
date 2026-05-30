using Granit.AI.Redaction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace Granit.AI.Tests.Redaction;

public sealed class AIRedactionStartupCheckTests
{
    [Fact]
    public async Task Warns_when_redaction_enabled_and_redactor_is_NoOp()
    {
        var logger = new ListLogger();
        ServiceProvider sp = BuildProvider(new NoOpAIContentRedactor(), redactionEnabled: true, logger);

        await StartHostedServicesAsync(sp);

        logger.Entries.ShouldContain(e =>
            e.Level == LogLevel.Warning
            && e.Message.Contains("NoOpAIContentRedactor", StringComparison.Ordinal)
            && e.Message.Contains("My Feature", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Silent_when_redaction_disabled()
    {
        var logger = new ListLogger();
        ServiceProvider sp = BuildProvider(new NoOpAIContentRedactor(), redactionEnabled: false, logger);

        await StartHostedServicesAsync(sp);

        logger.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task Silent_when_a_real_redactor_is_registered()
    {
        var logger = new ListLogger();
        ServiceProvider sp = BuildProvider(new FakeRedactor(), redactionEnabled: true, logger);

        await StartHostedServicesAsync(sp);

        logger.Entries.ShouldBeEmpty();
    }

    private static ServiceProvider BuildProvider(
        IAIContentRedactor redactor, bool redactionEnabled, ListLogger logger)
    {
        ServiceCollection services = [];
        services.AddSingleton(redactor);
        services.AddLogging(b => b.AddProvider(new ListLoggerProvider(logger)));
        services.AddAIRedactionStartupWarning("My Feature", _ => redactionEnabled);
        return services.BuildServiceProvider();
    }

    private static async Task StartHostedServicesAsync(ServiceProvider sp)
    {
        foreach (IHostedService hosted in sp.GetServices<IHostedService>())
        {
            await hosted.StartAsync(CancellationToken.None);
        }
    }

    private sealed class FakeRedactor : IAIContentRedactor
    {
        public string Redact(string content) => content;
    }

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed class ListLogger : ILogger
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

    private sealed class ListLoggerProvider(ListLogger logger) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => logger;
        public void Dispose() { }
    }
}
