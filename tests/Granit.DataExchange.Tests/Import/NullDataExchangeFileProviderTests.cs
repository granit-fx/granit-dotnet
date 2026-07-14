using Granit.DataExchange.Internal;
using Granit.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import;

public sealed class NullDataExchangeFileProviderTests
{
    private readonly NullDataExchangeFileProvider _provider = new();

    [Fact]
    public async Task OpenAsync_throws_with_actionable_message()
    {
        NotImplementedException ex = await Should.ThrowAsync<NotImplementedException>(
            () => _provider.OpenAsync(BlobReference.Create("ref")));

        ex.Message.ShouldContain("AddGranitDataExchangeBlobStorage");
        ex.Message.ShouldContain("AddInMemoryDataExchangeFileProvider");
    }

    [Fact]
    public async Task SaveAsync_throws_with_actionable_message()
    {
        await using MemoryStream content = new([1, 2, 3]);

        NotImplementedException ex = await Should.ThrowAsync<NotImplementedException>(
            () => _provider.SaveAsync("file.csv", content));

        ex.Message.ShouldContain("IDataExchangeFileProvider");
    }

    [Fact]
    public async Task DeleteAsync_throws_with_actionable_message()
    {
        await Should.ThrowAsync<NotImplementedException>(
            () => _provider.DeleteAsync(BlobReference.Create("ref")));
    }
}
