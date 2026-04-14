using System.Diagnostics.Metrics;
using System.Net;
using System.Text.Json;
using Granit.MultiTenancy;
using Granit.Tax.Builtin.Internal;
using Granit.Tax.Diagnostics;
using Granit.Tax.Options;
using Granit.Timing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Granit.Tax.Builtin.Tests.Internal;

public sealed class ViesValidatorTests : IDisposable
{
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();
    private readonly IFusionCache _cache = Substitute.For<IFusionCache>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly TestMeterFactory _meterFactory = new();
    private readonly TaxMetrics _metrics;
    private readonly TaxOptions _taxOptions = new() { AllowOfflineFallback = true, ValidationCacheTtlHours = 24 };

    private static readonly DateTimeOffset FixedNow = new(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);

    public ViesValidatorTests()
    {
        _metrics = new TaxMetrics(_meterFactory);
        _clock.Now.Returns(FixedNow);
    }

    public void Dispose() => _meterFactory.Dispose();

    private ViesValidator CreateSut() =>
        new(_httpClientFactory, _cache, MsOptions.Create(_taxOptions),
            _clock, _metrics, _currentTenant, NullLogger<ViesValidator>.Instance);

    /// <summary>
    /// Configures the FusionCache mock to execute the factory delegate, simulating a cache miss.
    /// The interface method has 6 params: (key, factory, failSafeDefault, options, tags, token).
    /// </summary>
    private void SetupCachePassthrough()
    {
#pragma warning disable CA2012 // Use ValueTasks correctly - NSubstitute mock setup requires calling GetOrSetAsync for matching
        _cache.GetOrSetAsync<TaxIdValidationResult>(
                Arg.Any<string>(),
                Arg.Any<Func<FusionCacheFactoryExecutionContext<TaxIdValidationResult>, CancellationToken, Task<TaxIdValidationResult>>>(),
                Arg.Any<MaybeValue<TaxIdValidationResult>>(),
                Arg.Any<FusionCacheEntryOptions?>(),
                Arg.Any<IEnumerable<string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                Func<FusionCacheFactoryExecutionContext<TaxIdValidationResult>, CancellationToken, Task<TaxIdValidationResult>> factory =
                    callInfo.ArgAt<Func<FusionCacheFactoryExecutionContext<TaxIdValidationResult>, CancellationToken, Task<TaxIdValidationResult>>>(1);
                CancellationToken ct = callInfo.ArgAt<CancellationToken>(5);
                return new ValueTask<TaxIdValidationResult>(factory(default!, ct));
            });
#pragma warning restore CA2012
    }

    /// <summary>
    /// Configures the FusionCache mock to return a pre-built result (cache hit).
    /// </summary>
    private void SetupCacheHit(TaxIdValidationResult cachedResult)
    {
#pragma warning disable CA2012 // Use ValueTasks correctly - NSubstitute mock setup requires calling GetOrSetAsync for matching
        _cache.GetOrSetAsync<TaxIdValidationResult>(
                Arg.Any<string>(),
                Arg.Any<Func<FusionCacheFactoryExecutionContext<TaxIdValidationResult>, CancellationToken, Task<TaxIdValidationResult>>>(),
                Arg.Any<MaybeValue<TaxIdValidationResult>>(),
                Arg.Any<FusionCacheEntryOptions?>(),
                Arg.Any<IEnumerable<string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<TaxIdValidationResult>(cachedResult));
#pragma warning restore CA2012
    }

    private void SetupHttpClient(HttpStatusCode statusCode, object? responseBody = null)
    {
        FakeHttpMessageHandler handler = new(statusCode, responseBody);
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://ec.europa.eu/taxation_customs/vies/rest-api/") };
        _httpClientFactory.CreateClient("Vies").Returns(httpClient);
    }

    // ======== Name property ========

    [Fact]
    public void Name_ShouldReturnVies() =>
        CreateSut().Name.ShouldBe("vies");

    // ======== Valid VAT from VIES ========

    [Fact]
    public async Task ValidateAsync_ViesReturnsValid_ShouldReturnValidResult()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        SetupCachePassthrough();
        SetupHttpClient(HttpStatusCode.OK, new
        {
            valid = true,
            name = "ACME BVBA",
            address = "RUE TEST 1",
            requestIdentifier = "WAPI-12345",
        });

        TaxIdValidationResult result = await CreateSut().ValidateAsync("BE0123456789", "BE", ct);

        result.IsValid.ShouldBeTrue();
        result.CompanyName.ShouldBe("ACME BVBA");
        result.CompanyAddress.ShouldBe("RUE TEST 1");
        result.RequestIdentifier.ShouldBe("WAPI-12345");
        result.Source.ShouldBe(TaxIdValidationSource.Vies);
        result.ValidatedAt.ShouldBe(FixedNow);
    }

    // ======== Invalid VAT from VIES ========

    [Fact]
    public async Task ValidateAsync_ViesReturnsInvalid_ShouldReturnInvalidResult()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        SetupCachePassthrough();
        SetupHttpClient(HttpStatusCode.OK, new
        {
            valid = false,
            name = (string?)null,
            address = (string?)null,
            requestIdentifier = "WAPI-99999",
        });

        TaxIdValidationResult result = await CreateSut().ValidateAsync("BE9999999999", "BE", ct);

        result.IsValid.ShouldBeFalse();
        result.Source.ShouldBe(TaxIdValidationSource.Vies);
    }

    // ======== VIES timeout + offline fallback enabled ========

    [Fact]
    public async Task ValidateAsync_ViesTimeout_FallbackEnabled_ShouldUseOfflineValidation()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _taxOptions.AllowOfflineFallback = true;
        SetupCachePassthrough();

        FakeHttpMessageHandler handler = new(new HttpRequestException("VIES unavailable"));
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://ec.europa.eu/taxation_customs/vies/rest-api/") };
        _httpClientFactory.CreateClient("Vies").Returns(httpClient);

        // BE0417497106 is a well-known valid Belgian VAT number
        TaxIdValidationResult result = await CreateSut().ValidateAsync("BE0417497106", "BE", ct);

        result.Source.ShouldBe(TaxIdValidationSource.OfflinePending);
        result.IsValid.ShouldBeTrue();
    }

    // ======== VIES timeout + offline fallback disabled ========

    [Fact]
    public async Task ValidateAsync_ViesTimeout_FallbackDisabled_ShouldReturnInvalid()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _taxOptions.AllowOfflineFallback = false;
        SetupCachePassthrough();

        FakeHttpMessageHandler handler = new(new HttpRequestException("VIES unavailable"));
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://ec.europa.eu/taxation_customs/vies/rest-api/") };
        _httpClientFactory.CreateClient("Vies").Returns(httpClient);

        TaxIdValidationResult result = await CreateSut().ValidateAsync("BE0123456789", "BE", ct);

        result.IsValid.ShouldBeFalse();
        result.Source.ShouldBe(TaxIdValidationSource.Offline);
    }

    // ======== Greece country code normalization ========

    [Fact]
    public async Task ValidateAsync_GreeceGrCode_ShouldNormalizeToElForVies()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        SetupCachePassthrough();

        string? capturedBody = null;
        FakeHttpMessageHandler handler = new(HttpStatusCode.OK, new
        {
            valid = true,
            name = "GREEK CORP",
            address = "ATHENS",
            requestIdentifier = "WAPI-GR-001",
        }, onRequest: async request =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync(ct);
        });

        HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://ec.europa.eu/taxation_customs/vies/rest-api/") };
        _httpClientFactory.CreateClient("Vies").Returns(httpClient);

        await CreateSut().ValidateAsync("EL123456789", "GR", ct);

        capturedBody.ShouldNotBeNull();
        capturedBody.ShouldContain("\"countryCode\":\"EL\"");
    }

    // ======== Cache hit scenario ========

    [Fact]
    public async Task ValidateAsync_CacheHit_ShouldReturnCachedResult()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TaxIdValidationResult cachedResult = new(
            IsValid: true, CompanyName: "Cached Corp", CompanyAddress: "Cached Street",
            RequestIdentifier: "CACHED-001", ValidatedAt: FixedNow.AddHours(-12),
            Source: TaxIdValidationSource.Vies);

        SetupCacheHit(cachedResult);

        TaxIdValidationResult result = await CreateSut().ValidateAsync("BE0123456789", "BE", ct);

        result.ShouldBe(cachedResult);
        _httpClientFactory.DidNotReceive().CreateClient(Arg.Any<string>());
    }

    // ======== Tax ID normalization (whitespace + casing) ========

    [Fact]
    public async Task ValidateAsync_TaxIdWithSpacesAndLowerCase_ShouldNormalize()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        SetupCachePassthrough();
        SetupHttpClient(HttpStatusCode.OK, new
        {
            valid = true,
            name = "NORM CORP",
            address = "STREET",
            requestIdentifier = "WAPI-NORM",
        });

        TaxIdValidationResult result = await CreateSut().ValidateAsync("be 0123 456 789", "BE", ct);

        result.IsValid.ShouldBeTrue();
    }

    // ======== HTTP error status triggers fallback ========

    [Fact]
    public async Task ValidateAsync_ViesReturnsServerError_FallbackEnabled_ShouldUseOffline()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _taxOptions.AllowOfflineFallback = true;
        SetupCachePassthrough();
        SetupHttpClient(HttpStatusCode.InternalServerError);

        TaxIdValidationResult result = await CreateSut().ValidateAsync("BE0417497106", "BE", ct);

        result.Source.ShouldBe(TaxIdValidationSource.OfflinePending);
    }

    // ======== TaskCanceledException also triggers fallback ========

    [Fact]
    public async Task ValidateAsync_TaskCanceled_FallbackEnabled_ShouldUseOffline()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _taxOptions.AllowOfflineFallback = true;
        SetupCachePassthrough();

        FakeHttpMessageHandler handler = new(new TaskCanceledException("Request timed out"));
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("https://ec.europa.eu/taxation_customs/vies/rest-api/") };
        _httpClientFactory.CreateClient("Vies").Returns(httpClient);

        TaxIdValidationResult result = await CreateSut().ValidateAsync("BE0417497106", "BE", ct);

        result.Source.ShouldBe(TaxIdValidationSource.OfflinePending);
    }

    // ======== Fake HTTP handler ========

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode? _statusCode;
        private readonly object? _responseBody;
        private readonly Exception? _exception;
        private readonly Func<HttpRequestMessage, Task>? _onRequest;

        public FakeHttpMessageHandler(HttpStatusCode statusCode, object? responseBody = null, Func<HttpRequestMessage, Task>? onRequest = null)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
            _onRequest = onRequest;
        }

        public FakeHttpMessageHandler(Exception exception)
        {
            _exception = exception;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_onRequest is not null)
            {
                await _onRequest(request);
            }

            if (_exception is not null)
            {
                throw _exception;
            }

            string? json = _responseBody is not null
                ? JsonSerializer.Serialize(_responseBody)
                : null;

            return new HttpResponseMessage(_statusCode!.Value)
            {
                Content = json is not null ? new StringContent(json, System.Text.Encoding.UTF8, "application/json") : null,
            };
        }
    }

    // ======== Test meter factory ========

    private sealed class TestMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _meters = [];
        public Meter Create(MeterOptions options) { Meter m = new(options); _meters.Add(m); return m; }
        public void Dispose() { foreach (Meter m in _meters) { m.Dispose(); } }
    }
}
