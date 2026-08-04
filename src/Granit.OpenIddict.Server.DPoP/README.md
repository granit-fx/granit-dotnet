# Granit.OpenIddict.Server.DPoP

Opt-in DPoP (RFC 9449) sender-constraining for the Granit OpenIddict server. Reference this
package to bind issued access tokens to a DPoP proof key (`cnf.jkt`) at the token endpoint and
to satisfy FAPI 2.0 sender-constraining.

The core `Granit.OpenIddict.Server` package carries no DPoP dependency, so mTLS-only or
Bearer-only deployments do not pull it in. Referencing this package auto-wires the DPoP
token-binding handler into the server pipeline via the Granit module system.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.OpenIddict.Server.DPoP
```

## Configuration

```jsonc
{
  "OpenIddict": {
    "SenderConstraining": "DPoP" // fails fast at startup if this package is not referenced
  }
}
```

`WithFapi2Profile()` sets `SenderConstraining = DPoP` automatically. The `Mtls` mode (RFC 8705) is
provided by `Granit.OpenIddict.Server.Mtls`.
