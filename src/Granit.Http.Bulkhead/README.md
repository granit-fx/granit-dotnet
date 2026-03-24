# Granit.Http.Bulkhead

Per-tenant bulkhead isolation for Granit APIs. Limits concurrent operations per tenant
using `System.Threading.RateLimiting.ConcurrencyLimiter` (built-in .NET). Prevents a
single tenant from monopolizing server resources (CPU, memory, threads). ASP.NET Core
endpoint filter (503 Service Unavailable) and Wolverine middleware.

Part of the [granit](https://granit-fx.dev) framework.

## Key characteristics

- **In-memory per pod** — each application instance maintains its own concurrency
  limiters. With N pods and `PermitLimit=P`, a tenant can use up to N×P concurrent
  operations cluster-wide. This is by design: the bulkhead protects local CPU/memory.
  For distributed rate limiting, use `Granit.RateLimiting` (Redis).
- **Automatic bypass** — machine actors (`ActorKind.System`, `ActorKind.ExternalSystem`)
  always bypass the bulkhead. Configurable `BypassRoles` for admin users.
- **Plan-based quotas** — optional `Granit.Features` integration resolves `PermitLimit`
  dynamically per SaaS plan (Basic: 5, Pro: 20, Enterprise: 50).
- **Queue support** — configurable `QueueLimit` and `QueueTimeout` per policy. Requests
  wait in queue when all concurrency slots are occupied.

## Installation

```bash
dotnet add package Granit.Http.Bulkhead
```

## Quick start

### Configuration

```json
{
  "Bulkhead": {
    "Enabled": true,
    "BypassRoles": ["SystemAdmin"],
    "Policies": {
      "api": { "PermitLimit": 20, "QueueLimit": 10, "QueueTimeout": "00:00:30" },
      "import": { "PermitLimit": 2, "QueueLimit": 5, "QueueTimeout": "00:01:00" },
      "report-generation": { "PermitLimit": 3, "QueueLimit": 0 }
    }
  }
}
```

### Module registration

```csharp
[DependsOn(typeof(GranitHttpBulkheadModule))]
public class AppModule : GranitModule { }
```

### ASP.NET Core endpoint filter

```csharp
app.MapGet("/api/reports", handler)
   .RequireGranitBulkhead("report-generation");
```

### Wolverine middleware

```csharp
[Bulkhead("import")]
public record ImportDataCommand(Guid TenantId, Stream Data);

// Register in Wolverine setup:
opts.Policies.AddMiddleware<BulkheadMiddleware>(
    chain => chain.MessageType
        .GetCustomAttributes(typeof(BulkheadAttribute), true).Length > 0);
```

## Dependencies

- `Granit`
- `Granit.Http.ExceptionHandling`
- `Granit.Features`
- `Granit.Users`

## Documentation

See the [full documentation](https://granit-fx.dev).
