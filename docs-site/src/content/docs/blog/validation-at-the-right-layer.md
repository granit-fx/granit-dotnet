---
title: "Validation at the Right Layer: FluentValidation + Endpoint Filters"
date: 2026-04-23
authors:
  - jfmeyers
tags:
  - best-practice
  - api
  - architecture
  - code-quality
description: "Scattering validation across controllers, services, and entities produces inconsistent errors and missed cases. Pin it to the API boundary with FluentValidation and endpoint filters."
excerpt: >
  Scattering validation across controllers, services, and entities produces
  inconsistent errors and missed cases. Pin it to the API boundary with
  FluentValidation and endpoint filters — and let the platform do the rest.
---

Where should you validate input? In the controller? In the service? In the entity constructor? Most codebases answer "all three", which is how you end up with three different error formats for the same broken request, two checks that contradict each other, and a fourth code path that forgot to validate at all. The fix is mechanical: **validate exactly once, at the API boundary, and let an endpoint filter run it for you**.

## The problem with ad-hoc validation

Look at any mid-size .NET codebase that grew organically. You will find every variant:

```csharp title="OrderEndpoints.cs — Don't do this"
app.MapPost("/orders", async (CreateOrderRequest request, IOrderService svc) =>
{
    if (string.IsNullOrWhiteSpace(request.CustomerEmail))
        return Results.BadRequest("Email required");
    if (request.Items is null || request.Items.Count == 0)
        return Results.BadRequest(new { error = "items.empty" });
    if (request.Total <= 0)
        return Results.Problem("Total must be positive", statusCode: 400);

    var order = await svc.CreateAsync(request);
    return Results.Created($"/orders/{order.Id}", order);
});
```

Three checks, three response shapes — `string`, anonymous object, ProblemDetails. Three status codes that all happen to be `400` for completely different reasons. And the moment a second endpoint accepts a `CreateOrderRequest`, every check is duplicated or, worse, slightly different.

Move the checks one layer down and the situation gets worse:

```csharp title="OrderService.cs — Don't do this either"
public async Task<Order> CreateAsync(CreateOrderRequest request)
{
    if (request.Items is null || request.Items.Count == 0)
        throw new ArgumentException("items required", nameof(request));
    // ...
}
```

Now you have validation logic that throws an exception the controller has to catch and translate. The error format leaks even further. The endpoint and the service disagree on what "valid" means. And the analyzer that should have flagged the missing check at the endpoint never fires, because the endpoint *technically* compiles fine.

## The principle: validate at the boundary

Input validation is **schema enforcement**. It answers one question: *can I trust the shape of this payload enough to hand it to the domain?* That is a transport-level concern, not a domain concern. The right place to enforce it is the API boundary — the same layer that already deserializes JSON, binds form data, and resolves DI.

The domain has a different job: enforce **invariants** (a `Money` cannot be negative, an `Order` cannot transition from `Cancelled` to `Shipped`). Invariants belong on aggregate roots, expressed via private setters and behavior methods. Confusing the two produces validators that re-check what the constructor already guarantees, and aggregate roots that throw `ArgumentException` to signal what should have been a `422`.

Two checks, two layers, two error formats — both correct in their own scope:

| Layer | Concern | Failure response |
| ----- | ------- | ---------------- |
| API boundary | Schema (required, length, format) | `422 Unprocessable Entity` with structured codes |
| Domain | Invariant (state machine, business rules) | `409 Conflict` or domain exception → `400` |

This article focuses on the first one — and shows how Granit makes it impossible to forget.

## FluentValidation done right

FluentValidation has been the de-facto .NET validator for a decade. It works by declaring rules in a class that targets a request type:

```csharp title="CreateOrderRequestValidator.cs"
public sealed class CreateOrderRequestValidator : GranitValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.CustomerEmail)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(254);

        RuleFor(x => x.Items)
            .NotEmpty()
            .Must(items => items.Count <= 100)
            .WithErrorCodeAndMessage("Granit:Validation:TooManyItems");

        RuleForEach(x => x.Items).SetValidator(new OrderItemRequestValidator());

        RuleFor(x => x.Total)
            .GreaterThan(0);
    }
}
```

`GranitValidator<T>` is a thin base class that emits **structured error codes** (`Granit:Validation:NotEmptyValidator`, `Granit:Validation:EmailValidator`) instead of raw English strings. The frontend resolves codes to localized messages via `GET /api/granit/localization`. No backend code change ships a French translation; no hardcoded English leaks into a German UI.

For custom rules, `WithErrorCodeAndMessage("Granit:Validation:TooManyItems")` keeps the code and the message key in sync — they are the same value. Diverging the two is the single most common cause of "validation passes but UI shows nothing", and the helper makes it impossible.

## Wiring it up — the wrong way

The classic FluentValidation tutorial tells you to add a service registration and an explicit filter per endpoint:

```csharp title="Program.cs — Don't do this in 2026"
builder.Services.AddValidatorsFromAssemblyContaining<CreateOrderRequestValidator>();

app.MapPost("/orders", async (CreateOrderRequest req, IValidator<CreateOrderRequest> v, ...) =>
{
    var result = await v.ValidateAsync(req);
    if (!result.IsValid)
        return Results.ValidationProblem(result.ToDictionary());
    // ...
});
```

Two problems: every endpoint has to remember to inject the validator and call it, and the moment somebody adds a new endpoint without those four lines, validation silently disappears for that route. Code review will not catch it. Tests will not catch it unless somebody writes a "did I forget to validate?" test for every endpoint, every time.

## Wiring it up — the right way

Granit ships an **endpoint filter** that runs validation automatically for every argument bound from the request body. You opt in once, on the route group, and every endpoint in the group is covered:

```csharp title="OrderEndpoints.cs — Do this"
public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGranitGroup("/orders")
            .WithTags("Orders");

        group.MapPost("/", CreateAsync)
            .WithName("CreateOrder")
            .WithSummary("Creates a new order.")
            .WithDescription("Validates the payload, charges the customer, and returns the created order.")
            .Produces<OrderResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        return endpoints;
    }

    private static async Task<Created<OrderResponse>> CreateAsync(
        CreateOrderRequest request,
        IOrderService service,
        CancellationToken cancellationToken)
    {
        var order = await service.CreateAsync(request, cancellationToken);
        return TypedResults.Created($"/orders/{order.Id}", order.ToResponse());
    }
}
```

`MapGranitGroup("/orders")` is the only difference from a stock `MapGroup("/orders")`. Under the hood, it adds a `FluentValidationAutoEndpointFilter` that:

1. Inspects every endpoint argument at request time.
2. Skips primitives, `Guid`, `DateTime`, `IFormFile`, `HttpContext`, `ClaimsPrincipal`, and `CancellationToken` — none of those have validators.
3. For each remaining complex argument, looks up `IValidator<T>` in the request scope.
4. If a validator exists, runs it. If validation fails, returns `422 Unprocessable Entity` with `HttpValidationProblemDetails`.
5. If no validator is registered, passes through silently.

The handler never sees an invalid payload. There is no `if (!ModelState.IsValid)` line to forget. The endpoint signature stays focused on the happy path.

## Auto-discovery: zero registration ceremony

Granit's validation module auto-discovers every `IValidator<T>` from every loaded module assembly at startup:

```csharp title="GranitValidationModule.cs (excerpt)"
foreach (Assembly assembly in context.ModuleAssemblies)
{
    context.Services.AddValidatorsFromAssembly(
        assembly, ServiceLifetime.Scoped, includeInternalTypes: true);
}
```

Drop a new validator class anywhere in your module, build, and it works. No `AddValidatorsFromAssemblyContaining<>` per project. No manual registration list that drifts out of date. The same convention applies to the validators shipped with `Granit.Validation.Europe` (Belgian NISS, French RPPS), `Granit.Validation.NorthAmerica`, and `Granit.Validation.UnitedKingdom` — install the package, get the validators.

## Why 422, not 400

`MapGranitGroup` returns `422 Unprocessable Entity` for validation failures, not `400 Bad Request`. The distinction matters and is enforced by RFC 9110:

- **`400 Bad Request`** — the server cannot parse the request at all. Malformed JSON, missing required header, invalid Content-Type. The client must fix its serializer.
- **`422 Unprocessable Entity`** — the syntax is fine, but the values do not satisfy the API's semantic rules. The client knows how to fix it: change the data and retry.

Frontend code can handle the two cases differently: a `400` is a bug, a `422` is user input the form should highlight. Conflating them forces every client to parse the body to figure out which is which. Returning `422` is one of those small details that turns "the API works" into "the API is pleasant to integrate".

## Free OpenAPI enrichment

Because validators are first-class .NET classes, Granit reads them at OpenAPI generation time and projects the rules into the schema:

```csharp title="FluentValidationSchemaTransformer.cs (excerpt)"
public Task TransformAsync(
    OpenApiSchema schema,
    OpenApiSchemaTransformerContext context,
    CancellationToken cancellationToken)
{
    Type schemaType = context.JsonTypeInfo.Type;
    if (scope.ServiceProvider.GetService(validatorType) is not IValidator validator)
    {
        return Task.CompletedTask;
    }

    IValidatorDescriptor descriptor = validator.CreateDescriptor();
    // emit maxLength, minLength, pattern, minimum, maximum, required, format
    // ...
}
```

The result: `maxLength`, `minLength`, `pattern`, `minimum`, `maximum`, `required`, and `format` end up in the generated `openapi.json`. Frontend code generators (Kiota, NSwag, openapi-typescript) pick them up automatically. Your TypeScript types know the email field has a max length of 254. Your Zod schemas can be derived without manual reconciliation.

Custom domain validators that have no standard OpenAPI equivalent — IBAN, E.164 phone, Belgian NISS — are exposed as `x-granit-validator` extensions, and a paired `WithPatternHint("Granit:Validation:Hints:Alpha2Code")` lets the frontend show a human-readable hint ("2 uppercase letters") instead of the raw regex.

## Opting out — when you have to

Sometimes you have an endpoint where the body cannot be validated declaratively (uploads with binary metadata, intentionally permissive admin endpoints). Add one attribute and the filter skips that route:

```csharp title="DiagnosticsEndpoints.cs"
group.MapPost("/raw-import", RawImportHandler)
    .WithMetadata(new SkipAutoValidationAttribute());
```

Per-endpoint opt-out is intentional: it shows up in code review, it is greppable, and it does not weaken the default for every other endpoint in the group.

## Architecture tests as the safety net

The convention is enforced at build time by `ValidationConventionTests`, which scans the assembly and fails the build if:

- A `*Request` record exists without a matching `IValidator<T>`.
- A route group is created with stock `MapGroup` instead of `MapGranitGroup`.
- A `WithMessage("hardcoded English string")` slips into a validator instead of `WithErrorCodeAndMessage("Granit:Validation:Code")`.

These tests are not optional. They run in the same shard as every other architecture test (`tests/Granit.ArchitectureTests`) and the CI gate refuses to merge a PR that violates them. The result: a junior dev cannot accidentally bypass validation, even by copy-pasting from the wrong tutorial.

## Where invariants belong

Validation at the boundary stops most malformed input. It does not — and should not — try to encode every business rule. Here is the dividing line:

| Concern | Where | Example |
| ------- | ----- | ------- |
| Required field | Validator | `RuleFor(x => x.Email).NotEmpty()` |
| Format / length | Validator | `EmailAddress()`, `MaximumLength(254)` |
| Cross-field consistency | Validator | `When(x => x.IsRecurring, () => RuleFor(x => x.Interval).NotNull())` |
| Aggregate state machine | Aggregate root | `Order.Ship()` throws if `Status != Confirmed` |
| Cross-aggregate invariant | Domain service | "User has not exceeded daily order limit" |
| Async uniqueness check | Domain or app service | "Email is not already taken" |

The async uniqueness case is the one most teams get wrong. They put it in the validator with `MustAsync`, which means the validator now needs a `DbContext` and runs an extra query on every request — including the ones that are about to fail because of trivially missing fields. Push uniqueness checks into the application service, where they share the transaction with the actual write and produce a `409 Conflict`, not a `422`.

## Key takeaways

- **Validate exactly once, at the API boundary.** Scattering checks across controllers, services, and entities produces drift, duplication, and silent gaps.
- **`MapGranitGroup` runs validation automatically** for every endpoint in the group. No per-endpoint filter, no `IValidator<T>` injection, no boilerplate.
- **Return `422 Unprocessable Entity`** for semantic validation failures, not `400 Bad Request`. The client can act on the difference.
- **Use structured error codes** (`Granit:Validation:*`) instead of hardcoded strings. The frontend localizes them via `GET /api/granit/localization`.
- **OpenAPI gets the rules for free** — `maxLength`, `pattern`, `required`, etc. flow into the generated schema, so frontend code generators stay accurate.
- **Domain invariants stay on aggregate roots.** Validation is for shape; the domain owns business rules.

## Further reading

- [Granit.Validation module reference](/dotnet/core/validation/)
- [RFC 7807 Problem Details — The Only Way to Return Errors](/blog/rfc-7807-problem-details-the-only-way-to-return-errors/)
- [Never Return EF Entities](/blog/never-return-ef-entities/) — the other side of the boundary
- [Guard Clauses Done Right](/blog/guard-clauses-done-right/) — when invariants belong in the constructor
