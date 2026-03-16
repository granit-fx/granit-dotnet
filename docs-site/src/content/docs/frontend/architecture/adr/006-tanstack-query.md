---
title: "ADR-006: TanStack Query for Data Fetching"
description: "Use TanStack Query v5 as the data fetching layer with intelligent caching, structured query keys, and typed mutations"
sidebar:
  order: 6
  label: "006 - TanStack Query"
---

> **Date:** 2026-03-04
> **Authors:** Jean-Francois Meyers
> **Scope:** All `@granit/react-*` packages with data fetching

## Context

The data-fetching React packages (`@granit/react-querying`,
`@granit/react-data-exchange`, `@granit/react-authorization`,
`@granit/react-authentication-api-keys`, etc.) need a caching and
synchronization layer with:

- **Intelligent cache**: avoid duplicate requests, targeted invalidation
- **Pagination**: native support for page/pageSize pagination
- **Mutations**: optimistic updates with cache invalidation
- **Retry and refetch**: network error resilience
- **DevTools**: cache inspection during development

## Decision

Use **TanStack Query v5** (`@tanstack/react-query ^5.0.0`) as the data
fetching layer. The dependency is declared in `peerDependencies`:

```json
{
  "peerDependencies": {
    "@tanstack/react-query": "^5.0.0"
  }
}
```

Public hooks (`usePermissions`, `useApiKeys`, `useIdentityCapabilities`, etc.)
encapsulate `useQuery` and `useMutation` from TanStack Query.

## Alternatives considered

### Option 1: TanStack Query v5 (selected)

- **Advantage**: rich API, typed query keys, mutations with invalidation,
  complete DevTools, large adoption, Orval compatibility
- **Disadvantage**: peer dependency, ~13 kB gzip, learning curve

### Option 2: SWR (Vercel)

- **Advantage**: simpler API, smaller bundle
- **Disadvantage**: no structured mutations, no typed query keys, limited
  DevTools, less suitable for complex CRUD operations

### Option 3: Custom hooks (useState + useEffect)

- **Advantage**: zero dependency, full control
- **Disadvantage**: reinventing cache, deduplication, pagination, and
  invalidation is a major effort with subtle bug risks

## Justification

| Criterion | TanStack Query | SWR | Custom hooks |
| --------- | -------------- | --- | ------------ |
| Cache management | Excellent | Good | Manual |
| Query key typing | Native | Manual | Manual |
| Mutation support | `useMutation` + invalidation | Manual | Manual |
| Pagination | Built-in | Partial | Manual |
| DevTools | Complete | Basic | None |
| Bundle size | ~13 kB gzip | ~4 kB gzip | 0 kB |
| Ecosystem (Orval) | Compatible | Partial | N/A |

## Consequences

### Positive

- Shared cache: all queries share the same `QueryClient`, enabling
  cross-invalidation
- Structured query keys: key factories (`permissionKeys`, `apiKeyKeys`)
  ensure key consistency across hooks
- Typed mutations: `useMutation` with `onSuccess` / `onError` / `onSettled`
  for business flows (export, import, workflow transitions)
- DevTools: cache inspection, query replay in development
- Ecosystem: compatible with Orval for automatic hook generation

### Negative

- Peer dependency: consumer applications must install and configure
  `@tanstack/react-query` (QueryClientProvider)
- Bundle size: ~13 kB gzip (acceptable for an SPA)
- Learning curve: concepts of stale time, gc time, invalidation, and query
  keys can be complex for new contributors

## Re-evaluation conditions

This decision should be re-evaluated if:

- React introduces a built-in data fetching primitive that replaces TanStack Query
- TanStack Query v6 introduces breaking changes that require major migration effort
- A significantly lighter alternative emerges with equivalent features

## References

- TanStack Query: <https://tanstack.com/query>
- Query key factories: <https://tkdodo.eu/blog/effective-react-query-keys>
