namespace Granit.BlobStorage.Proxy.Internal;

/// <summary>
/// Discriminator preventing cross-use of upload and download tokens.
/// </summary>
internal enum ProxyTokenType
{
    /// <summary>Token authorizes a single upload (PUT).</summary>
    Upload,

    /// <summary>Token authorizes a single download (GET).</summary>
    Download
}
