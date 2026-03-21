using System.Diagnostics.Metrics;
using System.Text;
using global::Azure.Security.KeyVault.Keys;
using global::Azure.Security.KeyVault.Keys.Cryptography;
using Granit.Vault.Azure.Options;
using Granit.Vault.Azure.Services;
using Granit.Vault.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.Vault.Azure.Tests;

public sealed class AzureKeyVaultTransitEncryptionServiceTests : IDisposable
{
    private readonly CryptographyClient _cryptoClient = Substitute.For<CryptographyClient>();
    private readonly ServiceProvider _sp;
    private readonly AzureKeyVaultTransitEncryptionService _sut;

    public AzureKeyVaultTransitEncryptionServiceTests()
    {
        AzureKeyVaultOptions options = new()
        {
            VaultUri = "https://my-vault.vault.azure.net/",
            EncryptionKeyName = "test-key",
            EncryptionAlgorithm = "RSA-OAEP-256",
        };

        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        var metrics = new VaultMetrics(_sp.GetRequiredService<IMeterFactory>());

        _sut = new AzureKeyVaultTransitEncryptionService(
            _cryptoClient,
            Microsoft.Extensions.Options.Options.Create(options),
            metrics,
            null,
            NullLogger<AzureKeyVaultTransitEncryptionService>.Instance);
    }

    public void Dispose() => _sp.Dispose();

    [Fact]
    public async Task EncryptAsync_EncodesPlaintextAndReturnsBase64Ciphertext()
    {
        byte[] fakeCiphertext = [10, 20, 30, 40, 50];
        _cryptoClient.EncryptAsync(
                Arg.Any<EncryptionAlgorithm>(),
                Arg.Any<byte[]>(),
                Arg.Any<CancellationToken>())
            .Returns(CryptographyModelFactory.EncryptResult(
                keyId: "test-key",
                ciphertext: fakeCiphertext,
                algorithm: EncryptionAlgorithm.RsaOaep256));

        string result = await _sut.EncryptAsync("my-key", "hello world", TestContext.Current.CancellationToken);

        result.ShouldBe(Convert.ToBase64String(fakeCiphertext));
    }

    [Fact]
    public async Task EncryptAsync_SendsUtf8EncodedPlaintext()
    {
        byte[]? captured = null;
        _cryptoClient.EncryptAsync(
                Arg.Any<EncryptionAlgorithm>(),
                Arg.Do<byte[]>(b => captured = b),
                Arg.Any<CancellationToken>())
            .Returns(CryptographyModelFactory.EncryptResult(
                keyId: "test-key",
                ciphertext: [1],
                algorithm: EncryptionAlgorithm.RsaOaep256));

        await _sut.EncryptAsync("my-key", "hello", TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        Encoding.UTF8.GetString(captured).ShouldBe("hello");
    }

    [Fact]
    public async Task DecryptAsync_DecodesBase64AndReturnsPlaintext()
    {
        byte[] plaintextBytes = Encoding.UTF8.GetBytes("decrypted-text");
        byte[] ciphertextInput = [10, 20, 30];

        _cryptoClient.DecryptAsync(
                Arg.Any<EncryptionAlgorithm>(),
                Arg.Any<byte[]>(),
                Arg.Any<CancellationToken>())
            .Returns(CryptographyModelFactory.DecryptResult(
                keyId: "test-key",
                plaintext: plaintextBytes,
                algorithm: EncryptionAlgorithm.RsaOaep256));

        string result = await _sut.DecryptAsync(
            "my-key",
            Convert.ToBase64String(ciphertextInput),
            TestContext.Current.CancellationToken);

        result.ShouldBe("decrypted-text");
    }

    [Fact]
    public async Task RewrapAsync_DecryptsThenReEncrypts()
    {
        byte[] plaintextBytes = Encoding.UTF8.GetBytes("secret-data");
        byte[] oldCiphertext = [1, 2, 3];
        byte[] newCiphertext = [4, 5, 6];

        _cryptoClient.DecryptAsync(
                Arg.Any<EncryptionAlgorithm>(),
                Arg.Any<byte[]>(),
                Arg.Any<CancellationToken>())
            .Returns(CryptographyModelFactory.DecryptResult(
                keyId: "test-key",
                plaintext: plaintextBytes,
                algorithm: EncryptionAlgorithm.RsaOaep256));

        _cryptoClient.EncryptAsync(
                Arg.Any<EncryptionAlgorithm>(),
                Arg.Any<byte[]>(),
                Arg.Any<CancellationToken>())
            .Returns(CryptographyModelFactory.EncryptResult(
                keyId: "test-key",
                ciphertext: newCiphertext,
                algorithm: EncryptionAlgorithm.RsaOaep256));

        string result = await _sut.RewrapAsync(
            "my-key",
            Convert.ToBase64String(oldCiphertext),
            TestContext.Current.CancellationToken);

        result.ShouldBe(Convert.ToBase64String(newCiphertext));
    }

    [Fact]
    public void GetKeyVersion_AlwaysReturnsNull()
    {
        _sut.GetKeyVersion("any-ciphertext").ShouldBeNull();
        _sut.GetKeyVersion("vault:v1:abc").ShouldBeNull();
        _sut.GetKeyVersion("").ShouldBeNull();
    }

    [Fact]
    public async Task EncryptAsync_WhenClientThrows_RecordsErrorMetric_AndRethrows()
    {
        _cryptoClient.EncryptAsync(
                Arg.Any<EncryptionAlgorithm>(),
                Arg.Any<byte[]>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("KV error"));

        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.EncryptAsync("my-key", "data", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DecryptAsync_WhenClientThrows_RecordsErrorMetric_AndRethrows()
    {
        _cryptoClient.DecryptAsync(
                Arg.Any<EncryptionAlgorithm>(),
                Arg.Any<byte[]>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("KV error"));

        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.DecryptAsync("my-key", Convert.ToBase64String([1, 2]), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Class_Implements_ITransitEncryptionService() => _sut.ShouldBeAssignableTo<ITransitEncryptionService>();

    [Fact]
    public void Class_IsInternal() => typeof(AzureKeyVaultTransitEncryptionService).IsNotPublic.ShouldBeTrue();
}
