using Granit.DataExchange.Import.Internal;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import;

public sealed class NullImportJobStoreListAsyncTests
{
    private readonly NullImportJobStore _store = new();

    [Fact]
    public async Task ListAsync_ThrowsNotImplementedException()
    {
        await Should.ThrowAsync<NotImplementedException>(() =>
            _store.ListAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ListAsync_WithStatusFilter_ThrowsNotImplementedException()
    {
        await Should.ThrowAsync<NotImplementedException>(() =>
            _store.ListAsync(
                status: Granit.DataExchange.Import.Domain.ImportJobStatus.Completed,
                cancellationToken: TestContext.Current.CancellationToken));
    }
}
