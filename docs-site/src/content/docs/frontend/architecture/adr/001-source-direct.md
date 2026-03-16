---
title: "ADR-001: TypeScript Source-Direct — No Build Step"
description: "Export raw TypeScript source files via package.json exports, relying on Vite for on-the-fly transpilation"
sidebar:
  order: 1
  label: "001 - Source-Direct"
---

> **Date:** 2026-02-27
> **Authors:** Jean-Francois Meyers
> **Scope:** All `@granit/*` packages

## Context

The `@granit/*` packages are consumed exclusively by Vite-based applications.
In local development, packages are linked via `pnpm link:` and resolved through
Vite aliases. Vite transpiles TypeScript on-the-fly via esbuild.

Maintaining a build step (`tsc`, `tsup`, `rollup`) for each package would
introduce:

- An additional `watch` process during development
- A propagation delay for modifications reaching applications
- Configuration complexity (source maps, declaration files, dual CJS/ESM)
- A risk of desynchronization between source and compiled artifacts

## Decision

The `@granit/*` packages export their raw `.ts` source files directly via the
`exports` field in `package.json`:

```json
{
  "exports": {
    ".": "./src/index.ts"
  }
}
```

No `dist/` directory is generated or committed. Consumer applications resolve
imports via Vite aliases pointing to the sources.

A `publishConfig` with `dist/` exports and a `tsup` build step is provided
for npm publication to GitHub Packages.

## Alternatives considered

### Option 1: Source-direct with Vite transpilation (selected)

- **Advantage**: instant HMR, zero build configuration, direct source maps
- **Disadvantage**: coupled to Vite (or any bundler capable of TypeScript
  transpilation)

### Option 2: tsup/rollup watch mode

- **Advantage**: standard npm distribution, compatible with any consumer
- **Disadvantage**: additional `watch` process per package, propagation delay,
  dual CJS/ESM complexity, source map indirection

### Option 3: tsc --watch with project references

- **Advantage**: official TypeScript tooling, incremental compilation
- **Disadvantage**: slow watch mode on large workspaces, declaration file
  management, no tree-shaking

## Justification

| Criterion | Source-direct | tsup/rollup watch | tsc --watch |
| --------- | ------------- | ----------------- | ----------- |
| HMR latency | Instant | ~1-3s per change | ~2-5s per change |
| Dev configuration | None | Per-package config | tsconfig references |
| Source maps | Direct to source | Through dist/ layer | Through dist/ layer |
| Refactoring support | Full (IDE resolves source) | Partial (may resolve dist/) | Good |
| CI type-check | `pnpm tsc --noEmit` | `pnpm tsc --noEmit` | `pnpm tsc --build` |
| npm publication | Requires `tsup` build | Ready | Ready |

## Packages used

| Package | Role |
| ------- | ---- |
| `tsup` | Build step for npm publication only (`publishConfig`) |
| `esbuild` (via Vite) | On-the-fly TypeScript transpilation in development |

## Consequences

### Positive

- Instant HMR: modifications in a package are reflected immediately in the application
- Zero build configuration in development
- Direct source maps to the original code (no intermediate layer)
- Fluid refactoring: TypeScript tools (rename, find references) traverse packages seamlessly
- Simplified CI: `pnpm tsc --noEmit` is sufficient for type checking

### Negative

- Coupled to Vite: consumer applications must use a bundler capable of
  TypeScript source transpilation
- Publication: a build step is required before npm publication, creating two
  consumption modes (source-direct vs dist)
- Compatibility: packages are not directly usable by a Node.js project without
  transpilation

## Re-evaluation conditions

This decision should be re-evaluated if:

- A consumer application needs to use a bundler that cannot transpile TypeScript (unlikely)
- The number of packages grows beyond a point where Vite's on-the-fly transpilation
  becomes a performance bottleneck
- TypeScript natively supports running `.ts` files without transpilation (Node.js `--strip-types`)

## References

- Vite TypeScript support: <https://vite.dev/guide/features#typescript>
- tsup: <https://tsup.egoist.dev/>
