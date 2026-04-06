---
title: "ADR-009: Branded Types for Dates, IDs, and Currencies"
description: "Use TypeScript branded types in @granit/types to distinguish ISODateString, EntityId, and CurrencyCode from plain strings at compile time"
sidebar:
  order: 9
  label: "009 - Branded Types"
---

> **Date:** 2026-04-06
> **Authors:** Jean-Francois Meyers
> **Scope:** All `@granit/*` packages

## Context

All `@granit/*` packages expose API response types that include date fields,
entity identifiers, and currency codes. Without further qualification, these
are all typed as `string`:

```ts
interface BackgroundJobStatus {
  readonly jobName: string;
  readonly nextExecutionAt: string | null; // date or arbitrary string?
}

function getInvoice(tenantId: string, invoiceId: string): Promise<Invoice> { ... }
// Nothing prevents: getInvoice(invoiceId, tenantId) — compiles, fails at runtime
```

This offers no compile-time distinction between a date string, a UUID, a name,
and a currency code. The result is silent bugs that only surface at runtime.

## Decision

Introduce **branded types** in the `@granit/types` package:

| Type | Brand | Usage |
| ---- | ----- | ----- |
| `ISODateString` | `__brand: 'ISODateString'` | All date fields across API types |
| `EntityId<Brand>` | `__entity: Brand` | All entity identifier fields |
| `UserId`, `TenantId`, `CorrelationId` | Pre-defined `EntityId` aliases | Cross-cutting IDs |
| `CurrencyCode` | Union type (not branded) | ISO 4217 currency codes |

```ts
// @granit/types
export type ISODateString = string & { readonly __brand: 'ISODateString' };
export type EntityId<Brand extends string> = string & { readonly __entity: Brand };
export type UserId = EntityId<'User'>;
export type TenantId = EntityId<'Tenant'>;
export type CurrencyCode = 'EUR' | 'USD' | 'GBP' | ...; // union, not branded
```

Constructors cast plain strings to their branded type with no runtime overhead:

```ts
export function toISODateString(value: string): ISODateString {
  return value as ISODateString;
}

export function toEntityId<Brand extends string>(value: string): EntityId<Brand> {
  return value as EntityId<Brand>;
}
```

### Using `ISODateString` in API types

Replace `string` with `ISODateString` for all date fields across package types:

```ts
// Before
interface BackgroundJobStatus {
  readonly nextExecutionAt: string | null;
  readonly lastExecutionAt: string | null;
}

// After
import type { ISODateString } from '@granit/types';

interface BackgroundJobStatus {
  readonly nextExecutionAt: ISODateString | null;
  readonly lastExecutionAt: ISODateString | null;
}
```

### Using `ISODateString` in application code

When constructing a date from `new Date()` or user input, wrap with
`toISODateString()`:

```ts
import { toISODateString } from '@granit/types';

const now = toISODateString(new Date().toISOString());
const createdAt = toISODateString(apiResponse.createdAt);
```

`ISODateString extends string`, so it is assignable anywhere a `string` is
expected (template literals, comparisons, `JSON.stringify`). The brand only
prevents accidental assignment of an arbitrary `string` where `ISODateString`
is required.

### Using `EntityId` in application code

Per-entity IDs are declared by each domain package using `EntityId<Brand>`:

```ts
// @granit/invoicing
import type { EntityId } from '@granit/types';
export type InvoiceId = EntityId<'Invoice'>;

// @granit/subscriptions
export type SubscriptionId = EntityId<'Subscription'>;
```

Parameter inversion becomes a compile error:

```ts
// Compile error — InvoiceId is not assignable to SubscriptionId
api.cancelSubscription(invoiceId);
```

Cross-cutting IDs (`UserId`, `TenantId`, `CorrelationId`) are declared once in
`@granit/types` and re-used across packages.

### `CurrencyCode` as a union type

Currency codes are represented as a **union type**, not a branded type.
A union provides auto-completion with exact valid values and prevents typos
without requiring a cast constructor:

```ts
const currency: CurrencyCode = 'EUR'; // ✓
const bad: CurrencyCode = 'XX';       // ✗ compile error
```

A branded `string` would allow any string value at the call site (after a cast),
defeating the purpose.

## Alternatives considered

### Option 1: Branded types in `@granit/types` (selected)

- **Advantage**: zero runtime overhead, single source of truth, cross-package
  reuse, brands are erased at compile time
- **Disadvantage**: requires `toISODateString()` / `toEntityId()` cast
  constructors at system boundaries (user input, API deserialization)

### Option 2: Runtime validation with `zod`

- **Advantage**: validates the actual string format at runtime (e.g. regex for
  ISO 8601)
- **Disadvantage**: runtime cost, requires schema definitions per type, overkill
  for values that arrive from trusted API responses

### Option 3: Keep plain `string`

- **Advantage**: zero migration effort
- **Disadvantage**: no compile-time distinction between semantically different
  string values; parameter inversion bugs are silent

### Option 4: Separate `@granit/branded-types` package

- **Advantage**: isolated package
- **Disadvantage**: adds a dependency hop for every consumer; `@granit/types`
  already exists as the home for cross-cutting type utilities

## Justification

| Criterion | Branded types | Zod schemas | Plain string |
| --------- | ------------- | ----------- | ------------ |
| Runtime overhead | None | Per-parse | None |
| Compile-time safety | Full | None | None |
| IDE auto-complete | ✓ | Partial | — |
| Migration effort | Low (cast at boundaries) | High | None |
| Format validation | None | Full | None |

Branded types give maximum compile-time safety with zero runtime cost. Format
validation (e.g. checking that a string is actually an ISO 8601 date) is the
responsibility of the API layer, not the type system.

## Consequences

### Positive

- Parameter inversion on entity IDs is a compile error, not a runtime bug
- Date fields are self-documenting: `nextExecutionAt: ISODateString` vs
  `nextExecutionAt: string`
- `ISODateString extends string` — no breaking change for existing consumers
  that pass dates as plain strings to template literals, comparisons, etc.
- `CurrencyCode` union provides auto-completion and prevents invalid codes

### Negative

- Cast constructors (`toISODateString`, `toEntityId`) are required at system
  boundaries (mock data, test fixtures, API deserialization)
- TypeScript's `noUncheckedIndexedAccess` interacts with branded types in
  array mutations — use `find()` + direct property mutation instead of
  `array[idx] = { ...spread }`

## Re-evaluation conditions

This decision should be re-evaluated if:

- TypeScript introduces a native `nominal` keyword that makes explicit casts
  unnecessary
- A Zod-based validation layer is introduced at the API response level,
  making branded constructors redundant

## References

- TypeScript handbook — template literal types: <https://www.typescriptlang.org/docs/handbook/2/template-literal-types.html>
- `@granit/types` source: `packages/@granit/types/src/`
