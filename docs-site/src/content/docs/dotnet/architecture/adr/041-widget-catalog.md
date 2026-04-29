---
title: "ADR-041: Widget catalog and naming convention"
description: "Granit ships a closed catalog of standard widget names that the manifest exposes for form fields and detail panels (text, money, date, lookup, file, textarea, select, …). The backend declares names; the front interprets them. App-custom widgets are namespaced as `custom:<app-prefix>-<name>` to avoid collisions and signal to the renderer that no built-in implementation exists."
sidebar:
  order: 41
  label: "041 - Widget Catalog"
---

> **Date:** 2026-04-30
> **Authors:** Jean-Francois Meyers
> **Scope:** granit-dotnet (`Granit.Entities` form / detail builders); granit-front (`@granit/entities-react` renderer)
> **Epic:** [#1506](https://github.com/granit-fx/granit-dotnet/issues/1506) — Refonte UI Hybride
> **Story:** [#1521](https://github.com/granit-fx/granit-dotnet/issues/1521) — ADR-041 Widget catalog
> **Status:** Accepted

## Context

[ADR-040](./040-three-tier-metadata-architecture) locked the rule: the backend declares a typed manifest, the React renderer consumes it. For each form field and detail-panel cell, the manifest needs to communicate **how to render** without describing HTML, CSS, or React internals — the backend never knows the front's implementation.

Two anti-patterns to avoid:

- **Frappe** ships ~30 `fieldtype` strings (`Data`, `Currency`, `Link`, `Table`, `Long Text`, …) baked into the platform. Adding a new fieldtype requires patching the framework. Custom apps that need novel widgets either monkey-patch the JS bundle or fall back to `Code` fields with hand-rolled scripts — both routes broken.
- **Odoo** uses XML `widget="..."` attributes that map to JS components in a registry, but the registry is open and untyped: any string works at write time, only fails at render time. Misspellings silently render the default widget; tenant customization through Studio adds new widget references that may not exist on every install.

Granit needs the closed-set ergonomics of Frappe (predictable, type-safe, testable) **and** an open extension point for app-specific widgets — without inviting the runtime-script Pandora's box.

## Decision

### 1. Standard widget catalog (closed set, framework-owned)

The framework ships a fixed catalog of **standard widget names** that every Granit React renderer is expected to support out of the box. The backend declares a name; the front maps it to a concrete component. Both sides own this catalog as a contract.

| Name | Semantics | Rendered as | Default config |
| ---- | --------- | ----------- | -------------- |
| `text` | Single-line free text | `<input type="text">` | maxLength from validator |
| `textarea` | Multi-line free text | `<textarea>` | rows: 4 (configurable) |
| `richtext` | WYSIWYG long-form text | rich editor (e.g. Tiptap) | toolbar: standard |
| `password` | Masked sensitive input | `<input type="password">` | autocomplete off |
| `email` | Email address | `<input type="email">` | format validator |
| `url` | Web URL | `<input type="url">` | format validator |
| `phone` | E.164 phone number | phone input with country picker | locale-aware |
| `number` | Generic numeric | `<input type="number">` | min/max/step from validator |
| `integer` | Whole number | `<input type="number" step="1">` | strict integer parsing |
| `decimal` | Fixed-precision decimal | numeric input | precision/scale config |
| `money` | Currency amount | numeric + symbol | `currencyCode` config (ISO 4217) |
| `percent` | Percentage 0..1 | numeric + `%` suffix | precision config |
| `boolean` | True / false | `<input type="checkbox">` | nullable: tri-state |
| `switch` | Boolean as toggle | toggle component | same as `boolean` |
| `select` | Single choice from fixed options | `<select>` or radio | `options[]` config |
| `multiselect` | Multiple choices from fixed options | multi-select | `options[]` config |
| `status` | Distinct semantic for kanban-aware enum | colored badge / lane-able select | `options[]` with color hints |
| `lookup` | Reference to another entity | typeahead with async fetch | `targetEntity`, `displayField` config |
| `multilookup` | Multiple references | multi-typeahead | same as `lookup` |
| `date` | Date without time | date picker | `min`/`max` config |
| `datetime` | Date + time | datetime picker | timezone config |
| `time` | Time without date | time picker | format config |
| `daterange` | Date range | range picker | same as `date` |
| `file` | Single file (Granit.BlobStorage ref) | file picker + preview | `accept`, `maxSize` config |
| `multifile` | Multiple files | multi-upload | same as `file` |
| `image` | Image (BlobStorage ref) | image picker + thumbnail | same as `file`, `accept: image/*` |
| `signature` | Hand-drawn signature | canvas + save-as-image | stored as BlobStorage ref |
| `color` | Hex color | color picker | `format: "#rrggbb"` |
| `code` | Source code with syntax highlighting | code editor (e.g. Monaco) | `language` config |
| `markdown` | Markdown source with preview | split editor | toolbar: standard |
| `json` | Raw JSON value | JSON editor | schema validation when known |
| `tags` | Free-form tag set | chip input | suggestions from completed values |
| `relation_inline` | Embedded child collection (e.g. invoice lines) | inline grid | `relationName` config |

This is the **v1 catalog**. Adding a new standard name requires an ADR amendment and coordinated change in `@granit/entities-react`. Removing a name is a breaking change.

### 2. Custom widget namespacing — `custom:<app-prefix>-<name>`

When an app needs a widget that isn't in the standard catalog, the backend declares `custom:<app-prefix>-<name>` and the front-end registers a matching component in its `customWidgetRegistry`. Examples:

- `custom:invoicing-line-editor` (the showcase app's bespoke invoice-line grid)
- `custom:crm-opportunity-pipeline` (a Kanban-of-Kanbans for sales)
- `custom:hr-org-chart` (org-chart picker)

**Rules:**

- The `custom:` prefix is mandatory. Any non-prefixed name not in the standard catalog is a hard error in `Granit.Entities.Endpoints` (rejected at boot via the integrity check from story #1541).
- The `<app-prefix>` segment must match `^[a-z][a-z0-9]*$` and identify the owning module / app (avoid collisions across customers).
- The `<name>` segment must match `^[a-z][a-z0-9-]*$` (kebab-case, no underscores).
- The frontend `customWidgetRegistry` is a **typed map** in TypeScript — missing-key access is a compile-time error in the showcase app, not a runtime fallthrough.
- Manifest payload includes `widget: "custom:..."` exactly as-declared; the front looks up the registry, falls back to a deliberately ugly placeholder + console error if missing (so the gap is loud in dev and CI screenshots).

### 3. Widget config — typed via JSON Schema, optional per widget

A widget can carry a config payload alongside its name:

```json
{
  "field": "amount",
  "widget": "money",
  "config": { "currencyCode": "EUR", "precision": 2 }
}
```

Each standard widget has a documented config schema (lives next to the widget in `@granit/entities-react`). The C# fluent builder exposes typed wrappers — `f.Field(x => x.Amount).Money("EUR", precision: 2)` — so the developer never types a JSON blob by hand.

For `custom:` widgets, the backend may pass any opaque JSON object; the front's typed registry validates it.

### 4. Validation surfaces in the schema, not the widget config

Per [ADR-040](./040-three-tier-metadata-architecture)'s Golden Rule, validation lives in Tier A (FluentValidation). The widget config carries **rendering hints** only (currency code, precision, accepted file types); it never declares `required`, `maxLength`, `pattern`, or other constraints. Those flow through the `schema` facet of the manifest (JSON Schema generated by `IJsonSchemaWriter` per #1517 / PR #1599).

This split keeps the contract clean:

- The widget tells the front *how to ask*.
- The schema tells the front (and the back) *what to accept*.

Both are sourced from the same `EntityDefinition`; the manifest emits them in parallel sections.

## Consequences

### Positive

- The standard catalog is **35 names long** and covers the vast majority of admin-CRUD needs (form fields, detail cells, kanban cards). Most apps will never need `custom:`.
- Adding a new standard widget is a deliberate, reviewed decision — caught at ADR review, never accidentally introduced via "let me just hardcode this string."
- The `custom:` namespace is a clean escape hatch with a hard prefix check; misspelling a standard name fails at boot, not at render time.
- Backend and frontend can evolve the catalog in lockstep via shared schema files (a future codegen story can emit TypeScript types from the C# enum).

### Negative / accepted trade-offs

- The catalog is opinionated: there's no `WYSIWYG-with-track-changes`, no `signature-with-handwriting-recognition`, no `voice-input`. Apps that need those use `custom:` — which means writing a React component, not just changing config.
- Tenant admins (Tier B Layer 1, ADR-040) cannot change a field's widget. They reorder, regroup, hide, but the widget choice is locked at compile time. Right boundary for ISO 27001 ("the bookkeeper sees a money input, not a code editor"); restrictive for unstructured personal-productivity scenarios (which Granit doesn't target — see ADR-040).
- Frontend renderer must be implemented for every standard name in v1 before the showcase ships Phase 1. The cobaye selection (Party + Invoice — see #1530) covers ~14 of the 35 standard widgets in real use; the remaining ~21 ship with placeholder renderers and integration tests in `granit-front` Phase 1.

## Cross-references

- [ADR-040](./040-three-tier-metadata-architecture) — Three-tier metadata architecture. Establishes that widget choice is a Tier A concern (compiled), not Tier B (runtime customization).
- [ADR-042](./042-view-catalog) — View catalog. Same closed-set + namespaced-extension pattern, applied to collection display strategies (List, Kanban, Calendar, …).
- [ADR-046](./046-entity-view) — `EntityView`. User-saved views may not change a collection's `kind`, mirroring the rule that user-saved field state may not change a widget.

## References

- Frappe DocField fieldtypes — research compiled in PR #1599 conversation. Closed set, no extension point ⇒ tenant apps work around with `Code` fields and JS overrides.
- Odoo widget registry — open string-based, no compile-time check. Misspellings fall back to the default widget silently.
- Notion property types — fully closed (~25 types), no extension. Same ergonomic dividend as Granit's standard catalog; no story for app-custom widgets — Notion users live within the catalog.
