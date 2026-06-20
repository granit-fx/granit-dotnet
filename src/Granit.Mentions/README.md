# Granit.Mentions

The `@` mention picker, built **on** [`Granit.DataLookup`](../Granit.DataLookup/README.md) with no
changes to that framework and **no mention-specific contract**. A mention is just a lookup source
opted into the picker — so any entity already exposed as a lookup (via its `QueryDefinition` or a
queryable) becomes mentionable with a single line and **zero new classes**.

## How it works

- A single facade `ILookupSource` named `mentions` is registered (`AddGranitMentions`).
- `AddMentionSource("user")` tags an existing lookup source as mentionable.
- The facade fans the picker out across the tagged sources (optionally narrowed by `scope.type`),
  applies **lenient** per-type authorization (an unauthorized type is skipped, never a 403 for the
  whole picker), merges and caps, and re-stamps each item's value as a composite `type:value` so one
  source resolves any type.

## Usage

Expose an entity as a lookup (reusing its `QueryDefinition`), then tag it mentionable:

```csharp
services.AddQueryDefinitionLookup<Invoice, MyDbContext>();   // Granit.DataLookup.EntityFrameworkCore
services.AddMentionSource("invoice");
```

Front-end picker:

```text
GET /lookups/mentions?search=<q>&scope.type=<optional type>
```

Selecting a suggestion yields the composite value `type:value`; resolve rehydrates it via
`GET /lookups/mentions/resolve?value=type:value`. AI chat resolves the same way and injects the
result into the turn wrapped in the untrusted-document envelope.

## Why no `IMentionResolver`

A mention and a lookup are the same primitive (a typed, searchable, ACL-bound reference). Reusing
`ILookupSource` means: one contract, one registry, and automatic registration via the existing
DataLookup adapters — instead of a hand-written resolver class per entity. Bespoke cases (a
non-EF/computed source) just implement `ILookupSource` directly and get tagged.

## Documentation

See the [full documentation](https://granit-fx.dev).
