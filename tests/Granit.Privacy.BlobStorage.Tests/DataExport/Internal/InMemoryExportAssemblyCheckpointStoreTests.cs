using Granit.Privacy.BlobStorage.DataExport.Internal;
using Granit.Privacy.DataExport;
using Shouldly;
using Xunit;

namespace Granit.Privacy.BlobStorage.Tests.DataExport.Internal;

public sealed class InMemoryExportAssemblyCheckpointStoreTests
{
    [Fact]
    public async Task GetAsync_ReturnsNull_WhenNeverSet()
    {
        InMemoryExportAssemblyCheckpointStore sut = new();
        (await sut.GetAsync(Guid.NewGuid(), tenantId: null, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_RoundTrips()
    {
        InMemoryExportAssemblyCheckpointStore sut = new();
        var requestId = Guid.NewGuid();
        ExportAssemblyCheckpoint cp = new(LastCompletedShardIndex: 2, NextFragmentIndex: 12, CompletedShardObjectKeys: ["a", "b", "c"]);

        await sut.SetAsync(requestId, tenantId: null, cp, TestContext.Current.CancellationToken);

        ExportAssemblyCheckpoint? read = await sut.GetAsync(requestId, tenantId: null, TestContext.Current.CancellationToken);
        read.ShouldBe(cp);
    }

    [Fact]
    public async Task SetAsync_IsTenantPartitioned()
    {
        InMemoryExportAssemblyCheckpointStore sut = new();
        var requestId = Guid.NewGuid();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await sut.SetAsync(requestId, tenantA, new ExportAssemblyCheckpoint(0, 1, ["k0"]), TestContext.Current.CancellationToken);

        (await sut.GetAsync(requestId, tenantA, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        (await sut.GetAsync(requestId, tenantB, TestContext.Current.CancellationToken)).ShouldBeNull();
        (await sut.GetAsync(requestId, tenantId: null, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task ClearAsync_RemovesCheckpoint()
    {
        InMemoryExportAssemblyCheckpointStore sut = new();
        var requestId = Guid.NewGuid();
        await sut.SetAsync(requestId, tenantId: null, new ExportAssemblyCheckpoint(0, 0, []), TestContext.Current.CancellationToken);

        await sut.ClearAsync(requestId, tenantId: null, TestContext.Current.CancellationToken);

        (await sut.GetAsync(requestId, tenantId: null, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task ClearAsync_OnMissing_IsNoOp()
    {
        InMemoryExportAssemblyCheckpointStore sut = new();
        await Should.NotThrowAsync(
            async () => await sut.ClearAsync(Guid.NewGuid(), tenantId: null, TestContext.Current.CancellationToken));
    }
}
