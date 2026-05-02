# Granit.Activities.Abstractions

Inter-module contracts for `Granit.Activities` — the cross-entity polymorphic
to-do module specified by [ADR-046][adr-046]. Pull this lightweight package
from any module that:

- declares `.Activities(...)` on its `EntityDefinition`, or
- contributes domain-specific activity types (Quote, Onboarding, …) via
  `IActivityTypeProvider`.

Pull the heavier `Granit.Activities` runtime module only from hosts that
actually resolve and persist activities.

## What's inside

| Type | Role |
| ---- | ---- |
| `ActivityType` | Immutable descriptor: `Name`, `Icon`, `DisplayKey`, optional `DefaultDurationMinutes` |
| `IActivityTypeProvider` | IoC contributor (per [ADR-045][adr-045]) — modules graft their domain-specific types onto the framework catalog |
| `IActivityRegistry` | Read-only view over the aggregated catalog; runtime impl ships in `Granit.Activities` |
| `StandardActivityTypes` | Framework starter catalog: `ToDo`, `Call`, `Meeting`, `Email` |
| `StandardActivityTypeProvider` | `IActivityTypeProvider` exposing the standard catalog; auto-registered by the runtime module |
| `AddActivityTypeProvider<T>()` | DI extension for cross-module providers |

## Example — contributing a domain-specific type

```csharp
// Granit.Sales/Activities/SalesActivityTypeProvider.cs
public sealed class SalesActivityTypeProvider : IActivityTypeProvider
{
    public IEnumerable<ActivityType> Provide() =>
    [
        new ActivityType("Quote", "file-text", "Activity:Quote"),
        new ActivityType("Demo",  "monitor",   "Activity:Demo", DefaultDurationMinutes: 60),
    ];
}

// Granit.Sales/GranitSalesModule.cs
public override void ConfigureServices(ServiceConfigurationContext ctx) =>
    ctx.Services.AddActivityTypeProvider<SalesActivityTypeProvider>();
```

Activity types from providers that are not loaded silently drop — entities
that opt into them via `AllowedTypes(...)` simply never surface the option.

[adr-045]: ../../docs-site/src/content/docs/dotnet/architecture/adr/045-contributor-pattern.md
[adr-046]: ../../docs-site/src/content/docs/dotnet/architecture/adr/046-activities-vs-timeline.md
