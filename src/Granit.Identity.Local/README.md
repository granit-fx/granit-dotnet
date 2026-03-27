# Granit.Identity.Local

Shared domain types and service abstractions for self-hosted Granit identity providers.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Identity.Local
```

## What's in this package

- **`GranitUser`** / **`GranitRole`** — ASP.NET Identity entities shared by all self-hosted providers
- **`GranitUserGroup`** / **`GranitUserGroupMember`** — Group domain models
- **Service interfaces** — `ILocalIdentityGroupStore`, `IEmailConfirmationService`,
  `IPasswordResetService`, `IAccountDeletionService`, `IImpersonationService`,
  `IPasskeyService`, `ITwoFactorService`, `IExternalLoginService`, `ITotpService`
- **Integration events** — `UserRegisteredEto`, `AccountDeletedEto`,
  `PasswordResetRequestedEto`, `UserImpersonatedEto`
- **`GranitPasskeyOptions`** — Passkey configuration options

## Purpose

This package exists to share identity types between multiple self-hosted providers
(OpenIddict, Duende) without coupling them to each other's EF Core packages.

## Dependencies

- `Granit.Events`
- `Granit.Guids`
- `Granit.Identity`
- `Granit.Timing`

## Documentation

See the [full documentation](https://granit-fx.dev).
