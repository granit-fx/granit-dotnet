---
title: "5 EF Core Mistakes That Kill Performance (and How to Fix Them)"
date: 2026-05-03
authors:
  - jfmeyers
tags:
  - best-practice
  - persistence
  - performance
description: "From accidental client evaluation to N+1 queries and tracked reads, these are the five EF Core anti-patterns that turn 50 ms endpoints into 5-second ones — and the exact fix for each."
excerpt: >
  From accidental client evaluation to N+1 queries and tracked reads, these are
  the five EF Core anti-patterns that turn 50 ms endpoints into 5-second ones —
  and the exact fix for each.
---

EF Core 10 is fast. The framework rewrites your LINQ into solid SQL, batches inserts, and ships query splitting out of the box. None of that helps if your code asks it to do the wrong thing. Ninety percent of the "EF Core is slow" tickets I see are five mistakes repeated under different names.

Here they are, with the diagnostic and the fix.

## 1. Returning a `List<T>` from a query that should stream

```csharp title="ExportEndpoint.cs — Bad"
var invoices = await db.Invoices
    .Where(i => i.TenantId == tenantId)
    .ToListAsync(ct); // 250k rows materialized into memory

foreach (var invoice in invoices)
{
    await writer.WriteAsync(invoice, ct);
}
```

`ToListAsync()` allocates the entire result set, plus change-tracker entries, plus navigation graph stubs. On a CSV export of 250k rows you've just moved 400 MB through GC for no reason.

```csharp title="ExportEndpoint.cs — Good"
var stream = db.Invoices
    .AsNoTracking()
    .Where(i => i.TenantId == tenantId)
    .AsAsyncEnumerable();

await foreach (var invoice in stream.WithCancellation(ct))
{
    await writer.WriteAsync(invoice, ct);
}
```

`AsAsyncEnumerable()` reads rows from the open `DbDataReader` one at a time. Memory stays flat regardless of result size. Add `AsNoTracking()` because you are not modifying anything — that alone removes the change-tracker overhead.

**Rule of thumb:** if the result set could plausibly grow past a few thousand rows, stream it. `ToListAsync()` is for known-bounded sets.

## 2. Tracked queries on read paths

```csharp title="GetInvoice.cs — Bad"
var invoice = await db.Invoices
    .Include(i => i.Lines)
    .FirstOrDefaultAsync(i => i.Id == id, ct);

return invoice is null
    ? Results.NotFound()
    : Results.Ok(invoice.ToResponse());
```

EF Core tracked the entity, hashed each property into the change tracker, and built navigation fixups. You then projected to a response and threw the entity away. Pure waste.

```csharp title="GetInvoice.cs — Good"
var response = await db.Invoices
    .AsNoTracking()
    .Where(i => i.Id == id)
    .Select(i => new InvoiceResponse(
        i.Id,
        i.Number,
        i.IssuedAt,
        i.Lines.Select(l => new LineResponse(l.Description, l.Amount)).ToList()))
    .FirstOrDefaultAsync(ct);

return response is null ? Results.NotFound() : Results.Ok(response);
```

Two wins:

- `AsNoTracking()` skips change tracking entirely — measurably cheaper on hot paths.
- `Select` projection generates a single SQL statement that reads only the columns the response needs. The `Include` you removed was returning 25 columns you never used.

Granit nudges you toward this with the [`Never return EF entities`](/blog/never-return-ef-entities/) rule. The performance gain is real; the API contract benefit is bigger.

## 3. The N+1 hidden behind a `foreach`

```csharp title="OrderListEndpoint.cs — Bad"
var orders = await db.Orders.ToListAsync(ct);

foreach (var order in orders)
{
    order.LatestPaymentStatus = await db.Payments
        .Where(p => p.OrderId == order.Id)
        .OrderByDescending(p => p.PaidAt)
        .Select(p => p.Status)
        .FirstOrDefaultAsync(ct); // One query per order — N+1
}
```

This pattern hides in services, mappers, and AutoMapper post-processors. With 500 orders you fire 501 SQL round trips. Database CPU is fine; your app waits on network latency 500 times.

```csharp title="OrderListEndpoint.cs — Good"
var orders = await db.Orders
    .AsNoTracking()
    .Select(o => new OrderResponse(
        o.Id,
        o.Number,
        o.Payments
            .OrderByDescending(p => p.PaidAt)
            .Select(p => p.Status)
            .FirstOrDefault()))
    .ToListAsync(ct);
```

One query. EF Core translates the inner `Select`/`OrderByDescending`/`FirstOrDefault` into a correlated subquery (or a window function on PostgreSQL). The execution plan is the database's job, not yours.

**Diagnostic:** turn on `QueryDiagnosticsBehavior` or `LogTo(Console.WriteLine, LogLevel.Information)` in dev. Any time you see the same parameterized query repeat with different IDs, you have an N+1.

## 4. Accidentally evaluating filters in C\#

```csharp title="UserSearch.cs — Bad"
var matches = await db.Users
    .Where(u => NormalizeEmail(u.Email) == NormalizeEmail(query)) // Client eval!
    .ToListAsync(ct);
```

`NormalizeEmail` is a C# method. EF Core cannot translate it to SQL, so on EF Core 3+ this throws `InvalidOperationException: could not be translated`. Many teams "fix" it by calling `.AsEnumerable()` first — which silently pulls **the entire `Users` table** into memory and runs the filter in C#.

```csharp title="UserSearch.cs — Good"
var normalized = NormalizeEmail(query);

var matches = await db.Users
    .Where(u => u.NormalizedEmail == normalized) // Server-side, indexed
    .AsNoTracking()
    .ToListAsync(ct);
```

Two practical fixes:

- **Persist the normalized form.** Add a `NormalizedEmail` column, populate it on save, index it. Filter on the column.
- **Or use a translatable function.** `EF.Functions.Like`, `EF.Functions.ILike`, `string.StartsWith` are all translated. Custom methods are not.

If you must transform user input, do it once outside the query and pass the result as a parameter — never inside the lambda.

## 5. `SaveChangesAsync` in a loop

```csharp title="ImportJob.cs — Bad"
foreach (var row in csvRows)
{
    db.Invoices.Add(MapToInvoice(row));
    await db.SaveChangesAsync(ct); // 50,000 round trips
}
```

Every `SaveChangesAsync` is a transaction commit. On 50k rows you have 50k commits, 50k flushes, and 50k WAL writes. The slow part is not EF Core — it's Postgres durability.

```csharp title="ImportJob.cs — Good"
const int batchSize = 1000;

for (int i = 0; i < csvRows.Count; i += batchSize)
{
    var batch = csvRows.Skip(i).Take(batchSize).Select(MapToInvoice);
    db.Invoices.AddRange(batch);
    await db.SaveChangesAsync(ct);
    db.ChangeTracker.Clear(); // Drop tracked entities before next batch
}
```

Three details:

- **Batch.** EF Core 10 already groups inserts into multi-row `INSERT` statements when the provider supports it. One `SaveChangesAsync` per 1000 rows commits one transaction per 1000 rows.
- **Clear the tracker.** Without it the change tracker grows unbounded across batches; performance degrades quadratically as it scans more entries on each save.
- **For 100k+ rows, skip EF Core.** Use `COPY` (Npgsql), `SqlBulkCopy` (SQL Server), or a dedicated bulk insert library. EF Core is for typed CRUD, not ETL.

## Bonus: measure before you change anything

EF Core ships diagnostics. Use them:

```csharp
optionsBuilder.LogTo(
    Console.WriteLine,
    [DbLoggerCategory.Database.Command.Name],
    LogLevel.Information);
```

Or hook OpenTelemetry: `Npgsql.NpgsqlActivitySource` and `Microsoft.EntityFrameworkCore` traces show every command, parameter set, and duration. You will spot a tracked-read and an N+1 in five minutes of looking at a real trace.

## Key takeaways

- **Stream large reads** with `AsAsyncEnumerable()` instead of materializing with `ToListAsync()`.
- **Project to response records** with `AsNoTracking()` — skip both change tracking and over-fetching.
- **Watch for N+1** in any `foreach` that hits the database; collapse into one query with subqueries or projections.
- **Never client-evaluate filters.** Persist normalized columns or use translatable EF functions.
- **Batch and clear** when bulk-inserting; for ETL workloads bypass EF Core entirely.

## Further reading

- [Persistence overview](/dotnet/data/persistence/)
- [Isolated DbContext per module](/blog/isolated-dbcontext-per-module/)
- [Never return your EF entities from an API](/blog/never-return-ef-entities/)
- [Don't mock the database — use Testcontainers](/blog/dont-mock-the-database-use-testcontainers/)
