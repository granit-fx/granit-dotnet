---
title: "Frontend ADRs \u2014 Architecture Decision Records"
description: Architecture Decision Records for the Granit TypeScript and React SDK — covering pnpm workspaces, React 19, headless packages, Keycloak, TanStack Query, Vitest, and OpenTelemetry.
sidebar:
  label: Architecture Decision Records
  order: 0
---

Architecture Decision Records (ADRs) for the Granit Frontend SDK document
significant technical decisions made during the development of the TypeScript
and React packages.

Each ADR follows the same template as backend ADRs: Context, Decision,
Alternatives Considered, Justification, Consequences, and Re-evaluation
Conditions.

## ADR index

| # | Title | Status | Date | Scope |
| - | ----- | ------ | ---- | ----- |
| [001](001-source-direct/) | TypeScript Source-Direct — No Build Step | Accepted | 2026-02-27 | All `@granit/*` packages |
| [002](002-pnpm-workspace/) | pnpm Workspace Monorepo | Accepted | 2026-02-27 | granit-front |
| [003](003-react-19/) | React 19 as Minimum Version | Accepted | 2026-02-27 | All React packages |
| [004](004-headless-packages/) | Headless Packages — Hooks Only, UI in Consumer Apps | Accepted | 2026-03-06 | All `@granit/*` packages |
| [005](005-keycloak/) | Keycloak as Authentication Provider | Accepted | 2026-02-27 | @granit/authentication |
| [006](006-tanstack-query/) | TanStack Query for Data Fetching | Accepted | 2026-03-04 | All React data-fetching packages |
| [007](007-vitest/) | Vitest as Test Runner | Accepted | 2026-02-27 | granit-front |
| [008](008-opentelemetry/) | OpenTelemetry for Distributed Tracing | Accepted | 2026-03-04 | @granit/tracing |
| [009](009-branded-types/) | Branded Types for Dates, IDs, and Currencies | Accepted | 2026-04-06 | All `@granit/*` packages |
