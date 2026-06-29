# Granit.Http.Cookies.Klaro

> **Deprecated.** This package is superseded by `Granit.Http.Cookies.CookieConsent`
> ([@cookieconsent/core](https://github.com/orestbida/cookieconsent) adapter). Migrate by
> replacing `UseKlaro()` with `UseCookieConsent()` and switching the front-end adapter
> from `@granit/cookies-klaro` to `@granit/cookies-cookieconsent`.
> See [granit-front#230](https://github.com/granit-fx/granit-front/issues/230) for context.

Klaro CMP integration for Granit.Http.Cookies. Implements `IConsentResolver` by parsing
the Klaro consent cookie and mapping per-service consent to GDPR `CookieCategory`.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Http.Cookies.Klaro
```

## Usage

> For new projects, use `Granit.Http.Cookies.CookieConsent` instead. This block
> is for maintainers of existing Klaro integrations.

`UseKlaro()` is a `GranitCookiesBuilder` extension chained inside the
`AddGranitCookies` callback:

```csharp
context.Services.AddGranitCookies(cookies =>
{
    cookies.UseKlaro();
});
```

Configuration lives under the `Http:Cookies:Klaro` section; `CookieName` is
optional (default: `"klaro"`). Service-to-category mappings now live under
`Cookies:ThirdPartyServices` (consumed via `IThirdPartyServiceRegistry`), not in
the Klaro section.

## Dependencies

- `Granit.Http.Cookies`

## Documentation

See the [full documentation](https://granit-fx.dev).
