---
title: "ADR-002: pnpm Workspace Monorepo"
description: "Use pnpm as the exclusive package manager for the monorepo with workspace:* protocol for internal dependencies"
sidebar:
  order: 2
  label: "002 - pnpm Workspace"
---

> **Date:** 2026-02-27
> **Authors:** Jean-Francois Meyers
> **Scope:** granit-front

## Context

The Granit frontend framework is composed of multiple packages with
inter-package dependencies. The package manager must:

- Manage a multi-package workspace with local symbolic links
- Efficiently resolve shared dependencies (hoisting)
- Offer fast resolution and installation performance
- Support the `workspace:*` protocol for internal dependencies

## Decision

Use **pnpm** as the exclusive package manager for the monorepo.

Configuration in `pnpm-workspace.yaml`:

```yaml
packages:
  - 'packages/@granit/*'
```

Inter-package dependencies use `workspace:*` in `peerDependencies`:

```json
{
  "peerDependencies": {
    "@granit/api-client": "workspace:*"
  }
}
```

## Alternatives considered

### Option 1: pnpm (selected)

- **Advantage**: content-addressable store, isolated `node_modules`, native
  `workspace:*` protocol, fast installs
- **Disadvantage**: less widespread than npm, pnpm-specific lock file

### Option 2: npm workspaces

- **Advantage**: zero additional tooling (ships with Node.js)
- **Disadvantage**: aggressive hoisting creates phantom dependencies, no
  `workspace:*` protocol, slower performance

### Option 3: Yarn Berry (PnP)

- **Advantage**: Plug'n'Play eliminates `node_modules`, zero-installs
- **Disadvantage**: PnP complicates Vite and TypeScript tool integration,
  zero-installs bloat the repository

## Justification

| Criterion | pnpm | npm workspaces | Yarn Berry |
| --------- | ---- | -------------- | ---------- |
| Dependency isolation | Strict (non-flat) | Hoisted (phantom deps) | Strict (PnP) |
| Install performance | Fast (content-addressable) | Moderate | Fast (zero-install) |
| `workspace:*` protocol | Native | Not supported | Supported |
| Vite compatibility | Works out of the box | Works | Requires PnP configuration |
| Cross-package commands | `pnpm -r exec`, `--filter` | `npm -ws exec` | `yarn workspaces foreach` |
| Community adoption | Growing rapidly | Universal | Declining |

## Consequences

### Positive

- Strict isolation: no phantom dependencies thanks to pnpm's non-flat `node_modules`
- Performance: content-addressable store shared across projects, near-instant
  installs after the first run
- `workspace:*`: automatic resolution of local packages with version replacement at publication
- `pnpm -r exec`: command execution across all packages (lint, tsc)
- `--filter`: targeted execution on a specific package

### Negative

- Adoption: pnpm is less widespread than npm, which may surprise new contributors
- Lock file: `pnpm-lock.yaml` is incompatible with other managers, imposing
  pnpm on all contributors
- Overrides: pnpm-specific syntax in `package.json` (`pnpm.overrides`)

## Re-evaluation conditions

This decision should be re-evaluated if:

- npm workspaces gains native `workspace:*` support and strict isolation
- A new package manager emerges with compelling advantages over pnpm
- The pnpm project is abandoned or maintenance slows significantly

## References

- pnpm: <https://pnpm.io/>
- pnpm workspaces: <https://pnpm.io/workspaces>
