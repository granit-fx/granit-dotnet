using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Granit.Timeline.Internal;

/// <summary>
/// RFC 4122 v5 (SHA-1, name-based) GUID generator.
/// </summary>
/// <remarks>
/// Used to derive deterministic Ids for shadow rows so the anchor operation is
/// idempotent without relying on database-side <c>ON CONFLICT</c> semantics:
/// the same <c>(TenantId, EntityType, EntityId, SourceKey, SourceId)</c> tuple
/// always maps to the same <c>TimelineEntry.Id</c>, and a duplicate insert
/// fails on the primary key — caught as a no-op.
/// </remarks>
internal static class DeterministicGuid
{
    /// <summary>Derives a v5 GUID from <paramref name="ns"/> and <paramref name="name"/>.</summary>
    public static Guid CreateV5(Guid ns, ReadOnlySpan<byte> name)
    {
        Span<byte> nsBytes = stackalloc byte[16];
        WriteGuidBigEndian(ns, nsBytes);

        Span<byte> hash = stackalloc byte[20];
        using var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        sha1.AppendData(nsBytes);
        sha1.AppendData(name);
        sha1.TryGetHashAndReset(hash, out _);

        // Set version (5) and IETF variant per RFC 4122 §4.3.
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);

        return ReadGuidBigEndian(hash[..16]);
    }

    /// <summary>Convenience overload for UTF-8 string names.</summary>
    public static Guid CreateV5(Guid ns, string name)
    {
        int byteCount = Encoding.UTF8.GetByteCount(name);
        byte[]? rented = byteCount <= 256 ? null : System.Buffers.ArrayPool<byte>.Shared.Rent(byteCount);
        Span<byte> buffer = rented ?? stackalloc byte[256];
        try
        {
            int written = Encoding.UTF8.GetBytes(name, buffer);
            return CreateV5(ns, buffer[..written]);
        }
        finally
        {
            if (rented is not null)
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    private static void WriteGuidBigEndian(Guid value, Span<byte> destination)
    {
        value.TryWriteBytes(destination);
        // Guid.TryWriteBytes writes the first three fields little-endian on
        // every platform; flip them to network order per RFC 4122.
        BinaryPrimitives.WriteUInt32BigEndian(destination, BinaryPrimitives.ReadUInt32LittleEndian(destination));
        BinaryPrimitives.WriteUInt16BigEndian(destination[4..], BinaryPrimitives.ReadUInt16LittleEndian(destination[4..]));
        BinaryPrimitives.WriteUInt16BigEndian(destination[6..], BinaryPrimitives.ReadUInt16LittleEndian(destination[6..]));
    }

    private static Guid ReadGuidBigEndian(ReadOnlySpan<byte> source)
    {
        Span<byte> swapped = stackalloc byte[16];
        source.CopyTo(swapped);
        BinaryPrimitives.WriteUInt32LittleEndian(swapped, BinaryPrimitives.ReadUInt32BigEndian(swapped));
        BinaryPrimitives.WriteUInt16LittleEndian(swapped[4..], BinaryPrimitives.ReadUInt16BigEndian(swapped[4..]));
        BinaryPrimitives.WriteUInt16LittleEndian(swapped[6..], BinaryPrimitives.ReadUInt16BigEndian(swapped[6..]));
        return new Guid(swapped);
    }
}
