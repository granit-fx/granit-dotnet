# Granit.OpenIddict.Identity

`IIdentityProvider` bridge over ASP.NET Core Identity for Granit.OpenIddict.

## What's in this package

- **`AspNetIdentityProvider`** — implements all 7 sub-interfaces of `IIdentityProvider`
- **`AspNetIdentityProviderCapabilities`** — declares provider capabilities
- **`AspNetIdentityUserLookupService`** — direct `UserManager` queries (no redundant cache)

## Usage

Add this module to your application:

```csharp
[DependsOn(typeof(GranitOpenIddictIdentityModule))]
public sealed class MyAppModule : GranitModule { }
```

## Important

Do NOT add `Granit.Identity.EntityFrameworkCore` alongside this package — it would create
a redundant `UserCache` layer. This package queries `UserManager<GranitUser>` directly.
