namespace Granit.Caching;

/// <summary>
/// No-op implementation of <see cref="ICacheValueEncryptor"/>.
/// Returns the input data unchanged.
/// Used by default with the Memory provider (development and tests).
/// </summary>
public sealed class NullCacheValueEncryptor : ICacheValueEncryptor
{
    /// <inheritdoc/>
    public byte[] Encrypt(byte[] plaintext) => plaintext;

    /// <inheritdoc/>
    public byte[] Decrypt(byte[] ciphertext) => ciphertext;
}
