namespace Granit.IO;

/// <summary>
/// Factory for securely-created temporary files. Resolved as a singleton.
/// </summary>
/// <remarks>
/// Files are created under a tenant-partitioned directory when a tenant is
/// available, with POSIX <c>0600</c> mode on Linux/macOS and a restrictive
/// NTFS ACL on Windows. The returned <see cref="ITempFile"/> auto-deletes on
/// disposal via <see cref="FileOptions.DeleteOnClose"/>.
/// </remarks>
public interface ITempFileFactory
{
    /// <summary>
    /// Creates a new temporary file for the given category and extension.
    /// </summary>
    /// <param name="category">Logical category, used as a sub-directory.
    /// Must match the regex <c>[a-z0-9-]{1,32}</c>.</param>
    /// <param name="extension">File extension (alphanumeric, 1..16 chars). A leading <c>.</c> is stripped.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An <see cref="ITempFile"/> owning the file handle.</returns>
    /// <exception cref="ArgumentException">When <paramref name="category"/> or <paramref name="extension"/> is invalid.</exception>
    /// <exception cref="IOException">When the file or directory cannot be created.</exception>
    ValueTask<ITempFile> CreateAsync(string category, string extension, CancellationToken ct = default);
}
