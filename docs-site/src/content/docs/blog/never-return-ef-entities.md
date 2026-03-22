---
title: "Never Return Your EF Entities From an API"
date: 2026-03-14
sidebar:
  order: 10
authors:
  - jfmeyers
tags:
  - best-practice
  - api
description: "Returning EF Core entities directly from your endpoints leaks internal structure, creates circular references, and exposes sensitive data. Use response records instead."
excerpt: >
  Returning EF Core entities directly from your endpoints leaks internal structure,
  creates circular references, and exposes sensitive data. Use response records instead.
---

Your API returns a user object. The frontend team notices a `PasswordHash` field in the JSON. A penetration tester finds that posting back the payload overwrites the `Role` property. A schema change in your database breaks every consumer. The root cause is always the same: **you returned your EF Core entity directly from an endpoint**.

## The problem

EF Core entities are persistence concerns. They carry navigation properties, shadow properties, change-tracking baggage, and fields that exist for the database, not for your API consumers. Returning them directly causes four distinct problems:

- **Data leakage** — sensitive columns (`PasswordHash`, `DeletedAt`, `TenantId`, internal flags) end up in the JSON response. You are one forgotten property away from a security incident.
- **Circular references** — navigation properties (`Order.Customer.Orders.Customer...`) cause `JsonException` at runtime or infinite loops with lenient serializers.
- **Lazy loading traps** — if lazy loading is enabled, serializing an entity triggers N+1 queries you never intended. Your "simple GET" suddenly fires 200 SQL statements.
- **Schema coupling** — your API contract is now your database schema. Rename a column, add a field, normalize a table, and every consumer breaks. You cannot evolve one without the other.

## Bad practice: returning the entity

```csharp title="GetInvoiceEndpoint.cs — Don't do this"
app.MapGet("/api/v1/invoices/{id:guid}", async (
    Guid id,
    AppDbContext db,
    CancellationToken ct) =>
{
    var invoice = await db.Invoices
        .Include(i => i.Customer)
        .Include(i => i.Lines)
        .FirstOrDefaultAsync(i => i.Id == id, ct);

    return invoice is null
        ? Results.NotFound()
        : Results.Ok(invoice); // Serializes the entire entity graph
});
```

This endpoint returns everything: the `Customer` navigation with all its properties, every `InvoiceLine` with internal pricing flags, the `TenantId`, the `DeletedAt` timestamp. The OpenAPI schema mirrors your database, and any schema migration is now a breaking API change.

## Good practice: response record + explicit mapping

Define a **response record** that represents exactly what the consumer needs. Nothing more.

```csharp title="InvoiceResponse.cs"
public sealed record InvoiceResponse(
    Guid Id,
    string InvoiceNumber,
    string CustomerName,
    DateTimeOffset IssuedAt,
    DateTimeOffset DueDate,
    decimal TotalExcludingTax,
    decimal TotalIncludingTax,
    IReadOnlyList<InvoiceLineResponse> Lines);

public sealed record InvoiceLineResponse(
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);
```

Then map explicitly in the endpoint:

```csharp title="GetInvoiceEndpoint.cs — Do this instead"
app.MapGet("/api/v1/invoices/{id:guid}", async Task<Results<Ok<InvoiceResponse>, ProblemHttpResult>> (
    Guid id,
    IInvoiceReader reader,
    CancellationToken ct) =>
{
    var invoice = await reader.FindAsync(id, ct);

    if (invoice is null)
    {
        return TypedResults.Problem("Invoice not found.", statusCode: 404);
    }

    var response = new InvoiceResponse(
        invoice.Id,
        invoice.InvoiceNumber,
        invoice.Customer.DisplayName,
        invoice.IssuedAt,
        invoice.DueDate,
        invoice.TotalExcludingTax,
        invoice.TotalIncludingTax,
        invoice.Lines.Select(l => new InvoiceLineResponse(
            l.Description,
            l.Quantity,
            l.UnitPrice,
            l.LineTotal)).ToList());

    return TypedResults.Ok(response);
});
```

The entity stays inside the service layer. The API consumer sees a stable, curated contract.

## Naming conventions

Granit enforces strict naming rules for endpoint DTOs to prevent OpenAPI schema collisions:

- **`*Request`** for input bodies (`CreateInvoiceRequest`, not `CreateInvoiceDto`).
- **`*Response`** for return types (`InvoiceResponse`, not `InvoiceDto`).
- **Module prefix** on every DTO. OpenAPI flattens namespaces into a single schema map. `TransitionRequest` from your Workflow module collides with `TransitionRequest` from your Notifications module. Use `WorkflowTransitionRequest` and `NotificationTransitionRequest`.
- **Never use the `Dto` suffix.** It says nothing about direction. `Request` and `Response` make intent explicit.
- **Shared cross-cutting types** like `PagedResult<T>` and `ProblemDetails` are exempt from the module prefix rule.

## Why it matters

**API contract stability.** You can rename database columns, split tables, add internal tracking fields. None of it touches the response record. Your consumers never know.

**Security.** The response record is an allowlist. Only the fields you explicitly include are serialized. No `PasswordHash`, no `TenantId`, no soft-delete flags leaking out. This is not defense in depth — it is the primary defense.

**OpenAPI schema clarity.** Your generated OpenAPI document describes what consumers actually receive, not the internal shape of your database. Schema names are meaningful (`InvoiceResponse`), not accidental (`Invoice` — is that the entity? the command? the event?).

**Testability.** Response records are plain data. You can construct them in tests without a `DbContext`, assert on them with Shouldly, and serialize them predictably.

## Further reading

- [CQRS pattern](/dotnet/architecture/patterns/cqrs/) — why Granit separates Reader and Writer interfaces
- [Layered architecture](/dotnet/architecture/patterns/layered-architecture/) — where entities and response records live
