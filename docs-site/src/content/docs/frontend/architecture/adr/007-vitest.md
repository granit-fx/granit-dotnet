---
title: "ADR-007: Vitest as Test Runner"
description: "Use Vitest for TypeScript unit testing with native ESM support and zero transpilation configuration"
sidebar:
  order: 7
  label: "007 - Vitest"
---

> **Date:** 2026-02-27
> **Authors:** Jean-Francois Meyers
> **Scope:** granit-front

## Context

The monorepo needs a unit test framework capable of:

- Executing TypeScript tests without prior transpilation
- Supporting ESM (`"type": "module"` in `package.json`)
- Offering a performant watch mode for development
- Generating coverage reports (lcov, html)
- Working in a pnpm multi-package workspace

## Decision

Use **Vitest** (v4) as the test framework for all packages in the monorepo:

```bash
pnpm test              # watch mode (development)
pnpm test:coverage     # v8 coverage (CI)
```

Tests are co-located with source code:

- `src/**/*.test.ts` for simple unit tests
- `src/__tests__/` for more complex test suites

The minimum coverage target is 80% on all new code.

## Alternatives considered

### Option 1: Vitest (selected)

- **Advantage**: uses the same transformation pipeline as Vite (esbuild),
  native ESM and TypeScript support, Jest-compatible API, fast file-system-based
  watch mode
- **Disadvantage**: younger ecosystem than Jest, some Jest plugins have no
  Vitest equivalent

### Option 2: Jest

- **Advantage**: industry standard, extensive plugin ecosystem
- **Disadvantage**: experimental and unstable ESM support, TypeScript
  configuration requires `ts-jest` or `@swc/jest`, slow watch mode on large
  workspaces

### Option 3: Node.js native test runner

- **Advantage**: zero dependency, ships with Node.js
- **Disadvantage**: limited API, no watch mode, no coverage integration,
  no mocking framework

## Justification

| Criterion | Vitest | Jest | Node.js test runner |
| --------- | ------ | ---- | ------------------- |
| TypeScript transpilation | esbuild (automatic) | ts-jest / @swc/jest | Manual |
| ESM support | Native | Experimental | Native |
| Watch mode | File-system-based (fast) | Polling (slow) | None |
| API compatibility | Jest-compatible | Standard | Limited |
| Coverage | v8 (built-in) | istanbul / v8 | Experimental |
| Workspace-aware | `vitest.workspace.ts` | Custom config | No |

## Consequences

### Positive

- Zero transpilation configuration: Vitest uses esbuild, same as Vite —
  `.ts` files are transpiled on-the-fly
- Native ESM: no issues with `import`/`export`, `import.meta`, etc.
- Performance: parallel execution, file-system-based watch mode (no polling)
- Jest-compatible API: `describe`, `it`, `expect`, `vi.fn()` — minimal
  learning curve for developers coming from Jest
- v8 coverage: lcov and html reports without additional dependencies
- Workspace-aware: `vitest.workspace.ts` for multi-package configuration

### Negative

- Ecosystem: some Jest plugins have no Vitest equivalent (marginal risk)
- Maturity: Vitest is more recent than Jest (mitigated by massive adoption
  in the Vite ecosystem)

## Re-evaluation conditions

This decision should be re-evaluated if:

- Jest achieves stable ESM support with comparable performance
- Vitest development slows or the project is abandoned
- A new test runner emerges with compelling advantages

## References

- Vitest: <https://vitest.dev/>
- Vitest workspace: <https://vitest.dev/guide/workspace>
