# Granit.OpenIddict.Server

OpenID Connect server configuration for Granit.OpenIddict. Configures endpoint URIs, flows,
signing keys, custom grant types, and ASP.NET Core integration.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.OpenIddict.Server
```

## Dependencies

- `Granit.OpenIddict`

## Usage

You normally do **not** reference this module directly. It is pulled in
transitively by `GranitOpenIddictEntityFrameworkCoreModule`, which declares
`[DependsOn(typeof(GranitOpenIddictServerModule))]`. Wiring
`builder.AddGranitOpenIddict(...)` (from `Granit.OpenIddict.EntityFrameworkCore`)
is enough to activate it.

Reference this module explicitly only if you are wiring the OIDC server without
the EF Core persistence layer (rare).

## Documentation

See the [full documentation](https://granit-fx.dev).
