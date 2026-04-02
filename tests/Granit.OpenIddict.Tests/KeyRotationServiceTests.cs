using Granit.Encryption;
using Granit.OpenIddict.Domain;
using Granit.OpenIddict.Internal;
using Granit.OpenIddict.Options;
using Granit.OpenIddict.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests;

public sealed class KeyRotationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly ISigningKeyStore _keyStore = Substitute.For<ISigningKeyStore>();
    private readonly IStringEncryptionService _encryptionService = Substitute.For<IStringEncryptionService>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();

    public KeyRotationServiceTests()
    {
        _timeProvider.GetUtcNow().Returns(Now);
        _encryptionService.Encrypt(Arg.Any<string>()).Returns(ci => $"enc:{ci.Arg<string>()[..8]}");

        // Default: no keys, no pruning
        _keyStore.GetActiveKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((SigningKey?)null);
        _keyStore.GetKeysAsync(Arg.Any<SigningKeyStatus[]>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _keyStore.PruneRevokedAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(0);
    }

    private KeyRotationService CreateSut(GranitKeyRotationOptions? options = null)
    {
        options ??= new GranitKeyRotationOptions
        {
            Enabled = true,
            KeyLifetime = TimeSpan.FromDays(90),
            GracePeriod = TimeSpan.FromDays(7),
            RotationLeadTime = TimeSpan.FromDays(14),
            RsaKeySize = 2048,
            SigningAlgorithm = "RS256",
        };

        return new KeyRotationService(
            _keyStore,
            _encryptionService,
            Microsoft.Extensions.Options.Options.Create(options),
            _timeProvider,
            NullLogger<KeyRotationService>.Instance);
    }

    [Fact]
    public async Task RotateAsync_WhenDisabled_ReturnsZeroResult()
    {
        KeyRotationService sut = CreateSut(new GranitKeyRotationOptions { Enabled = false });

        KeyRotationResult result = await sut.RotateAsync(TestContext.Current.CancellationToken);

        result.KeysGenerated.ShouldBe(0);
        result.KeysRetired.ShouldBe(0);
        result.KeysRevoked.ShouldBe(0);
        result.KeysPruned.ShouldBe(0);
    }

    [Fact]
    public async Task RotateAsync_WhenDisabled_DoesNotAccessKeyStore()
    {
        KeyRotationService sut = CreateSut(new GranitKeyRotationOptions { Enabled = false });

        await sut.RotateAsync(TestContext.Current.CancellationToken);

        await _keyStore.DidNotReceive().GetActiveKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RotateAsync_NoActiveKeys_GeneratesBothSigningAndEncryptionKeys()
    {
        KeyRotationService sut = CreateSut();

        KeyRotationResult result = await sut.RotateAsync(TestContext.Current.CancellationToken);

        result.KeysGenerated.ShouldBe(2);
        await _keyStore.Received(2).CreateAsync(Arg.Any<SigningKey>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RotateAsync_NoActiveKeys_GeneratesSigningKeyWithRS256()
    {
        SigningKey? capturedKey = null;
        _keyStore.CreateAsync(Arg.Do<SigningKey>(k =>
        {
            if (k.KeyType == "signing")
            {
                capturedKey = k;
            }
        }), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        KeyRotationService sut = CreateSut();

        await sut.RotateAsync(TestContext.Current.CancellationToken);

        capturedKey.ShouldNotBeNull();
        capturedKey.Algorithm.ShouldBe("RS256");
        capturedKey.KeyType.ShouldBe("signing");
        capturedKey.KeySize.ShouldBe(2048);
        capturedKey.Status.ShouldBe(SigningKeyStatus.Active);
        capturedKey.ActivatedAt.ShouldBe(Now);
        capturedKey.ExpiresAt.ShouldBe(Now.AddDays(90));
    }

    [Fact]
    public async Task RotateAsync_NoActiveKeys_GeneratesEncryptionKeyWithRsaOaep()
    {
        SigningKey? capturedKey = null;
        _keyStore.CreateAsync(Arg.Do<SigningKey>(k =>
        {
            if (k.KeyType == "encryption")
            {
                capturedKey = k;
            }
        }), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        KeyRotationService sut = CreateSut();

        await sut.RotateAsync(TestContext.Current.CancellationToken);

        capturedKey.ShouldNotBeNull();
        capturedKey.Algorithm.ShouldBe("RSA-OAEP");
        capturedKey.KeyType.ShouldBe("encryption");
    }

    [Fact]
    public async Task RotateAsync_NoActiveKeys_EncryptsKeyMaterial()
    {
        KeyRotationService sut = CreateSut();

        await sut.RotateAsync(TestContext.Current.CancellationToken);

        _encryptionService.Received(2).Encrypt(Arg.Any<string>());
    }

    [Fact]
    public async Task RotateAsync_ActiveKeysNotExpiring_DoesNotGenerateNewKeys()
    {
        SetupActiveKey("signing", Now.AddDays(30));
        SetupActiveKey("encryption", Now.AddDays(30));

        KeyRotationService sut = CreateSut();

        KeyRotationResult result = await sut.RotateAsync(TestContext.Current.CancellationToken);

        result.KeysGenerated.ShouldBe(0);
        await _keyStore.DidNotReceive().CreateAsync(Arg.Any<SigningKey>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RotateAsync_SigningKeyExpiringWithinLeadTime_RotatesSigningKey()
    {
        // Signing key expires in 10 days (< 14 days lead time)
        SigningKey signingKey = SetupActiveKey("signing", Now.AddDays(10));
        SetupActiveKey("encryption", Now.AddDays(60));

        KeyRotationService sut = CreateSut();

        KeyRotationResult result = await sut.RotateAsync(TestContext.Current.CancellationToken);

        result.KeysGenerated.ShouldBe(1);
        await _keyStore.Received(1).CreateAsync(Arg.Any<SigningKey>(), Arg.Any<CancellationToken>());
        await _keyStore.Received().UpdateAsync(signingKey, Arg.Any<CancellationToken>());
        signingKey.Status.ShouldBe(SigningKeyStatus.Retired);
        signingKey.RetiredAt.ShouldBe(Now);
    }

    [Fact]
    public async Task RotateAsync_RetiredKeysPastGracePeriod_RevokesKeys()
    {
        SetupActiveKey("signing", Now.AddDays(60));
        SetupActiveKey("encryption", Now.AddDays(60));

        var retiredKey = SigningKey.Create(
            "signing-old", "signing", "RS256", "enc:material",
            Now.AddDays(-120), Now.AddDays(-30), 2048);
        retiredKey.Retire(Now.AddDays(-10));

        _keyStore.GetKeysAsync(Arg.Is<SigningKeyStatus[]>(s => s.Contains(SigningKeyStatus.Retired)), Arg.Any<CancellationToken>())
            .Returns([retiredKey]);

        KeyRotationService sut = CreateSut();

        KeyRotationResult result = await sut.RotateAsync(TestContext.Current.CancellationToken);

        result.KeysRevoked.ShouldBe(1);
        retiredKey.Status.ShouldBe(SigningKeyStatus.Revoked);
    }

    [Fact]
    public async Task RotateAsync_RetiredKeysWithinGracePeriod_DoesNotRevoke()
    {
        SetupActiveKey("signing", Now.AddDays(60));
        SetupActiveKey("encryption", Now.AddDays(60));

        var retiredKey = SigningKey.Create(
            "signing-recent", "signing", "RS256", "enc:material",
            Now.AddDays(-80), Now.AddDays(-1), 2048);
        retiredKey.Retire(Now.AddDays(-3)); // Retired 3 days ago, grace = 7 days

        _keyStore.GetKeysAsync(Arg.Is<SigningKeyStatus[]>(s => s.Contains(SigningKeyStatus.Retired)), Arg.Any<CancellationToken>())
            .Returns([retiredKey]);

        KeyRotationService sut = CreateSut();

        KeyRotationResult result = await sut.RotateAsync(TestContext.Current.CancellationToken);

        result.KeysRevoked.ShouldBe(0);
        retiredKey.Status.ShouldBe(SigningKeyStatus.Retired);
    }

    [Fact]
    public async Task RotateAsync_RevokedKeysOlderThan30Days_Pruned()
    {
        SetupActiveKey("signing", Now.AddDays(60));
        SetupActiveKey("encryption", Now.AddDays(60));

        _keyStore.PruneRevokedAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(3);

        KeyRotationService sut = CreateSut();

        KeyRotationResult result = await sut.RotateAsync(TestContext.Current.CancellationToken);

        result.KeysPruned.ShouldBe(3);
        await _keyStore.Received().PruneRevokedAsync(
            Now.AddDays(-30), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RotateAsync_FullLifecycle_HandlesAllPhases()
    {
        // Active signing key expiring soon → rotation
        SigningKey expiringKey = SetupActiveKey("signing", Now.AddDays(5));
        SetupActiveKey("encryption", Now.AddDays(60));

        // Retired key past grace period → revocation
        var oldRetired = SigningKey.Create(
            "signing-ancient", "signing", "RS256", "enc:material",
            Now.AddDays(-200), Now.AddDays(-100), 2048);
        oldRetired.Retire(Now.AddDays(-20));
        _keyStore.GetKeysAsync(Arg.Is<SigningKeyStatus[]>(s => s.Contains(SigningKeyStatus.Retired)), Arg.Any<CancellationToken>())
            .Returns([oldRetired]);

        // 2 pruned keys
        _keyStore.PruneRevokedAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(2);

        KeyRotationService sut = CreateSut();

        KeyRotationResult result = await sut.RotateAsync(TestContext.Current.CancellationToken);

        result.KeysGenerated.ShouldBe(1);
        result.KeysRevoked.ShouldBe(1);
        result.KeysPruned.ShouldBe(2);
    }

    private SigningKey SetupActiveKey(string keyType, DateTimeOffset expiresAt)
    {
        var key = SigningKey.Create(
            $"{keyType}-active", keyType, keyType == "signing" ? "RS256" : "RSA-OAEP",
            "enc:material", Now.AddDays(-30), expiresAt, 2048);

        _keyStore.GetActiveKeyAsync(keyType, Arg.Any<CancellationToken>())
            .Returns(key);

        return key;
    }
}
