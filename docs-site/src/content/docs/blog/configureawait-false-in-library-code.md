---
title: "ConfigureAwait(false) in Library Code — Still Relevant in .NET 10?"
date: 2026-04-29
authors:
  - jfmeyers
tags:
  - best-practice
  - async
  - performance
  - code-quality
description: "ASP.NET Core dropped the synchronization context years ago. So why do every Granit library and the .NET runtime itself still write ConfigureAwait(false) on every await? Because libraries do not get to choose their host."
excerpt: >
  ASP.NET Core dropped the synchronization context years ago. So why do every
  Granit library and the .NET runtime itself still write ConfigureAwait(false)
  on every await? Because libraries do not get to choose their host.
---

Every junior dev who reads modern ASP.NET Core code asks the same question. "If ASP.NET Core has no synchronization context, why is the codebase peppered with `.ConfigureAwait(false)`? Isn't that obsolete?" The short answer is no, but the longer answer is more interesting — and it explains why **library code** plays by different rules than **app code**, even in 2026.

## What `ConfigureAwait(false)` actually does

When you `await` a `Task`, the compiler captures the **current `SynchronizationContext`** (or the current `TaskScheduler` if there is none) and resumes the continuation on that context. `ConfigureAwait(false)` tells the compiler not to capture it — resume on whatever thread the awaited operation completes on, usually a thread-pool thread.

```csharp title="What the compiler sees"
// With default behavior (capture context)
await dbContext.SaveChangesAsync();
// Compiler: capture SyncContext, resume continuation on it

// With ConfigureAwait(false)
await dbContext.SaveChangesAsync().ConfigureAwait(false);
// Compiler: do not capture, resume on any thread
```

That single bool determines whether your continuation runs on the UI thread, the request thread, or a thread-pool thread. In code that does not need a specific thread to run on (most of it), capturing the context is at best wasted work and at worst a deadlock generator.

## Why the rule exists at all

Two real failure modes drove the rule into existence:

### 1. Deadlocks in apps with a captured context

The classic blocking-on-async deadlock:

```csharp title="DemoController.cs (WPF / WinForms / classic ASP.NET)"
public IActionResult Index()
{
    // BAD: synchronously waits on async work
    var data = _service.GetDataAsync().Result;
    return View(data);
}
```

```csharp title="DataService.cs"
public async Task<Data> GetDataAsync()
{
    // Default: captures the calling context (UI thread / ASP.NET request thread)
    var raw = await _http.GetStringAsync("...");
    return Parse(raw);
}
```

The continuation needs to resume on the captured context. The captured context is the one thread the caller has blocked by waiting on `.Result`. Deadlock. Add `ConfigureAwait(false)` inside `GetDataAsync` and the continuation runs on the thread pool, the method completes, the `.Result` returns, the world keeps turning.

### 2. Performance overhead

Even when there is no deadlock, capturing and posting back to the context costs. `SynchronizationContext.Post` is not free — it queues a delegate, which the context dispatcher eventually pumps. On a UI thread that is exactly what you want; on a hot async pipeline buried five layers deep in a library, you pay that cost on every await for nothing.

## ASP.NET Core changed the calculus

Here is what flipped the conversation in 2018: **ASP.NET Core has no `SynchronizationContext`**. None. `SynchronizationContext.Current` is `null` inside an MVC action, a Minimal API endpoint, a SignalR hub method. The runtime made a deliberate choice to ditch the request-affinity model that classic ASP.NET inherited from WebForms.

Consequences:

- The classic `.Result` deadlock cannot happen in ASP.NET Core. There is nothing to capture, so there is nothing to be blocked behind.
- `ConfigureAwait(false)` is a no-op inside ASP.NET Core code. The compiler still emits the call, but the runtime has nothing to resume on either way.
- The Microsoft team that wrote ASP.NET Core stopped using `ConfigureAwait` in their own application code.

Stephen Toub's seminal "ConfigureAwait FAQ" landed the same year. The takeaway most developers remembered was *"ASP.NET Core doesn't need ConfigureAwait(false)"*, which is true. The takeaway most developers forgot was *"libraries still do"*, which is also true and matters more.

## The rule that actually matters in 2026

Application code and library code are not the same thing:

| Code type | `ConfigureAwait(false)` rule | Reason |
| --------- | ---------------------------- | ------ |
| ASP.NET Core endpoint, controller, BackgroundService | Optional | No SyncContext to capture |
| Console / worker service / CLI | Optional | No SyncContext to capture |
| WPF / WinForms / MAUI / Blazor Server / Xamarin | **Required** to avoid UI deadlocks | These hosts install a SyncContext |
| **Library code (NuGet packages, shared internal libs)** | **Required** | The library does not know which host it will run in |
| xUnit `async Task` test | Optional | xUnit removed its SyncContext |
| Roslyn analyzer / source generator | Required | Hosted by VS / Rider with rich SyncContexts |

**A NuGet package never gets to choose its host.** Today it ships into an ASP.NET Core 10 service. Next quarter, the same DLL is referenced from a MAUI app, a WPF tool, a SignalR hub, a Blazor Server page, an Office add-in. If a single `await` inside that library does not call `.ConfigureAwait(false)`, the day someone uses it from a UI thread and accidentally blocks on it, they hit a deadlock — and the bug report blames your package, not their `.Result`.

## The Granit answer

Granit is 128 NuGet packages. Every one of them is library code by definition. The rule across the codebase is non-negotiable:

> **Every `await` in `src/` calls `.ConfigureAwait(false)`. No exceptions, no analyzers turning a blind eye to a "fast path".**

A `grep` across the source tree turns up over 2,700 occurrences. The convention is enforced by the .NET CA2007 analyzer in library projects and by code review on every PR. New code without it does not merge.

```csharp title="EfStoreBase.cs (excerpt)"
protected async Task<TEntity?> FindAsync(
    Guid id, CancellationToken cancellationToken)
{
    return await DbContext.Set<TEntity>()
        .AsNoTracking()
        .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
        .ConfigureAwait(false);
}
```

```csharp title="GranitApplication.cs (excerpt)"
foreach (ModuleDescriptor module in modules)
{
    await module.Instance
        .ConfigureServicesAsync(context)
        .ConfigureAwait(false);
}
```

```csharp title="FluentValidationAutoEndpointFilter.cs (excerpt)"
ValidationResult result = await validator
    .ValidateAsync(validationContext, context.HttpContext.RequestAborted)
    .ConfigureAwait(false);
```

It looks repetitive because it is. That repetition is the point: a new contributor cannot accidentally introduce a host-specific assumption.

## Why "but it's a no-op in ASP.NET Core" is the wrong frame

The argument against the rule goes: *"99% of our users run our library on ASP.NET Core. The call is a no-op. Why pollute every line?"* Three reasons:

1. **The 1% will find you.** A WPF dev tool, a MAUI sample, a Blazor Server page — sooner or later somebody hosts your code on a thread with a context. That person did not read your README and does not know your library's "ASP.NET Core only" caveat.
2. **The convention is the enforcement mechanism.** "ConfigureAwait(false) on every await" is a one-line rule a junior dev can follow. "ConfigureAwait(false) when running outside ASP.NET Core, except in xUnit tests, except when you know the host has no context" is a rule nobody can follow consistently.
3. **The cost is one method call.** It is JIT-elided when the compiler can prove the context is null. The "noise" argument is aesthetic, not technical.

## What about `ConfigureAwait(ConfigureAwaitOptions)`?

.NET 8 added a richer overload with `ConfigureAwaitOptions` flags:

```csharp
await task.ConfigureAwait(ConfigureAwaitOptions.None);
await task.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
await task.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
await task.ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext);
```

This is mostly orthogonal to the original `ConfigureAwait(false)` debate. The flags exist for niche scenarios — `SuppressThrowing` lets you await a faulted task without re-throwing (handy when you only care about completion), `ForceYielding` guarantees the continuation runs asynchronously even if the task already completed.

For library code, the rule stays the same: pass `ConfigureAwaitOptions.None` (the equivalent of `ConfigureAwait(false)`) when you call this overload at all. Most library code never needs the new flags and keeps the simpler `ConfigureAwait(false)` form.

## What about `await using` and `await foreach`?

Both have their own `ConfigureAwait` overloads. Library code should use them:

```csharp title="Async disposal"
await using var connection = new SqlConnection(connectionString)
    .ConfigureAwait(false);
```

```csharp title="Async enumeration"
await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
{
    // ...
}
```

`WithCancellation` and `ConfigureAwait` on `IAsyncEnumerable` are extension methods, so they read like normal awaits. Granit applies them everywhere — `EfStoreBase`, the export orchestrator, the SSE notification channel.

## Where you can stop writing it

If you are writing a top-level ASP.NET Core service that nobody else consumes — your application code, not your shared libraries — you can drop the calls. The decision should be deliberate:

- The project is **never** going to be referenced as a library. (No `<PackAsNuGet>true</PackAsNuGet>`, no internal sharing.)
- Every host that runs the code is known to have no `SynchronizationContext`. (ASP.NET Core, Worker Service, Console.)
- You accept that any future change to the deployment model — running the same code as a library, hosting from a UI app for testing — will require auditing every await.

For Granit, none of those bullets apply. Every project is potentially a library; the framework cannot know what hosts it. The rule stays.

## The ten-second decision tree

When reviewing a PR with a new `await`:

1. Is this code inside `src/` of a NuGet-published or internally-shared library? → **`.ConfigureAwait(false)` required.**
2. Is this code inside an ASP.NET Core endpoint, BackgroundService, or test? → Optional. Be consistent with the surrounding file.
3. Is this code inside a WPF, MAUI, Blazor Server, or VS extension? → **`.ConfigureAwait(false)` required, unless this specific await needs to resume on the UI thread.**
4. Are you sure? → Default to `.ConfigureAwait(false)`. The cost of getting it right is one method call. The cost of getting it wrong is a deadlock at 3 AM.

## Key takeaways

- **`ConfigureAwait(false)` is not obsolete.** It is a no-op in ASP.NET Core, but ASP.NET Core is not the only host your library will ever run in.
- **Library code uses it on every `await`.** The convention is the enforcement mechanism — there is no reliable conditional rule.
- **App code can skip it** in ASP.NET Core / Worker / Console — but only if you accept that "this stays an app forever" is a hard constraint.
- **UI hosts (WPF, MAUI, Blazor Server, VS extensions) still install a `SynchronizationContext`.** The classic deadlock can absolutely still happen there.
- **The new `ConfigureAwaitOptions` overload does not change the rule** — pass `None` (equivalent to `false`) in library code.
- **If in doubt, write it.** One method call, JIT-friendly, harmless when there is no context to suppress.

## Further reading

- [Stephen Toub — ConfigureAwait FAQ](https://devblogs.microsoft.com/dotnet/configureawait-faq/) — the canonical reference, still accurate
- [.NET 8 ConfigureAwaitOptions reference](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.configureawaitoptions)
- [Granit coding standards](/contributing/coding-standards/) — full async conventions
- [Stop Using DateTime.Now](/blog/stop-using-datetime-now/) — another "the rule still applies, despite the rumor that it doesn't" best-practice
