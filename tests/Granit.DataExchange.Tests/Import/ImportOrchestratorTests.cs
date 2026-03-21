using Granit.DataExchange.Import.Internal;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import;

public sealed class ImportOrchestratorTests
{
    private readonly ImportOrchestrator _orchestrator = new();

    [Fact]
    public async Task ExecuteAsync_ThrowsNotImplementedException()
    {
        NotImplementedException ex = await Should.ThrowAsync<NotImplementedException>(() =>
            _orchestrator.ExecuteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("Granit.DataExchange.EntityFrameworkCore");
    }

    [Fact]
    public async Task DryRunAsync_ThrowsNotImplementedException()
    {
        NotImplementedException ex = await Should.ThrowAsync<NotImplementedException>(() =>
            _orchestrator.DryRunAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("Granit.DataExchange.EntityFrameworkCore");
    }
}
