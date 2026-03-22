# Granit.Identity.Local.AspNetCore

`IIdentityProvider` bridge over ASP.NET Core Identity for Granit.OpenIddict.

## What's in this package

- **`AspNetIdentityProvider`** — implements all 7 sub-interfaces of `IIdentityProvider`
- **`AspNetIdentityProviderCapabilities`** — declares provider capabilities (`IsLocalStore = true`)
- **`AspNetIdentityUserLookupService`** — direct `UserManager` queries (no redundant cache)

## Usage

Add this module to your application:

```csharp
[DependsOn(typeof(GranitIdentityLocalAspNetCoreModule))]
public sealed class MyAppModule : GranitModule { }
```

## Do NOT add Granit.Identity.Federated.EntityFrameworkCore

When using `Granit.Identity.Local.AspNetCore`, **do not add** `Granit.Identity.Federated.EntityFrameworkCore`
to your project. It is unnecessary and creates problems:

- **`UserCacheEntry` is redundant** — `GranitUser` IS the source of truth, already
  queryable via SQL. No cache table needed.
- **`UserCacheSyncMiddleware` is wasteful** — syncing JWT claims into a cache of data
  that's already local creates unnecessary writes on every request.
- **Data duplication risk** — three user tables (`GranitUser` + `UserCacheEntry` + app
  `UserProfile`) lead to desynchronization.

A warning is logged at startup if both packages are detected:

```text
WARN: Granit.Identity.Federated.EntityFrameworkCore is loaded alongside Granit.Identity.Local.AspNetCore.
UserCacheEntry is redundant when the identity provider stores users locally (GranitUser).
```

See [ADR-019](docs-site/src/content/docs/dotnet/architecture/adr/019-user-lookup-dual-mode.md)
for the full architectural rationale.
