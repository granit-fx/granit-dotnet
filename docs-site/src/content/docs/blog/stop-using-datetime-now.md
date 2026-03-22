---
title: "Stop Using DateTime.Now — Inject TimeProvider Instead"
date: 2026-02-14
sidebar:
  order: 2
authors:
  - jfmeyers
tags:
  - best-practice
  - testing
description: "DateTime.Now makes your code untestable and timezone-fragile. Here is how to replace it with TimeProvider and IClock for deterministic, test-friendly time."
excerpt: >
  DateTime.Now makes your code untestable and timezone-fragile. Here is how to
  replace it with TimeProvider and IClock for deterministic, test-friendly time.
---

Your tests pass at 2 PM and fail at midnight. A report generates the wrong date for users in Tokyo. A scheduled job fires twice during a DST transition. The root cause is always the same: **a static call to `DateTime.Now` buried somewhere in your business logic**.

## The problem

`DateTime.Now` and `DateTime.UtcNow` are static. You cannot mock them, swap them, or control them in tests. They also return `DateTime` instead of `DateTimeOffset`, which silently drops timezone information.

```csharp title="InvoiceService.cs — Don't do this"
public class InvoiceService
{
    public Invoice CreateInvoice(Order order)
    {
        return new Invoice
        {
            OrderId = order.Id,
            IssuedAt = DateTime.UtcNow, // Untestable. What date will the test assert?
            DueDate = DateTime.UtcNow.AddDays(30), // DST? Timezone? Good luck.
        };
    }
}
```

Three problems in two lines:

- **Untestable** — you cannot assert on a value that changes every millisecond.
- **Timezone-blind** — `DateTime` has no offset. Is this UTC? Local? Server time?
- **Non-deterministic** — the same code produces different results depending on when and where it runs.

## The fix: TimeProvider + IClock

.NET 8 introduced **`TimeProvider`**, a built-in abstraction for time. Granit wraps it in **`IClock`**, which adds timezone-aware conversions for multi-tenant applications.

```csharp title="InvoiceService.cs — Do this instead"
public class InvoiceService(IClock clock)
{
    public Invoice CreateInvoice(Order order)
    {
        var now = clock.Now; // DateTimeOffset, always UTC
        return new Invoice
        {
            OrderId = order.Id,
            IssuedAt = now,
            DueDate = now.AddDays(30),
        };
    }
}
```

`IClock.Now` returns a **`DateTimeOffset`** in UTC. No ambiguity, no static coupling.

## How Granit registers it

`GranitTimingModule` wires everything as singletons — zero configuration needed:

```csharp title="TimingServiceCollectionExtensions.cs"
services.TryAddSingleton(TimeProvider.System);
services.TryAddSingleton<ICurrentTimezoneProvider, CurrentTimezoneProvider>();
services.TryAddSingleton<IClock, Clock>();
```

`TimeProvider.System` is the standard .NET provider: thread-safe, stateless, production-ready. The `Clock` class delegates to it:

```csharp title="Clock.cs"
public sealed class Clock(
    TimeProvider timeProvider,
    ICurrentTimezoneProvider timezoneProvider) : IClock
{
    public DateTimeOffset Now => timeProvider.GetUtcNow();
}
```

If your module depends on `GranitTimingModule`, `IClock` is available for injection everywhere — services, handlers, interceptors.

## Testing with FakeTimeProvider

This is where the pattern pays off. Microsoft ships **`FakeTimeProvider`** in `Microsoft.Extensions.Time.Testing` — a drop-in replacement that lets you control time deterministically.

```csharp title="InvoiceServiceTests.cs"
public sealed class InvoiceServiceTests
{
    private readonly FakeTimeProvider _fakeTime = new();
    private readonly InvoiceService _sut;

    public InvoiceServiceTests()
    {
        var timezoneProvider = Substitute.For<ICurrentTimezoneProvider>();
        var clock = new Clock(_fakeTime, timezoneProvider);
        _sut = new InvoiceService(clock);
    }

    [Fact]
    public void CreateInvoice_SetsDueDateTo30DaysFromNow()
    {
        // Arrange — freeze time
        var issued = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);
        _fakeTime.SetUtcNow(issued);

        // Act
        var invoice = _sut.CreateInvoice(new Order { Id = Guid.NewGuid() });

        // Assert — deterministic, no flaky test
        invoice.IssuedAt.ShouldBe(issued);
        invoice.DueDate.ShouldBe(issued.AddDays(30));
    }
}
```

No mocking framework needed for time. No `Thread.Sleep`. No "it works if you run it fast enough". The test is **deterministic**: it passes at 3 AM, on CI, on any timezone.

## Granit enforces it with a Roslyn analyzer

Forgetting to use `IClock` is easy. Granit makes it hard. The **`DateTimeNowAnalyzer`** (diagnostic `GRSEC001`) flags any direct use of `DateTime.Now`, `DateTime.UtcNow`, or `DateTimeOffset.Now` in your source code.

```
GRSEC001: Use IClock or TimeProvider instead of DateTime.Now
```

The analyzer runs at build time. You cannot accidentally ship code that bypasses the abstraction.

## Key takeaways

- **Never use `DateTime.Now` or `DateTime.UtcNow`** in application code. Inject `IClock` or `TimeProvider` instead.
- **`IClock.Now`** always returns `DateTimeOffset` in UTC — no timezone ambiguity.
- **`FakeTimeProvider`** makes time-dependent tests deterministic with zero effort.
- **Granit enforces this at build time** with the `GRSEC001` Roslyn analyzer. You cannot forget.

## Further reading

- [Core & Utilities reference](/dotnet/core/module-system/) — IClock, TimeProvider, IGuidGenerator
- [Testing stack (ADR-003)](/dotnet/architecture/adr/003-testing-stack/) — xUnit, Shouldly, FakeTimeProvider
