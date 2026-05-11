using System;
using System.IO;
using TagLib;

namespace Granit.Documents.AssetMetadata.AudioVideo.Internal;

/// <summary>
/// Adapts a seekable <see cref="Stream"/> to the
/// <see cref="TagLib.File.IFileAbstraction"/> contract. TagLibSharp infers
/// the container format from the file extension carried by
/// <see cref="Name"/>, so the caller must hand a meaningful extension.
/// </summary>
internal sealed class StreamFileAbstraction(string name, Stream stream) : TagLib.File.IFileAbstraction
{
    /// <inheritdoc />
    public string Name { get; } = name;

    /// <inheritdoc />
    public Stream ReadStream { get; } = stream;

    /// <inheritdoc />
    public Stream WriteStream => throw new NotSupportedException("Read-only abstraction.");

    /// <inheritdoc />
    public void CloseStream(Stream stream)
    {
        // Leave the caller-owned stream open — the extractor manages its lifetime.
    }
}
