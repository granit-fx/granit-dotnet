# Granit.Identity.Local.AspNetIdentity

`IIdentityProvider` bridge over ASP.NET Core Identity for the Granit OpenIddict module.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Identity.Local.AspNetIdentity
```

## What's in this package

- **`AspNetIdentityProvider`** — implements all 7 sub-interfaces of `IIdentityProvider`
- **`AspNetIdentityProviderCapabilities`** — declares provider capabilities (`IsLocalStore = true`)
- **`AspNetIdentityUserLookupService`** — direct `UserManager` queries (no redundant cache)

## Do NOT add Granit.Identity.Federated.EntityFrameworkCore

When using `Granit.Identity.Local.AspNetIdentity`, **do not add** `Granit.Identity.Federated.EntityFrameworkCore`
to your project. `FederatedIdentity` is redundant when the identity provider stores users
locally (`LocalIdentity`). A warning is logged at startup if both packages are detected.

See [ADR-019](https://granit-fx.dev/dotnet/architecture/adr/019-user-lookup-dual-mode/)
for the full architectural rationale.

## Dependencies

- `Granit.Identity`
- `Granit.OpenIddict.EntityFrameworkCore`

## Documentation

See the [full documentation](https://granit-fx.dev).
