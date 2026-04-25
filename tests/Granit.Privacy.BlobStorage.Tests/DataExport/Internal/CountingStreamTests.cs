using Granit.Privacy.BlobStorage.DataExport.Exceptions;
using Granit.Privacy.BlobStorage.DataExport.Internal;
using Shouldly;
using Xunit;

namespace Granit.Privacy.BlobStorage.Tests.DataExport.Internal;

public sealed class CountingStreamTests
{
    [Fact]
    public void Write_UnderLimit_DoesNotThrow_AndTracksBytes()
    {
        using MemoryStream inner = new();
        using CountingStream sut = new(inner, maxBytes: 100);

        sut.Write([1, 2, 3, 4, 5], 0, 5);
        sut.Write([6, 7], 0, 2);

        sut.BytesWritten.ShouldBe(7);
        inner.ToArray().Length.ShouldBe(7);
    }

    [Fact]
    public void Write_ExceedingLimit_ThrowsSizeLimitExceededException()
    {
        using MemoryStream inner = new();
        using CountingStream sut = new(inner, maxBytes: 5);

        sut.Write([1, 2, 3], 0, 3);

        PrivacyExportSizeLimitExceededException ex = Should.Throw<PrivacyExportSizeLimitExceededException>(
            () => sut.Write([4, 5, 6, 7], 0, 4));
        ex.MaxBytes.ShouldBe(5);
        ex.ObservedBytes.ShouldBe(7);
    }

    [Fact]
    public async Task WriteAsync_ExceedingLimit_ThrowsSizeLimitExceededException()
    {
        using MemoryStream inner = new();
        await using CountingStream sut = new(inner, maxBytes: 4);

        await sut.WriteAsync(new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);

        await Should.ThrowAsync<PrivacyExportSizeLimitExceededException>(async () =>
            await sut.WriteAsync(new byte[] { 4, 5 }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void WriteByte_ExceedingLimit_Throws()
    {
        using MemoryStream inner = new();
        using CountingStream sut = new(inner, maxBytes: 1);

        sut.WriteByte(1);

        Should.Throw<PrivacyExportSizeLimitExceededException>(() => sut.WriteByte(2));
    }

    [Fact]
    public void CanRead_IsFalse_CanSeek_IsFalse()
    {
        using MemoryStream inner = new();
        using CountingStream sut = new(inner, maxBytes: 10);

        sut.CanRead.ShouldBeFalse();
        sut.CanSeek.ShouldBeFalse();
        sut.CanWrite.ShouldBeTrue();
    }

    [Fact]
    public void Dispose_DisposesInnerStream()
    {
        MemoryStream inner = new();

        using (CountingStream sut = new(inner, maxBytes: 10))
        {
            sut.WriteByte(1);
        }

        Should.Throw<ObjectDisposedException>(() => inner.WriteByte(2));
    }
}
