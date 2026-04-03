using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Granit.Tax.Diagnostics;
using Granit.Tax.Options;
using Granit.Timing;
using Granit.Validation.Europe.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Tax.Internal.Internal;

/// <summary>
/// EU VIES (VAT Information Exchange System) online validator.
/// </summary>
/// <remarks>
/// Calls the EU Commission REST API. Falls back to offline format validation
/// via <see cref="EuropeanVatAlgorithm"/> when VIES is unavailable, depending
/// on <see cref="TaxOptions.AllowOfflineFallback"/>.
/// </remarks>
internal sealed partial class ViesValidator(
    IHttpClientFactory httpClientFactory,
    IOptions<TaxOptions> taxOptions,
    IClock clock,
    TaxMetrics metrics,
    ILogger<ViesValidator> logger) : ITaxIdValidator
{
    /// <inheritdoc/>
    public string Name => "vies";

    /// <inheritdoc/>
    public async Task<TaxIdValidationResult> ValidateAsync(
        string taxId, string countryCode,
        CancellationToken cancellationToken = default)
    {
        // Strip country prefix if present (VIES expects them separately)
        string vatNumber = taxId;
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
                .PostAsJsonAsync("check-vat-number", request, cancellationToken)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            ViesResponse? viesResponse = await response.Content
                .ReadFromJsonAsync<ViesResponse>(cancellationToken)
                .ConfigureAwait(false);

            metrics.RecordViesRequest(null, success: true);

            if (viesResponse is null)
            {
                return FallbackOrReject(taxId, countryCode, "Empty VIES response");
            }

            Log.ViesValidated(logger, taxId, viesResponse.IsValid);

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
            metrics.RecordViesRequest(null, success: false);
            Log.ViesUnavailable(logger, ex);

            return FallbackOrReject(taxId, countryCode, $"VIES unavailable: {ex.Message}");
        }
    }

    private TaxIdValidationResult FallbackOrReject(
        string taxId, string countryCode, string reason)
    {
        if (!taxOptions.Value.AllowOfflineFallback)
        {
            Log.ViesFallbackDisabled(logger, taxId);
            return new TaxIdValidationResult(
                IsValid: false, CompanyName: null, CompanyAddress: null,
                RequestIdentifier: null, ValidatedAt: clock.Now,
                Source: TaxIdValidationSource.Offline);
        }

        // Offline format validation as fallback
        bool formatValid = EuropeanVatAlgorithm.IsValid(taxId);

        Log.ViesFallbackOffline(logger, taxId, formatValid);

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

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "VIES validated {TaxId}: {IsValid}")]
        public static partial void ViesValidated(ILogger logger, string taxId, bool isValid);

        [LoggerMessage(Level = LogLevel.Warning, Message = "VIES unavailable")]
        public static partial void ViesUnavailable(ILogger logger, Exception ex);

        [LoggerMessage(Level = LogLevel.Warning, Message = "VIES fallback disabled, rejecting {TaxId}")]
        public static partial void ViesFallbackDisabled(ILogger logger, string taxId);

        [LoggerMessage(Level = LogLevel.Information, Message = "VIES offline fallback for {TaxId}: format valid = {FormatValid}")]
        public static partial void ViesFallbackOffline(ILogger logger, string taxId, bool formatValid);
    }
}
