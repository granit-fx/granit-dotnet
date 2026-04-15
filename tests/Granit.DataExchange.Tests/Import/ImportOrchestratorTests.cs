using System.Diagnostics.Metrics;
using Granit.DataExchange.Diagnostics;
using Granit.DataExchange.Import;
using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Internal;
using Granit.DataExchange.Import.Pipeline;
using Granit.DataExchange.Import.Reporting;
using Granit.Events;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import;

public sealed class ImportOrchestratorTests
{
    private readonly IImportJobReader _jobReader = Substitute.For<IImportJobReader>();
    private readonly IImportJobWriter _jobWriter = Substitute.For<IImportJobWriter>();
    private readonly IDataExchangeFileProvider _fileProvider = Substitute.For<IDataExchangeFileProvider>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ILocalEventBus _eventBus = Substitute.For<ILocalEventBus>();
    private readonly DataExchangeMetrics _metrics;
    private readonly IOptions<ImportOptions> _options = Options.Create(new ImportOptions());
    private readonly ImportOrchestrator _orchestrator;

    public ImportOrchestratorTests()
    {
        _metrics = new DataExchangeMetrics(new ServiceCollection().AddMetrics().BuildServiceProvider().GetRequiredService<IMeterFactory>());

        ServiceCollection services = new();
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        _orchestrator = new ImportOrchestrator(
            _jobReader, _jobWriter, _fileProvider, serviceProvider,
            _clock, _eventBus, _metrics, _options);
    }

    [Fact]
    public async Task ExecuteAsync_WhenJobNotFound_ThrowsInvalidOperationException()
    {
        _jobReader.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ImportJob?)null);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            _orchestrator.ExecuteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("not found");
    }

    [Fact]
    public async Task DryRunAsync_WhenJobNotFound_ThrowsInvalidOperationException()
    {
        _jobReader.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ImportJob?)null);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            _orchestrator.DryRunAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("not found");
    }
}
