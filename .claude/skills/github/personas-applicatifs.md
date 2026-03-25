# Application Personas — Granit

Canonical persona registry for user stories across the `granit-dotnet` repository.

**Status**: Approved | **Version**: 1.0 | **Date**: 2026-03-16
**Scope**: `granit-dotnet`, `granit-front`
**Canonical location**: `granit-dotnet/docs/guide/personas-applicatifs.md`

## Usage rules

1. **Use exclusively** the canonical personas below in user stories (`As a [persona]`)
2. **Never introduce** a new persona without prior validation and addition to this registry
3. **Never use** hybrid roles — choose the primary persona for the need
4. **Context** (on-call, audit, incident) belongs in the story body, not in the persona
5. **One person** can carry multiple personas (e.g. a tech lead can be both Développeur and Architecte)

## Technical personas

Personas who build applications with Granit packages.

### Développeur

| Field | Value |
|---|---|
| **Canonical name** | Développeur |
| **Description** | Writes application code consuming Granit packages. Primary user of the framework. |
| **Responsibilities** | Module integration, DI configuration, endpoint implementation, unit tests |
| **Absorbed synonyms** | Backend developer, fullstack developer, Guava developer, new team member |
| **Usage** | ~80% of Granit stories |

### Architecte

| Field | Value |
|---|---|
| **Canonical name** | Architecte |
| **Description** | Designs system architecture, selects Granit modules, defines integration patterns |
| **Responsibilities** | Module selection, dependency graph, cross-cutting concerns, ADRs |
| **Absorbed synonyms** | Solution architect, tech lead, technical decision maker |
| **Usage** | Module design stories, multi-module integration, architectural constraints |

### Ingénieur DevOps

| Field | Value |
|---|---|
| **Canonical name** | Ingénieur DevOps |
| **Description** | Deploys, monitors, and operates applications built with Granit |
| **Responsibilities** | CI/CD pipelines, secret management, health checks, observability configuration |
| **Absorbed synonyms** | DevOps, platform engineer |
| **Usage** | Vault, Diagnostics, Observability, CI/CD stories |

### RSSI

| Field | Value |
|---|---|
| **Canonical name** | RSSI |
| **Description** | Responsible for information system security, pilots ISO 27001 / GDPR compliance |
| **Responsibilities** | Security policies, audit, encryption at rest and in transit |
| **Absorbed synonyms** | Security officer, compliance officer, CISO |
| **Usage** | Security, Encryption, Audit trail stories |

### DPO

| Field | Value |
|---|---|
| **Canonical name** | DPO |
| **Description** | Data Protection Officer, pilots GDPR compliance |
| **Responsibilities** | Data minimization, right to erasure, pseudonymization, DPIA |
| **Absorbed synonyms** | RSSI/DPO (for data-only topics) |
| **Usage** | Privacy, MultiTenancy GDPR, Timeline (data retention) stories |

## End-user personas

Personas who use applications built with Granit.

### Visitor

| Field | Value |
|---|---|
| **Canonical name** | Visitor |
| **Description** | Non-authenticated user browsing public content |
| **Responsibilities** | Browsing public pages, consenting to cookies, initiating authentication |
| **Absorbed synonyms** | Anonymous user, unauthenticated user |
| **Usage** | Cookies consent, public endpoints, rate limiting, CORS stories |

### Authenticated User

| Field | Value |
|---|---|
| **Canonical name** | Authenticated User |
| **Description** | Logged-in user with standard application permissions |
| **Responsibilities** | Managing personal data, receiving notifications, using application features |
| **Absorbed synonyms** | User, logged-in user, standard user |
| **Usage** | Notifications, Timeline, Settings, Localization, QueryEngine stories |

### Administrator

| Field | Value |
|---|---|
| **Canonical name** | Administrator |
| **Description** | Admin user managing application configuration and users |
| **Responsibilities** | Feature flag management, reference data, identity administration, tenant configuration |
| **Absorbed synonyms** | App admin, back-office operator, super user |
| **Usage** | Features, ReferenceData, Identity, MultiTenancy admin stories |

### Approver

| Field | Value |
|---|---|
| **Canonical name** | Approver |
| **Description** | Workflow role that validates, approves or rejects transitions |
| **Responsibilities** | Reviewing pending workflow items, approving or rejecting with comments |
| **Absorbed synonyms** | Validator, reviewer, workflow approver |
| **Usage** | Workflow, BackgroundJobs approval stories |

## Persona × module matrix

| Persona | Core | Auth | Workflow | Notifications | Privacy | Vault | Observability |
|---|---|---|---|---|---|---|---|
| Développeur | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Architecte | ✓ | ✓ | ✓ | — | ✓ | ✓ | ✓ |
| Ingénieur DevOps | — | — | — | — | — | ✓ | ✓ |
| RSSI | — | ✓ | — | — | ✓ | ✓ | ✓ |
| DPO | — | — | — | — | ✓ | — | — |
| Visitor | — | ✓ | — | — | ✓ | — | — |
| Authenticated User | — | ✓ | ✓ | ✓ | ✓ | — | — |
| Administrator | — | ✓ | ✓ | ✓ | — | — | — |
| Approver | — | — | ✓ | ✓ | — | — | — |

## Changelog

| Date | Version | Author | Description |
|---|---|---|---|
| 2026-03-16 | 1.0 | JF | Initial registry — 9 canonical personas for granit-dotnet |
