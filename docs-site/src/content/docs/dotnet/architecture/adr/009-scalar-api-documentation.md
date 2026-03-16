---
title: "ADR-009: Scalar.AspNetCore — Interactive API Documentation"
description: "Adoption of Scalar as the interactive OpenAPI documentation UI replacing Swagger UI"
sidebar:
  order: 9
  label: "009 - Scalar API Docs"
---

> **Date:** 2026-02-26
> **Authors:** Jean-Francois Meyers
> **Scope:** granit-dotnet (Granit.ApiDocumentation)

## Context

The platform REST APIs require an interactive documentation interface for
developers and integrators. Since .NET 9, Microsoft removed Swashbuckle
(Swagger UI) from the default template in favor of
`Microsoft.AspNetCore.OpenApi` for OpenAPI spec generation.

The documentation UI choice must natively integrate with the new
.NET 10 OpenAPI pipeline without depending on Swashbuckle.

## Decision

**Scalar.AspNetCore** as the interactive OpenAPI documentation UI.

## Alternatives considered

### Option 1: Scalar (selected)

- **License**: MIT
- **Advantage**: native `Microsoft.AspNetCore.OpenApi` integration (.NET 9+),
  modern and responsive UI, built-in "try it", customizable themes,
  API search, OpenAPI 3.1 support
- **Configuration**: `app.MapScalarApiReference()` — one line

### Option 2: Swagger UI (Swashbuckle)

- **Advantage**: historical standard, very wide adoption
- **Disadvantage**: Swashbuckle is **abandoned** (last release 2023,
  removed from .NET 9 template by Microsoft), dependency on NSwag for
  spec generation (overlap with `Microsoft.AspNetCore.OpenApi`),
  dated UI

### Option 3: ReDoc

- **License**: MIT
- **Advantage**: elegant static documentation, three-column layout
- **Disadvantage**: no native "try it" (read-only), requires custom
  integration with the .NET OpenAPI pipeline, less interactive

### Option 4: RapiDoc

- **License**: MIT
- **Advantage**: lightweight, web component, themes
- **Disadvantage**: smaller community, no official .NET integration,
  irregular maintenance

### Option 5: Stoplight Elements

- **License**: Apache-2.0
- **Advantage**: modern UI, API design support
- **Disadvantage**: SaaS-oriented (Stoplight Studio), non-official .NET
  integration, advanced features are paid

## Justification

| Criterion | Scalar | Swagger UI | ReDoc | RapiDoc | Elements |
| --------- | ------ | ---------- | ----- | ------- | -------- |
| License | MIT | MIT | MIT | MIT | Apache-2.0 |
| .NET 10 integration | Native | Abandoned | Custom | Custom | Custom |
| Interactive try-it | Yes | Yes | No | Yes | Yes |
| Modern UI | Yes | No | Yes | Yes | Yes |
| Active maintenance | Yes | No (2023) | Yes | Irregular | Yes |
| .NET configuration | 1 line | ~10 lines | Custom | Custom | Custom |
| OpenAPI 3.1 | Yes | Partial | Yes | Yes | Yes |

## Consequences

### Positive

- Native integration with `Microsoft.AspNetCore.OpenApi` (zero Swashbuckle)
- Modern UI with try-it, search, and themes
- Minimal configuration (`app.MapScalarApiReference()`)
- Compatible with Asp.Versioning (multiple versions in the spec)
- Active maintenance and regular releases

### Negative

- Scalar is less known than Swagger UI (familiarization curve for the team)
- Relatively recent project (2023) — less track record than Swagger UI
- Some advanced features (mocking, testing) are in Scalar Cloud (SaaS)
