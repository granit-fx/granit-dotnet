using Granit.DataExchange.Internal;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import;

public sealed class InMemoryDataExchangeFileProviderTests
{
    private readonly InMemoryDataExchangeFileProvider _provider = new();

    [Fact]
    public async Task SaveAsync_ReturnsNonEmptyReference()
    {
        await using MemoryStream content = new([1, 2, 3]);

        string reference = await _provider.SaveAsync("file.csv", content, TestContext.Current.CancellationToken);

        reference.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task OpenAsync_ReturnsStoredContent()
    {
        byte[] expected = [10, 20, 30];
        await using MemoryStream content = new(expected);
        string reference = await _provider.SaveAsync("file.csv", content, TestContext.Current.CancellationToken);

        await using Stream result = await _provider.OpenAsync(reference, TestContext.Current.CancellationToken);
        await using MemoryStream resultBuffer = new();
        await result.CopyToAsync(resultBuffer, TestContext.Current.CancellationToken);

        resultBuffer.ToArray().ShouldBe(expected);
    }

    [Fact]
    public async Task OpenAsync_UnknownReference_ThrowsFileNotFoundException()
    {
        await Should.ThrowAsync<FileNotFoundException>(() =>
            _provider.OpenAsync("nonexistent", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteAsync_RemovesFile()
    {
        await using MemoryStream content = new([1, 2, 3]);
        string reference = await _provider.SaveAsync("file.csv", content, TestContext.Current.CancellationToken);

        await _provider.DeleteAsync(reference, TestContext.Current.CancellationToken);

        await Should.ThrowAsync<FileNotFoundException>(() =>
            _provider.OpenAsync(reference, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteAsync_UnknownReference_DoesNotThrow() =>
        await Should.NotThrowAsync(() =>
            _provider.DeleteAsync("nonexistent", TestContext.Current.CancellationToken));

    [Fact]
    public async Task SaveAsync_GeneratesUniqueReferences()
    {
        await using MemoryStream content1 = new([1]);
        await using MemoryStream content2 = new([2]);

        string ref1 = await _provider.SaveAsync("a.csv", content1, TestContext.Current.CancellationToken);
        string ref2 = await _provider.SaveAsync("b.csv", content2, TestContext.Current.CancellationToken);

        ref1.ShouldNotBe(ref2);
    }

    [Fact]
    public async Task StreamingSaveAsync_RoundTrips_WrittenBytes()
    {
        byte[] expected = [10, 20, 30];

        string reference = await _provider.SaveAsync(
            "file.csv",
            "text/csv",
            async (stream, ct) => await stream.WriteAsync(expected, ct),
            TestContext.Current.CancellationToken);

        await using Stream result = await _provider.OpenAsync(reference, TestContext.Current.CancellationToken);
        await using MemoryStream resultBuffer = new();
        await result.CopyToAsync(resultBuffer, TestContext.Current.CancellationToken);

        resultBuffer.ToArray().ShouldBe(expected);
    }

    [Fact]
    public async Task StreamingSaveAsync_InvokesCallback()
    {
        bool invoked = false;

        await _provider.SaveAsync(
            "file.csv",
            "text/csv",
            (_, _) =>
            {
                invoked = true;
                return Task.CompletedTask;
            },
            TestContext.Current.CancellationToken);

        invoked.ShouldBeTrue();
    }
}
