---
title: "ADR-005: Keycloak as Authentication Provider"
description: "Use Keycloak for OIDC/OAuth 2.0 authentication to meet HDS compliance and enable self-hosted European deployment"
sidebar:
  order: 5
  label: "005 - Keycloak"
---

> **Date:** 2026-02-27
> **Authors:** Jean-Francois Meyers
> **Scope:** @granit/authentication, @granit/react-authentication, @granit/react-authorization

## Context

The platform operates in a healthcare data hosting (HDS) context that imposes
strict authentication requirements:

- **OpenID Connect / OAuth 2.0**: standard protocols required
- **Complete audit**: connection and session traceability
- **Multi-tenant**: realm isolation per tenant
- **HDS compliance**: the provider must be hostable in France on private
  infrastructure (no US cloud)
- **Open source**: no commercial lock-in

## Decision

Use **Keycloak** as the authentication provider via the following packages:

- `@granit/authentication` — OIDC types (framework-agnostic)
- `@granit/react-authentication` — Keycloak init hook, auth context factory,
  mock provider
- `@granit/react-authorization` — Permission checking hooks (RBAC)

The packages encapsulate `keycloak-js` and expose:

- A React authentication context (`BaseAuthContextType`)
- Integration hooks (`useAuth`, `useKeycloakInit`, `usePermissions`)
- A mock provider for tests and local development
- A 401 interceptor for revoked session handling

The `BaseAuthContextType` is an extensible interface: consumer applications
add their own fields (e.g. `register`, `hasAdminRole`).

## Alternatives considered

### Option 1: Keycloak (selected)

- **Advantage**: open source (Apache 2.0), self-hosted, standard protocols,
  CNCF project, large community, realm-per-tenant isolation
- **Disadvantage**: operational burden (deployment, updates, realm backup,
  monitoring), `keycloak-js` API sometimes unstable between major versions

### Option 2: Auth0

- **Advantage**: fully managed SaaS, excellent developer experience
- **Disadvantage**: **incompatible with HDS** — hosted in the US, Cloud Act
  applies, commercial lock-in

### Option 3: Azure AD B2C

- **Advantage**: native Azure integration, managed service
- **Disadvantage**: Azure dependency, US Cloud Act applies, complex
  configuration for custom flows

### Option 4: Custom OIDC implementation

- **Advantage**: full control, no external dependency
- **Disadvantage**: enormous implementation effort, security risk, no
  community review, certification burden

## Justification

| Criterion | Keycloak | Auth0 | Azure AD B2C | Custom |
| --------- | -------- | ----- | ------------ | ------ |
| HDS compliance | Yes (self-hosted EU) | No (US) | No (US Cloud Act) | Yes |
| Open source | Yes (Apache 2.0) | No | No | Yes |
| OIDC/OAuth 2.0 | Native | Native | Native | Manual |
| Multi-tenant (realms) | Native | Per-tenant plans | Partial | Manual |
| Operational cost | Self-hosted | SaaS fee | Azure fee | Development cost |
| Community | CNCF, Red Hat | Large | Large | None |

## Packages used

| Package | Role |
| ------- | ---- |
| `keycloak-js` | Official Keycloak JavaScript adapter |

## Consequences

### Positive

- HDS compliance: Keycloak is self-hosted on private European infrastructure
- Standards: OpenID Connect, OAuth 2.0, SAML 2.0 supported natively
- Extensibility: themes, SPIs, identity federation
- Community: CNCF project, active Red Hat maintenance
- Isolation: one realm per tenant, no shared data

### Negative

- Operations: Keycloak must be deployed and maintained (updates, realm backup,
  monitoring)
- Complexity: realm, client, and role configuration is non-trivial
- `keycloak-js`: the official client library has occasionally unstable APIs
  between major versions

## Re-evaluation conditions

This decision should be re-evaluated if:

- A European managed identity service emerges with HDS certification
- Keycloak maintenance burden becomes disproportionate
- `keycloak-js` is deprecated in favor of a generic OIDC client

## References

- Keycloak: <https://www.keycloak.org/>
- keycloak-js: <https://www.keycloak.org/docs/latest/securing_apps/index.html#_javascript_adapter>
