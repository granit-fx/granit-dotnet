using System.Security.Cryptography;
using Granit.Privacy.BlobStorage.DataExport.Internal;
using Shouldly;
using Xunit;

namespace Granit.Privacy.BlobStorage.Tests.DataExport.Internal;

public sealed class Sha256ComputingStreamTests
{
    [Fact]
    public async Task WriteAsync_AccumulatesHash_MatchingSha256OverInnerBytes()
    {
        byte[] payload = [.. Enumerable.Range(0, 1024).Select(i => (byte)(i % 256))];
        byte[] expected = SHA256.HashData(payload);

        await using MemoryStream inner = new();
        await using (Sha256ComputingStream sut = new(inner, leaveOpen: true))
        {
            // Write in three uneven chunks to exercise the memory + span + offset overloads.
            await sut.WriteAsync(payload.AsMemory(0, 300), TestContext.Current.CancellationToken);
            await sut.WriteAsync(payload.AsMemory(300, 500), TestContext.Current.CancellationToken);
            sut.Write(payload, 800, 224); // sync byte[] overload

            byte[] computed = sut.ComputeHashAndReset();
            computed.ShouldBe(expected);
        }

        inner.ToArray().ShouldBe(payload);
    }

    [Fact]
    public async Task WriteByte_IsIncludedInDigest()
    {
        byte[] expected = SHA256.HashData([0xAB]);

        await using MemoryStream inner = new();
        await using Sha256ComputingStream sut = new(inner, leaveOpen: true);
        sut.WriteByte(0xAB);

        sut.ComputeHashAndReset().ShouldBe(expected);
        inner.ToArray().ShouldBe([0xAB]);
    }
}
