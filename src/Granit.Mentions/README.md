# Granit.Mentions

Domain-neutral `@` mention seam: a typed, searchable, ACL-bound entity reference consumed by AI chat,
Timeline, and other features. A mention isn't an AI concept — the AI is just one consumer (it injects
a resolved mention into the prompt as untrusted context).

The picker rides on [`Granit.DataLookup`](../Granit.DataLookup/README.md): a single `mentions` facade
source exposes every opted-in resolver through `GET /lookups/mentions`, with **no changes to the
DataLookup framework**.

## Concepts

- **`IMentionResolver`** — one mentionable type (`user`, `invoice`, …). Searches candidates
  (`MentionSuggestion`) and resolves a chosen reference to a `MentionTarget`. Declares an optional
  `RequiredPermission`.
- **`IMentionRegistry`** — the opted-in resolvers for the current scope, keyed by type.
- **`IMentionAuthorizer`** — enforces each resolver's `RequiredPermission` (default:
  `PermissionMentionAuthorizer` over `IPermissionChecker`). Shared by the picker and AI chat.
- **`MentionLookupSource`** — the `ILookupSource` facade (`Name = "mentions"`): fans out the query
  across resolvers (optionally narrowed by `scope.type`), applies **lenient** per-type auth (an
  unauthorized type is skipped, never a 403 for the whole picker), merges and caps, and encodes the
  chosen reference as a composite `type:id` value so one source resolves any type.

## Usage

Add resolvers via the builder; the registry and `mentions` facade are wired automatically:

```csharp
services.AddGranitMentions(b => b.Add<InvoiceMentionResolver>());
```

Front-end picker:

```text
GET /lookups/mentions?search=<q>&scope.type=<optional type>
```

Selecting a suggestion yields the composite value `type:id`; resolve rehydrates it via
`GET /lookups/mentions/resolve?value=type:id`.

## Authoring a resolver

Implement `IMentionResolver` in a bridge package referencing both `Granit.Mentions` and the source
module — e.g. [`Granit.Identity.Mentions`](../Granit.Identity.Mentions/README.md) exposes `@user`.

## Documentation

See the [full documentation](https://granit-fx.dev).
