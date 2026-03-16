---
title: "From Zero to CRUD in 10 Minutes with Granit"
date: 2026-03-07
authors:
  - jfmeyers
tags:
  - tutorial
  - persistence
description: "Build a complete CRUD module with Granit in under 10 minutes — from entity definition to validated endpoints with full audit trail and soft delete."
excerpt: >
  Build a complete CRUD module with Granit in under 10 minutes — from entity
  definition to validated endpoints with full audit trail and soft delete.
---

You have heard about the module system, browsed the pattern catalog, maybe skimmed an ADR or two. Now you want to build something. This tutorial walks you through creating a **Product catalog module** from an empty folder to a working API with full audit trail, soft delete, validation, and tenant isolation -- all in under 10 minutes.

The entire module is roughly 150 lines of code. Granit handles the rest.

## Step 1: Create the entity

Every Granit module starts with a **domain entity**. The framework provides an entity hierarchy that layers audit and lifecycle behavior on top of a base `Entity` class:

- **`Entity`** -- `Guid Id` only.
- **`CreationAuditedEntity`** -- adds `CreatedAt` and `CreatedBy`.
- **`AuditedEntity`** -- adds `ModifiedAt` and `ModifiedBy`.
- **`FullAuditedEntity`** -- adds `ISoftDeletable` (`IsDeleted`, `DeletedAt`, `DeletedBy`).

For a product catalog, you want the full trail. A product can be created, updated, and soft-deleted -- never hard-deleted.

```csharp title="Domain/Product.cs"
using Granit.Core.Domain;

namespace ProductCatalog.Domain;

public sealed class Product : FullAuditedEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public string Sku { get; set; } = string.Empty;
}
```

That is it. No marker interfaces for audit, no manual `DateTime.UtcNow` calls. The `AuditedEntityInterceptor` and `SoftDeleteInterceptor` from `Granit.Persistence` populate the audit fields automatically when EF Core saves changes.

## Step 2: Create the DbContext

Granit follows the **isolated DbContext** pattern: each module owns its own `DbContext`, decoupled from the host application's context. This keeps module boundaries clean and avoids a single monolithic context with hundreds of entity sets.

```csharp title="EntityFrameworkCore/ProductDbContext.cs"
using Granit.Core.DataFiltering;
using Granit.Core.MultiTenancy;
using Granit.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Domain;

namespace ProductCatalog.EntityFrameworkCore;

internal sealed class ProductDbContext(
    DbContextOptions<ProductDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProductDbContext).Assembly);
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
```

Three things to note:

- **`ICurrentTenant?` and `IDataFilter?`** are injected as optional parameters. If multi-tenancy is not installed, a `NullTenantContext` is used and the tenant filter is simply skipped.
- **`ApplyGranitConventions`** registers one named query filter per applicable interface (`ISoftDeletable`, `IMultiTenant`, `IActive`, `IProcessingRestrictable`, `IPublishable`). You never write a manual `HasQueryFilter` — each filter is independent and bypassable per query via `IgnoreQueryFilters([GranitFilterNames.SoftDelete])`.
- **`ApplyConfigurationsFromAssembly`** picks up any `IEntityTypeConfiguration<T>` in the same assembly. Add one if you need column constraints, indexes, or value conversions.

To register the context with Granit's interceptor wiring, use `AddGranitDbContext`:

```csharp title="ProductModule.cs (partial — context registration)"
services.AddGranitDbContext<ProductDbContext>(options =>
    options.UseNpgsql(configuration.GetConnectionString("ProductCatalog")));
```

This replaces the boilerplate of manually resolving `AuditedEntityInterceptor` and `SoftDeleteInterceptor` from the service provider. One line, all interceptors wired.

## Step 3: Create Request and Response records

Granit follows strict DTO conventions: **`Request`** for input bodies, **`Response`** for return types. Never suffix with `Dto`. Never return EF Core entities directly -- always map to a response record.

Prefix your DTOs with the module context to avoid OpenAPI schema collisions. `ProductCreateRequest`, not `CreateRequest`.

```csharp title="Contracts/ProductCreateRequest.cs"
namespace ProductCatalog.Contracts;

public sealed record ProductCreateRequest(
    string Name,
    string? Description,
    decimal Price,
    string Sku);
```

```csharp title="Contracts/ProductUpdateRequest.cs"
namespace ProductCatalog.Contracts;

public sealed record ProductUpdateRequest(
    string Name,
    string? Description,
    decimal Price,
    string Sku);
```

```csharp title="Contracts/ProductResponse.cs"
namespace ProductCatalog.Contracts;

public sealed record ProductResponse(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    string Sku,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? ModifiedAt,
    string? ModifiedBy);
```

The response includes audit fields. The caller sees who created or modified each product without any extra work on your side.

## Step 4: Add FluentValidation

Granit uses **FluentValidation** with structured error codes. Validators are discovered automatically when registered with `AddGranitValidatorsFromAssemblyContaining`. Error messages are returned as codes (e.g., `Granit:Validation:NotEmptyValidator`) that the frontend resolves from its localization dictionary.

```csharp title="Validators/ProductCreateRequestValidator.cs"
using FluentValidation;
using ProductCatalog.Contracts;

namespace ProductCatalog.Validators;

internal sealed class ProductCreateRequestValidator
    : AbstractValidator<ProductCreateRequest>
{
    public ProductCreateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Sku)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0);
    }
}
```

Write a matching validator for `ProductUpdateRequest`. The rules are usually identical -- if they diverge later (e.g., SKU becomes immutable on update), having separate validators pays off.

## Step 5: Create the endpoints

Granit uses **Minimal APIs** with route groups. Validation is applied per-endpoint using the `.ValidateBody<T>()` extension method, which adds a `FluentValidationEndpointFilter<T>`. If the request body fails validation, the filter short-circuits with a **422 Unprocessable Entity** response containing `HttpValidationProblemDetails`.

For errors, always use `TypedResults.Problem` (RFC 7807 Problem Details). Never return raw strings or `BadRequest<string>`.

```csharp title="Endpoints/ProductEndpoints.cs"
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Contracts;
using ProductCatalog.EntityFrameworkCore;

namespace ProductCatalog.Endpoints;

internal static class ProductEndpoints
{
    public static void MapProductEndpoints(this IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder group = routes
            .MapGroup("/api/v1/products")
            .WithTags("Products");

        group.MapGet("/", ListProducts);
        group.MapGet("/{id:guid}", GetProduct);
        group.MapPost("/", CreateProduct).ValidateBody<ProductCreateRequest>();
        group.MapPut("/{id:guid}", UpdateProduct).ValidateBody<ProductUpdateRequest>();
        group.MapDelete("/{id:guid}", DeleteProduct);
    }

    private static async Task<Ok<List<ProductResponse>>> ListProducts(
        ProductDbContext db,
        CancellationToken cancellationToken)
    {
        List<ProductResponse> products = await db.Products
            .Select(p => new ProductResponse(
                p.Id, p.Name, p.Description, p.Price, p.Sku,
                p.CreatedAt, p.CreatedBy, p.ModifiedAt, p.ModifiedBy))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(products);
    }

    private static async Task<Results<Ok<ProductResponse>, ProblemHttpResult>> GetProduct(
        Guid id,
        ProductDbContext db,
        CancellationToken cancellationToken)
    {
        ProductResponse? product = await db.Products
            .Where(p => p.Id == id)
            .Select(p => new ProductResponse(
                p.Id, p.Name, p.Description, p.Price, p.Sku,
                p.CreatedAt, p.CreatedBy, p.ModifiedAt, p.ModifiedBy))
            .FirstOrDefaultAsync(cancellationToken);

        if (product is null)
        {
            return TypedResults.Problem(
                detail: $"Product {id} not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok(product);
    }

    private static async Task<Created<ProductResponse>> CreateProduct(
        ProductCreateRequest request,
        ProductDbContext db,
        CancellationToken cancellationToken)
    {
        Product product = new()
        {
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            Sku = request.Sku,
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        ProductResponse response = new(
            product.Id, product.Name, product.Description, product.Price, product.Sku,
            product.CreatedAt, product.CreatedBy, product.ModifiedAt, product.ModifiedBy);

        return TypedResults.Created($"/api/v1/products/{product.Id}", response);
    }

    private static async Task<Results<Ok<ProductResponse>, ProblemHttpResult>> UpdateProduct(
        Guid id,
        ProductUpdateRequest request,
        ProductDbContext db,
        CancellationToken cancellationToken)
    {
        Product? product = await db.Products
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
        {
            return TypedResults.Problem(
                detail: $"Product {id} not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        product.Name = request.Name;
        product.Description = request.Description;
        product.Price = request.Price;
        product.Sku = request.Sku;

        await db.SaveChangesAsync(cancellationToken);

        ProductResponse response = new(
            product.Id, product.Name, product.Description, product.Price, product.Sku,
            product.CreatedAt, product.CreatedBy, product.ModifiedAt, product.ModifiedBy);

        return TypedResults.Ok(response);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteProduct(
        Guid id,
        ProductDbContext db,
        CancellationToken cancellationToken)
    {
        Product? product = await db.Products
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
        {
            return TypedResults.Problem(
                detail: $"Product {id} not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        db.Products.Remove(product);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }
}
```

Notice that the DELETE handler calls `Remove()`, not a manual `IsDeleted = true`. The `SoftDeleteInterceptor` from `Granit.Persistence` intercepts the delete operation and converts it to a soft delete automatically. The entity stays in the database with `IsDeleted = true`, `DeletedAt` timestamped, and `DeletedBy` set to the current user. The global query filter ensures soft-deleted products are excluded from all subsequent queries.

## Step 6: Wire up the module

Now bring everything together in the **module class** and the **application entry point**.

```csharp title="ProductModule.cs"
using Granit.Core.Modularity;
using Granit.Persistence;
using Granit.Validation.Extensions;
using Microsoft.Extensions.Configuration;
using ProductCatalog.EntityFrameworkCore;
using ProductCatalog.Validators;

namespace ProductCatalog;

[DependsOn(typeof(GranitPersistenceModule))]
public sealed class ProductModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        IConfiguration configuration = context.Configuration;

        context.Services.AddGranitDbContext<ProductDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("ProductCatalog")));

        context.Services.AddGranitValidatorsFromAssemblyContaining<ProductCreateRequestValidator>();
    }
}
```

The `[DependsOn(typeof(GranitPersistenceModule))]` attribute declares a direct dependency. Granit resolves the module graph topologically -- `GranitPersistenceModule` and its own dependencies (`GranitTimingModule`, `GranitGuidsModule`, `GranitSecurityModule`, `GranitExceptionHandlingModule`) are configured first. You only declare **direct** dependencies; transitive ones are resolved automatically.

The validator registration call `AddGranitValidatorsFromAssemblyContaining` scans the assembly for all `IValidator<T>` implementations and registers them as scoped services. Without this call, `FluentValidationEndpointFilter<T>` silently passes through without validation -- a subtle bug that is easy to miss.

Now wire the module into your application:

```csharp title="Program.cs"
using Granit.Core.Extensions;
using ProductCatalog;
using ProductCatalog.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.AddGranit(granit =>
{
    granit.AddModule<ProductModule>();
});

var app = builder.Build();
app.UseGranit();
app.MapProductEndpoints();
app.Run();
```

`AddGranit` discovers the full module dependency tree from `ProductModule`. `UseGranit` runs the post-build initialization phase (e.g., running migrations, seeding data if modules opt into it). Endpoint mapping is explicit -- you decide which routes are exposed.

## Step 7: Run and test with Scalar

Add the `Granit.ApiDocumentation` package to get **Scalar** (the OpenAPI documentation UI) out of the box. Start the application and navigate to `/scalar` to see your endpoints, try requests, and inspect the generated OpenAPI schema.

Create a product:

```http
POST /api/v1/products
Content-Type: application/json

{
  "name": "Mechanical Keyboard",
  "description": "Cherry MX Brown switches, hot-swappable",
  "price": 149.99,
  "sku": "KB-MX-001"
}
```

The response includes the auto-generated `Id` and audit fields:

```json
{
  "id": "01968a3c-...",
  "name": "Mechanical Keyboard",
  "description": "Cherry MX Brown switches, hot-swappable",
  "price": 149.99,
  "sku": "KB-MX-001",
  "createdAt": "2026-03-07T14:30:00Z",
  "createdBy": "user@example.com",
  "modifiedAt": null,
  "modifiedBy": null
}
```

Try sending an invalid request (empty name, negative price) and watch FluentValidation return a 422 with structured error codes.

Delete the product, then query the list -- it disappears from results. But if you check the database directly, the row is still there with `IsDeleted = true`. That is soft delete working through the interceptor and query filter.

## What you got for free

Stop and count what Granit handled without a single line of infrastructure code from you:

- **Audit trail** -- `CreatedAt`, `CreatedBy`, `ModifiedAt`, `ModifiedBy` populated automatically by `AuditedEntityInterceptor`. ISO 27001 compliance out of the box.
- **Soft delete** -- `Remove()` calls are intercepted and converted to logical deletes. `IsDeleted`, `DeletedAt`, `DeletedBy` are set. GDPR right-to-erasure support without manual plumbing.
- **Global query filters** -- soft-deleted entities are excluded from all queries. If multi-tenancy is installed, tenant isolation is enforced at the query level. Filters can be bypassed at runtime with `IDataFilter.Disable<ISoftDeletable>()` when you need to include deleted records (e.g., admin audit views).
- **Validation pipeline** -- FluentValidation runs before your handler. Structured error codes are returned as RFC 7807 Problem Details. The frontend maps codes to localized messages.
- **Sequential GUIDs** -- `IGuidGenerator` produces sequential GUIDs optimized for clustered indexes. No fragmentation, no performance cliff on large tables.
- **Interceptor wiring** -- `AddGranitDbContext` resolves and attaches EF Core interceptors from the DI container. You never manually configure them.

## Further reading

- [Getting started](/dotnet/getting-started/) -- installation, prerequisites, first project setup.
- [Persistence module reference](/dotnet/data/persistence/) -- interceptors, migrations, query filters, data filtering.
- [Core module reference](/dotnet/core/module-system/) -- module system, entity hierarchy, IGuidGenerator, IClock.
- [Isolated DbContext pattern](/dotnet/architecture/patterns/layered-architecture/) -- why each module owns its context.
- [FluentValidation (ADR-006)](/dotnet/architecture/adr/006-fluentvalidation/) -- decision record for the validation stack.
