---
title: "Expression Trees"
description: "How Granit dynamically builds EF Core query filters using expression trees for marker interfaces"
sidebar:
  order: 49
---

## Definition

Expression Trees allow building queries or filters at runtime as a syntax tree,
instead of writing them statically in source code. EF Core translates these
trees into SQL.

In Granit, `ApplyGranitConventions()` dynamically builds EF Core query filters
for each entity by combining `ISoftDeletable`, `IActive`, and `IMultiTenant`
filters into a single expression.

## Diagram

```mermaid
flowchart TD
    AGC["ApplyGranitConventions()"] --> SCAN[Scan DbContext entities]
    SCAN --> CHECK{Which interfaces implemented?}

    CHECK -->|ISoftDeletable| E1["Expression: !e.IsDeleted<br/>or !proxy.SoftDeleteEnabled"]
    CHECK -->|IActive| E2["Expression: e.IsActive<br/>or !proxy.ActiveEnabled"]
    CHECK -->|IMultiTenant| E3["Expression: e.TenantId == proxy.CurrentTenantId<br/>or !proxy.MultiTenantEnabled"]

    E1 --> COMBINE["Expression.AndAlso()<br/>Combine all conditions"]
    E2 --> COMBINE
    E3 --> COMBINE

    COMBINE --> LAMBDA["Expression.Lambda of Func T bool"]
    LAMBDA --> HQF["entity.HasQueryFilter(lambda)"]
```

## Implementation in Granit

| Component | File | Lines |
| --------- | ---- | ----- |
| `ApplyGranitConventions()` | `src/Granit.Persistence/Extensions/ModelBuilderExtensions.cs` | 54-126 |
| `FilterProxy` | `src/Granit.Persistence/Extensions/ModelBuilderExtensions.cs` | 133-140 |

### Why a FilterProxy?

EF Core cannot translate arbitrary method calls (like
`dataFilter.IsEnabled<ISoftDeletable>()`) in a query filter. The `FilterProxy`
exposes **simple properties** that EF Core extracts as SQL parameters:

```csharp
// FilterProxy exposes properties that EF Core understands
internal sealed class FilterProxy(IDataFilter? dataFilter, ICurrentTenant? tenant)
{
    public bool SoftDeleteEnabled => dataFilter?.IsEnabled<ISoftDeletable>() ?? true;
    public bool ActiveEnabled => dataFilter?.IsEnabled<IActive>() ?? true;
    public bool MultiTenantEnabled => dataFilter?.IsEnabled<IMultiTenant>() ?? true;
    public Guid? CurrentTenantId => tenant?.Id;
}
```

### Why a single HasQueryFilter?

EF Core (versions before 10) overwrites previous query filters if
`HasQueryFilter()` is called multiple times on the same entity. The combined
expression via `AndAlso` solves this problem in a single call.

## Rationale

Dynamic construction handles all combinations of interfaces automatically (an
entity can implement 0, 1, 2, or 3 marker interfaces) without writing specific
code for each combination (2^3 = 8 cases).

## Usage example

```csharp
// The application calls a single line in OnModelCreating
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ApplyGranitConventions(serviceProvider);
    // -> Dynamically builds query filters for all entities
}

// The filter is automatic and transparent
List<Patient> patients = await db.Patients.ToListAsync(ct);
// Generated SQL:
// SELECT * FROM Patients
// WHERE (@SoftDeleteEnabled = 0 OR IsDeleted = 0)
//   AND (@MultiTenantEnabled = 0 OR TenantId = @CurrentTenantId)
```

## Further reading

- [Expression Trees -- Microsoft .NET Documentation](https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/expression-trees/)
