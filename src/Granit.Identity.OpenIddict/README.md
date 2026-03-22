# Granit.Identity.OpenIddict

`IIdentityProvider` bridge over ASP.NET Core Identity for Granit.OpenIddict.

## What's in this package

- **`AspNetIdentityProvider`** — implements all 7 sub-interfaces of `IIdentityProvider`
- **`AspNetIdentityProviderCapabilities`** — declares provider capabilities (`IsLocalStore = true`)
- **`AspNetIdentityUserLookupService`** — direct `UserManager` queries (no redundant cache)

## Usage

Add this module to your application:

```csharp
[DependsOn(typeof(GranitIdentityOpenIddictModule))]
public sealed class MyAppModule : GranitModule { }
```

## Do NOT add Granit.Identity.EntityFrameworkCore

When using `Granit.Identity.OpenIddict`, **do not add** `Granit.Identity.EntityFrameworkCore`
to your project. It is unnecessary and creates problems:

- **`UserCacheEntry` is redundant** — `GranitUser` IS the source of truth, already
  queryable via SQL. No cache table needed.
- **`UserCacheSyncMiddleware` is wasteful** — syncing JWT claims into a cache of data
  that's already local creates unnecessary writes on every request.
- **Data duplication risk** — three user tables (`GranitUser` + `UserCacheEntry` + app
  `UserProfile`) lead to desynchronization.

A warning is logged at startup if both packages are detected:

```
WARN: Granit.Identity.EntityFrameworkCore is loaded alongside Granit.Identity.OpenIddict.
UserCacheEntry is redundant when the identity provider stores users locally (GranitUser).
```

See [ADR-019](docs-site/src/content/docs/dotnet/architecture/adr/019-user-lookup-dual-mode.md)
for the full architectural rationale.
