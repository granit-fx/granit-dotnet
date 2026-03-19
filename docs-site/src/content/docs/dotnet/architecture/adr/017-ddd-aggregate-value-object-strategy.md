---
title: "ADR-017: DDD Aggregate Root & Value Object Strategy"
description: "Phased migration from anemic entities to proper aggregate roots and value objects"
sidebar:
  order: 17
  label: "017 - DDD Aggregates & VOs"
---

> **Date:** 2026-03-19
> **Authors:** Jean-Francois Meyers
> **Scope:** granit-dotnet (all modules with domain entities)

## Context

The framework provides strong DDD building blocks (`AggregateRoot`, `ValueObject`,
`IDomainEventSource`, `IIntegrationEventSource`, two EF Core interceptors) but adoption
is near-zero:

- **0 entities** inherit from `AggregateRoot` — all use `Entity` or audited variants
- **0 `ValueObject` subclasses** exist in production code
- **7 entities** with state machines have public setters and manual event management
- **21 entities** are anemic, but most are legitimately so (audit logs, reference data,
  config storage)

This creates a gap between the framework's capabilities and how modules use them.

## Decision

### 1. Classification criteria

**Aggregate Root** — use when the entity:

- Has a state machine (status transitions with business rules)
- Raises domain or integration events
- Encapsulates invariants that must be protected from external mutation
- Is the root of a consistency boundary

**Entity (anemic, legitimate)** — use when the entity:

- Is append-only / immutable after creation (audit logs, consent records)
- Is a configuration record (settings, feature flags, localization overrides)
- Is a cache/mirror of an external system (identity provider cache)
- Is a lookup table (reference data)
- Is an internal EF Core mapping entity (not part of the public domain)

### 2. Aggregate Root patterns

#### Factory method for creation

```csharp
public sealed class BlobDescriptor : AggregateRoot, IMultiTenant
{
    private BlobDescriptor() { } // EF Core materializer

    public static BlobDescriptor Create(Guid id, Guid? tenantId, ...) => new()
    {
        Id = id,
        TenantId = tenantId,
        Status = BlobStatus.Pending,
    };
}
```

#### Private setters with behavior methods

```csharp
public BlobStatus Status { get; private set; }

public void MarkAsValid(string contentType, long size, DateTimeOffset at)
{
    if (Status != BlobStatus.Uploading)
        throw new InvalidOperationException(...);

    Status = BlobStatus.Valid;
    VerifiedContentType = contentType;
    SizeBytes = size;
    ValidatedAt = at;

    AddDomainEvent(new BlobValidatedEvent(Id, ContainerName, contentType, size));
}
```

#### Private EF Core constructor (mandatory)

Every aggregate root with `private set` properties **must** keep a parameterless
constructor for EF Core materialization:

```csharp
private BlobDescriptor() { } // Required — EF Core uses this, not the factory
```

Without it, EF Core attempts to use the factory method and crashes at runtime
when it cannot map constructor parameters to database columns.

#### Explicit interface for `IMultiTenant`

When `TenantId` has `private set`, the interceptor cannot assign it. Use explicit
interface implementation:

```csharp
public Guid? TenantId { get; private set; }

Guid? IMultiTenant.TenantId
{
    get => TenantId;
    set => TenantId = value;
}
```

This preserves DDD encapsulation while allowing infrastructure code to inject
the tenant identifier.

### 3. Value Object strategy

#### `SingleValueObject<T>` for single-property VOs

Most value objects wrap a single primitive. A `SingleValueObject<T>` base class
provides:

- Automatic `GetEqualityComponents()` implementation
- `implicit operator` for backward compatibility
- `ToString()` delegation

#### JSON serialization

`System.Text.Json` serializes value objects as nested objects by default:
`{"value": "text/html"}` instead of `"text/html"`.

A `SingleValueObjectJsonConverterFactory` registered globally ensures transparent
serialization as the underlying primitive.

#### EF Core value converters

Single-value VOs use `ValueConverter<TVO, TPrimitive>` — the database column type
stays the same, so **no EF Core migration is needed**.

A `SingleValueObjectConvention` auto-applies the correct converter to any property
of type `SingleValueObject<T>`.

### 4. Entity hierarchy

```text
Entity (Guid Id)
├── CreationAuditedEntity (+ CreatedAt, CreatedBy)
│   ├── AuditedEntity (+ ModifiedAt, ModifiedBy)
│   │   └── FullAuditedEntity (+ ISoftDeletable)
│   └── CreationAuditedAggregateRoot (+ IDomainEventSource, IIntegrationEventSource)
│       ├── AuditedAggregateRoot (+ ModifiedAt, ModifiedBy)
│       │   └── FullAuditedAggregateRoot (+ ISoftDeletable)
│       └── (no further specialization)
└── AggregateRoot (+ IDomainEventSource, IIntegrationEventSource)

ValueObject
└── SingleValueObject<T> (single-primitive wrapper)
```

## Alternatives considered

### Keep entities anemic, behavior in services

- **Rejected**: service-layer state changes bypass invariant enforcement, domain events
  must be raised explicitly by services (easy to forget), and public setters allow
  accidental mutation from anywhere in the codebase.

### Use owned types for all Value Objects

- **Rejected for single-value VOs**: owned types create separate columns or tables, which
  is overkill for a single-property wrapper. Value converters map to the same column
  with zero schema change.
- **Acceptable for multi-value VOs** (e.g., `Money` with `Amount` + `Currency`): owned
  types are the right choice here.

## Consequences

- **Breaking change**: public setter removal in Phase 3. Acceptable in current dev stage.
- **No EF Core migrations**: aggregate roots add no persisted columns; value converters
  map to the same column types.
- **Architecture tests** enforce the new conventions, preventing regression.
