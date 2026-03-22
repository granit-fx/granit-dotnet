# Granit.Identity.Federated

Base package for federated identity providers (Keycloak, Entra ID, Cognito, Google Cloud).

## What's in this package

- **`GranitIdentityFederatedModule`** — base module for all federated providers
- Shared abstractions for federated identity synchronization

## Provider packages

| Package | Provider |
|---------|----------|
| `Granit.Identity.Federated.Keycloak` | Keycloak Admin REST API |
| `Granit.Identity.Federated.EntraId` | Microsoft Entra ID (Graph API) |
| `Granit.Identity.Federated.Cognito` | AWS Cognito |
| `Granit.Identity.Federated.GoogleCloud` | Google Cloud / Firebase Auth |

## Cache layer

Add `Granit.Identity.Federated.EntityFrameworkCore` for local SQL cache
(`CachedIdentityUser`) with login-time sync middleware.
