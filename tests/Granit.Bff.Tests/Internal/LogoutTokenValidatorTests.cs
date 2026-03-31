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
        string token = "!!!invalid-base64!!!.payload.signature";

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
