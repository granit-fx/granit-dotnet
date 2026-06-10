# Granit.Authentication.External.Facebook

Facebook external login provider for [`Granit.Authentication.External`](../Granit.Authentication.External).

Reference this package and add a provider of type `Facebook` under
`Authentication:External:Providers`; the module registers a Facebook handler
(`AddFacebook`) per matching entry, with the scheme named after the provider.

```jsonc
{ "Type": "Facebook", "ClientId": "<app-id>", "ClientSecret": "<app-secret>", "Scopes": ["email", "public_profile"] }
```

Credentials come from user-secrets / environment variables / Vault. The redirect URI to
register is `/signin-facebook` (the handler default).
