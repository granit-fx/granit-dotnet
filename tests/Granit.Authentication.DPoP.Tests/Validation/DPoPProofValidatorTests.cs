// =============================================================================
// Tests — DPoPProofValidator (RFC 9449 §4.3 / §7)
// =============================================================================
// Validates the full DPoP proof validation pipeline: structure, header claims,
// payload claims, timing, replay protection, and signature verification.
// =============================================================================

using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Granit.Authentication.DPoP.Diagnostics;
using Granit.Authentication.DPoP.Options;
using Granit.Authentication.DPoP.Validation;
using Granit.Authentication.DPoP.Validation.Internal;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Authentication.DPoP.Tests.Validation;

public sealed class DPoPProofValidatorTests : IDisposable
{
    private const string DefaultMethod = "GET";
    private const string DefaultUri = "https://api.example.com/resource";

    private readonly IClock _clock;
    private readonly IFusionCache _cache;
    private readonly DPoPValidationOptions _options;
    private readonly DPoPProofValidator _validator;
    private readonly ServiceProvider _serviceProvider;

    public DPoPProofValidatorTests()
    {
        _clock = Substitute.For<IClock>();
        _clock.Now.Returns(DateTimeOffset.UtcNow);

        _cache = Substitute.For<IFusionCache>();

        _options = new DPoPValidationOptions
        {
            EnableReplayProtection = false,
            RequireNonce = false,
        };

        ServiceCollection services = new();
        services.AddMetrics();
        _serviceProvider = services.BuildServiceProvider();

        IOptions<DPoPValidationOptions> optionsWrapper = Microsoft.Extensions.Options.Options.Create(_options);
        IMeterFactory meterFactory = _serviceProvider.GetRequiredService<IMeterFactory>();
        DPoPValidationMetrics metrics = new(meterFactory);

        _validator = new DPoPProofValidator(optionsWrapper, _clock, metrics, _cache);
    }

    public void Dispose() => _serviceProvider.Dispose();

    // ── Structure validation ──

    [Theory]
    [InlineData("single-part")]
    [InlineData("two.parts")]
    [InlineData("four.parts.here.extra")]
    public async Task ValidateAsync_InvalidJwtStructure_ReturnsFailure(string malformedJwt)
    {
        DPoPValidationResult result = await _validator.ValidateAsync(
            malformedJwt, DefaultMethod, DefaultUri, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldBe("Invalid JWT structure.");
    }

    // ── Header validation ──

    [Fact]
    public async Task ValidateAsync_InvalidHeaderEncoding_ReturnsFailure()
    {
        string jwt = "!!!invalid-base64!!!.eyJ0ZXN0IjoxfQ.signature";

        DPoPValidationResult result = await _validator.ValidateAsync(
            jwt, DefaultMethod, DefaultUri, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldBe("Invalid JWT header encoding.");
    }

    [Fact]
    public async Task ValidateAsync_MissingTypClaim_ReturnsFailure()
    {
        string header = Base64UrlEncode("""{"alg":"ES256","jwk":{"kty":"EC"}}""");
        string jwt = $"{header}.eyJ0ZXN0IjoxfQ.signature";

        DPoPValidationResult result = await _validator.ValidateAsync(
            jwt, DefaultMethod, DefaultUri, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Error!.ShouldContain("Missing or invalid typ claim");
    }

    [Fact]
    public async Task ValidateAsync_WrongTypClaim_ReturnsFailure()
    {
        string header = Base64UrlEncode("""{"typ":"JWT","alg":"ES256","jwk":{"kty":"EC"}}""");
        string jwt = $"{header}.eyJ0ZXN0IjoxfQ.signature";

        DPoPValidationResult result = await _validator.ValidateAsync(
            jwt, DefaultMethod, DefaultUri, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Error!.ShouldContain("Missing or invalid typ claim");
    }

    [Fact]
    public async Task ValidateAsync_MissingAlgClaim_ReturnsFailure()
    {
        string header = Base64UrlEncode("""{"typ":"dpop+jwt","jwk":{"kty":"EC"}}""");
        string jwt = $"{header}.eyJ0ZXN0IjoxfQ.signature";

        DPoPValidationResult result = await _validator.ValidateAsync(
            jwt, DefaultMethod, DefaultUri, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldBe("Missing alg claim.");
    }

    [Fact]
    public async Task ValidateAsync_DisallowedAlgorithm_ReturnsFailure()
    {
        string header = Base64UrlEncode("""{"typ":"dpop+jwt","alg":"RS256","jwk":{"kty":"RSA"}}""");
        string jwt = $"{header}.eyJ0ZXN0IjoxfQ.signature";

        DPoPValidationResult result = await _validator.ValidateAsync(
            jwt, DefaultMethod, DefaultUri, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldBe("Algorithm 'RS256' is not allowed.");
    }

    [Fact]
    public async Task ValidateAsync_MissingJwkClaim_ReturnsFailure()
    {
        string header = Base64UrlEncode("""{"typ":"dpop+jwt","alg":"ES256"}""");
        string jwt = $"{header}.eyJ0ZXN0IjoxfQ.signature";

        DPoPValidationResult result = await _validator.ValidateAsync(
            jwt, DefaultMethod, DefaultUri, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldBe("Missing jwk claim in header.");
    }

    [Fact]
    public async Task ValidateAsync_PrivateKeyInJwk_ReturnsFailure()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        ECParameters ecParams = key.ExportParameters(includePrivateParameters: true);
        string x = Base64UrlEncodeBytes(ecParams.Q.X!);
        string y = Base64UrlEncodeBytes(ecParams.Q.Y!);
        string d = Base64UrlEncodeBytes(ecParams.D!);

        string jwkJson = $$$"""{"typ":"dpop+jwt","alg":"ES256","jwk":{"kty":"EC","crv":"P-256","x":"{{{x}}}","y":"{{{y}}}","d":"{{{d}}}"}}""";
        string header = Base64UrlEncode(jwkJson);
        string jwt = $"{header}.eyJ0ZXN0IjoxfQ.signature";

        DPoPValidationResult result = await _validator.ValidateAsync(
            jwt, DefaultMethod, DefaultUri, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Error!.ShouldContain("JWK must not contain private key parameter");
    }

    // ── Payload validation ──

    [Fact]
    public async Task ValidateAsync_HtmMismatch_ReturnsFailure()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        DateTimeOffset now = _clock.Now;

        // Create proof with POST but validate against GET
        string proof = CreateDPoPProof(key, "POST", DefaultUri, now);

        DPoPValidationResult result = await _validator.ValidateAsync(
            proof, "GET", DefaultUri, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldBe("htm claim does not match request method.");
    }

    [Fact]
    public async Task ValidateAsync_HtuMismatch_ReturnsFailure()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        DateTimeOffset now = _clock.Now;

        string proof = CreateDPoPProof(key, DefaultMethod, "https://api.example.com/other", now);

        DPoPValidationResult result = await _validator.ValidateAsync(
            proof, DefaultMethod, DefaultUri, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldBe("htu claim does not match request URI.");
    }

    [Fact]
    public async Task ValidateAsync_MissingIat_ReturnsFailure()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        DateTimeOffset now = _clock.Now;

        string proof = CreateDPoPProof(key, DefaultMethod, DefaultUri, now,
            modifyPayload: payload => payload.Remove("iat"));

        DPoPValidationResult result = await _validator.ValidateAsync(
            proof, DefaultMethod, DefaultUri, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldBe("Missing iat claim.");
    }

    [Fact]
    public async Task ValidateAsync_IatTooOld_ReturnsFailure()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        DateTimeOffset now = _clock.Now;

        // iat is 10 minutes ago — exceeds default MaxProofLifetime of 5 minutes
        DateTimeOffset oldIat = now.AddMinutes(-10);
        string proof = CreateDPoPProof(key, DefaultMethod, DefaultUri, oldIat);

        DPoPValidationResult result = await _validator.ValidateAsync(
            proof, DefaultMethod, DefaultUri, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldBe("Proof is too old (iat).");
    }

    [Fact]
    public async Task ValidateAsync_IatInFuture_ReturnsFailure()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        DateTimeOffset now = _clock.Now;

        // iat is 2 minutes in the future — exceeds default ClockSkew of 30 seconds
        DateTimeOffset futureIat = now.AddMinutes(2);
        string proof = CreateDPoPProof(key, DefaultMethod, DefaultUri, futureIat);

        DPoPValidationResult result = await _validator.ValidateAsync(
            proof, DefaultMethod, DefaultUri, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldBe("Proof iat is in the future.");
    }

    [Fact]
    public async Task ValidateAsync_ExpiredProof_ReturnsFailure()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        DateTimeOffset now = _clock.Now;

        // exp was 2 minutes ago (well beyond ClockSkew)
        long expiredExp = now.AddMinutes(-2).ToUnixTimeSeconds();
        string proof = CreateDPoPProof(key, DefaultMethod, DefaultUri, now,
            modifyPayload: payload => payload["exp"] = expiredExp);

        DPoPValidationResult result = await _validator.ValidateAsync(
            proof, DefaultMethod, DefaultUri, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldBe("Proof has expired.");
    }

    // ── Signature validation ──

    [Fact]
    public async Task ValidateAsync_ValidEcProof_ReturnsSuccess()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        DateTimeOffset now = _clock.Now;

        string proof = CreateDPoPProof(key, DefaultMethod, DefaultUri, now);

        DPoPValidationResult result = await _validator.ValidateAsync(
            proof, DefaultMethod, DefaultUri, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
        result.Error.ShouldBeNull();
    }

    [Fact]
    public async Task ValidateAsync_ValidProof_ReturnsThumbprint()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        DateTimeOffset now = _clock.Now;

        string proof = CreateDPoPProof(key, DefaultMethod, DefaultUri, now);

        DPoPValidationResult result = await _validator.ValidateAsync(
            proof, DefaultMethod, DefaultUri, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
        result.JwkThumbprint.ShouldNotBeNullOrEmpty();
        result.JwkThumbprint!.Length.ShouldBe(43, "SHA-256 base64url is always 43 chars");
    }

    [Fact]
    public async Task ValidateAsync_InvalidSignature_ReturnsFailure()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var differentKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        DateTimeOffset now = _clock.Now;

        // Build header with differentKey's public key but sign with signingKey
        ECParameters differentParams = differentKey.ExportParameters(includePrivateParameters: false);
        string x = Base64UrlEncodeBytes(differentParams.Q.X!);
        string y = Base64UrlEncodeBytes(differentParams.Q.Y!);

        var headerClaims = new Dictionary<string, object>
        {
            ["typ"] = "dpop+jwt",
            ["alg"] = "ES256",
            ["jwk"] = new Dictionary<string, object>
            {
                ["kty"] = "EC",
                ["crv"] = "P-256",
                ["x"] = x,
                ["y"] = y,
            },
        };

        long iat = now.ToUnixTimeSeconds();
        var payloadClaims = new Dictionary<string, object>
        {
            ["htm"] = DefaultMethod,
            ["htu"] = DefaultUri,
            ["iat"] = iat,
            ["jti"] = Guid.NewGuid().ToString(),
        };

        string headerJson = JsonSerializer.Serialize(headerClaims);
        string payloadJson = JsonSerializer.Serialize(payloadClaims);
        string headerB64 = Base64UrlEncode(headerJson);
        string payloadB64 = Base64UrlEncode(payloadJson);

        // Sign with the WRONG key (signingKey, not differentKey)
        byte[] signingInput = Encoding.ASCII.GetBytes($"{headerB64}.{payloadB64}");
        byte[] signature = signingKey.SignData(signingInput, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        string signatureB64 = Base64UrlEncodeBytes(signature);

        string proof = $"{headerB64}.{payloadB64}.{signatureB64}";

        DPoPValidationResult result = await _validator.ValidateAsync(
            proof, DefaultMethod, DefaultUri, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldBe("Invalid proof signature.");
    }

    // ── Replay protection ──

    [Fact]
    public async Task ValidateAsync_ReplayProtection_MissingJti_ReturnsFailure()
    {
        _options.EnableReplayProtection = true;

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        DateTimeOffset now = _clock.Now;

        string proof = CreateDPoPProof(key, DefaultMethod, DefaultUri, now,
            modifyPayload: payload => payload.Remove("jti"));

        DPoPValidationResult result = await _validator.ValidateAsync(
            proof, DefaultMethod, DefaultUri, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Error!.ShouldContain("Missing jti claim");
    }

    [Fact]
    public async Task ValidateAsync_ReplayProtection_DuplicateJti_ReturnsFailure()
    {
        _options.EnableReplayProtection = true;
        string jti = Guid.NewGuid().ToString();
        string cacheKey = $"dpop:jti:{jti}";

        // Simulate that this jti already exists in the cache
        _cache.TryGetAsync<bool>(cacheKey, Arg.Any<FusionCacheEntryOptions?>(), Arg.Any<CancellationToken>())
            .Returns(MaybeValue<bool>.FromValue(true));

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        DateTimeOffset now = _clock.Now;

        string proof = CreateDPoPProof(key, DefaultMethod, DefaultUri, now, jti: jti);

        DPoPValidationResult result = await _validator.ValidateAsync(
            proof, DefaultMethod, DefaultUri, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Error!.ShouldContain("Proof replay detected");
    }

    // ── Nonce generation ──

    [Fact]
    public async Task GenerateNonceAsync_WhenNotRequired_ReturnsNull()
    {
        _options.RequireNonce = false;

        string? nonce = await _validator.GenerateNonceAsync(TestContext.Current.CancellationToken);

        nonce.ShouldBeNull();
    }

    // ── URI normalization (query/fragment stripped, trailing slash removed) ──

    [Fact]
    public async Task ValidateAsync_HtuWithQueryString_MatchesBaseUri()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        DateTimeOffset now = _clock.Now;

        // Proof uses URI without query, request URI has query — should still match
        string proof = CreateDPoPProof(key, DefaultMethod, DefaultUri, now);

        DPoPValidationResult result = await _validator.ValidateAsync(
            proof, DefaultMethod, $"{DefaultUri}?page=1&size=10", TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_HtuWithTrailingSlash_MatchesWithout()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        DateTimeOffset now = _clock.Now;

        // Proof uses URI with trailing slash
        string proof = CreateDPoPProof(key, DefaultMethod, $"{DefaultUri}/", now);

        DPoPValidationResult result = await _validator.ValidateAsync(
            proof, DefaultMethod, DefaultUri, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    // ── Helper: DPoP proof JWT builder ──

    private static string CreateDPoPProof(
        ECDsa key,
        string method,
        string uri,
        DateTimeOffset iat,
        string? jti = null,
        string? nonce = null,
        Action<Dictionary<string, object>>? modifyHeader = null,
        Action<Dictionary<string, object>>? modifyPayload = null)
    {
        ECParameters ecParams = key.ExportParameters(includePrivateParameters: false);
        string x = Base64UrlEncodeBytes(ecParams.Q.X!);
        string y = Base64UrlEncodeBytes(ecParams.Q.Y!);

        var headerClaims = new Dictionary<string, object>
        {
            ["typ"] = "dpop+jwt",
            ["alg"] = "ES256",
            ["jwk"] = new Dictionary<string, object>
            {
                ["kty"] = "EC",
                ["crv"] = "P-256",
                ["x"] = x,
                ["y"] = y,
            },
        };

        var payloadClaims = new Dictionary<string, object>
        {
            ["htm"] = method,
            ["htu"] = uri,
            ["iat"] = iat.ToUnixTimeSeconds(),
            ["jti"] = jti ?? Guid.NewGuid().ToString(),
        };

        if (nonce is not null)
        {
            payloadClaims["nonce"] = nonce;
        }

        modifyHeader?.Invoke(headerClaims);
        modifyPayload?.Invoke(payloadClaims);

        string headerJson = JsonSerializer.Serialize(headerClaims);
        string payloadJson = JsonSerializer.Serialize(payloadClaims);
        string headerB64 = Base64UrlEncode(headerJson);
        string payloadB64 = Base64UrlEncode(payloadJson);

        byte[] signingInput = Encoding.ASCII.GetBytes($"{headerB64}.{payloadB64}");
        byte[] signature = key.SignData(signingInput, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        string signatureB64 = Base64UrlEncodeBytes(signature);

        return $"{headerB64}.{payloadB64}.{signatureB64}";
    }

    // ── Base64url helpers ──

    private static string Base64UrlEncode(string value) =>
        Base64UrlEncodeBytes(Encoding.UTF8.GetBytes(value));

    private static string Base64UrlEncodeBytes(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
