---
title: "Guard Clauses Done Right"
date: 2026-02-28
sidebar:
  order: 6
authors:
  - jfmeyers
tags:
  - best-practice
  - code-quality
description: "Manual null checks are noisy and inconsistent. .NET provides built-in guard methods that are cleaner, faster, and throw the right exceptions every time."
excerpt: >
  Manual null checks are noisy and inconsistent. .NET provides built-in guard
  methods that are cleaner, faster, and throw the right exceptions every time.
---

Every .NET codebase has dozens of methods that start with the same ritual: check a parameter, throw if it is null, repeat. The code is correct but **verbose, inconsistent, and easy to get wrong**. Since .NET 6, there is a better way — and Granit enforces it across all 100+ packages.

## The problem

Manual null checks are boilerplate. They are also a source of subtle bugs: typos in `nameof()`, wrong exception types, or checks that simply get forgotten during a late-night commit.

```csharp title="TenantService.cs — Don't do this"
public class TenantService
{
    public async Task<Tenant> GetTenantAsync(string tenantId, IDbConnection connection)
    {
        if (tenantId == null)
            throw new ArgumentNullException(nameof(tenantId));

        if (string.IsNullOrEmpty(tenantId))
            throw new ArgumentException("Value cannot be empty.", nameof(tenantId));

        if (connection == null)
            throw new ArgumentNullException(nameof(connection));

        // Business logic starts here — after 8 lines of ceremony.
        return await connection.QuerySingleAsync<Tenant>(tenantId);
    }
}
```

Three parameters, eight lines of guard code. Every developer writes these slightly differently: some use `is null`, some use `== null`, some throw `ArgumentException` for empty strings, some do not. The inconsistency adds up.

Worse, if you rename a parameter and forget to update the `nameof()`, the exception message points to a parameter that no longer exists. The compiler will not catch it.

## The fix: built-in ThrowIf* methods

.NET ships a family of **static guard methods** on the exception types themselves. They validate the argument and throw the correct exception with the correct parameter name — automatically.

```csharp title="TenantService.cs — Do this instead"
public class TenantService
{
    public async Task<Tenant> GetTenantAsync(string tenantId, IDbConnection connection)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        ArgumentNullException.ThrowIfNull(connection);

        return await connection.QuerySingleAsync<Tenant>(tenantId);
    }
}
```

Two lines instead of eight. No `nameof()`. No chance of a mismatch.

## How it works: CallerArgumentExpression

The magic behind these methods is **`[CallerArgumentExpression]`**, a compiler attribute introduced in C# 10. When you write:

```csharp
ArgumentNullException.ThrowIfNull(connection);
```

The compiler rewrites it at compile time to pass `"connection"` as the parameter name. You get the correct name in the exception message without writing it yourself. Rename the variable, and the exception message updates automatically.

## The full ThrowIf* family

.NET provides guard methods for the most common precondition checks. Here is the complete set available in .NET 10:

| Method | Throws | Use case |
|--------|--------|----------|
| `ArgumentNullException.ThrowIfNull(value)` | `ArgumentNullException` | Null reference or nullable value type |
| `ArgumentException.ThrowIfNullOrEmpty(value)` | `ArgumentNullException` / `ArgumentException` | Null or empty string |
| `ArgumentException.ThrowIfNullOrWhiteSpace(value)` | `ArgumentNullException` / `ArgumentException` | Null, empty, or whitespace string |
| `ArgumentOutOfRangeException.ThrowIfZero(value)` | `ArgumentOutOfRangeException` | Numeric zero |
| `ArgumentOutOfRangeException.ThrowIfNegative(value)` | `ArgumentOutOfRangeException` | Negative number |
| `ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value)` | `ArgumentOutOfRangeException` | Zero or negative |
| `ArgumentOutOfRangeException.ThrowIfGreaterThan(value, other)` | `ArgumentOutOfRangeException` | Value exceeds upper bound |
| `ArgumentOutOfRangeException.ThrowIfLessThan(value, other)` | `ArgumentOutOfRangeException` | Value below lower bound |
| `ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(value, other)` | `ArgumentOutOfRangeException` | Value at or above bound |
| `ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, other)` | `ArgumentOutOfRangeException` | Value at or below bound |
| `ArgumentOutOfRangeException.ThrowIfEqual(value, other)` | `ArgumentOutOfRangeException` | Value equals a forbidden value |
| `ArgumentOutOfRangeException.ThrowIfNotEqual(value, other)` | `ArgumentOutOfRangeException` | Value does not equal expected |
| `ObjectDisposedException.ThrowIf(condition, instance)` | `ObjectDisposedException` | Object already disposed |

These cover the vast majority of parameter validation. For domain-specific preconditions (entity not found, invalid state transitions), Granit uses **semantic exceptions** like `NotFoundException` and `BusinessException` instead — see the [Guard Clause pattern page](/dotnet/architecture/patterns/guard-clause/).

## A real-world example from Granit

Here is how `ThrowIf*` methods look in practice, taken from a typical Granit service:

```csharp title="BlobStorageService.cs"
public async Task<BlobDescriptor> UploadAsync(
    string containerName,
    Stream content,
    string fileName,
    string? contentType = null,
    CancellationToken cancellationToken = default)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
    ArgumentNullException.ThrowIfNull(content);
    ArgumentException.ThrowIfNullOrEmpty(fileName);
    ArgumentOutOfRangeException.ThrowIfZero(content.Length);

    // All preconditions validated — business logic starts clean.
    var descriptor = new BlobDescriptor(containerName, fileName, contentType);
    await _storage.PutAsync(descriptor, content, cancellationToken);
    return descriptor;
}
```

Four guards, four lines. Each one throws the **right exception type** with the **right parameter name**. No `nameof()`, no string literals, no room for error.

## Why it matters

This is not just about saving keystrokes. The consistency has real benefits:

- **Correct exception types** — `ThrowIfNullOrEmpty` throws `ArgumentNullException` for null and `ArgumentException` for empty. Manual code often gets this wrong.
- **Refactoring-safe** — `CallerArgumentExpression` tracks the parameter name at compile time. Rename freely.
- **Performance** — these methods are implemented with `[StackTraceHidden]` and aggressive inlining. The JIT optimizes the non-throwing path to near zero overhead.
- **Consistency** — across 93 Granit packages, every guard clause looks the same. New contributors read one, they have read them all.

## Further reading

- [Core & Utilities reference](/dotnet/core/module-system/) — Module system, shared domain types, guard patterns
- [Guard Clause pattern](/dotnet/architecture/patterns/guard-clause/) — Semantic exceptions and RFC 7807 ProblemDetails
