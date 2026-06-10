# Granit.Authentication.External.Apple

Sign in with Apple external login provider for [`Granit.Authentication.External`](../Granit.Authentication.External),
backed by `AspNet.Security.OAuth.Apple`.

Reference this package and add a provider of type `Apple` under
`Authentication:External:Providers`; the module registers an Apple handler (`AddApple`)
per matching entry, with the scheme named after the provider.

Apple's client secret is a short-lived JWT signed with a P8 private key, so the key
material goes in `Properties`:

```jsonc
{
  "Type": "Apple",
  "ClientId": "<services-id>",
  "Properties": { "TeamId": "<team-id>", "KeyId": "<key-id>", "PrivateKey": "<PEM contents of the .p8>" },
  "Scopes": ["name", "email"]
}
```

`ClientId` is the Apple Services ID. Secrets come from user-secrets / environment
variables / Vault. The redirect URI to register is `/signin-apple` (the handler default).
