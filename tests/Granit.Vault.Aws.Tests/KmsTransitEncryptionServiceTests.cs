using System.Diagnostics.Metrics;
using System.Text;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Granit.Vault.Aws.Options;
using Granit.Vault.Aws.Services;
using Granit.Vault.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Vault.Aws.Tests;

public sealed class KmsTransitEncryptionServiceTests : IDisposable
{
    private readonly IAmazonKeyManagementService _kmsClient = Substitute.For<IAmazonKeyManagementService>();
    private readonly ServiceProvider _sp;
    private readonly KmsTransitEncryptionService _sut;

    public KmsTransitEncryptionServiceTests()
    {
        AwsVaultOptions options = new()
        {
            Region = "eu-west-1",
            KmsKeyId = "alias/test-key",
        };

        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        var metrics = new VaultMetrics(_sp.GetRequiredService<IMeterFactory>());

        _sut = new KmsTransitEncryptionService(
            _kmsClient,
            Microsoft.Extensions.Options.Options.Create(options),
            metrics,
            null,
            NullLogger<KmsTransitEncryptionService>.Instance);
    }

    public void Dispose() => _sp.Dispose();

    [Fact]
    public async Task EncryptAsync_EncodesPlaintextAndReturnsCiphertext()
    {
        byte[] fakeCiphertext = [1, 2, 3, 4, 5];
        _kmsClient.EncryptAsync(Arg.Any<EncryptRequest>(), Arg.Any<CancellationToken>())
            .Returns(new EncryptResponse { CiphertextBlob = new MemoryStream(fakeCiphertext) });

        string result = await _sut.EncryptAsync("my-key", "hello world", TestContext.Current.CancellationToken);

        result.ShouldBe(Convert.ToBase64String(fakeCiphertext));
    }

    [Fact]
    public async Task EncryptAsync_SendsCorrectKeyId()
    {
        _kmsClient.EncryptAsync(Arg.Any<EncryptRequest>(), Arg.Any<CancellationToken>())
            .Returns(new EncryptResponse { CiphertextBlob = new MemoryStream([1]) });

        EncryptRequest? captured = null;
        await _kmsClient.EncryptAsync(
            Arg.Do<EncryptRequest>(r => captured = r),
            Arg.Any<CancellationToken>());

        await _sut.EncryptAsync("my-key", "test", TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.KeyId.ShouldBe("alias/test-key");
    }

    [Fact]
    public async Task EncryptAsync_SendsUtf8EncodedPlaintext()
    {
        _kmsClient.EncryptAsync(Arg.Any<EncryptRequest>(), Arg.Any<CancellationToken>())
            .Returns(new EncryptResponse { CiphertextBlob = new MemoryStream([1]) });

        EncryptRequest? captured = null;
        await _kmsClient.EncryptAsync(
            Arg.Do<EncryptRequest>(r => captured = r),
            Arg.Any<CancellationToken>());

        await _sut.EncryptAsync("my-key", "hello", TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        byte[] sentBytes = captured.Plaintext.ToArray();
        Encoding.UTF8.GetString(sentBytes).ShouldBe("hello");
    }

    [Fact]
    public async Task DecryptAsync_DecodesBase64AndReturnsPlaintext()
    {
        byte[] plaintextBytes = Encoding.UTF8.GetBytes("decrypted-text");
        byte[] ciphertextInput = [10, 20, 30];

        _kmsClient.DecryptAsync(Arg.Any<DecryptRequest>(), Arg.Any<CancellationToken>())
            .Returns(new DecryptResponse { Plaintext = new MemoryStream(plaintextBytes) });

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

        _kmsClient.DecryptAsync(Arg.Any<DecryptRequest>(), Arg.Any<CancellationToken>())
            .Returns(new DecryptResponse { Plaintext = new MemoryStream(Encoding.UTF8.GetBytes("ok")) });

        DecryptRequest? captured = null;
        await _kmsClient.DecryptAsync(
            Arg.Do<DecryptRequest>(r => captured = r),
            Arg.Any<CancellationToken>());

        await _sut.DecryptAsync(
            "my-key",
            Convert.ToBase64String(ciphertextBytes),
            TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.CiphertextBlob.ToArray().ShouldBe(ciphertextBytes);
    }

    [Fact]
    public void Class_Implements_ITransitEncryptionService() =>
        _sut.ShouldBeAssignableTo<ITransitEncryptionService>();

    [Fact]
    public void Class_IsInternal() =>
        typeof(KmsTransitEncryptionService).IsNotPublic.ShouldBeTrue();
}
