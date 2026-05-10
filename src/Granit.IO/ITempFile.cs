namespace Granit.IO;

/// <summary>
/// Represents a securely-created temporary file owned by the current process.
/// </summary>
/// <remarks>
/// <para>
/// The backing file is created with restrictive permissions (POSIX <c>0600</c> on
/// Linux/macOS, NTFS ACL restricted to the current user on Windows) and the
/// <see cref="FileOptions.DeleteOnClose"/> flag — so closing the underlying
/// stream removes the file from disk.
/// </para>
/// <para>
/// Writes are bounded by <see cref="MaxSizeBytes"/>; exceeding the cap throws
/// <see cref="IOException"/>.
/// </para>
/// <para>
/// Always wrap usage in <c>await using</c> to guarantee disposal of the
/// underlying file handle and stream.
/// </para>
/// </remarks>
public interface ITempFile : IAsyncDisposable
{
    /// <summary>Absolute path to the underlying file on disk.</summary>
    string Path { get; }

    /// <summary>Read/write stream over the underlying file. Bounded by <see cref="MaxSizeBytes"/>.</summary>
    Stream Stream { get; }

    /// <summary>Maximum size (bytes) writeable to <see cref="Stream"/> before <see cref="IOException"/> is thrown.</summary>
    long MaxSizeBytes { get; }
}
