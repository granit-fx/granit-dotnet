# Granit.AI.Tools

The agentic tool seam for Granit AI (ADR-067). Defines `IAITool` — the single
agent-as-tool abstraction — an application-opt-in `IAIToolRegistry`, and automatic
projection of registered tools to `Microsoft.Extensions.AI` `AITool` declarations for
`ChatOptions.Tools`.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.AI.Tools
```

## Usage

Tools are exposed by explicit application registration — never by a framework-wide
attribute. Every tool runs strictly under the calling user's identity and ACLs.

```csharp
services.AddGranitAITools(tools =>
{
    tools.Add<QueryDataTool>();
    tools.Add<SearchTool>();
});
```

The orchestrator emits declarations automatically:

```csharp
chatOptions.Tools = [.. projector.ProjectAll()];
```

## Dependencies

- `Granit`

## Documentation

See the [full documentation](https://granit-fx.dev).
