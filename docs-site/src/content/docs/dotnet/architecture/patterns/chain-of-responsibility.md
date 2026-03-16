---
title: "Chain of Responsibility"
description: "Ordered handler pipeline for tenant resolution and blob validation in Granit"
sidebar:
  order: 20
---

## Definition

The Chain of Responsibility pattern passes a request along an ordered chain
of handlers. Each handler decides whether to process the request or forward it
to the next one. The first capable handler short-circuits the chain.

## Diagram

```mermaid
sequenceDiagram
    participant P as TenantResolverPipeline
    participant HR as HeaderTenantResolver (100)
    participant JR as JwtClaimTenantResolver (200)
    participant CR as CustomResolver (300)

    P->>HR: ResolveAsync(httpContext)
    alt X-Tenant-Id header present
        HR-->>P: TenantInfo (short-circuit)
    else Header absent
        HR-->>P: null
        P->>JR: ResolveAsync(httpContext)
        alt JWT claim present
            JR-->>P: TenantInfo (short-circuit)
        else Claim absent
            JR-->>P: null
            P->>CR: ResolveAsync(httpContext)
            CR-->>P: TenantInfo or null
        end
    end
```

## Implementation in Granit

### Tenant resolution

| Component | File | Role |
|-----------|------|------|
| `TenantResolverPipeline` | `src/Granit.MultiTenancy/Pipeline/TenantResolverPipeline.cs` | Iterates `ITenantResolver` instances by ascending `Order` |
| `HeaderTenantResolver` | `src/Granit.MultiTenancy/Resolvers/HeaderTenantResolver.cs` | Order=100, reads `X-Tenant-Id` |
| `JwtClaimTenantResolver` | `src/Granit.MultiTenancy/Resolvers/JwtClaimTenantResolver.cs` | Order=200, reads the JWT claim |

### Blob validation

| Component | File | Order | Role |
|-----------|------|-------|------|
| `MagicBytesValidator` | `src/Granit.BlobStorage/Validators/MagicBytesValidator.cs` | 10 | Verifies the actual MIME type |
| `MaxSizeValidator` | `src/Granit.BlobStorage/Validators/MaxSizeValidator.cs` | 20 | Verifies the file size |

The blob validation pipeline is a special case: **all** validators are executed
(no short-circuit), but the order determines error message priority.

## Rationale

The tenant resolution chain allows adding resolvers (query string, cookie,
subdomain) without modifying existing code. The ordering via the `Order`
property is configurable without recompilation.

## Usage example

```csharp
// Add a custom resolver -- inserts into the chain by Order
public sealed class SubdomainTenantResolver : ITenantResolver
{
    public int Order => 50; // Before HeaderTenantResolver (100)

    public Task<TenantInfo?> ResolveAsync(HttpContext context, CancellationToken cancellationToken)
    {
        string host = context.Request.Host.Host;
        // Extract tenant from subdomain...
        return Task.FromResult<TenantInfo?>(tenantInfo);
    }
}

// Registration
services.AddSingleton<ITenantResolver, SubdomainTenantResolver>();
```

## Further reading

- [Chain of Responsibility -- refactoring.guru](https://refactoring.guru/design-patterns/chain-of-responsibility)
