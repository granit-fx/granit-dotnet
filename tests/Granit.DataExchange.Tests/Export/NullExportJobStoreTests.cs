using Granit.DataExchange.Export;
using Granit.DataExchange.Export.Domain;
using Granit.DataExchange.Export.Internal;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Export;

public sealed class NullExportJobStoreTests
{
    private readonly NullExportJobStore _store = new();

    [Fact]
    public async Task GetAsync_ReturnsNull()
    {
        ExportJob? result = await _store.GetAsync(
            Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task CreateAsync_DoesNotThrow()
    {
        var job = ExportJob.Create(Guid.NewGuid(), "Test", "csv",
            new ExportRequest("Test", "csv", null, false, null, null, null, null));

        await Should.NotThrowAsync(() =>
            _store.CreateAsync(job, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateAsync_DoesNotThrow()
    {
        var job = ExportJob.Create(Guid.NewGuid(), "Test", "csv",
            new ExportRequest("Test", "csv", null, false, null, null, null, null));

        await Should.NotThrowAsync(() =>
            _store.UpdateAsync(job, TestContext.Current.CancellationToken));
    }
}
