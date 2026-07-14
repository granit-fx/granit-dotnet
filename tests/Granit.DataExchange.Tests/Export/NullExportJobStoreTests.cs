using Granit.DataExchange.Export.Internal;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Export;

public sealed class NullExportJobStoreTests
{
    private readonly NullExportJobStore _store = new();

    [Fact]
    public async Task GetAsync_ThrowsNotImplementedException()
    {
        await Should.ThrowAsync<NotImplementedException>(() =>
            _store.GetAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateAsync_ThrowsNotImplementedException()
    {
        await Should.ThrowAsync<NotImplementedException>(() =>
            _store.CreateAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateAsync_ThrowsNotImplementedException()
    {
        await Should.ThrowAsync<NotImplementedException>(() =>
            _store.UpdateAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExceptionMessage_ContainsGuidance()
    {
        NotImplementedException ex = await Should.ThrowAsync<NotImplementedException>(() =>
            _store.GetAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("Granit.DataExchange.EntityFrameworkCore");
    }
}
