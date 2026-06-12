using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Granit.Auditing;
using Granit.Auditing.Domain;
using Granit.Authentication.ApiKeys.Domain;
using Granit.Authentication.ApiKeys.Internal;
using Granit.Authentication.ApiKeys.Options;
using Granit.Domain;
using Granit.Timing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.Tests;

public sealed class ApiKeyAuthenticationHandlerTests
{
    private readonly IApiKeyStore _store = Substitute.For<IApiKeyStore>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IApiKeyCacheService _cacheService = Substitute.For<IApiKeyCacheService>();
    private readonly ApiKeyOptions _options = new();
    private readonly IApiKeyHasher _hasher =
        new ApiKeyHasher(Microsoft.Extensions.Options.Options.Create(new ApiKeysOptions()));
    private readonly ApiKeyGenerator _generator = new(
        new ApiKeyHasher(Microsoft.Extensions.Options.Options.Create(new ApiKeysOptions())));

    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    public ApiKeyAuthenticationHandlerTests()
    {
        _clock.Now.Returns(Now);
    }

    private async Task<AuthenticateResult> AuthenticateAsync(
        string? authorizationHeader,
        IPAddress? remoteIp = null,
        IApiKeyCacheService? cacheService = null,
        IAuditingWriter? auditingWriter = null)
    {
        IOptionsMonitor<ApiKeyOptions> optionsMonitor = Substitute.For<IOptionsMonitor<ApiKeyOptions>>();
        optionsMonitor.Get(ApiKeyAuthenticationDefaults.AuthenticationScheme).Returns(_options);
        optionsMonitor.CurrentValue.Returns(_options);

        NullLoggerFactory loggerFactory = NullLoggerFactory.Instance;

        var handler = new ApiKeyAuthenticationHandler(
            optionsMonitor,
            loggerFactory,
            UrlEncoder.Default,
            _store,
            _hasher,
            _clock,
            cacheService);

        var context = new DefaultHttpContext();

        if (auditingWriter is not null)
        {
            IServiceProvider services = Substitute.For<IServiceProvider>();
            services.GetService(typeof(IAuditingWriter)).Returns(auditingWriter);
            context.RequestServices = services;
        }

        if (authorizationHeader is not null)
        {
            context.Request.Headers.Authorization = authorizationHeader;
        }

        context.Connection.RemoteIpAddress = remoteIp ?? IPAddress.Parse("127.0.0.1");

        var scheme = new AuthenticationScheme(
            ApiKeyAuthenticationDefaults.AuthenticationScheme,
            displayName: null,
            typeof(ApiKeyAuthenticationHandler));

        await handler.InitializeAsync(scheme, context);

        return await handler.AuthenticateAsync();
    }

    private static ApiKeyEntry CreateActiveApiKey(ApiKeyGenerationResult result)
    {
        var entry = ApiKeyEntry.Create(
            Guid.NewGuid(),
            "Test Key",
            ApiKeyType.Secret,
            "live",
            result.HashedKey,
            result.Prefix,
            result.LastFourChars);
        entry.UpdatePermissions(["MyApp.Patients.Read", "MyApp.Patients.Write"]);
        return entry;
    }

    // --- No result scenarios ---

    [Fact]
    public async Task HandleAuthenticate_NoAuthorizationHeader_ReturnsNoResult()
    {
        AuthenticateResult result = await AuthenticateAsync(null);

        result.None.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAuthenticate_EmptyAuthorizationHeader_ReturnsNoResult()
    {
        AuthenticateResult result = await AuthenticateAsync("");

        result.None.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAuthenticate_NonApiKeyBearerToken_ReturnsNoResult()
    {
        AuthenticateResult result = await AuthenticateAsync("Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...");

        result.None.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAuthenticate_NonApiKeyRawToken_ReturnsNoResult()
    {
        AuthenticateResult result = await AuthenticateAsync("some-random-token");

        result.None.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAuthenticate_BearerPrefixWithoutGkPrefix_ReturnsNoResult()
    {
        AuthenticateResult result = await AuthenticateAsync("Bearer not_a_gk_key");

        result.None.ShouldBeTrue();
    }

    // --- Key not found ---

    [Fact]
    public async Task HandleAuthenticate_KeyNotFoundInStore_ReturnsFail()
    {
        ApiKeyGenerationResult gen = _generator.Generate(ApiKeyType.Secret, "live");

        _store.FindByHashAsync(gen.HashedKey, Arg.Any<CancellationToken>())
            .Returns((ApiKeyEntry?)null);

        AuthenticateResult result = await AuthenticateAsync($"Bearer {gen.RawSecret}");

        result.Succeeded.ShouldBeFalse();
        result.Failure!.Message.ShouldBe("Invalid API key.");
    }

    [Fact]
    public async Task HandleAuthenticate_Failure_WritesAccessDeniedAudit()
    {
        ApiKeyGenerationResult gen = _generator.Generate(ApiKeyType.Secret, "live");
        _store.FindByHashAsync(gen.HashedKey, Arg.Any<CancellationToken>())
            .Returns((ApiKeyEntry?)null);
        IAuditingWriter auditingWriter = Substitute.For<IAuditingWriter>();

        AuthenticateResult result = await AuthenticateAsync(
            $"Bearer {gen.RawSecret}", auditingWriter: auditingWriter);

        result.Succeeded.ShouldBeFalse();
        await auditingWriter.Received(1).WriteAsync(
            Arg.Is<AuditEntry>(e => e.Category == AuditCategory.AccessDenied),
            Arg.Any<CancellationToken>());
    }

    // --- Revoked key ---

    [Fact]
    public async Task HandleAuthenticate_RevokedKey_ReturnsFail()
    {
        ApiKeyGenerationResult gen = _generator.Generate(ApiKeyType.Secret, "live");
        ApiKeyEntry apiKey = CreateActiveApiKey(gen);
        apiKey.Revoke(Now.AddDays(-1));

        _store.FindByHashAsync(gen.HashedKey, Arg.Any<CancellationToken>())
            .Returns(apiKey);

        AuthenticateResult result = await AuthenticateAsync($"Bearer {gen.RawSecret}");

        result.Succeeded.ShouldBeFalse();
        result.Failure!.Message.ShouldBe("API key has been revoked.");
    }

    // --- Expired key ---

    [Fact]
    public async Task HandleAuthenticate_ExpiredKey_ReturnsFail()
    {
        ApiKeyGenerationResult gen = _generator.Generate(ApiKeyType.Secret, "live");
        ApiKeyEntry apiKey = CreateActiveApiKey(gen);
        apiKey.SetExpiration(Now.AddMinutes(-1));

        _store.FindByHashAsync(gen.HashedKey, Arg.Any<CancellationToken>())
            .Returns(apiKey);

        AuthenticateResult result = await AuthenticateAsync($"Bearer {gen.RawSecret}");

        result.Succeeded.ShouldBeFalse();
        result.Failure!.Message.ShouldBe("API key has expired.");
    }

    [Fact]
    public async Task HandleAuthenticate_ExpiresAtExactlyNow_ReturnsFail()
    {
        ApiKeyGenerationResult gen = _generator.Generate(ApiKeyType.Secret, "live");
        ApiKeyEntry apiKey = CreateActiveApiKey(gen);
        apiKey.SetExpiration(Now); // Exact boundary: <= now means expired

        _store.FindByHashAsync(gen.HashedKey, Arg.Any<CancellationToken>())
            .Returns(apiKey);

        AuthenticateResult result = await AuthenticateAsync($"Bearer {gen.RawSecret}");

        result.Succeeded.ShouldBeFalse();
        result.Failure!.Message.ShouldBe("API key has expired.");
    }

    [Fact]
    public async Task HandleAuthenticate_KeyNotYetExpired_Succeeds()
    {
        ApiKeyGenerationResult gen = _generator.Generate(ApiKeyType.Secret, "live");
        ApiKeyEntry apiKey = CreateActiveApiKey(gen);
        apiKey.SetExpiration(Now.AddHours(1));

        _store.FindByHashAsync(gen.HashedKey, Arg.Any<CancellationToken>())
            .Returns(apiKey);

        AuthenticateResult result = await AuthenticateAsync($"Bearer {gen.RawSecret}");

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAuthenticate_NoExpiration_Succeeds()
    {
        ApiKeyGenerationResult gen = _generator.Generate(ApiKeyType.Secret, "live");
        ApiKeyEntry apiKey = CreateActiveApiKey(gen);
        // ExpiresAt is null by default from factory — no need to set it

        _store.FindByHashAsync(gen.HashedKey, Arg.Any<CancellationToken>())
            .Returns(apiKey);

        AuthenticateResult result = await AuthenticateAsync($"Bearer {gen.RawSecret}");

        result.Succeeded.ShouldBeTrue();
    }

    // --- CIDR validation ---

    [Fact]
    public async Task HandleAuthenticate_IpNotInAllowedCidr_ReturnsFail()
    {
        ApiKeyGenerationResult gen = _generator.Generate(ApiKeyType.Secret, "live");
        ApiKeyEntry apiKey = CreateActiveApiKey(gen);
        apiKey.UpdateAllowedCidrs(["10.0.0.0/8"]);

        _store.FindByHashAsync(gen.HashedKey, Arg.Any<CancellationToken>())
            .Returns(apiKey);

        AuthenticateResult result = await AuthenticateAsync(
            $"Bearer {gen.RawSecret}",
            remoteIp: IPAddress.Parse("192.168.1.1"));

        result.Succeeded.ShouldBeFalse();
        result.Failure!.Message.ShouldBe("IP address not in allowed CIDR ranges.");
    }

    [Fact]
    public async Task HandleAuthenticate_IpInAllowedCidr_Succeeds()
    {
        ApiKeyGenerationResult gen = _generator.Generate(ApiKeyType.Secret, "live");
        ApiKeyEntry apiKey = CreateActiveApiKey(gen);
        apiKey.UpdateAllowedCidrs(["10.0.0.0/8"]);

        _store.FindByHashAsync(gen.HashedKey, Arg.Any<CancellationToken>())
            .Returns(apiKey);

        AuthenticateResult result = await AuthenticateAsync(
            $"Bearer {gen.RawSecret}",
            remoteIp: IPAddress.Parse("10.1.2.3"));

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAuthenticate_EmptyAllowedCidrs_AllowsAnyIp()
    {
        ApiKeyGenerationResult gen = _generator.Generate(ApiKeyType.Secret, "live");
        ApiKeyEntry apiKey = CreateActiveApiKey(gen);
        // AllowedCidrs is empty by default from factory — no restriction

        _store.FindByHashAsync(gen.HashedKey, Arg.Any<CancellationToken>())
            .Returns(apiKey);

        AuthenticateResult result = await AuthenticateAsync(
            $"Bearer {gen.RawSecret}",
            remoteIp: IPAddress.Parse("1.2.3.4"));

        result.Succeeded.ShouldBeTrue();
    }

    // --- Successful authentication: claims ---

    [Fact]
    public async Task HandleAuthenticate_ValidKey_ReturnsSuccessWithCorrectClaims()
    {
        ApiKeyGenerationResult gen = _generator.Generate(ApiKeyType.Secret, "live");
        ApiKeyEntry apiKey = CreateActiveApiKey(gen);

        _store.FindByHashAsync(gen.HashedKey, Arg.Any<CancellationToken>())
            .Returns(apiKey);

        AuthenticateResult result = await AuthenticateAsync($"Bearer {gen.RawSecret}");

        result.Succeeded.ShouldBeTrue();
        result.Ticket.ShouldNotBeNull();
        result.Ticket.AuthenticationScheme.ShouldBe(ApiKeyAuthenticationDefaults.AuthenticationScheme);

        ClaimsPrincipal principal = result.Principal!;
        principal.Identity!.IsAuthenticated.ShouldBeTrue();

        // NameIdentifier and Name
        principal.FindFirstValue(ClaimTypes.NameIdentifier).ShouldBe(apiKey.Id.ToString());
        principal.FindFirstValue(ClaimTypes.Name).ShouldBe("Test Key");

        // ApiKey-specific claims
        principal.FindFirstValue(ApiKeyClaimTypes.ActorKind).ShouldBe(nameof(Users.ActorKind.ExternalSystem));
        principal.FindFirstValue(ApiKeyClaimTypes.ApiKeyId).ShouldBe(apiKey.Id.ToString());
        principal.FindFirstValue(ApiKeyClaimTypes.ApiKeyType).ShouldBe(ApiKeyType.Secret.ToString());
        principal.FindFirstValue(ApiKeyClaimTypes.Environment).ShouldBe("live");

        // Permissions
        var permissions = principal.FindAll(ApiKeyClaimTypes.Permission).Select(c => c.Value).ToList();
        permissions.ShouldContain("MyApp.Patients.Read");
        permissions.ShouldContain("MyApp.Patients.Write");
        permissions.Count.ShouldBe(2);
    }

    [Fact]
    public async Task HandleAuthenticate_KeyWithTenantId_IncludesTenantClaim()
    {
        ApiKeyGenerationResult gen = _generator.Generate(ApiKeyType.Secret, "live");
        var tenantId = Guid.NewGuid();
        ApiKeyEntry apiKey = CreateActiveApiKey(gen);
        ((IMultiTenant)apiKey).TenantId = tenantId;

        _store.FindByHashAsync(gen.HashedKey, Arg.Any<CancellationToken>())
            .Returns(apiKey);

        AuthenticateResult result = await AuthenticateAsync($"Bearer {gen.RawSecret}");

        result.Succeeded.ShouldBeTrue();
        result.Principal!.FindFirstValue(ApiKeyClaimTypes.TenantId).ShouldBe(tenantId.ToString());
    }

    [Fact]
    public async Task HandleAuthenticate_KeyWithoutTenantId_NoTenantClaim()
    {
        ApiKeyGenerationResult gen = _generator.Generate(ApiKeyType.Secret, "live");
        ApiKeyEntry apiKey = CreateActiveApiKey(gen);
        // TenantId is null by default from factory — no need to set it

        _store.FindByHashAsync(gen.HashedKey, Arg.Any<CancellationToken>())
            .Returns(apiKey);

        AuthenticateResult result = await AuthenticateAsync($"Bearer {gen.RawSecret}");

        result.Succeeded.ShouldBeTrue();
        result.Principal!.FindFirst(ApiKeyClaimTypes.TenantId).ShouldBeNull();
    }

    [Fact]
    public async Task HandleAuthenticate_KeyWithNoPermissions_NoClaims()
    {
        ApiKeyGenerationResult gen = _generator.Generate(ApiKeyType.Secret, "live");
        ApiKeyEntry apiKey = CreateActiveApiKey(gen);
        apiKey.UpdatePermissions([]);

        _store.FindByHashAsync(gen.HashedKey, Arg.Any<CancellationToken>())
            .Returns(apiKey);

        AuthenticateResult result = await AuthenticateAsync($"Bearer {gen.RawSecret}");

        result.Succeeded.ShouldBeTrue();
        result.Principal!.FindAll(ApiKeyClaimTypes.Permission).ShouldBeEmpty();
    }

    // --- Raw gk_ token (without Bearer prefix) ---

    [Fact]
    public async Task HandleAuthenticate_RawGkToken_Succeeds()
    {
        ApiKeyGenerationResult gen = _generator.Generate(ApiKeyType.Secret, "live");
        ApiKeyEntry apiKey = CreateActiveApiKey(gen);

        _store.FindByHashAsync(gen.HashedKey, Arg.Any<CancellationToken>())
            .Returns(apiKey);

        // Send raw gk_ token without "Bearer " prefix
        AuthenticateResult result = await AuthenticateAsync(gen.RawSecret);

        result.Succeeded.ShouldBeTrue();
    }

    // --- TrackLastUsed ---

    [Fact]
    public async Task HandleAuthenticate_TrackLastUsedEnabled_UpdatesLastUsed()
    {
        _options.TrackLastUsed = true;

        ApiKeyGenerationResult gen = _generator.Generate(ApiKeyType.Secret, "live");
        ApiKeyEntry apiKey = CreateActiveApiKey(gen);

        _store.FindByHashAsync(gen.HashedKey, Arg.Any<CancellationToken>())
            .Returns(apiKey);

        await AuthenticateAsync($"Bearer {gen.RawSecret}");

        await _store.Received(1).UpdateLastUsedAsync(apiKey.Id, Now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAuthenticate_TrackLastUsedDisabled_DoesNotUpdateLastUsed()
    {
        _options.TrackLastUsed = false;

        ApiKeyGenerationResult gen = _generator.Generate(ApiKeyType.Secret, "live");
        ApiKeyEntry apiKey = CreateActiveApiKey(gen);

        _store.FindByHashAsync(gen.HashedKey, Arg.Any<CancellationToken>())
            .Returns(apiKey);

        await AuthenticateAsync($"Bearer {gen.RawSecret}");

        await _store.DidNotReceive().UpdateLastUsedAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    // --- Cache service ---

    [Fact]
    public async Task HandleAuthenticate_WithCacheService_UsesCacheForLookup()
    {
        ApiKeyGenerationResult gen = _generator.Generate(ApiKeyType.Secret, "live");
        ApiKeyEntry apiKey = CreateActiveApiKey(gen);

        _cacheService.GetOrLoadAsync(
                gen.HashedKey,
                Arg.Any<Func<CancellationToken, Task<ApiKeyEntry?>>>(),
                _options.CacheDuration,
                Arg.Any<CancellationToken>())
            .Returns(apiKey);

        AuthenticateResult result = await AuthenticateAsync(
            $"Bearer {gen.RawSecret}",
            cacheService: _cacheService);

        result.Succeeded.ShouldBeTrue();

        // Cache service was called
        await _cacheService.Received(1).GetOrLoadAsync(
            gen.HashedKey,
            Arg.Any<Func<CancellationToken, Task<ApiKeyEntry?>>>(),
            _options.CacheDuration,
            Arg.Any<CancellationToken>());

        // Direct store was NOT called
        await _store.DidNotReceive().FindByHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAuthenticate_WithoutCacheService_UsesStoreDirect()
    {
        ApiKeyGenerationResult gen = _generator.Generate(ApiKeyType.Secret, "live");
        ApiKeyEntry apiKey = CreateActiveApiKey(gen);

        _store.FindByHashAsync(gen.HashedKey, Arg.Any<CancellationToken>())
            .Returns(apiKey);

        AuthenticateResult result = await AuthenticateAsync(
            $"Bearer {gen.RawSecret}",
            cacheService: null);

        result.Succeeded.ShouldBeTrue();
        await _store.Received(1).FindByHashAsync(gen.HashedKey, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAuthenticate_CacheReturnsNull_ReturnsFail()
    {
        ApiKeyGenerationResult gen = _generator.Generate(ApiKeyType.Secret, "live");

        _cacheService.GetOrLoadAsync(
                Arg.Any<string>(),
                Arg.Any<Func<CancellationToken, Task<ApiKeyEntry?>>>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns((ApiKeyEntry?)null);

        AuthenticateResult result = await AuthenticateAsync(
            $"Bearer {gen.RawSecret}",
            cacheService: _cacheService);

        result.Succeeded.ShouldBeFalse();
        result.Failure!.Message.ShouldBe("Invalid API key.");
    }

    // --- Validation order: revoked checked before expired ---

    [Fact]
    public async Task HandleAuthenticate_RevokedAndExpired_ReturnsRevokedError()
    {
        ApiKeyGenerationResult gen = _generator.Generate(ApiKeyType.Secret, "live");
        ApiKeyEntry apiKey = CreateActiveApiKey(gen);
        apiKey.Revoke(Now.AddDays(-2));
        apiKey.SetExpiration(Now.AddDays(-1));

        _store.FindByHashAsync(gen.HashedKey, Arg.Any<CancellationToken>())
            .Returns(apiKey);

        AuthenticateResult result = await AuthenticateAsync($"Bearer {gen.RawSecret}");

        result.Succeeded.ShouldBeFalse();
        result.Failure!.Message.ShouldBe("API key has been revoked.");
    }

    // --- Different key types ---

    [Theory]
    [InlineData(ApiKeyType.Secret)]
    [InlineData(ApiKeyType.Publishable)]
    [InlineData(ApiKeyType.Webhook)]
    [InlineData(ApiKeyType.Ephemeral)]
    public async Task HandleAuthenticate_AllKeyTypes_Succeed(ApiKeyType keyType)
    {
        ApiKeyGenerationResult gen = _generator.Generate(keyType, "live");
        var apiKey = ApiKeyEntry.Create(
            Guid.NewGuid(),
            "Test Key",
            keyType,
            "live",
            gen.HashedKey,
            gen.Prefix,
            gen.LastFourChars);
        apiKey.UpdatePermissions(["MyApp.Patients.Read", "MyApp.Patients.Write"]);

        _store.FindByHashAsync(gen.HashedKey, Arg.Any<CancellationToken>())
            .Returns(apiKey);

        AuthenticateResult result = await AuthenticateAsync($"Bearer {gen.RawSecret}");

        result.Succeeded.ShouldBeTrue();
        result.Principal!.FindFirstValue(ApiKeyClaimTypes.ApiKeyType).ShouldBe(keyType.ToString());
    }
}
