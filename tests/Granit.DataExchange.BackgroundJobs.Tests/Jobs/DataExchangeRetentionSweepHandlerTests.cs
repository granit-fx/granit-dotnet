using Granit.DataExchange.BackgroundJobs.Jobs;
using Granit.DataExchange.BackgroundJobs.Options;
using Granit.DataExchange.BackgroundJobs.Services;
using Granit.DataExchange.Diagnostics;
using Granit.DataExchange.Retention;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Granit.DataExchange.BackgroundJobs.Tests.Jobs;

public sealed class DataExchangeRetentionSweepHandlerTests : IDisposable
{
    private readonly IDataExchangeRetentionStore _store = Substitute.For<IDataExchangeRetentionStore>();
    private readonly IDataExchangeFileProvider _fileProvider = Substitute.For<IDataExchangeFileProvider>();
    private readonly ServiceProvider _sp;
    private readonly DataExchangeMetrics _metrics;

    public DataExchangeRetentionSweepHandlerTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        _metrics = new DataExchangeMetrics(_sp.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>());
    }

    public void Dispose() => _sp.Dispose();

    [Fact]
    public async Task HandleAsync_DelegatesToRetentionSweepService()
    {
        _store.GetImportJobsWithExpiredFilesAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _store.GetExportJobsWithExpiredFilesAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _store.GetStuckImportJobsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _store.GetStuckExportJobsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _store.GetExpiredTerminalImportJobsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _store.GetExpiredTerminalExportJobsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        RetentionSweepService service = new(
            _store, _fileProvider, new FakeTimeProvider(), _metrics,
            Microsoft.Extensions.Options.Options.Create(new DataExchangeRetentionOptions()),
            NullLogger<RetentionSweepService>.Instance);

        await DataExchangeRetentionSweepHandler.HandleAsync(
            new DataExchangeRetentionSweepJob(), service, TestContext.Current.CancellationToken);

        await _store.Received(1).GetImportJobsWithExpiredFilesAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}
