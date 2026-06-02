# Granit.Http.Cookies.CookieConsent

[@cookieconsent/core](https://github.com/orestbida/cookieconsent) CMP integration for
`Granit.Http.Cookies`. Implements `IConsentResolver` by reading the `cc_cookie` consent cookie
and mapping its `categories` array to Granit `CookieCategory` values.

Replaces `Granit.Http.Cookies.Klaro` (deprecated). The `@granit/cookies` core abstraction is
unchanged — only the adapter differs.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Http.Cookies.CookieConsent
```

## Dependencies

- `Granit.Http.Cookies`

## Usage

```csharp
builder.Services
    .AddGranitCookies()
    .UseCookieConsent();
```

### Configuration

```json
{
  "Http": {
    "Cookies": {
      "CookieConsent": {
        "CookieName": "cc_cookie",
        "FunctionalCategoryName": "functional",
        "AnalyticsCategoryName": "analytics",
        "MarketingCategoryName": "marketing",
        "SaleOrSharingCategoryName": "sale_or_sharing"
      }
    }
  }
}
```

Category names must match those configured in the front-end
`@granit/cookies-cookieconsent` adapter. The defaults align with the
`@cookieconsent/core` built-in category identifiers.

## Documentation

See the [full documentation](https://granit-fx.dev).
