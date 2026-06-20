# Granit.Mentions

The `@` mention picker, built **on** [`Granit.DataLookup`](../Granit.DataLookup/README.md) with no
changes to that framework and **no mention-specific contract**. A mention is just a lookup source
opted into the picker — so any entity already exposed as a lookup (via its `QueryDefinition` or a
queryable) becomes mentionable with a single line and **zero new classes**.

## How it works

- A single facade `ILookupSource` named `mentions` is registered (`AddGranitMentions`).
- `AddMentionSource("users")` tags an existing lookup source as mentionable. Source names follow the
  DataLookup convention: a plural, kebab-case collection noun (`users`, `tenants`, `ref-countries`).
- The facade fans the picker out across the tagged sources (optionally narrowed by `scope.type`),
  applies **lenient** per-type authorization (an unauthorized type is skipped, never a 403 for the
  whole picker), merges and caps, and re-stamps each item's value as a composite `type:value` so one
  source resolves any type.

## Host wiring

Mentions are served by the **`Granit.DataLookup` endpoints** — there is no mention-specific
endpoint. A host that wants the `@` picker:

```csharp
// 1. Expose each entity as a lookup, then tag it mentionable.
services.AddQueryDefinitionLookup<Invoice, MyDbContext>();   // Granit.DataLookup.EntityFrameworkCore
services.AddMentionSource("invoices");
services.AddMentionSource("users");   // the "users" lookup is auto-registered by AddGranitIdentityEntityFrameworkCore — @user

// 2. Map the DataLookup endpoints (this is what serves the picker).
app.MapGranitDataLookups();
```

## Endpoints

| Purpose | Route |
| ------- | ----- |
| Search the picker | `GET /lookups/mentions?search=<q>&scope.type=<optional type>` |
| Resolve a selection | `GET /lookups/mentions/resolve?value=<type>:<id>` |
| List mentionable + other sources | `GET /lookups` |

Each suggestion's `value` is the composite `type:id` (e.g. `users:3f2a…`) and `extra.type` carries
the type — the front sends `type:id` back, and an AI-chat turn carries it as a `MentionRequest`. AI
chat resolves through the same facade and injects the result wrapped in the untrusted-document
envelope.

## Authorization

Two layers: the whole `/lookups` group requires `DataLookup.Lookups.Read` (access to pickers), and
each tagged source's own `RequiredPermission` is checked **leniently** inside the facade — an
unauthorized type is dropped from the results, never a 403 for the whole picker. For `@user` the
per-type gate is `Identity.Users.Read`.

## Why no `IMentionResolver`

A mention and a lookup are the same primitive (a typed, searchable, ACL-bound reference). Reusing
`ILookupSource` means: one contract, one registry, and automatic registration via the existing
DataLookup adapters — instead of a hand-written resolver class per entity. Bespoke cases (a
non-EF/computed source) just implement `ILookupSource` directly and get tagged.

## Documentation

See the [full documentation](https://granit-fx.dev).
