using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Granit.MultiTenancy;
using Granit.Tax.Diagnostics;
using Granit.Tax.Options;
using Granit.Timing;
using Granit.Validation.Europe.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Tax.Internal.Internal;

/// <summary>
/// EU VIES (VAT Information Exchange System) online validator with in-memory cache.
/// </summary>
/// <remarks>
/// <para>
/// Calls the EU Commission REST API. Caches results in <see cref="IFusionCache"/>
/// (configurable TTL, default 24h) to avoid repeated API calls for the same VAT number.
/// </para>
/// <para>
/// Falls back to offline format validation via <see cref="EuropeanVatAlgorithm"/>
/// when VIES is unavailable, depending on <see cref="TaxOptions.AllowOfflineFallback"/>.
/// </para>
/// </remarks>
internal sealed partial class ViesValidator(
    IHttpClientFactory httpClientFactory,
    IFusionCache cache,
    IOptions<TaxOptions> taxOptions,
    IClock clock,
    TaxMetrics metrics,
    ICurrentTenant currentTenant,
    ILogger<ViesValidator> logger) : ITaxIdValidator
{
    private const string CacheKeyPrefix = "granit:tax:vies:";

    private static string BuildCacheKey(string tenantSegment, string normalizedTaxId) =>
        $"{CacheKeyPrefix}{tenantSegment}:{normalizedTaxId}";

    /// <inheritdoc/>
    public string Name => "vies";

    /// <inheritdoc/>
    public async Task<TaxIdValidationResult> ValidateAsync(
        string taxId, string countryCode,
        CancellationToken cancellationToken = default)
    {
        string normalizedTaxId = taxId.ToUpperInvariant().Replace(" ", "", StringComparison.Ordinal);
        string tenantSegment = currentTenant.IsAvailable ? currentTenant.Id!.Value.ToString() : "global";
        string cacheKey = BuildCacheKey(tenantSegment, normalizedTaxId);

        var ttl = TimeSpan.FromHours(taxOptions.Value.ValidationCacheTtlHours);

        TaxIdValidationResult result = await cache.GetOrSetAsync<TaxIdValidationResult>(
            cacheKey,
            async (_, ct) =>
            {
                // Strip country prefix if present (VIES expects them separately)
                string vatNumber = normalizedTaxId;
                if (vatNumber.Length > 2 && char.IsLetter(vatNumber[0]) && char.IsLetter(vatNumber[1]))
                {
                    vatNumber = vatNumber[2..];
                }

                // Map GR → EL for VIES (VIES uses EL for Greece)
                string viesCountry = string.Equals(countryCode, "GR", StringComparison.OrdinalIgnoreCase)
                    ? "EL"
                    : countryCode.ToUpperInvariant();

                try
                {
                    HttpClient httpClient = httpClientFactory.CreateClient("Vies");
                    var request = new ViesRequest(viesCountry, vatNumber);

                    HttpResponseMessage response = await httpClient
                        .PostAsJsonAsync("check-vat-number", request, ct)
                        .ConfigureAwait(false);

                    response.EnsureSuccessStatusCode();

                    ViesResponse? viesResponse = await response.Content
                        .ReadFromJsonAsync<ViesResponse>(ct)
                        .ConfigureAwait(false);

                    metrics.RecordViesRequest(currentTenant.Id?.ToString(), success: true);

                    if (viesResponse is null)
                    {
                        return FallbackOrReject(normalizedTaxId);
                    }

                    Log.ViesValidated(logger, MaskTaxId(normalizedTaxId), viesResponse.IsValid);

                    return new TaxIdValidationResult(
                        IsValid: viesResponse.IsValid,
                        CompanyName: viesResponse.Name,
                        CompanyAddress: viesResponse.Address,
                        RequestIdentifier: viesResponse.RequestIdentifier,
                        ValidatedAt: clock.Now,
                        Source: TaxIdValidationSource.Vies);
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    metrics.RecordViesRequest(currentTenant.Id?.ToString(), success: false);
                    Log.ViesUnavailable(logger, ex);

                    return FallbackOrReject(normalizedTaxId);
                }
            },
            new FusionCacheEntryOptions { Duration = ttl },
            token: cancellationToken).ConfigureAwait(false);

        return result;
    }

    private TaxIdValidationResult FallbackOrReject(string taxId)
    {
        if (!taxOptions.Value.AllowOfflineFallback)
        {
            Log.ViesFallbackDisabled(logger, MaskTaxId(taxId));
            return new TaxIdValidationResult(
                IsValid: false, CompanyName: null, CompanyAddress: null,
                RequestIdentifier: null, ValidatedAt: clock.Now,
                Source: TaxIdValidationSource.Offline);
        }

        bool formatValid = EuropeanVatAlgorithm.IsValid(taxId);
        Log.ViesFallbackOffline(logger, MaskTaxId(taxId), formatValid);

        return new TaxIdValidationResult(
            IsValid: formatValid,
            CompanyName: null,
            CompanyAddress: null,
            RequestIdentifier: null,
            ValidatedAt: clock.Now,
            Source: TaxIdValidationSource.OfflinePending);
    }

    private sealed record ViesRequest(
        [property: JsonPropertyName("countryCode")] string CountryCode,
        [property: JsonPropertyName("vatNumber")] string VatNumber);

    private sealed record ViesResponse(
        [property: JsonPropertyName("isValid")] bool IsValid,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("address")] string? Address,
        [property: JsonPropertyName("requestIdentifier")] string? RequestIdentifier);

    private static string MaskTaxId(string taxId) =>
        taxId.Length > 4 ? $"{taxId[..2]}***{taxId[^4..]}" : "***";

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Debug, Message = "VIES validated {MaskedTaxId}: {IsValid}")]
        public static partial void ViesValidated(ILogger logger, string maskedTaxId, bool isValid);

        [LoggerMessage(Level = LogLevel.Warning, Message = "VIES unavailable")]
        public static partial void ViesUnavailable(ILogger logger, Exception ex);

        [LoggerMessage(Level = LogLevel.Warning, Message = "VIES fallback disabled, rejecting {MaskedTaxId}")]
        public static partial void ViesFallbackDisabled(ILogger logger, string maskedTaxId);

        [LoggerMessage(Level = LogLevel.Debug, Message = "VIES offline fallback for {MaskedTaxId}: format valid = {FormatValid}")]
        public static partial void ViesFallbackOffline(ILogger logger, string maskedTaxId, bool formatValid);
    }
}
