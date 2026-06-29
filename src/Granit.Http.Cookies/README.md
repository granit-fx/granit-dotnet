# Granit.Http.Cookies

HTTP cookie management with Strict Registry Pattern for Granit. GDPR-compliant by design:
fail-fast on unregistered cookies, per-category consent enforcement. GDPR/ISO 27001 compliant.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Http.Cookies
```

## Usage

Register cookie definitions and the consent resolver in `ConfigureServices` via
the `AddGranitCookies` builder callback:

```csharp
context.Services.AddGranitCookies(cookies =>
{
    cookies.RegisterSessionCookie();
    cookies.RegisterCookie(new CookieDefinition(
        Name: "user_lang",
        Category: CookieCategory.Preferences,
        RetentionDays: 365,
        IsHttpOnly: false,
        Purpose: "User language preference"));
    // Attach a consent resolver, e.g. cookies.UseKlaro() (Granit.Http.Cookies.Klaro)
});
```

Cookies registered through the callback are discovered at startup. The
`ICookieRegistry` enforces strict policy: it fails fast on unregistered cookies
and applies per-category consent enforcement.

## Dependencies

- `Granit.Timing`

## Documentation

See the [full documentation](https://granit-fx.dev).
