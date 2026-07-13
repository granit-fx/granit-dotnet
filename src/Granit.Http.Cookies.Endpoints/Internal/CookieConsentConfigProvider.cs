using Granit.Http.Cookies.Endpoints.Dtos;
using Granit.Modularity;

namespace Granit.Http.Cookies.Endpoints.Internal;

/// <summary>
/// Maps the cookie and third-party service registries to the public-facing
/// <see cref="CookieConsentConfigResponse"/>.
/// </summary>
internal sealed class CookieConsentConfigProvider(
    ICookieRegistry cookieRegistry,
    IThirdPartyServiceRegistry serviceRegistry)
    : IModuleConfigProvider<CookieConsentConfigResponse>
{
    public CookieConsentConfigResponse GetConfig()
    {
        var cookies = cookieRegistry.GetAll()
            .Select(c => new CookieDefinitionResponse(
                c.Name,
                CategoryToSnakeCase(c.Category),
                c.RetentionDays,
                c.Purpose))
            .ToList();

        var services = serviceRegistry.GetAll()
            .Select(s => new ThirdPartyServiceResponse(
                s.Name,
                CategoryToSnakeCase(s.Category),
                s.CookiePatterns))
            .ToList();

        return new CookieConsentConfigResponse(cookies, services);
    }

    private static string CategoryToSnakeCase(CookieCategory category) => category switch
    {
        CookieCategory.StrictlyNecessary => "strictly_necessary",
        CookieCategory.Preferences => "preferences",
        CookieCategory.Analytics => "analytics",
        CookieCategory.Marketing => "marketing",
        CookieCategory.SaleOrSharing => "sale_or_sharing",
        // Lower-casing a multi-word enum name glues its words together — every
        // category must have an explicit snake_case mapping above.
        _ => category.ToString().ToLowerInvariant(),
    };
}
