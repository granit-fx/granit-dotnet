# Granit.Authentication.External

Config-driven external/social login for Granit. Registers ASP.NET Core authentication
handlers from the `Authentication:External:Providers` configuration section, with a
startup guard that fails fast when a configured provider has no registered scheme.

## How it works

Each provider is a separate package (`Granit.Authentication.External.Google`,
`.Microsoft`, `.Apple`, `.GitHub`, `.Facebook`, `.Oidc`). A host opts in by referencing
the package (its module auto-registers via `[DependsOn]`) and listing the provider under
configuration — no bespoke wiring.

```jsonc
"Authentication": {
  "External": {
    "AutoRegisterExternalUsers": true,
    "Providers": [
      { "Type": "Google", "ClientId": "...", "ClientSecret": "...", "Scopes": ["openid", "profile", "email"] }
    ]
  }
}
```

`Type` selects the handler package; the scheme name defaults to `Type` (override with
`Name` to run several instances of the same kind). Credentials should come from
user-secrets / environment variables / Vault — never committed.

`AutoRegisterExternalUsers` (default `true`) sits alongside `Providers` under
`Authentication:External`: when `true`, an external login for an unknown email
automatically provisions a new `LocalIdentity` user. Set it `false` to reject
unknown users and require admin pre-provisioning.

## Key types

- `ExternalAuthOptions` / `ExternalAuthProvider` — the bound configuration.
- `AddExternalProviderSchemes(...)` — the helper each provider package calls to register
  one scheme per configured provider of its type.
- `OAuthProviderOptionsExtensions.ApplyExternalProvider(...)` — shared mapping for
  OAuth-based handlers (client id/secret, sign-in scheme, scopes, callback path).
- `GranitAuthenticationExternalModule` — binds the options and runs the config/scheme guard.
