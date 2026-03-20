using System.Diagnostics.Metrics;
using System.Text;
using Google.Cloud.Kms.V1;
using Google.Protobuf;
using Granit.Vault.Diagnostics;
using Granit.Vault.GoogleCloud.Options;
using Granit.Vault.GoogleCloud.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Vault.GoogleCloud.Tests;

public sealed class CloudKmsTransitEncryptionServiceTests : IDisposable
{
    private readonly KeyManagementServiceClient _kmsClient = Substitute.For<KeyManagementServiceClient>();
    private readonly ServiceProvider _sp;
    private readonly CloudKmsTransitEncryptionService _sut;

    public CloudKmsTransitEncryptionServiceTests()
    {
        GoogleCloudVaultOptions options = new()
        {
            ProjectId = "my-project",
            Location = "europe-west1",
            KeyRing = "granit-keyring",
            CryptoKey = "granit-key",
        };

        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        var metrics = new VaultMetrics(_sp.GetRequiredService<IMeterFactory>());

        _sut = new CloudKmsTransitEncryptionService(
            _kmsClient,
            Microsoft.Extensions.Options.Options.Create(options),
            metrics,
            null,
            NullLogger<CloudKmsTransitEncryptionService>.Instance);
    }

    public void Dispose() => _sp.Dispose();

    [Fact]
    public async Task EncryptAsync_EncodesPlaintextAndReturnsCiphertext()
    {
        byte[] fakeCiphertext = [1, 2, 3, 4, 5];
        _kmsClient.EncryptAsync(
                Arg.Any<CryptoKeyName>(),
                Arg.Any<ByteString>(),
                Arg.Any<CancellationToken>())
            .Returns(new EncryptResponse { Ciphertext = ByteString.CopyFrom(fakeCiphertext) });

        string result = await _sut.EncryptAsync("my-key", "hello world", TestContext.Current.CancellationToken);

        result.ShouldBe(Convert.ToBase64String(fakeCiphertext));
    }

    [Fact]
    public async Task EncryptAsync_SendsUtf8EncodedPlaintext()
    {
        ByteString? captured = null;
        _kmsClient.EncryptAsync(
                Arg.Any<CryptoKeyName>(),
                Arg.Do<ByteString>(b => captured = b),
                Arg.Any<CancellationToken>())
            .Returns(new EncryptResponse { Ciphertext = ByteString.CopyFrom([1]) });

        await _sut.EncryptAsync("my-key", "hello", TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        Encoding.UTF8.GetString(captured.ToByteArray()).ShouldBe("hello");
    }

    [Fact]
    public async Task DecryptAsync_DecodesBase64AndReturnsPlaintext()
    {
        byte[] plaintextBytes = Encoding.UTF8.GetBytes("decrypted-text");
        byte[] ciphertextInput = [10, 20, 30];

        _kmsClient.DecryptAsync(
                Arg.Any<CryptoKeyName>(),
                Arg.Any<ByteString>(),
                Arg.Any<CancellationToken>())
            .Returns(new DecryptResponse { Plaintext = ByteString.CopyFrom(plaintextBytes) });

        string result = await _sut.DecryptAsync(
            "my-key",
            Convert.ToBase64String(ciphertextInput),
            TestContext.Current.CancellationToken);

        result.ShouldBe("decrypted-text");
    }

    [Fact]
    public async Task DecryptAsync_SendsCiphertextBlobFromBase64()
    {
        byte[] ciphertextBytes = [5, 6, 7, 8];
        ByteString? captured = null;

        _kmsClient.DecryptAsync(
                Arg.Any<CryptoKeyName>(),
                Arg.Do<ByteString>(b => captured = b),
                Arg.Any<CancellationToken>())
            .Returns(new DecryptResponse { Plaintext = ByteString.CopyFrom(Encoding.UTF8.GetBytes("ok")) });

        await _sut.DecryptAsync(
            "my-key",
            Convert.ToBase64String(ciphertextBytes),
            TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.ToByteArray().ShouldBe(ciphertextBytes);
    }

    [Fact]
    public void Class_Implements_ITransitEncryptionService() =>
        _sut.ShouldBeAssignableTo<ITransitEncryptionService>();

    [Fact]
    public void Class_IsInternal() =>
        typeof(CloudKmsTransitEncryptionService).IsNotPublic.ShouldBeTrue();
}
