---
title: "ADR-010: Scriban — Text Template Engine"
description: "Scriban provides a sandboxed, high-performance Liquid-compatible template engine for Granit document generation — safe for multi-tenant user-supplied templates."
sidebar:
  order: 10
  label: "010 - Scriban"
---

> **Date:** 2026-02-27
> **Authors:** Jean-Francois Meyers
> **Scope:** granit-dotnet (Granit.Templating.Scriban)

## Context

The `Granit.Templating` module provides a document generation pipeline:
text template -> HTML rendering -> conversion to final format (PDF, Excel, etc.).

The text template engine must:

- **Security**: execute templates in a sandbox (no filesystem or network access)
- **Extensibility**: custom functions, global variables (date, user, tenant)
- **Performance**: template compilation, caching
- **Syntax**: intuitive for non-developers (business operations)

## Decision

**Scriban** as the text template engine for document generation.

## Alternatives considered

### Option 1: Scriban (selected)

- **License**: BSD-2-Clause
- **Advantage**: sandboxed by default (no system access), intuitive Liquid-like
  syntax, extensible (custom functions, `GlobalContext`), template compilation
  and caching, excellent performance (~10x faster than Razor)
- **Size**: lightweight (~200 KB)

### Option 2: Razor (RazorLight)

- **Advantage**: familiar C# syntax for .NET developers, powerful
- **Disadvantage**: **not sandboxed** (full .NET runtime access —
  security risk if templates are user-editable),
  dependency on Roslyn compiler (heavy, ~20 MB), high compilation time,
  RazorLight is a less maintained third-party wrapper

### Option 3: Fluid (Liquid .NET)

- **License**: MIT
- **Advantage**: .NET implementation of Liquid (Shopify standard), sandboxed,
  similar syntax to Scriban
- **Disadvantage**: less performant than Scriban on benchmarks,
  more limited extensibility (no native `GlobalContext`), smaller
  .NET community

### Option 4: Handlebars.NET

- **License**: MIT
- **Advantage**: .NET port of Handlebars.js, logicless templates
- **Disadvantage**: "logicless" too restrictive (no complex conditions,
  no advanced loops), extensibility via helpers only,
  lower performance than Scriban

### Option 5: Mustache (Stubble)

- **License**: MIT
- **Advantage**: multi-language standard, very simple
- **Disadvantage**: too minimalistic (no filters, no functions,
  no expressions), unsuitable for complex document generation

## Justification

| Criterion | Scriban | Razor | Fluid | Handlebars | Mustache |
| --------- | ------- | ----- | ----- | ---------- | -------- |
| License | BSD-2-Clause | MIT | MIT | MIT | MIT |
| Sandbox | Yes (native) | No | Yes | Partial | Yes |
| Extensibility | Excellent | Total (C#) | Good | Limited | Very limited |
| Performance | Very fast | Slow (compilation) | Fast | Medium | Fast |
| Intuitive syntax | Yes (Liquid-like) | C# (dev only) | Yes | Yes | Yes |
| Size | ~200 KB | ~20 MB (Roslyn) | ~150 KB | ~100 KB | ~50 KB |
| GlobalContext | Yes | No | No | No | No |

## Consequences

### Positive

- Templates executed in a sandbox: no risk of arbitrary code execution
- Liquid-like syntax accessible to business users
- GlobalContext for enrichment variables (date, tenant, user)
- Template caching and compilation for performance
- Complete pipeline: Scriban -> HTML -> PDF (via PuppeteerSharp)

### Negative

- Scriban-specific syntax (not a standard like Liquid or Mustache)
- BSD-2-Clause (very permissive, but less common than MIT/Apache-2.0)
- Project maintained by an individual developer (Alexandre Mutel — also
  author of markdig, SharpDX, etc.)
