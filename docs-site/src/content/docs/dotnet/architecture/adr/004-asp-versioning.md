---
title: "ADR-004: Asp.Versioning — REST API Versioning"
description: "Adoption of Asp.Versioning for semantic REST API versioning with OpenAPI integration"
sidebar:
  order: 4
  label: "004 - API Versioning"
---

> **Date:** 2026-02-22
> **Authors:** Jean-Francois Meyers
> **Scope:** granit-dotnet (Granit.ApiVersioning)

## Context

The platform REST APIs must support versioning to allow contract evolution
without breaking existing clients. This need is particularly critical in a
healthcare context (ISO 27001) where third-party integrators (laboratories,
EHR systems) have long update cycles.

Versioning must be:

- **Explicit**: each endpoint declares its version
- **Negotiable**: the client chooses the version via URL, header or query string
- **Documented**: versions appear in the OpenAPI spec (Scalar UI)

## Decision

**Asp.Versioning.Mvc** (+ ApiExplorer) for semantic API versioning.

## Alternatives considered

### Option 1: Asp.Versioning (selected)

- **License**: MIT (.NET Foundation)
- **Advantage**: official .NET Foundation package (formerly Microsoft.AspNetCore.Mvc.Versioning),
  support for URL segment (`/api/v1/...`), header (`api-version`), query string (`?api-version=1`),
  media type. ApiExplorer integration for OpenAPI
- **Maturity**: 8+ years, migrated from the historical Microsoft package

### Option 2: Manual URL versioning (routing convention)

- **Advantage**: zero dependency, simple for basic cases
- **Disadvantage**: no version negotiation, no sunset policies,
  code duplication between versions, no automatic OpenAPI integration

### Option 3: Custom naming convention (namespace-based)

- **Advantage**: clear code organization by namespace/version
- **Disadvantage**: requires a homegrown framework, no standard,
  maintenance and documentation burden on the team

## Justification

| Criterion | Asp.Versioning | Manual URL | Custom |
| --------- | -------------- | ---------- | ------ |
| .NET standard | Yes (.NET Foundation) | No | No |
| Version modes | URL, header, QS, media type | URL only | Variable |
| Integrated OpenAPI | Yes (ApiExplorer) | No | No |
| Sunset policies | Yes | No | No |
| Maintenance effort | None (community) | High | Very high |

## Consequences

### Positive

- .NET ecosystem standard, abundant documentation
- Multi-modal versioning (URL segment by default in Granit)
- Automatic integration with Scalar UI via ApiExplorer
- Sunset headers for progressive deprecation of old versions

### Negative

- Preview version (10.0.0-preview.1) for .NET 10 — to monitor
- Initial configuration required (default convention in `GranitApiVersioningModule`)
