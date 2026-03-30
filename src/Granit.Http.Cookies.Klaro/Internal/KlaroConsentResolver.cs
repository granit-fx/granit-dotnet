using System.Text.Json;
using Granit.Http.Cookies.Klaro.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Http.Cookies.Klaro.Internal;

/// <summary>
/// Resolves cookie consent by reading the Klaro consent cookie.
/// Maps Klaro per-service consent to Granit per-category consent.
/// </summary>
/// <remarks>
/// Resolution logic:
/// <list type="number">
///   <item>Read the Klaro cookie from the request (name configured in <see cref="KlaroOptions.CookieName"/>).</item>
///   <item>Parse it as a JSON object: <c>{"serviceName": true/false, ...}</c>.</item>
///   <item>Find all service names mapped to the requested <see cref="CookieCategory"/> via <see cref="IThirdPartyServiceRegistry"/>.</item>
///   <item>Return <c>true</c> only if <b>all</b> mapped services have consent granted.</item>
///   <item>If no services are mapped for the category, return <c>false</c> (fail-safe).</item>
/// </list>
/// </remarks>
internal sealed partial class KlaroConsentResolver(
    IOptions<KlaroOptions> options,
    IThirdPartyServiceRegistry serviceRegistry,
    ILogger<KlaroConsentResolver> logger) : IConsentResolver
{
    /// <inheritdoc/>
    public Task<bool> HasConsentAsync(HttpContext httpContext, CookieCategory category)
    {
        if (category == CookieCategory.StrictlyNecessary)
        {
            return Task.FromResult(true);
        }

        KlaroOptions klaroOptions = options.Value;
        string? cookieValue = httpContext.Request.Cookies[klaroOptions.CookieName];

        if (string.IsNullOrEmpty(cookieValue))
        {
            return Task.FromResult(false);
        }

        var serviceNames = serviceRegistry
            .GetByCategory(category)
            .Select(s => s.Name)
            .ToList();

        if (serviceNames.Count == 0)
        {
            LogNoServicesForCategory(category);
            return Task.FromResult(false);
        }

        bool granted = ParseAndCheckConsent(cookieValue, serviceNames, category);
        return Task.FromResult(granted);
    }

    private bool ParseAndCheckConsent(
        string cookieValue,
        List<string> serviceNames,
        CookieCategory category)
    {
        try
        {
            using var document = JsonDocument.Parse(cookieValue);
            JsonElement root = document.RootElement;

            foreach (string serviceName in serviceNames)
            {
                if (!root.TryGetProperty(serviceName, out JsonElement element)
                    || element.ValueKind != JsonValueKind.True)
                {
                    return false;
                }
            }

            return true;
        }
        catch (JsonException ex)
        {
            LogKlaroCookieParseError(ex, category);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "No third-party services mapped to category {Category}; returning false (fail-safe)")]
    private partial void LogNoServicesForCategory(CookieCategory category);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to parse Klaro cookie for category {Category}; returning false (fail-safe)")]
    private partial void LogKlaroCookieParseError(Exception exception, CookieCategory category);
}
