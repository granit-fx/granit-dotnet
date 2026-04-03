---
title: "[LoggerMessage] Over String Interpolation"
date: 2026-04-03
sidebar:
  order: 18
authors:
  - jfmeyers
tags:
  - best-practice
  - performance
  - observability
  - code-quality
description: "String interpolation in log calls allocates on every invocation, even when the log level is disabled. Source-generated [LoggerMessage] eliminates that cost entirely."
excerpt: >
  String interpolation in log calls allocates on every invocation, even when the
  log level is disabled. Source-generated [LoggerMessage] eliminates that cost
  entirely and gives you structured logs for free.
---

Your API processes 5,000 requests per second. Every request logs 4-6 messages. That is 30,000 string allocations per second — even when your log level is set to `Warning` and none of those `Debug` lines ever reach a sink. The strings are built, hashed, compared, and garbage-collected for nothing. The fix has been in the framework since .NET 6, but most codebases still do not use it.

## The problem with string interpolation in logs

Consider this line:

```csharp title="OrderService.cs — Don't do this"
logger.LogInformation($"Processing order {orderId} for tenant {tenantId}");
```

Three things happen **every time this line executes**, regardless of the configured log level:

1. **String allocation** — the runtime interpolates `orderId` and `tenantId` into a new string, calling `.ToString()` on each value.
2. **No structured data** — the resulting string is opaque. Your log aggregator (Seq, Loki, Datadog) cannot index `orderId` as a searchable field.
3. **No level guard** — the `ILogger` extension method checks `IsEnabled` internally, but only *after* the caller has already paid the allocation cost.

Even the message-template overload has a subtle cost:

```csharp title="OrderService.cs — Better, but not ideal"
logger.LogInformation("Processing order {OrderId} for tenant {TenantId}",
    orderId, tenantId);
```

This gives you structured logging, but the arguments are still boxed into an `object[]` on every call. For `Guid` parameters, that means two boxing allocations plus the array. When the level is disabled, all of that work is thrown away.

## The solution: `[LoggerMessage]` source generation

The `[LoggerMessage]` attribute, introduced in .NET 6, instructs the Roslyn source generator to emit a high-performance logging method at compile time:

```csharp title="OrderService.cs — Do this"
public sealed partial class OrderService(ILogger<OrderService> logger)
{
    public void Process(Guid orderId, Guid tenantId)
    {
        LogProcessingOrder(orderId, tenantId);
        // ...
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Processing order {OrderId} for tenant {TenantId}")]
    private partial void LogProcessingOrder(Guid orderId, Guid tenantId);
}
```

The generated code does three things the hand-written version cannot:

1. **Level check first** — the generated method calls `logger.IsEnabled(LogLevel.Information)` *before* touching any argument. If the level is disabled, it returns immediately. Zero allocations.
2. **No boxing** — arguments are passed through generic `LogStateHolder<T>` structs, not `object[]`. Value types stay on the stack.
3. **Cached message template** — the format string is parsed once at startup and reused. No per-call parsing.

## What the generated code actually looks like

When you write a `[LoggerMessage]` partial method, the source generator emits something like this (simplified):

```csharp title="Generated code (simplified)"
private static readonly Action<ILogger, Guid, Guid, Exception?> s_logProcessingOrder =
    LoggerMessage.Define<Guid, Guid>(
        LogLevel.Information,
        new EventId(0, nameof(LogProcessingOrder)),
        "Processing order {OrderId} for tenant {TenantId}");

private void LogProcessingOrder(Guid orderId, Guid tenantId)
{
    if (logger.IsEnabled(LogLevel.Information))
    {
        s_logProcessingOrder(logger, orderId, tenantId, null);
    }
}
```

The `Action<>` delegate is allocated once and cached in a static field. Every subsequent call is a direct delegate invocation — no reflection, no allocation, no parsing.

## Real-world patterns from Granit

Across 190+ source files, the Granit framework uses `[LoggerMessage]` exclusively. Here are patterns worth studying.

### Dedicated log class for a module

When a module has many log messages, extract them into a dedicated `static partial` class:

```csharp title="RateLimitingLog.cs"
internal static partial class RateLimitingLog
{
    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Rate limit exceeded for policy '{PolicyName}' (tenant: {TenantId}). " +
                  "Remaining: {Remaining}, RetryAfter: {RetryAfterSeconds}s.")]
    public static partial void LogRateLimitExceeded(
        ILogger logger, string policyName, string? tenantId,
        int remaining, double retryAfterSeconds);

    [LoggerMessage(Level = LogLevel.Trace,
        Message = "Rate limit checked for policy '{PolicyName}' (tenant: {TenantId}). " +
                  "Remaining: {Remaining}/{Limit}.")]
    public static partial void LogRateLimitChecked(
        ILogger logger, string policyName, string? tenantId,
        int remaining, int limit);
}
```

Key details: the class is `static partial`, methods are `public static partial`, and `ILogger` is passed as the first parameter. This pattern keeps handler/service classes focused on business logic.

### Event IDs for correlation

Assign numeric event IDs when you need to correlate log entries in a pipeline:

```csharp title="ExportOrchestrator.cs"
internal sealed partial class ExportOrchestrator
{
    [LoggerMessage(1, LogLevel.Information,
        "Export job {ExportJobId} queued for '{DefinitionName}' format '{Format}'")]
    private partial void LogExportQueued(
        Guid exportJobId, string definitionName, string format);

    [LoggerMessage(2, LogLevel.Information,
        "Export job {ExportJobId} completed for '{DefinitionName}' ({RowCount} rows)")]
    private partial void LogExportCompleted(
        Guid exportJobId, string definitionName, int rowCount);

    [LoggerMessage(3, LogLevel.Error,
        "Export job {ExportJobId} failed")]
    private partial void LogExportFailed(Guid exportJobId, Exception ex);
}
```

The positional syntax (`1, LogLevel.Information, "..."`) is shorthand for `EventId = 1, Level = ..., Message = ...`. Event IDs are stable identifiers — your monitoring can alert on `EventId == 3` without fragile string matching.

### Exception as first parameter

When logging an exception, pass it as the **first parameter** (by convention) or anywhere in the signature — the source generator recognizes `Exception` parameters automatically:

```csharp title="NotificationDeliveryHandler.cs"
[LoggerMessage(Level = LogLevel.Warning,
    Message = "Notification delivery failed via '{ChannelName}' " +
              "for delivery {DeliveryId} notification {NotificationId}")]
private partial void LogNotificationDeliveryFailed(
    Exception exception, string channelName,
    Guid deliveryId, Guid notificationId);
```

The `Exception` is **not** interpolated into the message string. It is attached as structured data — your log sink renders the stack trace separately, and you can filter on exception type.

### Compliance-critical logging

For audit trails (ISO 27001) or systems where log gaps are unacceptable, use `LogLevel.Critical` to distinguish lost data from ordinary errors:

```csharp title="AuditingPersistenceWorker.cs"
[LoggerMessage(Level = LogLevel.Warning,
    Message = "Audit persistence attempt {Attempt}/{MaxAttempts} failed, retrying")]
private partial void LogPersistenceRetry(
    int attempt, int maxAttempts, Exception exception);

[LoggerMessage(Level = LogLevel.Critical,
    Message = "Audit log entry dropped after all retry attempts " +
              "— audit trail gap (ISO 27001 A.12.4)")]
private partial void LogPersistenceDropped(Exception exception);
```

The `Critical` level triggers a different alert pipeline — PagerDuty instead of Slack, immediate escalation instead of daily digest. That distinction only works when your log levels are intentional, not random.

## The three levels of wrong

To summarize the progression from worst to best:

| Approach | Structured | Allocation when disabled | Boxed args |
|----------|-----------|------------------------|------------|
| `$"Order {id}"` | No | Yes (string) | N/A |
| `"Order {Id}", id` | Yes | Yes (object[]) | Yes |
| `[LoggerMessage]` | Yes | **None** | **No** |

## PII: the compliance angle

Structured logging makes it trivially easy to accidentally log personal data. When you write `$"User {email} logged in"`, the email address is baked into an opaque string — you cannot redact it downstream.

With `[LoggerMessage]`, parameter names are explicit and scannable. Granit enforces this with a Roslyn analyzer (`GRSEC011`) that flags PII-indicative placeholder names (`Email`, `Phone`, `IpAddress`, `Token`) at compile time:

```text
warning GRSEC011: LoggerMessage template contains PII placeholder '{Email}'.
Log identifiers (userId, correlationId) instead of PII values.
GDPR Art. 5 — data minimization.
```

Architecture tests reinforce this: `LoggerMessagePiiConventionTests` scans every `[LoggerMessage]` in the solution and fails the build if a template parameter matches a PII pattern without a documented exemption.

The result: **zero PII in log output, enforced at compile time**. No grep-and-pray audits before a compliance review.

## Migration checklist

Adopting `[LoggerMessage]` in an existing codebase is mechanical:

1. **Add `partial` to the class** — the source generator needs a partial class to emit the generated method.
2. **Replace each `logger.Log*()` call** with a `[LoggerMessage]` partial method. Name the method `Log` + what happened (`LogOrderCreated`, `LogRetryFailed`).
3. **Move `Exception` to a parameter** — do not interpolate stack traces. The generator handles it.
4. **Use `PascalCase` placeholders** — `{OrderId}` not `{orderId}`. This is the structured logging convention and what log aggregators index on.
5. **Assign event IDs** for messages you alert on. Skip them for debug/trace noise.
6. **Run `dotnet build`** — the generator validates your method signatures. Mismatched parameter names or missing `partial` keywords are compile errors, not runtime surprises.

## Key takeaways

- **String interpolation in log calls allocates on every invocation**, even when the log level is disabled. At scale, this is measurable GC pressure.
- **`[LoggerMessage]` source generation** eliminates allocations, avoids boxing, and caches the message template — all at compile time.
- **Structured parameters** let log aggregators index and filter on individual fields instead of parsing strings.
- **PII safety** becomes enforceable at build time when parameters are explicitly named — Roslyn analyzers can flag sensitive data before it reaches production logs.
- **Migration is mechanical**: add `partial`, extract methods, assign event IDs. No architectural changes required.

## Further reading

- [Anti-patterns: string interpolation in log calls](/troubleshooting/anti-patterns/)
- [Coding standards: logging conventions](/contributing/coding-standards/)
- [Observability: PII redaction](/dotnet/core/observability/)
- [Analyzers: GRSEC011 rule](/dotnet/core/analyzers/)
- [[GeneratedRegex] Is Not Optional — Compiled Regex Is Dead](/blog/generated-regex-is-not-optional/) — same source-generation philosophy applied to regex
- [Stop Using DateTime.Now](/blog/stop-using-datetime-now/) — another "the runtime gives you better tools" best-practice
