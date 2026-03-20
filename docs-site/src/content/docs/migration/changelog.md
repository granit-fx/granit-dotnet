---
title: Changelog
description: Changelog format, conventions, and how to read Granit version history
sidebar:
  order: 1
---

Granit maintains a `CHANGELOG.md` file in the repository root following the
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) format combined with
[Conventional Commits](https://www.conventionalcommits.org/).

## Where to find it

The canonical changelog lives at the repository root:

```text
granit-dotnet/
  CHANGELOG.md    <-- version history
  src/
  tests/
  ...
```

## Format conventions

Each release section uses the following structure:

```markdown
## [1.2.0] - 2026-03-10

### Added
- feat(notifications): add WhatsApp delivery channel (`Granit.Notifications.WhatsApp`)
- feat(templating): add Scriban `date.to_iso` custom function

### Changed
- refactor(persistence): improve `AuditedEntityInterceptor` batching performance

### Deprecated
- deprecate(caching): `IDistributedCacheManager.Remove()` -- use `RemoveAsync()` instead

### Fixed
- fix(identity): correct Keycloak token refresh race condition
- fix(blob-storage): handle S3 multipart upload timeout on large files

### Security
- security(vault): rotate transit encryption keys on startup when key age exceeds threshold

### Breaking Changes
- breaking(core): rename `IGranitModule.Initialize()` to `OnApplicationInitialization()`
  - **Migration**: rename the method override in your module classes
```

### Categories

| Category | When to use |
| -------- | ----------- |
| **Added** | New features, new packages, new public APIs |
| **Changed** | Non-breaking changes to existing functionality |
| **Deprecated** | APIs marked with `[Obsolete]`, scheduled for removal |
| **Removed** | APIs removed (major versions only) |
| **Fixed** | Bug fixes |
| **Security** | Vulnerability patches, security improvements |
| **Breaking Changes** | Changes that require consumer action (major versions only) |

### Scope conventions

The scope in parentheses maps to the Granit module area:

| Scope | Packages |
| ----- | -------- |
| `core` | `Granit.Core`, `Granit.Timing`, `Granit.Guids` |
| `persistence` | `Granit.Persistence`, `Granit.Persistence.Migrations` |
| `caching` | `Granit.Caching`, `Granit.Caching.StackExchangeRedis` |
| `identity` | `Granit.Identity`, `Granit.Identity.Keycloak`, `Granit.Identity.EntityFrameworkCore` |
| `notifications` | All `Granit.Notifications.*` packages |
| `templating` | `Granit.Templating`, `Granit.Templating.Scriban`, `Granit.DocumentGeneration` |
| `blob-storage` | `Granit.BlobStorage`, `Granit.BlobStorage.S3` |
| `security` | `Granit.Security`, `Granit.Authentication.*`, `Granit.Authorization.*` |
| `vault` | `Granit.Vault`, `Granit.Encryption` |
| `observability` | `Granit.Observability`, `Granit.Diagnostics` |
| `workflow` | All `Granit.Workflow.*` packages |
| `data-exchange` | All `Granit.DataExchange.*` packages |
| `localization` | All `Granit.Localization.*` packages |

## Reading version numbers

Given version `1.2.3`:

- **1** (major) -- incrementing this means breaking changes exist. Read the
  "Breaking Changes" section carefully.
- **2** (minor) -- new features were added. Your existing code continues to work
  without changes.
- **3** (patch) -- bug fixes only. Safe to upgrade without reviewing the
  changelog in detail.

## Automation

Changelog entries are derived from Conventional Commit messages. The commit
types map directly to changelog categories:

| Commit prefix | Changelog category |
| ------------- | ------------------ |
| `feat:` | Added |
| `refactor:`, `perf:` | Changed |
| `fix:` | Fixed |
| `security:` | Security |
| `docs:` | Not included (documentation-only changes) |
| `test:`, `ci:`, `chore:` | Not included |

Commits with a `BREAKING CHANGE:` footer or a `!` after the type (e.g.,
`feat!:`) are additionally listed under **Breaking Changes**.
