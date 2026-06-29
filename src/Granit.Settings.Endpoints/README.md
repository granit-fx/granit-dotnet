# Granit.Settings.Endpoints

Minimal API endpoints for Granit settings management: user-scoped preferences (locale, timezone, custom), global and tenant administration. Ships an optional `SettingsCultureMiddleware` for `CultureInfo`/timezone hydration from user settings (opt-in — see below).

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Settings.Endpoints
```

## Dependencies

- `Granit.Authorization`
- `Granit.Http.ApiDocumentation`
- `Granit.Settings`
- `Granit.Timing`
- `Granit.Validation`
- `Granit.Workspaces.Abstractions`

## Optional: culture/timezone hydration

The module **ships** `SettingsCultureMiddleware` (in `Granit.Settings.Endpoints.Middleware`)
that loads the user's locale and timezone settings from the cascading store into the
request `CultureInfo`. It is **not** registered automatically — add it to your pipeline
after authentication to enable it:

```csharp
app.UseMiddleware<SettingsCultureMiddleware>();
```

The middleware is a no-op for anonymous requests.

## Documentation

See the [full documentation](https://granit-fx.dev).
