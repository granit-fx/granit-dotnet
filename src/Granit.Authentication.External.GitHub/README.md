# Granit.Authentication.External.GitHub

GitHub external login provider for [`Granit.Authentication.External`](../Granit.Authentication.External),
backed by `AspNet.Security.OAuth.GitHub`.

Reference this package and add a provider of type `GitHub` under
`Authentication:External:Providers`; the module registers a GitHub handler (`AddGitHub`)
per matching entry, with the scheme named after the provider.

```jsonc
{ "Type": "GitHub", "ClientId": "<oauth-app-id>", "ClientSecret": "...", "Scopes": ["read:user", "user:email"] }
```

Credentials come from user-secrets / environment variables / Vault. The redirect URI to
register is `/signin-github` (the handler default).
