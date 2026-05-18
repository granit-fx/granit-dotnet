using Granit.IO.Internal;
using Shouldly;
using Xunit;

namespace Granit.IO.Tests;

public sealed class LimitedStreamTests
{
    [Fact]
    public void Constructor_NullInner_Throws() =>
        Should.Throw<ArgumentNullException>(() => new LimitedStream(null!, 100));

    [Fact]
    public void Constructor_NegativeMax_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new LimitedStream(new MemoryStream(), -1));

    [Fact]
    public void Write_BelowCap_Succeeds()
    {
        using MemoryStream inner = new();
        using LimitedStream stream = new(inner, 100);

        stream.Write(new byte[50]);

        stream.Length.ShouldBe(50);
    }

    [Fact]
    public void Write_BeyondCap_Throws()
    {
        using MemoryStream inner = new();
        using LimitedStream stream = new(inner, 100);

        Should.Throw<IOException>(() => stream.Write(new byte[101]));
    }

    [Fact]
    public async Task WriteAsync_BeyondCap_Throws()
    {
        using MemoryStream inner = new();
        using LimitedStream stream = new(inner, 100);

        await Should.ThrowAsync<IOException>(async () =>
            await stream.WriteAsync(new byte[101]));
    }

    [Fact]
    public void WriteAsyncArray_BeyondCap_Throws()
    {
        using MemoryStream inner = new();
        using LimitedStream stream = new(inner, 100);

#pragma warning disable CA1849, CA2012 // Test intentionally exercises the byte[] overload synchronously.
        Should.Throw<IOException>(() => stream.WriteAsync(new byte[101], 0, 101).GetAwaiter().GetResult());
#pragma warning restore CA1849, CA2012
    }

    [Fact]
    public void WriteByte_BeyondCap_Throws()
    {
        using MemoryStream inner = new();
        using LimitedStream stream = new(inner, 1);

        stream.WriteByte(1);
        Should.Throw<IOException>(() => stream.WriteByte(2));
    }

    [Fact]
    public void SetLength_BeyondCap_Throws()
    {
        using MemoryStream inner = new();
        using LimitedStream stream = new(inner, 100);

        Should.Throw<IOException>(() => stream.SetLength(101));
    }

    [Fact]
    public void SetLength_WithinCap_Succeeds()
    {
        using MemoryStream inner = new();
        using LimitedStream stream = new(inner, 100);

        stream.SetLength(50);

        stream.Length.ShouldBe(50);
    }

    [Fact]
    public void Read_PassesThrough()
    {
        byte[] payload = [1, 2, 3, 4];
        using MemoryStream inner = new(payload);
        using LimitedStream stream = new(inner, 100);

        byte[] buffer = new byte[4];
        int read = stream.Read(buffer, 0, 4);

        read.ShouldBe(4);
        buffer.ShouldBe(payload);
    }

    [Fact]
    public void Seek_PassesThrough()
    {
        using MemoryStream inner = new();
        using LimitedStream stream = new(inner, 100);

        stream.Write(new byte[50]);
        stream.Seek(0, SeekOrigin.Begin);

        stream.Position.ShouldBe(0);
    }

    [Fact]
    public void MultipleWrites_Accumulate()
    {
        using MemoryStream inner = new();
        using LimitedStream stream = new(inner, 100);

        stream.Write(new byte[40]);
        stream.Write(new byte[40]);

        stream.Length.ShouldBe(80);

        Should.Throw<IOException>(() => stream.Write(new byte[21]));
    }

    [Fact]
    public void Capability_FlagsMirrorInner()
    {
        using MemoryStream inner = new();
        using LimitedStream stream = new(inner, 100);

        stream.CanRead.ShouldBe(inner.CanRead);
        stream.CanWrite.ShouldBe(inner.CanWrite);
        stream.CanSeek.ShouldBe(inner.CanSeek);
    }

    [Fact]
    public void MaxSizeBytes_Exposed()
    {
        using LimitedStream stream = new(new MemoryStream(), 123);

        stream.MaxSizeBytes.ShouldBe(123);
    }

    [Fact]
    public void Position_GetSet_PassesThrough()
    {
        using MemoryStream inner = new();
        inner.Write(new byte[20]);
        using LimitedStream stream = new(inner, 100);

        stream.Position = 5;

        stream.Position.ShouldBe(5);
    }

    [Fact]
    public void Flush_PassesThrough()
    {
        using MemoryStream inner = new();
        using LimitedStream stream = new(inner, 100);

        Should.NotThrow(stream.Flush);
    }

    [Fact]
    public async Task FlushAsync_PassesThrough()
    {
        using MemoryStream inner = new();
        using LimitedStream stream = new(inner, 100);

        await Should.NotThrowAsync(() => stream.FlushAsync(CancellationToken.None));
    }

    [Fact]
    public void Read_Span_PassesThrough()
    {
        byte[] payload = [9, 8, 7, 6];
        using MemoryStream inner = new(payload);
        using LimitedStream stream = new(inner, 100);

        Span<byte> buffer = stackalloc byte[4];
        int read = stream.Read(buffer);

        read.ShouldBe(4);
        buffer.ToArray().ShouldBe(payload);
    }

    [Fact]
    public async Task ReadAsync_Array_PassesThrough()
    {
        byte[] payload = [1, 2, 3];
        using MemoryStream inner = new(payload);
        using LimitedStream stream = new(inner, 100);

        byte[] buffer = new byte[3];
#pragma warning disable CA1835 // Intentionally exercises the byte[] overload for coverage.
        int read = await stream.ReadAsync(buffer, 0, 3, CancellationToken.None);
#pragma warning restore CA1835

        read.ShouldBe(3);
    }

    [Fact]
    public async Task ReadAsync_Memory_PassesThrough()
    {
        byte[] payload = [1, 2, 3];
        using MemoryStream inner = new(payload);
        using LimitedStream stream = new(inner, 100);

        byte[] buffer = new byte[3];
        int read = await stream.ReadAsync(buffer.AsMemory());

        read.ShouldBe(3);
    }

    [Fact]
    public void WriteSpan_BelowCap_Succeeds()
    {
        using MemoryStream inner = new();
        using LimitedStream stream = new(inner, 100);

        stream.Write(new byte[] { 1, 2, 3 }.AsSpan());

        stream.Length.ShouldBe(3);
    }

    [Fact]
    public void WriteSpan_BeyondCap_Throws()
    {
        using MemoryStream inner = new();
        using LimitedStream stream = new(inner, 2);

        Should.Throw<IOException>(() => stream.Write(new byte[] { 1, 2, 3 }.AsSpan()));
    }

    [Fact]
    public async Task DisposeAsync_DisposesInner()
    {
        MemoryStream inner = new();
        LimitedStream stream = new(inner, 100);

        await stream.DisposeAsync();

        Should.Throw<ObjectDisposedException>(() => inner.WriteByte(1));
    }
}
