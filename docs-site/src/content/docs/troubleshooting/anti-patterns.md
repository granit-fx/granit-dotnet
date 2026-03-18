---
title: "Anti-Patterns \u2014 Common .NET Mistakes to Avoid"
description: Avoid these .NET anti-patterns in Granit — DateTime.Now, new Regex, string interpolation in logs, lock on object, unnamed HasQueryFilter, and more with correct alternatives.
sidebar:
  label: Anti-patterns
  order: 2
---

These patterns compile without errors but cause problems at runtime, fail Granit
analyzers, or create maintenance debt. Each entry shows the anti-pattern and its
correct alternative.

## Using `DateTime.Now` or `DateTime.UtcNow`

**Why it is a problem**: Direct calls to `DateTime.Now` are untestable (you
cannot control time in tests) and ignore the application's configured timezone.
Granit provides `TimeProvider` and `IClock` for this purpose.

```csharp
// Anti-pattern
var now = DateTime.UtcNow;
entity.CreatedAt = DateTime.Now;

// Correct -- inject TimeProvider (preferred in .NET 10)
public class MyService(TimeProvider timeProvider)
{
    public void DoWork()
    {
        var now = timeProvider.GetUtcNow();
    }
}

// Correct -- inject IClock (Granit abstraction)
public class MyService(IClock clock)
{
    public void DoWork()
    {
        var now = clock.Now;
    }
}
```

## String interpolation in log calls

**Why it is a problem**: String interpolation allocates a string on every call,
even when the log level is disabled. Granit requires `[LoggerMessage]`
source-generated logging for zero-allocation structured logs.

```csharp
// Anti-pattern -- allocates on every call
logger.LogInformation($"Processing order {orderId} for tenant {tenantId}");

// Anti-pattern -- message template is fine, but still not source-generated
logger.LogInformation("Processing order {OrderId} for tenant {TenantId}",
    orderId, tenantId);

// Correct -- source-generated, zero-allocation when level is disabled
[LoggerMessage(Level = LogLevel.Information,
    Message = "Processing order {OrderId} for tenant {TenantId}")]
private static partial void LogProcessingOrder(
    ILogger logger, Guid orderId, Guid tenantId);
```

## `new Regex()` instead of `[GeneratedRegex]`

**Why it is a problem**: `new Regex(..., RegexOptions.Compiled)` compiles the
pattern at runtime using reflection emit. `[GeneratedRegex]` produces the same
optimized code at compile time with no runtime cost. On user input, always set a
timeout to prevent ReDoS attacks.

```csharp
// Anti-pattern
private static readonly Regex EmailPattern =
    new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

// Correct
[GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$",
    RegexOptions.None, matchTimeoutMilliseconds: 1000)]
private static partial Regex EmailPattern();
```

## Returning EF Core entities from endpoints

**Why it is a problem**: Returning entities directly couples your API contract to
your database schema, leaks navigation properties, and prevents independent
evolution of the API surface. Granit requires `*Response` records for all
endpoint return values.

```csharp
// Anti-pattern -- leaks entity structure, navigation properties, shadow properties
app.MapGet("/products/{id}", async (Guid id, MyDbContext db) =>
    await db.Products.FindAsync(id));

// Correct -- map to a response record
app.MapGet("/products/{id}", async (Guid id, MyDbContext db) =>
{
    var product = await db.Products.FindAsync(id);
    return product is null
        ? Results.NotFound()
        : Results.Ok(new ProductResponse(product.Id, product.Name, product.Price));
});

public record ProductResponse(Guid Id, string Name, decimal Price);
```

## Using `TypedResults.BadRequest<string>()` for errors

**Why it is a problem**: Returning a plain string body does not conform to
RFC 7807 Problem Details. Clients that parse `application/problem+json` will not
understand the response. Granit standardizes on `TypedResults.Problem()`.

```csharp
// Anti-pattern
return TypedResults.BadRequest("Product name is required");

// Correct -- RFC 7807 Problem Details
return TypedResults.Problem(
    detail: "Product name is required",
    statusCode: StatusCodes.Status400BadRequest);
```

## Manual `HasQueryFilter` for standard interfaces

**Why it is a problem**: `ApplyGranitConventions` already registers query filters
for `ISoftDeletable`, `IMultiTenant`, `IActive`, `IProcessingRestrictable`, and
`IPublishable`. Adding manual filters causes duplicates (EF Core merges them with
AND, potentially double-filtering) or conflicts when the convention filter uses a
different expression.

```csharp
// Anti-pattern -- conflicts with ApplyGranitConventions
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasQueryFilter(p => !p.IsDeleted); // duplicate filter
    }
}

// Correct -- let ApplyGranitConventions handle it
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasIndex(p => p.Name);
        // No HasQueryFilter -- handled by ApplyGranitConventions
    }
}
```

## `BuildServiceProvider()` in configuration

**Why it is a problem**: Calling `BuildServiceProvider()` inside a service
registration lambda creates a second DI container. Services resolved from it are
different instances than those in the real container, breaking singleton
guarantees and causing subtle bugs. The compiler emits `ASP0000` for this.

```csharp
// Anti-pattern -- creates a second container
services.AddSingleton<IMyService>(sp =>
{
    var provider = services.BuildServiceProvider(); // ASP0000
    var config = provider.GetRequiredService<IConfiguration>();
    return new MyService(config);
});

// Correct -- use the service provider passed to the factory
services.AddSingleton<IMyService>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new MyService(config);
});
```

## Merging Reader/Writer interfaces

**Why it is a problem**: Granit follows CQRS (Command Query Responsibility
Segregation). `I*Reader` and `I*Writer` interfaces are intentionally separate to
allow independent scaling, caching of read paths, and clear separation of
concerns. Combining them into a single `I*Store` interface in constructors
defeats this design.

```csharp
// Anti-pattern -- merging read and write concerns
public class BlobService(IBlobDescriptorStore store)
{
    // store.GetAsync(...) and store.SaveAsync(...) in the same dependency
}

// Correct -- separate reader and writer
public class BlobService(
    IBlobDescriptorReader reader,
    IBlobDescriptorWriter writer)
{
    // Queries go through reader, commands go through writer
}
```

:::note
`IBlobDescriptorStore` exists as a convenience registration that implements both
interfaces. It is acceptable in simple scenarios, but constructors should depend
on the specific interface they need.
:::

## Using `Dto` suffix on request/response types

**Why it is a problem**: Granit uses `Request` for input bodies and `Response`
for return types. The `Dto` suffix is ambiguous and does not indicate direction.
Additionally, module-specific DTOs must be prefixed with the module context to
avoid OpenAPI schema conflicts.

```csharp
// Anti-pattern
public record ProductDto(Guid Id, string Name);
public record CreateProductDto(string Name, decimal Price);

// Correct
public record ProductResponse(Guid Id, string Name);
public record CreateProductRequest(string Name, decimal Price);

// For module-specific types, add the module prefix
public record WorkflowTransitionRequest(string TargetState);
// NOT: TransitionRequest (too generic, causes OpenAPI conflicts)
```
