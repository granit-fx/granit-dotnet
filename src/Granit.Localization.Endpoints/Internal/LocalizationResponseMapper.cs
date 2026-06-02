using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Granit.Localization.Endpoints.Dtos;
using Granit.Localization.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Localization;

namespace Granit.Localization.Endpoints.Internal;

/// <summary>
/// Shared mapping and validation helpers for localization endpoints.
/// </summary>
internal static partial class LocalizationResponseMapper
{
    // BCP 47 language tag: 2-8 alpha primary subtag, optional hyphen-separated subtags (1-8 alphanumeric).
    [GeneratedRegex(@"^[a-zA-Z]{2,8}(-[a-zA-Z0-9]{1,8})*$")]
    internal static partial Regex Bcp47Pattern();

    internal static ApplicationLocalizationResponse BuildLocalizationResponse(
        GranitLocalizationOptions options,
        IStringLocalizerFactory localizerFactory,
        string? cultureName,
        HttpResponse response)
    {
        CultureInfo culture = string.IsNullOrWhiteSpace(cultureName)
            ? CultureInfo.CurrentUICulture
            : CultureInfo.GetCultureInfo(cultureName);

        CultureInfo previousCulture = CultureInfo.CurrentUICulture;
        Dictionary<string, IReadOnlyDictionary<string, string>> resources;

        try
        {
            CultureInfo.CurrentUICulture = culture;
            resources = BuildResources(options, localizerFactory);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }

        List<LanguageInfoResponse> languages = [.. options.Languages
            .Select(l => new LanguageInfoResponse(l.CultureName, l.DisplayName, l.FlagIcon, l.IsDefault))];

        response.Headers.CacheControl = "public, max-age=3600";
        response.Headers.Vary = "Accept-Language";

        return new ApplicationLocalizationResponse(culture.Name, resources, languages);
    }

    internal static ProblemHttpResult StoreNotRegistered() =>
        TypedResults.Problem(
            detail: "No localization override store is registered. Add GranitLocalizationDatabaseSourceEntityFrameworkCoreModule.",
            statusCode: StatusCodes.Status501NotImplemented);

    internal static ProblemHttpResult? ValidateBcp47(string cultureName) =>
        Bcp47Pattern().IsMatch(cultureName)
            ? null
            : TypedResults.Problem(
                detail: $"Culture name '{cultureName}' is not a valid BCP 47 tag.",
                statusCode: StatusCodes.Status400BadRequest);

    internal static ProblemHttpResult? ValidateMaxLength(string value, string paramName, int maxLength) =>
        value.Length <= maxLength
            ? null
            : TypedResults.Problem(
                detail: $"{paramName} must not exceed {maxLength} characters.",
                statusCode: StatusCodes.Status400BadRequest);

    private static Dictionary<string, IReadOnlyDictionary<string, string>> BuildResources(
        GranitLocalizationOptions options,
        IStringLocalizerFactory localizerFactory)
    {
        Dictionary<string, IReadOnlyDictionary<string, string>> resources = new(StringComparer.Ordinal);

        foreach (Type resourceType in options.Resources.GetAll().Select(resourceInfo => resourceInfo.ResourceType))
        {
            string name = resourceType
                .GetCustomAttribute<LocalizationResourceNameAttribute>()?.Name
                ?? resourceType.Name;

            IStringLocalizer localizer = localizerFactory.Create(resourceType);

            var translations = localizer
                .GetAllStrings(includeParentCultures: true)
                .ToDictionary(s => s.Name, s => s.Value, StringComparer.Ordinal);

            resources[name] = translations;
        }

        return resources;
    }
}
