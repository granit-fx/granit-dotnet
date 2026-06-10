# Granit.Authentication.External.Oidc

Generic OpenID Connect external login provider for [`Granit.Authentication.External`](../Granit.Authentication.External).

Reference this package and add a provider of type `Oidc` under
`Authentication:External:Providers`; the module registers an authorization-code + PKCE
OpenID Connect handler (`AddOpenIdConnect`) per matching entry. Set `Authority` to the
issuer URL. Use distinct `Name` values to wire several OIDC providers.

```jsonc
{ "Type": "Oidc", "Name": "corp-sso", "Authority": "https://login.example.com/", "ClientId": "...", "ClientSecret": "...", "Scopes": ["openid", "profile", "email"] }
```

Credentials come from user-secrets / environment variables / Vault.
