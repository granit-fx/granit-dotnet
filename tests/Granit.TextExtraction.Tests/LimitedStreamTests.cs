using System.Text;
using Granit.TextExtraction.Exceptions;
using Shouldly;
using Xunit;

namespace Granit.TextExtraction.Tests;

public sealed class LimitedStreamTests
{
    [Fact]
    public void Read_within_cap_succeeds()
    {
        byte[] payload = Encoding.UTF8.GetBytes("hello world");
        using MemoryStream source = new(payload);
        using LimitedStream limited = new(source, maxBytes: 100);

        byte[] buffer = new byte[payload.Length];
        int read = limited.Read(buffer, 0, buffer.Length);

        read.ShouldBe(payload.Length);
        limited.BytesRead.ShouldBe(payload.Length);
        Encoding.UTF8.GetString(buffer).ShouldBe("hello world");
    }

    [Fact]
    public void Read_exceeding_cap_throws_input_too_large()
    {
        byte[] payload = new byte[1024];
        using MemoryStream source = new(payload);
        using LimitedStream limited = new(source, maxBytes: 256);

        byte[] buffer = new byte[1024];

        TextExtractionException tex = Should.Throw<TextExtractionException>(
            () => limited.Read(buffer, 0, buffer.Length));

        tex.Reason.ShouldBe("input_too_large");
    }

    [Fact]
    public async Task ReadAsync_exceeding_cap_throws_input_too_large()
    {
        byte[] payload = new byte[2048];
        await using MemoryStream source = new(payload);
        await using LimitedStream limited = new(source, maxBytes: 512);

        byte[] buffer = new byte[2048];

        TextExtractionException tex = await Should.ThrowAsync<TextExtractionException>(
            async () => _ = await limited.ReadAsync(buffer.AsMemory(), TestContext.Current.CancellationToken));

        tex.Reason.ShouldBe("input_too_large");
    }

    [Fact]
    public void Constructor_rejects_non_readable_stream()
    {
        using MemoryStream source = new(new byte[8], writable: true);
        source.Dispose();

        Should.Throw<ArgumentException>(() => new LimitedStream(source, maxBytes: 8));
    }

    [Fact]
    public void Constructor_rejects_non_positive_cap()
    {
        using MemoryStream source = new();
        Should.Throw<ArgumentOutOfRangeException>(() => new LimitedStream(source, maxBytes: 0));
        Should.Throw<ArgumentOutOfRangeException>(() => new LimitedStream(source, maxBytes: -1));
    }

    [Fact]
    public void Seek_set_write_are_not_supported()
    {
        using MemoryStream source = new(new byte[8]);
        using LimitedStream limited = new(source, maxBytes: 8);

        Should.Throw<NotSupportedException>(() => limited.Seek(0, SeekOrigin.Begin));
        Should.Throw<NotSupportedException>(() => limited.SetLength(1));
        Should.Throw<NotSupportedException>(() => limited.Write(new byte[1], 0, 1));
    }
}
