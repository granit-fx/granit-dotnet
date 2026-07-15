# Granit.Identity.Federated.Endpoints

ASP.NET Core HTTP integration for `Granit.Identity.Federated`. Ships the
login-time `UserCacheSyncMiddleware` and the `UseGranitIdentityUserCacheSync`
pipeline extension, which upsert the current authenticated user's federated
cache entry from JWT claims when the cached entry is missing or stale.

This package exists to keep the ASP.NET Core dependency out of the federated
**domain** package (`Granit.Identity.Federated`) and the **data-only**
persistence package (`Granit.Identity.Federated.EntityFrameworkCore`). It ships
only HTTP-integration middleware — no Minimal API endpoints — so, unlike a
typical `.Endpoints` package, it carries no OpenAPI document or
`*EndpointsOptions` class.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Identity.Federated.Endpoints
```

## Usage

Register the middleware after authentication and tenant resolution:

```csharp
app.UseGranitIdentityUserCacheSync();
```
