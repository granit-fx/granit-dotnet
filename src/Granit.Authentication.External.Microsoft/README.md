# Granit.Authentication.External.Microsoft

Microsoft account external login provider for [`Granit.Authentication.External`](../Granit.Authentication.External).

Reference this package and add a provider of type `Microsoft` under
`Authentication:External:Providers`; the module registers a Microsoft account handler
(`AddMicrosoftAccount`) per matching entry, with the scheme named after the provider.

```jsonc
{ "Type": "Microsoft", "ClientId": "...", "ClientSecret": "...", "Scopes": ["openid", "profile", "email"] }
```

Credentials come from user-secrets / environment variables / Vault. The redirect URI to
register is `/signin-microsoft` (the handler default).
