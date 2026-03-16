---
title: "[GeneratedRegex] Is Not Optional — Compiled Regex Is Dead"
date: 2026-02-21
authors:
  - jfmeyers
tags:
  - best-practice
  - performance
description: "new Regex(..., RegexOptions.Compiled) allocates at startup and hides patterns from the JIT. Source-generated regex is faster, safer, and enforced at build time."
excerpt: >
  new Regex(..., RegexOptions.Compiled) allocates at startup and hides patterns
  from the JIT. Source-generated regex is faster, safer, and enforced at build time.
---

You ship a validation library. It runs 200,000 regex matches per second against user input — email addresses, phone numbers, tax identifiers. Each `new Regex(..., RegexOptions.Compiled)` allocates IL at startup, blocks the first request, and makes the pattern invisible to static analysis. Meanwhile, a carefully crafted input triggers **catastrophic backtracking** and your API hangs for 30 seconds. The root cause is the same every time: **runtime-compiled regex with no timeout**.

## The problem

`RegexOptions.Compiled` generates IL at runtime. That sounds fast, but it comes with real costs:

- **Cold-start penalty** — the JIT compiles the pattern on first use, blocking the calling thread.
- **No static analysis** — the pattern is an opaque string. The compiler cannot verify it, warn about syntax errors, or optimize it.
- **No AOT support** — `Compiled` is incompatible with Native AOT. If you target `PublishAot`, those regex calls silently fall back to interpreted mode.
- **No ReDoS protection by default** — without an explicit timeout, a malicious input can pin a thread indefinitely.

```csharp title="EmailValidator.cs — Don't do this"
public static class EmailValidator
{
    // Allocated at class load, JIT-compiled at first call, no timeout
    private static readonly Regex EmailPattern = new(
        @"^[a-zA-Z0-9._%+\-]+@([a-zA-Z0-9\-]+\.)+[a-zA-Z]{2,}$",
        RegexOptions.Compiled);

    public static bool IsValid(string email) => EmailPattern.IsMatch(email);
}
```

This pattern has been the default recommendation since .NET Framework. It is no longer the right choice.

## The fix: `[GeneratedRegex]`

.NET 7 introduced **source-generated regex**. The Roslyn source generator emits a purpose-built `Regex` subclass at compile time — no runtime IL generation, no startup cost, full AOT compatibility.

```csharp title="EmailValidator.cs — Do this instead"
public static partial class EmailValidator
{
    [GeneratedRegex(@"^[a-zA-Z0-9._%+\-]+@([a-zA-Z0-9\-]+\.)+[a-zA-Z]{2,}$", RegexOptions.None, 100)]
    private static partial Regex EmailPattern();

    public static bool IsValid(string email) => EmailPattern().IsMatch(email);
}
```

Three differences, all of them improvements:

- **Compile-time generation** — the pattern is parsed and optimized during build. A malformed pattern produces a build error, not a runtime `ArgumentException`.
- **Zero cold-start cost** — the generated code is a regular method. No IL emission, no JIT surprise on the first request.
- **Explicit timeout** — the third argument (`100`) sets a **100ms match timeout**. If a pathological input triggers backtracking, the engine throws `RegexMatchTimeoutException` instead of hanging.

## How Granit uses it

Every regex in the Granit framework follows this pattern. The validation library is a good example:

```csharp title="ContactValidatorExtensions.cs"
[GeneratedRegex(@"^[a-zA-Z0-9._%+\-]+@([a-zA-Z0-9\-]+\.)+[a-zA-Z]{2,}$", RegexOptions.None, 100)]
private static partial Regex EmailRegex();

[GeneratedRegex(@"^\+[1-9]\d{6,14}$", RegexOptions.None, 100)]
private static partial Regex E164Regex();
```

```csharp title="BicSwiftAlgorithm.cs"
[GeneratedRegex(@"^[A-Z]{4}[A-Z]{2}[A-Z0-9]{2}([A-Z0-9]{3})?$", RegexOptions.None, 100)]
private static partial Regex BicRegex();
```

Even architecture tests — where performance is less critical — use `[GeneratedRegex]` for consistency:

```csharp title="ArchitectureTests.cs"
[GeneratedRegex(@"(?<=^[ \t]+(?:(?:public|private|protected|internal|static|override|sealed|virtual|new)\s+)*)async\s+void\s+\w+", RegexOptions.Multiline)]
private static partial Regex AsyncVoidMethod();
```

The convention is simple: **if it is a regex, it is `[GeneratedRegex]`**. No exceptions, no "it is only used once so it does not matter".

## The timeout matters

The third parameter deserves its own mention. **ReDoS** (Regular Expression Denial of Service) is a real attack vector. A pattern like `(a+)+$` matched against `aaaaaaaaaaaaaaaaX` causes exponential backtracking. Without a timeout, a single HTTP request can consume a thread for minutes.

Granit enforces a **100ms timeout on every regex that processes user input**. This is enough for any legitimate match and short enough to abort an attack before it causes damage. Internal-only patterns (like the architecture test above) may omit the timeout when the input is trusted source code, but the default stance is clear: **set a timeout unless you can prove the input is safe**.

## Key takeaways

- **Never use `new Regex(..., RegexOptions.Compiled)`**. It is slower to start, invisible to static analysis, and incompatible with AOT.
- **Always use `[GeneratedRegex]`** on a `static partial` method. The source generator handles optimization at build time.
- **Always set a timeout** (third parameter) when the regex processes user input. 100ms is a sensible default.
- **Make the class `partial`**. The source generator needs a partial class to emit the implementation. This is the most common mistake when migrating.

## Further reading

- [Core & Utilities reference](/dotnet/core/module-system/) — validation extensions, regex conventions
- [FluentValidation (ADR-006)](/dotnet/architecture/adr/006-fluentvalidation/) — how Granit integrates FluentValidation with generated regex
