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

The orchestrator drives the agentic loop (think → call → execute → repeat), bounded by an
iteration cap and per-result context guards, stamping usage on completion:

```csharp
var result = await orchestrator.RunAsync(new AIOrchestrationRequest
{
    WorkspaceName = "support-chat",
    Messages = [new ChatMessage(ChatRole.User, "What changed last week?")],
});
```

Bounds are configured under `AI:Tools:Orchestration` (`MaxIterations`,
`MaxToolResultCharacters`).

The orchestrator system prompt is composed, in strict precedence, as **framework guardrails
(code-first, versioned, non-editable) + workspace prompt + user custom context + per-tool
instructions**. The guardrail version is stamped into the `AIUsageRecord` for auditability
without persisting prompt content.

## Dependencies

- `Granit`
- `Granit.AI`

## Documentation

See the [full documentation](https://granit-fx.dev).
