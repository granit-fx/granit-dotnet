using System.Security.Cryptography;
using System.Text;

namespace Granit.Vault.Internal;

/// <summary>
/// Holds dynamic database credentials as <see cref="byte"/> arrays and zeroizes the
/// previous buffers whenever new credentials are applied. Mitigates credential
/// residency in process memory after rotation (CWE-522 / GDPR Art. 32).
/// </summary>
/// <remarks>
/// <para>
/// .NET strings are immutable and cannot be securely erased. Keeping credentials in
/// <c>byte[]</c> lets us wipe the previous generation via
/// <see cref="CryptographicOperations.ZeroMemory(Span{byte})"/> the moment a rotation
/// completes. Transient string copies produced by <see cref="Username"/> /
/// <see cref="Password"/> reads are short-lived and scoped to the consumer.
/// </para>
/// <para>
/// Thread-safety: readers use <see cref="Volatile.Read(ref byte[])"/>; writers swap
/// via <see cref="Interlocked.Exchange{T}(ref T, T)"/> then zeroize the old buffer.
/// Callers are expected to be single-writer (the credential background service).
/// </para>
/// </remarks>
internal sealed class ZeroizingCredentialStore
{
    private byte[] _username = [];
    private byte[] _password = [];

    public string Username
    {
        get
        {
            byte[] bytes = Volatile.Read(ref _username);
            return bytes.Length == 0 ? string.Empty : Encoding.UTF8.GetString(bytes);
        }
    }

    public string Password
    {
        get
        {
            byte[] bytes = Volatile.Read(ref _password);
            return bytes.Length == 0 ? string.Empty : Encoding.UTF8.GetString(bytes);
        }
    }

    public bool IsReady => Volatile.Read(ref _username).Length > 0;

    public void Apply(string username, string password)
    {
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(password);

        byte[] newUsername = Encoding.UTF8.GetBytes(username);
        byte[] newPassword = Encoding.UTF8.GetBytes(password);

        byte[] oldUsername = Interlocked.Exchange(ref _username, newUsername);
        byte[] oldPassword = Interlocked.Exchange(ref _password, newPassword);

        CryptographicOperations.ZeroMemory(oldUsername);
        CryptographicOperations.ZeroMemory(oldPassword);
    }
}
