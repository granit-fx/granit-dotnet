---
title: "ADR-004: Headless Packages — Hooks Only, UI in Consumer Apps"
description: "Framework packages export only hooks, types, providers, and utilities — no React components"
sidebar:
  order: 4
  label: "004 - Headless Packages"
---

> **Date:** 2026-03-06
> **Authors:** Jean-Francois Meyers
> **Scope:** All `@granit/*` packages

## Context

Initially, the monorepo contained UI packages that exposed React components
(buttons, modals, tables, etc.) alongside business hooks. This approach caused
several problems:

- **Tight coupling** between business logic and visual presentation
- **Design divergence**: different consumer applications use different design
  systems (components, tokens, spacing)
- **Duplication**: each application ended up wrapping UI components to adapt
  them to its design system
- **Heavy UI dependencies**: Tailwind CSS, lucide-react, shadcn/ui had to be
  aligned between the framework and applications

A refactoring (2026-03-06) eliminated the UI layer from the framework.

## Decision

The `@granit/*` packages are strictly **headless**: they export only hooks,
types, providers, and utility functions. No React component (JSX) is exported
by the framework.

UI components live exclusively in consumer applications, each implementing its
own design system.

## Alternatives considered

### Option 1: Headless packages (selected)

- **Advantage**: clean separation between logic and presentation, no UI
  dependency in the framework, stable TypeScript API contract
- **Disadvantage**: component duplication across consumer applications

### Option 2: Shared component library with theming

- **Advantage**: consistent UI across applications, single implementation
- **Disadvantage**: theme configuration complexity, design system lock-in,
  heavy dependencies in the framework

### Option 3: Headless + optional UI package

- **Advantage**: best of both worlds — headless core with optional UI
- **Disadvantage**: double maintenance burden, risk of UI package becoming
  mandatory over time, version alignment complexity

## Justification

| Criterion | Headless | Shared components | Headless + optional UI |
| --------- | -------- | ----------------- | --------------------- |
| Logic/UI separation | Complete | Coupled | Complete |
| Design freedom | Full | Constrained | Full |
| Framework dependencies | Minimal | Heavy (Tailwind, etc.) | Minimal core |
| Test complexity | Low (hooks only) | Higher (DOM, styles) | Low core |
| Component duplication | Yes | No | Partial |
| Maintenance | Low | High | Medium |

## Consequences

### Positive

- Clean separation between logic (framework) and presentation (application)
- Design freedom: each application chooses its design system without constraint
- Fewer dependencies: packages no longer need Tailwind, lucide-react, or any
  component library
- Simplified testing: testing hooks is simpler than testing components (no DOM,
  no styles)
- Stable API: the public interface is a TypeScript contract (types + hook
  signatures), independent of visual rendering

### Negative

- Component duplication: consumer applications implement their own table,
  export modal, etc.
- No cross-app UI consistency: the framework does not guarantee visual
  homogeneity between applications
- Initial effort: each new application must implement the UI layer for each
  `@granit/*` package it uses

## Re-evaluation conditions

This decision should be re-evaluated if:

- All consumer applications converge on the same design system
- A headless component library (e.g. Radix, Ark UI) provides sufficient
  abstraction to share UI without design coupling
- The duplication cost exceeds the coupling cost

## References

- Headless UI pattern: <https://www.merrickchristensen.com/articles/headless-user-interface-components/>
- TanStack Table (headless reference): <https://tanstack.com/table>
