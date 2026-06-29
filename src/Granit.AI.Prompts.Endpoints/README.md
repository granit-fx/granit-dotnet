# Granit.AI.Prompts.Endpoints

HTTP endpoints for [Granit.AI.Prompts](../Granit.AI.Prompts): the prompt-catalogue CRUD,
copy-on-customise, and the grouped picker query behind the chat `/` picker, with permission
gating and validation.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.AI.Prompts.Endpoints
```

## Usage

Declare the module on your host module so its `ConfigureServices` runs, then map the endpoints:

```csharp
// Step 1: add the module dependency
[DependsOn(typeof(GranitAIPromptsEndpointsModule))]
public sealed class MyHostModule : GranitModule { }

// Step 2: map the endpoints on your API route group
app.MapGranitPrompts();
```

Persistence is a separate concern: wire `Granit.AI.Prompts.EntityFrameworkCore`
(`builder.AddGranitAIPromptsEntityFrameworkCore(...)`) so the catalogue can persist and load
system/user prompts.

Endpoints are gated by `AIPrompts.Templates.{Read,Manage,Delete}` and scoped to the calling user.
The catalogue is the framework-seeded system prompts plus the caller's own; system prompts are
read-only — customise one (`POST /prompts/{id}/customise`) to get a private, editable copy.
System prompt names and descriptions are stored as localization keys and resolved to the request
culture from the `AIPrompts` resource.

## Endpoints

| Method | Route | Permission | Purpose |
| ------ | ----- | ---------- | ------- |
| GET | `/prompts` | Read | List the catalogue (summaries) |
| GET | `/prompts/picker` | Read | Catalogue grouped by category for the `/` picker |
| GET | `/prompts/{id}` | Read | Get one prompt with its content |
| POST | `/prompts` | Manage | Create a private prompt |
| PUT | `/prompts/{id}` | Manage | Update one of your prompts |
| POST | `/prompts/{id}/customise` | Manage | Copy a system prompt into an editable private one |
| DELETE | `/prompts/{id}` | Delete | Delete one of your prompts |

## Dependencies

- `Granit.AI.Prompts`
- `Granit.Authorization`
- `Granit.Validation`
- `Granit.Http.ApiDocumentation`

## Documentation

See the [full documentation](https://granit-fx.dev).
