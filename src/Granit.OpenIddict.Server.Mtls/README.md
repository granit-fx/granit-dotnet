# Granit.OpenIddict.Server.Mtls

Opt-in mutual-TLS (RFC 8705) sender-constraining for the Granit OpenIddict server. Reference this
package to bind issued access tokens to the client's TLS certificate (`cnf.x5t#S256`) at the token
endpoint and to satisfy FAPI 2.0 sender-constraining.

The core `Granit.OpenIddict.Server` package carries no mTLS dependency, so DPoP-only or Bearer-only
deployments do not pull it in. Referencing this package auto-wires the certificate token-binding
handler into the server pipeline via the Granit module system.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.OpenIddict.Server.Mtls
```

## Configuration

```jsonc
{
  "OpenIddict": {
    "SenderConstraining": "Mtls" // fails fast at startup if this package is not referenced
  }
}
```

`WithFapi2Profile()` only falls back to `DPoP` when no mechanism is set — configure
`SenderConstraining = Mtls` to pick mTLS instead.

Resource servers validate the resulting binding with `Granit.Authentication.Mtls`. The `DPoP` mode
(RFC 9449) is provided by `Granit.OpenIddict.Server.DPoP`.
