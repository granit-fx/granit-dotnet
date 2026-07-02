using System.Security.Cryptography;
using System.Text;
using Granit.Privacy.DataExport.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Tests.DataExport.Security;

public sealed class EphemeralExportContentEncryptorTests
{
    private static EphemeralExportContentEncryptor CreateEncryptor() =>
        new(NullLogger<EphemeralExportContentEncryptor>.Instance);

    [Fact]
    public void Encrypt_then_Decrypt_round_trips_the_plaintext()
    {
        using EphemeralExportContentEncryptor encryptor = CreateEncryptor();
        byte[] plaintext = Encoding.UTF8.GetBytes("""{"payload":{"userId":"u1"},"integrityTag":"v1:abc"}""");

        byte[] ciphertext = encryptor.Encrypt(plaintext);
        byte[] recovered = encryptor.Decrypt(ciphertext);

        recovered.ShouldBe(plaintext);
    }

    [Fact]
    public void Encrypt_does_not_leave_plaintext_in_the_ciphertext()
    {
        using EphemeralExportContentEncryptor encryptor = CreateEncryptor();
        byte[] plaintext = Encoding.UTF8.GetBytes("SENSITIVE-PII-MARKER");

        byte[] ciphertext = encryptor.Encrypt(plaintext);

        Encoding.UTF8.GetString(ciphertext).ShouldNotContain("SENSITIVE-PII-MARKER");
        // 12-byte nonce + 16-byte tag overhead, then the same-length ciphertext body.
        ciphertext.Length.ShouldBe(plaintext.Length + 28);
    }

    [Fact]
    public void Encrypt_uses_a_fresh_nonce_each_call()
    {
        using EphemeralExportContentEncryptor encryptor = CreateEncryptor();
        byte[] plaintext = Encoding.UTF8.GetBytes("same input");

        byte[] first = encryptor.Encrypt(plaintext);
        byte[] second = encryptor.Encrypt(plaintext);

        // Random nonce ⇒ identical plaintext yields distinct ciphertext (no ECB-style leak).
        first.ShouldNotBe(second);
    }

    [Fact]
    public void Decrypt_throws_on_a_flipped_ciphertext_byte_before_exposing_plaintext()
    {
        using EphemeralExportContentEncryptor encryptor = CreateEncryptor();
        byte[] ciphertext = encryptor.Encrypt(Encoding.UTF8.GetBytes("tamper-me"));

        // Flip a byte in the encrypted body (past the 12-byte nonce + 16-byte tag).
        ciphertext[^1] ^= 0x01;

        // GCM authenticates the tag first and throws WITHOUT returning any plaintext.
        Should.Throw<CryptographicException>(() => encryptor.Decrypt(ciphertext));
    }

    [Fact]
    public void Decrypt_throws_when_the_gcm_tag_is_flipped()
    {
        using EphemeralExportContentEncryptor encryptor = CreateEncryptor();
        byte[] ciphertext = encryptor.Encrypt(Encoding.UTF8.GetBytes("hello"));

        // Corrupt a byte inside the 16-byte authentication tag region.
        ciphertext[12] ^= 0xFF;

        Should.Throw<CryptographicException>(() => encryptor.Decrypt(ciphertext));
    }

    [Fact]
    public void Decrypt_from_a_different_key_fails_authentication()
    {
        using EphemeralExportContentEncryptor writer = CreateEncryptor();
        using EphemeralExportContentEncryptor reader = CreateEncryptor();
        byte[] ciphertext = writer.Encrypt(Encoding.UTF8.GetBytes("cross-key"));

        // Each ephemeral instance holds a distinct random key — the classic
        // multi-replica failure. Authentication fails rather than yielding garbage.
        Should.Throw<CryptographicException>(() => reader.Decrypt(ciphertext));
    }

    [Fact]
    public void Decrypt_throws_ArgumentException_on_a_truncated_ciphertext()
    {
        using EphemeralExportContentEncryptor encryptor = CreateEncryptor();

        // Shorter than the nonce+tag overhead — cannot possibly be a valid envelope.
        Should.Throw<ArgumentException>(() => encryptor.Decrypt(new byte[10]));
    }

    [Fact]
    public void Encrypt_after_Dispose_throws_ObjectDisposedException()
    {
        EphemeralExportContentEncryptor encryptor = CreateEncryptor();
        encryptor.Dispose();

        Should.Throw<ObjectDisposedException>(() => encryptor.Encrypt([1, 2, 3]));
    }
}
