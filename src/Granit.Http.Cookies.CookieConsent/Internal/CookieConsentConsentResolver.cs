using System.Text.Json;
using Granit.Http.Cookies.CookieConsent.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Http.Cookies.CookieConsent.Internal;

/// <summary>
/// Resolves cookie consent by reading the @cookieconsent/core consent cookie.
/// Maps the cookie's <c>categories</c> array to Granit <see cref="CookieCategory"/> values.
/// </summary>
/// <remarks>
/// Resolution logic:
/// <list type="number">
///   <item>Read the CookieConsent cookie from the request (name configured in <see cref="CookieConsentOptions.CookieName"/>).</item>
///   <item>Parse it as a JSON object: <c>{"categories": ["necessary", "analytics"], ...}</c>.</item>
///   <item>Map the requested <see cref="CookieCategory"/> to its configured category name.</item>
///   <item>Return <c>true</c> only if the category name appears in the <c>categories</c> array.</item>
///   <item>If the cookie is absent or malformed, return <c>false</c> (fail-safe).</item>
/// </list>
/// </remarks>
internal sealed partial class CookieConsentConsentResolver(
    IOptions<CookieConsentOptions> options,
    ILogger<CookieConsentConsentResolver> logger) : IConsentResolver
{
    /// <inheritdoc/>
    public Task<bool> HasConsentAsync(HttpContext httpContext, CookieCategory category)
    {
        if (category == CookieCategory.StrictlyNecessary)
        {
            return Task.FromResult(true);
        }

        CookieConsentOptions opts = options.Value;
        string? cookieValue = httpContext.Request.Cookies[opts.CookieName];

        if (string.IsNullOrEmpty(cookieValue))
        {
            return Task.FromResult(false);
        }

        string? categoryName = MapCategoryName(category, opts);
        if (categoryName is null)
        {
            LogUnknownCategory(category);
            return Task.FromResult(false);
        }

        bool granted = ParseAndCheckConsent(cookieValue, categoryName, category);
        return Task.FromResult(granted);
    }

    private bool ParseAndCheckConsent(string cookieValue, string categoryName, CookieCategory category)
    {
        try
        {
            using var document = JsonDocument.Parse(cookieValue);
            JsonElement root = document.RootElement;

            if (!root.TryGetProperty("categories", out JsonElement categoriesElement)
                || categoriesElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (JsonElement element in categoriesElement.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.String
                    && element.GetString() == categoryName)
                {
                    return true;
                }
            }

            return false;
        }
        catch (JsonException ex)
        {
            LogCookieParseError(ex, category);
            return false;
        }
    }

    private static string? MapCategoryName(CookieCategory category, CookieConsentOptions opts) => category switch
    {
        CookieCategory.Preferences => opts.FunctionalCategoryName,
        CookieCategory.Analytics => opts.AnalyticsCategoryName,
        CookieCategory.Marketing => opts.MarketingCategoryName,
        CookieCategory.SaleOrSharing => opts.SaleOrSharingCategoryName,
        _ => null,
    };

    [LoggerMessage(Level = LogLevel.Debug, Message = "Unknown CookieCategory {Category}; returning false (fail-safe)")]
    private partial void LogUnknownCategory(CookieCategory category);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to parse CookieConsent cookie for category {Category}; returning false (fail-safe)")]
    private partial void LogCookieParseError(Exception exception, CookieCategory category);
}
