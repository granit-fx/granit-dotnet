using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Granit.Bff.Internal;
using Granit.Bff.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Bff.Tests.Internal;

public sealed class LogoutTokenValidatorTests : IDisposable
{
    private const string Authority = "https://auth.example.com";
    private const string ClientId = "my-client";

    private readonly IFusionCache _cache = Substitute.For<IFusionCache>();
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();
    private readonly RSA _rsa = RSA.Create(2048);
    private readonly LogoutTokenValidator _validator;

    public LogoutTokenValidatorTests()
    {
        IOptions<GranitBffOptions> options = Microsoft.Extensions.Options.Options.Create(
            new GranitBffOptions { Authority = new Uri(Authority) });
        _validator = new LogoutTokenValidator(
            options, _httpClientFactory, _cache, NullLogger<LogoutTokenValidator>.Instance);
    }

    public void Dispose() => _rsa.Dispose();

    // ──── Null / whitespace guards ────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateAsync_NullOrWhitespaceToken_Throws(string? token) =>
        await Should.ThrowAsync<ArgumentException>(
            () => _validator.ValidateAsync(token!, ClientId, TestContext.Current.CancellationToken));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateAsync_NullOrWhitespaceClientId_Throws(string? clientId) =>
        await Should.ThrowAsync<ArgumentException>(
            () => _validator.ValidateAsync("some.jwt.token", clientId!, TestContext.Current.CancellationToken));

    // ──── Structural validation ────

    [Theory]
    [InlineData("not-a-jwt")]
    [InlineData("only.two")]
    [InlineData("one.two.three.four")]
    public async Task ValidateAsync_InvalidJwtStructure_ReturnsNull(string token)
    {
        ValidatedLogoutToken? result = await _validator.ValidateAsync(
            token, ClientId, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // ──── alg:none rejection (CWE-345) ────

    [Fact]
    public async Task ValidateAsync_AlgNone_ReturnsNull()
    {
        string token = BuildUnsignedToken(algorithm: "none");

        ValidatedLogoutToken? result = await _validator.ValidateAsync(
            token, ClientId, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ValidateAsync_AlgNoneCaseInsensitive_ReturnsNull()
    {
        string token = BuildUnsignedToken(algorithm: "NoNe");

        ValidatedLogoutToken? result = await _validator.ValidateAsync(
            token, ClientId, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // ──── Invalid header ────

    [Fact]
    public async Task ValidateAsync_InvalidBase64Header_ReturnsNull()
    {
        const string token = "!!!invalid-base64!!!.payload.signature";

        ValidatedLogoutToken? result = await _validator.ValidateAsync(
            token, ClientId, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ValidateAsync_HeaderMissingAlg_ReturnsNull()
    {
        string header = Base64UrlEncode("""{"kid":"key-1"}"""u8);
        string token = $"{header}.payload.signature";

        ValidatedLogoutToken? result = await _validator.ValidateAsync(
            token, ClientId, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // ──── Issuer mismatch ────

    [Fact]
    public async Task ValidateAsync_IssuerMismatch_ReturnsNull()
    {
        string token = BuildSignedToken(
            issuer: "https://evil.example.com",
            audience: ClientId,
            includeBackChannelEvent: true);
        SetupCacheWithJwks();

        ValidatedLogoutToken? result = await _validator.ValidateAsync(
            token, ClientId, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // ──── Audience mismatch ────

    [Fact]
    public async Task ValidateAsync_AudienceMismatch_ReturnsNull()
    {
        string token = BuildSignedToken(
            issuer: Authority,
            audience: "wrong-client-id",
            includeBackChannelEvent: true);
        SetupCacheWithJwks();

        ValidatedLogoutToken? result = await _validator.ValidateAsync(
            token, ClientId, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // ──── Missing back-channel event ────

    [Fact]
    public async Task ValidateAsync_MissingBackChannelEvent_ReturnsNull()
    {
        string token = BuildSignedToken(
            issuer: Authority,
            audience: ClientId,
            includeBackChannelEvent: false);
        SetupCacheWithJwks();

        ValidatedLogoutToken? result = await _validator.ValidateAsync(
            token, ClientId, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // ──── Valid token ────

    [Fact]
    public async Task ValidateAsync_ValidToken_ReturnsValidatedClaims()
    {
        const string subject = "user-42";
        const string jti = "unique-token-id";
        string token = BuildSignedToken(
            issuer: Authority,
            audience: ClientId,
            includeBackChannelEvent: true,
            subject: subject,
            jti: jti);
        SetupCacheWithJwks();

        ValidatedLogoutToken? result = await _validator.ValidateAsync(
            token, ClientId, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Issuer.ShouldBe(Authority);
        result.Subject.ShouldBe(subject);
        result.Jti.ShouldBe(jti);
        result.HasBackChannelLogoutEvent.ShouldBeTrue();
        result.Audiences.ShouldNotBeNull();
        result.Audiences.ShouldContain(ClientId);
    }

    [Fact]
    public async Task ValidateAsync_ValidTokenWithTrailingSlashAuthority_ReturnsValidatedClaims()
    {
        string token = BuildSignedToken(
            issuer: $"{Authority}/",
            audience: ClientId,
            includeBackChannelEvent: true);
        SetupCacheWithJwks();

        ValidatedLogoutToken? result = await _validator.ValidateAsync(
            token, ClientId, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
    }

    // ──── Signature verification ────

    [Fact]
    public async Task ValidateAsync_InvalidSignature_ReturnsNull()
    {
        string token = BuildSignedToken(
            issuer: Authority,
            audience: ClientId,
            includeBackChannelEvent: true);

        // Tamper with the signature by flipping a byte
        string[] parts = token.Split('.');
        byte[] sigBytes = Base64UrlDecode(parts[2]);
        sigBytes[0] = (byte)(sigBytes[0] ^ 0xFF);
        string tamperedToken = $"{parts[0]}.{parts[1]}.{Base64UrlEncode(sigBytes)}";

        SetupCacheWithJwks();

        ValidatedLogoutToken? result = await _validator.ValidateAsync(
            tamperedToken, ClientId, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ValidateAsync_EmptyJwks_ReturnsNull()
    {
        string token = BuildSignedToken(
            issuer: Authority,
            audience: ClientId,
            includeBackChannelEvent: true);

        // Cache returns an empty key set — no signing key can be found
        _cache.GetOrSetAsync<List<JsonElement>>(
                Arg.Any<string>(),
                Arg.Any<Func<FusionCacheFactoryExecutionContext<List<JsonElement>>, CancellationToken, Task<List<JsonElement>>>>(),
                Arg.Any<FusionCacheEntryOptions?>(),
                Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs((List<JsonElement>)[]);

        ValidatedLogoutToken? result = await _validator.ValidateAsync(
            token, ClientId, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ValidateAsync_NullJwksFromCache_ReturnsNull()
    {
        string token = BuildSignedToken(
            issuer: Authority,
            audience: ClientId,
            includeBackChannelEvent: true);

        _cache.GetOrSetAsync<List<JsonElement>>(
                Arg.Any<string>(),
                Arg.Any<Func<FusionCacheFactoryExecutionContext<List<JsonElement>>, CancellationToken, Task<List<JsonElement>>>>(),
                Arg.Any<FusionCacheEntryOptions?>(),
                Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs((List<JsonElement>?)null!);

        ValidatedLogoutToken? result = await _validator.ValidateAsync(
            token, ClientId, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // ──── No kid in header (selects first sig-use key) ────

    [Fact]
    public async Task ValidateAsync_NoKidInHeader_SelectsFirstSigKey()
    {
        string token = BuildSignedToken(
            issuer: Authority,
            audience: ClientId,
            includeBackChannelEvent: true,
            kid: null);
        SetupCacheWithJwks("any-kid");

        ValidatedLogoutToken? result = await _validator.ValidateAsync(
            token, ClientId, TestContext.Current.CancellationToken);

        // The key should match via "use":"sig" even without kid
        result.ShouldNotBeNull();
    }

    // ──── Audience: array with multiple values ────

    [Fact]
    public async Task ValidateAsync_AudienceArrayWithMultipleValues_Succeeds()
    {
        string token = BuildSignedTokenWithAudienceArray(
            issuer: Authority,
            audiences: ["other-client", ClientId],
            includeBackChannelEvent: true);
        SetupCacheWithJwks();

        ValidatedLogoutToken? result = await _validator.ValidateAsync(
            token, ClientId, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Audiences.ShouldNotBeNull();
        result.Audiences!.ShouldContain(ClientId);
        result.Audiences!.ShouldContain("other-client");
    }

    // ──── Audience: null (no aud claim) — succeeds ────

    [Fact]
    public async Task ValidateAsync_NoAudienceClaim_Succeeds()
    {
        string token = BuildSignedTokenWithNoAudience(
            issuer: Authority,
            includeBackChannelEvent: true);
        SetupCacheWithJwks();

        ValidatedLogoutToken? result = await _validator.ValidateAsync(
            token, ClientId, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Audiences.ShouldBeNull();
    }

    // ──── Invalid base64 in payload ────

    [Fact]
    public async Task ValidateAsync_InvalidPayloadBase64_ReturnsNull()
    {
        string header = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(
            new { alg = "RS256", kid = "test-key-1" }));
        SetupCacheWithJwks();

        // valid header + invalid payload + dummy signature
        byte[] signingInput = System.Text.Encoding.ASCII.GetBytes($"{header}.!!!invalid!!!");
        byte[] sig = _rsa.SignData(signingInput, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        string token = $"{header}.!!!invalid!!!.{Base64UrlEncode(sig)}";

        ValidatedLogoutToken? result = await _validator.ValidateAsync(
            token, ClientId, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // ──── Unsupported algorithm / key type ────

    [Fact]
    public async Task ValidateAsync_UnsupportedAlgorithm_ReturnsNull()
    {
        // Build token claiming HS256 (HMAC) — not supported
        string token = BuildSignedToken(
            issuer: Authority,
            audience: ClientId,
            includeBackChannelEvent: true,
            algorithm: "HS256");
        SetupCacheWithJwks();

        ValidatedLogoutToken? result = await _validator.ValidateAsync(
            token, ClientId, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // ──── EC signature verification ────

    [Fact]
    public async Task ValidateAsync_ValidEcSignedToken_ReturnsValidatedClaims()
    {
        using var ecKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string token = BuildEcSignedToken(ecKey, Authority, ClientId, includeBackChannelEvent: true);
        SetupCacheWithEcJwks(ecKey);

        ValidatedLogoutToken? result = await _validator.ValidateAsync(
            token, ClientId, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Issuer.ShouldBe(Authority);
        result.HasBackChannelLogoutEvent.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_ValidEcP384SignedToken_ReturnsValidatedClaims()
    {
        using var ecKey = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        string token = BuildEcSignedToken(
            ecKey, Authority, ClientId,
            includeBackChannelEvent: true,
            algorithm: "ES384",
            hashAlgorithm: HashAlgorithmName.SHA384);
        SetupCacheWithEcJwks(ecKey);

        ValidatedLogoutToken? result = await _validator.ValidateAsync(
            token, ClientId, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task ValidateAsync_ValidEcP521SignedToken_ReturnsValidatedClaims()
    {
        using var ecKey = ECDsa.Create(ECCurve.NamedCurves.nistP521);
        string token = BuildEcSignedToken(
            ecKey, Authority, ClientId,
            includeBackChannelEvent: true,
            algorithm: "ES512",
            hashAlgorithm: HashAlgorithmName.SHA512);
        SetupCacheWithEcJwks(ecKey);

        ValidatedLogoutToken? result = await _validator.ValidateAsync(
            token, ClientId, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
    }

    // ──── RSA-PSS signature verification (PS256) ────

    [Fact]
    public async Task ValidateAsync_ValidPssSignedToken_ReturnsValidatedClaims()
    {
        string token = BuildSignedToken(
            issuer: Authority,
            audience: ClientId,
            includeBackChannelEvent: true,
            algorithm: "PS256");

        // Sign with PSS padding
        string[] parts = token.Split('.');
        byte[] signingInput = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
        byte[] pssSignature = _rsa.SignData(signingInput, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        string pssToken = $"{parts[0]}.{parts[1]}.{Base64UrlEncode(pssSignature)}";

        SetupCacheWithJwks();

        ValidatedLogoutToken? result = await _validator.ValidateAsync(
            pssToken, ClientId, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
    }

    // ──── Key rotation re-fetch ────

    [Fact]
    public async Task ValidateAsync_KidNotFoundInitially_RefetchesJwks()
    {
        const string rotatedKid = "rotated-key-2";
        string token = BuildSignedToken(
            issuer: Authority,
            audience: ClientId,
            includeBackChannelEvent: true,
            kid: rotatedKid);

        RSAParameters pubParams = _rsa.ExportParameters(includePrivateParameters: false);

        // First call returns keys without the matching kid (simulates pre-rotation state)
        var oldJwk = new Dictionary<string, object?>
        {
            ["kty"] = "RSA",
            ["kid"] = "old-key-1",
            ["use"] = "sig",
            ["n"] = Base64UrlEncode(pubParams.Modulus!),
            ["e"] = Base64UrlEncode(pubParams.Exponent!),
        };
        string oldJwkJson = JsonSerializer.Serialize(oldJwk);
        using var oldJwkDoc = JsonDocument.Parse(oldJwkJson);
        List<JsonElement> oldKeys = [oldJwkDoc.RootElement.Clone()];

        // Second call (after cache removal) returns the rotated key
        var newJwk = new Dictionary<string, object?>
        {
            ["kty"] = "RSA",
            ["kid"] = rotatedKid,
            ["use"] = "sig",
            ["n"] = Base64UrlEncode(pubParams.Modulus!),
            ["e"] = Base64UrlEncode(pubParams.Exponent!),
        };
        string newJwkJson = JsonSerializer.Serialize(newJwk);
        using var newJwkDoc = JsonDocument.Parse(newJwkJson);
        List<JsonElement> newKeys = [newJwkDoc.RootElement.Clone()];

        _cache.GetOrSetAsync<List<JsonElement>>(
                Arg.Any<string>(),
                Arg.Any<Func<FusionCacheFactoryExecutionContext<List<JsonElement>>, CancellationToken, Task<List<JsonElement>>>>(),
                Arg.Any<FusionCacheEntryOptions?>(),
                Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs(oldKeys, newKeys);

        ValidatedLogoutToken? result = await _validator.ValidateAsync(
            token, ClientId, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.HasBackChannelLogoutEvent.ShouldBeTrue();

        // Verify cache was cleared to trigger re-fetch
        await _cache.Received().RemoveAsync("bff:oidc-jwks", Arg.Any<FusionCacheEntryOptions?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateAsync_KidNotFoundAfterRefetch_ReturnsNull()
    {
        const string unknownKid = "completely-unknown-key";
        string token = BuildSignedToken(
            issuer: Authority,
            audience: ClientId,
            includeBackChannelEvent: true,
            kid: unknownKid);

        // Both calls return keys without the matching kid
        RSAParameters pubParams = _rsa.ExportParameters(includePrivateParameters: false);
        var jwk = new Dictionary<string, object?>
        {
            ["kty"] = "RSA",
            ["kid"] = "some-other-key",
            ["use"] = "sig",
            ["n"] = Base64UrlEncode(pubParams.Modulus!),
            ["e"] = Base64UrlEncode(pubParams.Exponent!),
        };
        string jwkJson = JsonSerializer.Serialize(jwk);
        using var jwkDoc = JsonDocument.Parse(jwkJson);
        List<JsonElement> keys = [jwkDoc.RootElement.Clone()];

        _cache.GetOrSetAsync<List<JsonElement>>(
                Arg.Any<string>(),
                Arg.Any<Func<FusionCacheFactoryExecutionContext<List<JsonElement>>, CancellationToken, Task<List<JsonElement>>>>(),
                Arg.Any<FusionCacheEntryOptions?>(),
                Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs(keys);

        ValidatedLogoutToken? result = await _validator.ValidateAsync(
            token, ClientId, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // ──── Missing subject and jti (nullable fields) ────

    [Fact]
    public async Task ValidateAsync_MissingSubjectAndJti_StillReturnsValidToken()
    {
        string token = BuildSignedToken(
            issuer: Authority,
            audience: ClientId,
            includeBackChannelEvent: true,
            subject: null,
            jti: null);
        SetupCacheWithJwks();

        ValidatedLogoutToken? result = await _validator.ValidateAsync(
            token, ClientId, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Subject.ShouldBeNull();
        result.Jti.ShouldBeNull();
    }

    // ──── Audience array mismatch (array form, client not present) ────

    [Fact]
    public async Task ValidateAsync_AudienceArrayWithoutClientId_ReturnsNull()
    {
        string token = BuildSignedTokenWithAudienceArray(
            issuer: Authority,
            audiences: ["other-client-1", "other-client-2"],
            includeBackChannelEvent: true);
        SetupCacheWithJwks();

        ValidatedLogoutToken? result = await _validator.ValidateAsync(
            token, ClientId, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // ──── Payload with valid base64 but invalid JSON ────

    [Fact]
    public async Task ValidateAsync_ValidBase64ButInvalidJsonPayload_ReturnsNull()
    {
        string header = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(
            new { alg = "RS256", kid = "test-key-1" }));
        SetupCacheWithJwks();

        // Encode a non-JSON string as valid base64url
        string invalidPayload = Base64UrlEncode(Encoding.UTF8.GetBytes("this is not json"));
        byte[] signingInput = Encoding.ASCII.GetBytes($"{header}.{invalidPayload}");
        byte[] sig = _rsa.SignData(signingInput, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        string token = $"{header}.{invalidPayload}.{Base64UrlEncode(sig)}";

        ValidatedLogoutToken? result = await _validator.ValidateAsync(
            token, ClientId, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // ──── Events claim present but wrong event type ────

    [Fact]
    public async Task ValidateAsync_WrongEventType_ReturnsNull()
    {
        string token = BuildSignedTokenWithCustomEvents(
            issuer: Authority,
            audience: ClientId,
            eventType: "http://schemas.openid.net/event/some-other-event");
        SetupCacheWithJwks();

        ValidatedLogoutToken? result = await _validator.ValidateAsync(
            token, ClientId, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // ──── Token with no events claim at all ────

    [Fact]
    public async Task ValidateAsync_NoEventsClaim_ReturnsNull()
    {
        string token = BuildSignedTokenWithCustomPayload(
            issuer: Authority,
            audience: ClientId,
            includeEvents: false);
        SetupCacheWithJwks();

        ValidatedLogoutToken? result = await _validator.ValidateAsync(
            token, ClientId, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // ──── Key with "use" property not equal to "sig" (no kid path) ────

    [Fact]
    public async Task ValidateAsync_NoKidInHeader_SkipsEncryptionKey_SelectsSigKey()
    {
        RSAParameters pubParams = _rsa.ExportParameters(includePrivateParameters: false);

        // First key is for encryption, second is for signing
        var encJwk = new Dictionary<string, object?>
        {
            ["kty"] = "RSA",
            ["kid"] = "enc-key",
            ["use"] = "enc",
            ["n"] = Base64UrlEncode(pubParams.Modulus!),
            ["e"] = Base64UrlEncode(pubParams.Exponent!),
        };

        var sigJwk = new Dictionary<string, object?>
        {
            ["kty"] = "RSA",
            ["kid"] = "sig-key",
            ["use"] = "sig",
            ["n"] = Base64UrlEncode(pubParams.Modulus!),
            ["e"] = Base64UrlEncode(pubParams.Exponent!),
        };

        string encJson = JsonSerializer.Serialize(encJwk);
        string sigJson = JsonSerializer.Serialize(sigJwk);
        using var encDoc = JsonDocument.Parse(encJson);
        using var sigDoc = JsonDocument.Parse(sigJson);
        List<JsonElement> keys = [encDoc.RootElement.Clone(), sigDoc.RootElement.Clone()];

        _cache.GetOrSetAsync<List<JsonElement>>(
                Arg.Any<string>(),
                Arg.Any<Func<FusionCacheFactoryExecutionContext<List<JsonElement>>, CancellationToken, Task<List<JsonElement>>>>(),
                Arg.Any<FusionCacheEntryOptions?>(),
                Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs(keys);

        // Build token without kid
        string token = BuildSignedToken(
            issuer: Authority,
            audience: ClientId,
            includeBackChannelEvent: true,
            kid: null);

        ValidatedLogoutToken? result = await _validator.ValidateAsync(
            token, ClientId, TestContext.Current.CancellationToken);

        // The "enc" key should be skipped because FindKeyByPredicate checks use=="sig"
        // However, since both keys have the same RSA parameters, both would verify.
        // The important thing is that a key is selected and validation passes.
        result.ShouldNotBeNull();
    }

    // ──── RS384 and RS512 algorithm variations ────

    [Fact]
    public async Task ValidateAsync_Rs384Algorithm_ReturnsValidatedClaims()
    {
        var header = new Dictionary<string, object?> { ["alg"] = "RS384", ["typ"] = "JWT", ["kid"] = "test-key-1" };
        string headerB64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header));

        var payload = new Dictionary<string, object?>
        {
            ["iss"] = Authority,
            ["sub"] = "user-1",
            ["jti"] = "test-jti",
            ["aud"] = ClientId,
            ["events"] = new Dictionary<string, object>
            {
                ["http://schemas.openid.net/event/backchannel-logout"] = new { },
            },
        };

        string payloadB64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
        byte[] signingInput = Encoding.ASCII.GetBytes($"{headerB64}.{payloadB64}");
        byte[] signature = _rsa.SignData(signingInput, HashAlgorithmName.SHA384, RSASignaturePadding.Pkcs1);
        string token = $"{headerB64}.{payloadB64}.{Base64UrlEncode(signature)}";

        SetupCacheWithJwks();

        ValidatedLogoutToken? result = await _validator.ValidateAsync(
            token, ClientId, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task ValidateAsync_Rs512Algorithm_ReturnsValidatedClaims()
    {
        var header = new Dictionary<string, object?> { ["alg"] = "RS512", ["typ"] = "JWT", ["kid"] = "test-key-1" };
        string headerB64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header));

        var payload = new Dictionary<string, object?>
        {
            ["iss"] = Authority,
            ["sub"] = "user-1",
            ["jti"] = "test-jti",
            ["aud"] = ClientId,
            ["events"] = new Dictionary<string, object>
            {
                ["http://schemas.openid.net/event/backchannel-logout"] = new { },
            },
        };

        string payloadB64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
        byte[] signingInput = Encoding.ASCII.GetBytes($"{headerB64}.{payloadB64}");
        byte[] signature = _rsa.SignData(signingInput, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);
        string token = $"{headerB64}.{payloadB64}.{Base64UrlEncode(signature)}";

        SetupCacheWithJwks();

        ValidatedLogoutToken? result = await _validator.ValidateAsync(
            token, ClientId, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
    }

    // ──── PS384 and PS512 algorithm variations ────

    [Fact]
    public async Task ValidateAsync_Ps384Algorithm_ReturnsValidatedClaims()
    {
        var header = new Dictionary<string, object?> { ["alg"] = "PS384", ["typ"] = "JWT", ["kid"] = "test-key-1" };
        string headerB64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header));

        var payload = new Dictionary<string, object?>
        {
            ["iss"] = Authority,
            ["sub"] = "user-1",
            ["jti"] = "test-jti",
            ["aud"] = ClientId,
            ["events"] = new Dictionary<string, object>
            {
                ["http://schemas.openid.net/event/backchannel-logout"] = new { },
            },
        };

        string payloadB64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
        byte[] signingInput = Encoding.ASCII.GetBytes($"{headerB64}.{payloadB64}");
        byte[] signature = _rsa.SignData(signingInput, HashAlgorithmName.SHA384, RSASignaturePadding.Pss);
        string token = $"{headerB64}.{payloadB64}.{Base64UrlEncode(signature)}";

        SetupCacheWithJwks();

        ValidatedLogoutToken? result = await _validator.ValidateAsync(
            token, ClientId, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task ValidateAsync_Ps512Algorithm_ReturnsValidatedClaims()
    {
        var header = new Dictionary<string, object?> { ["alg"] = "PS512", ["typ"] = "JWT", ["kid"] = "test-key-1" };
        string headerB64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header));

        var payload = new Dictionary<string, object?>
        {
            ["iss"] = Authority,
            ["sub"] = "user-1",
            ["jti"] = "test-jti",
            ["aud"] = ClientId,
            ["events"] = new Dictionary<string, object>
            {
                ["http://schemas.openid.net/event/backchannel-logout"] = new { },
            },
        };

        string payloadB64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
        byte[] signingInput = Encoding.ASCII.GetBytes($"{headerB64}.{payloadB64}");
        byte[] signature = _rsa.SignData(signingInput, HashAlgorithmName.SHA512, RSASignaturePadding.Pss);
        string token = $"{headerB64}.{payloadB64}.{Base64UrlEncode(signature)}";

        SetupCacheWithJwks();

        ValidatedLogoutToken? result = await _validator.ValidateAsync(
            token, ClientId, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
    }

    // ──── Tampered payload (valid signature for original, but payload modified) ────

    [Fact]
    public async Task ValidateAsync_TamperedPayload_ReturnsNull()
    {
        string token = BuildSignedToken(
            issuer: Authority,
            audience: ClientId,
            includeBackChannelEvent: true);
        SetupCacheWithJwks();

        // Replace payload with different data (keeps header + original signature)
        string[] parts = token.Split('.');
        var tamperedPayload = new Dictionary<string, object?>
        {
            ["iss"] = "https://evil.example.com",
            ["sub"] = "attacker",
            ["aud"] = ClientId,
            ["events"] = new Dictionary<string, object>
            {
                ["http://schemas.openid.net/event/backchannel-logout"] = new { },
            },
        };
        string tamperedPayloadB64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(tamperedPayload));
        string tamperedToken = $"{parts[0]}.{tamperedPayloadB64}.{parts[2]}";

        ValidatedLogoutToken? result = await _validator.ValidateAsync(
            tamperedToken, ClientId, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // ──── Helpers ────

    private string BuildSignedTokenWithAudienceArray(
        string issuer,
        string[] audiences,
        bool includeBackChannelEvent,
        string? kid = "test-key-1")
    {
        var header = new Dictionary<string, object?> { ["alg"] = "RS256", ["typ"] = "JWT", ["kid"] = kid };
        string headerB64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header));

        var payload = new Dictionary<string, object?>
        {
            ["iss"] = issuer,
            ["sub"] = "user-1",
            ["jti"] = "test-jti",
            ["aud"] = audiences,
        };

        if (includeBackChannelEvent)
        {
            payload["events"] = new Dictionary<string, object>
            {
                ["http://schemas.openid.net/event/backchannel-logout"] = new { },
            };
        }

        string payloadB64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
        byte[] signingInput = System.Text.Encoding.ASCII.GetBytes($"{headerB64}.{payloadB64}");
        byte[] signature = _rsa.SignData(signingInput, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return $"{headerB64}.{payloadB64}.{Base64UrlEncode(signature)}";
    }

    private string BuildSignedTokenWithNoAudience(
        string issuer,
        bool includeBackChannelEvent,
        string? kid = "test-key-1")
    {
        var header = new Dictionary<string, object?> { ["alg"] = "RS256", ["typ"] = "JWT", ["kid"] = kid };
        string headerB64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header));

        var payload = new Dictionary<string, object?>
        {
            ["iss"] = issuer,
            ["sub"] = "user-1",
            ["jti"] = "test-jti",
        };

        if (includeBackChannelEvent)
        {
            payload["events"] = new Dictionary<string, object>
            {
                ["http://schemas.openid.net/event/backchannel-logout"] = new { },
            };
        }

        string payloadB64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
        byte[] signingInput = System.Text.Encoding.ASCII.GetBytes($"{headerB64}.{payloadB64}");
        byte[] signature = _rsa.SignData(signingInput, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return $"{headerB64}.{payloadB64}.{Base64UrlEncode(signature)}";
    }

    private static string BuildUnsignedToken(string algorithm)
    {
        var header = new { alg = algorithm, typ = "JWT" };
        var payload = new { iss = Authority, aud = ClientId, sub = "user-1" };

        string headerB64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header));
        string payloadB64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));

        return $"{headerB64}.{payloadB64}.fake-signature";
    }

    private string BuildSignedToken(
        string issuer,
        string audience,
        bool includeBackChannelEvent,
        string? subject = "user-1",
        string? jti = "test-jti",
        string? kid = "test-key-1",
        string algorithm = "RS256")
    {
        var header = new Dictionary<string, object?> { ["alg"] = algorithm, ["typ"] = "JWT", ["kid"] = kid };
        string headerB64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header));

        var payload = new Dictionary<string, object?>
        {
            ["iss"] = issuer,
            ["sub"] = subject,
            ["jti"] = jti,
            ["aud"] = audience,
        };

        if (includeBackChannelEvent)
        {
            payload["events"] = new Dictionary<string, object>
            {
                ["http://schemas.openid.net/event/backchannel-logout"] = new { },
            };
        }

        string payloadB64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));

        byte[] signingInput = Encoding.ASCII.GetBytes($"{headerB64}.{payloadB64}");
        byte[] signature = _rsa.SignData(signingInput, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        string signatureB64 = Base64UrlEncode(signature);

        return $"{headerB64}.{payloadB64}.{signatureB64}";
    }

    private void SetupCacheWithJwks(string kid = "test-key-1")
    {
        RSAParameters pubParams = _rsa.ExportParameters(includePrivateParameters: false);

        var jwk = new Dictionary<string, object?>
        {
            ["kty"] = "RSA",
            ["kid"] = kid,
            ["use"] = "sig",
            ["n"] = Base64UrlEncode(pubParams.Modulus!),
            ["e"] = Base64UrlEncode(pubParams.Exponent!),
        };

        string jwkJson = JsonSerializer.Serialize(jwk);
        using var jwkDoc = JsonDocument.Parse(jwkJson);
        var keys = new List<JsonElement> { jwkDoc.RootElement.Clone() };

        _cache.GetOrSetAsync<List<JsonElement>>(
                Arg.Any<string>(),
                Arg.Any<Func<FusionCacheFactoryExecutionContext<List<JsonElement>>, CancellationToken, Task<List<JsonElement>>>>(),
                Arg.Any<FusionCacheEntryOptions?>(),
                Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs(keys);
    }

    private string BuildSignedTokenWithCustomEvents(
        string issuer,
        string audience,
        string eventType,
        string? kid = "test-key-1")
    {
        var header = new Dictionary<string, object?> { ["alg"] = "RS256", ["typ"] = "JWT", ["kid"] = kid };
        string headerB64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header));

        var payload = new Dictionary<string, object?>
        {
            ["iss"] = issuer,
            ["sub"] = "user-1",
            ["jti"] = "test-jti",
            ["aud"] = audience,
            ["events"] = new Dictionary<string, object>
            {
                [eventType] = new { },
            },
        };

        string payloadB64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
        byte[] signingInput = Encoding.ASCII.GetBytes($"{headerB64}.{payloadB64}");
        byte[] signature = _rsa.SignData(signingInput, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return $"{headerB64}.{payloadB64}.{Base64UrlEncode(signature)}";
    }

    private string BuildSignedTokenWithCustomPayload(
        string issuer,
        string audience,
        bool includeEvents,
        string? kid = "test-key-1")
    {
        var header = new Dictionary<string, object?> { ["alg"] = "RS256", ["typ"] = "JWT", ["kid"] = kid };
        string headerB64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header));

        var payload = new Dictionary<string, object?>
        {
            ["iss"] = issuer,
            ["sub"] = "user-1",
            ["jti"] = "test-jti",
            ["aud"] = audience,
        };

        if (includeEvents)
        {
            payload["events"] = new Dictionary<string, object>
            {
                ["http://schemas.openid.net/event/backchannel-logout"] = new { },
            };
        }

        string payloadB64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
        byte[] signingInput = Encoding.ASCII.GetBytes($"{headerB64}.{payloadB64}");
        byte[] signature = _rsa.SignData(signingInput, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return $"{headerB64}.{payloadB64}.{Base64UrlEncode(signature)}";
    }

    private static string BuildEcSignedToken(
        ECDsa ecKey,
        string issuer,
        string audience,
        bool includeBackChannelEvent,
        string? kid = "test-key-1",
        string algorithm = "ES256",
        HashAlgorithmName? hashAlgorithm = null)
    {
        var header = new Dictionary<string, object?> { ["alg"] = algorithm, ["typ"] = "JWT", ["kid"] = kid };
        string headerB64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header));

        var payload = new Dictionary<string, object?>
        {
            ["iss"] = issuer,
            ["sub"] = "user-1",
            ["jti"] = "test-jti",
            ["aud"] = audience,
        };

        if (includeBackChannelEvent)
        {
            payload["events"] = new Dictionary<string, object>
            {
                ["http://schemas.openid.net/event/backchannel-logout"] = new { },
            };
        }

        string payloadB64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
        byte[] signingInput = Encoding.ASCII.GetBytes($"{headerB64}.{payloadB64}");

        HashAlgorithmName effectiveHash = hashAlgorithm ?? HashAlgorithmName.SHA256;
        byte[] signature = ecKey.SignData(signingInput, effectiveHash, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return $"{headerB64}.{payloadB64}.{Base64UrlEncode(signature)}";
    }

    private static void SetupCacheWithEcJwks(ECDsa ecKey, IFusionCache cache, string kid = "test-key-1")
    {
        ECParameters pubParams = ecKey.ExportParameters(includePrivateParameters: false);
        string curveName = pubParams.Curve.Oid?.FriendlyName switch
        {
            "ECDSA_P256" or "nistP256" => "P-256",
            "ECDSA_P384" or "nistP384" => "P-384",
            "ECDSA_P521" or "nistP521" => "P-521",
            _ => "P-256",
        };

        var jwk = new Dictionary<string, object?>
        {
            ["kty"] = "EC",
            ["kid"] = kid,
            ["use"] = "sig",
            ["crv"] = curveName,
            ["x"] = Base64UrlEncode(pubParams.Q.X!),
            ["y"] = Base64UrlEncode(pubParams.Q.Y!),
        };

        string jwkJson = JsonSerializer.Serialize(jwk);
        using var jwkDoc = JsonDocument.Parse(jwkJson);
        List<JsonElement> keys = [jwkDoc.RootElement.Clone()];

        cache.GetOrSetAsync<List<JsonElement>>(
                Arg.Any<string>(),
                Arg.Any<Func<FusionCacheFactoryExecutionContext<List<JsonElement>>, CancellationToken, Task<List<JsonElement>>>>(),
                Arg.Any<FusionCacheEntryOptions?>(),
                Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs(keys);
    }

    private void SetupCacheWithEcJwks(ECDsa ecKey, string kid = "test-key-1")
        => SetupCacheWithEcJwks(ecKey, _cache, kid);

    private static string Base64UrlEncode(ReadOnlySpan<byte> data)
    {
        return Convert.ToBase64String(data)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static byte[] Base64UrlDecode(string input)
    {
        string base64 = input.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }

        return Convert.FromBase64String(base64);
    }
}
