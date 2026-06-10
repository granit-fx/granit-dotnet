# Granit.Authentication.External.Google

Google external login provider for [`Granit.Authentication.External`](../Granit.Authentication.External).

Reference this package and add a provider of type `Google` under
`Authentication:External:Providers`; the module registers a Google authentication handler
(`AddGoogle`) per matching entry, with the scheme named after the provider.

```jsonc
{ "Type": "Google", "ClientId": "...apps.googleusercontent.com", "ClientSecret": "...", "Scopes": ["openid", "profile", "email"] }
```

Credentials come from user-secrets / environment variables / Vault. The redirect URI to
register in Google is `/signin-google` (the handler default).
