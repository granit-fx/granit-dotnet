---
title: "ADR-006: FluentValidation — Business Validation Framework"
description: "Adoption of FluentValidation for composable business validation with Wolverine pipeline integration"
sidebar:
  order: 6
  label: "006 - FluentValidation"
---

> **Date:** 2026-02-24
> **Authors:** Jean-Francois Meyers
> **Scope:** granit-dotnet (Granit.Validation, Granit.Wolverine)

## Context

The platform requires a validation framework for:

- **Business validation**: complex and composable rules (address, SIRET, IBAN,
  email, locale) with standardized error codes
- **Wolverine integration**: automatic command validation before execution
  via the middleware pipeline (`WolverineFx.FluentValidation`)
- **Error codes**: mapping to RFC 7807 ProblemDetails for HTTP responses
- **Extensibility**: custom reusable validators across modules

## Decision

**FluentValidation** as the business validation framework.

## Alternatives considered

### Option 1: FluentValidation (selected)

- **License**: Apache-2.0
- **Advantage**: composable fluent API (`RuleFor(x => x.Email).EmailAddress()`),
  native Wolverine integration, large community, easy custom validators
- **Maturity**: 15+ years, de facto standard for .NET validation

### Option 2: DataAnnotations only

- **Advantage**: native .NET, zero dependency, integrated with model binding
- **Disadvantage**: limited to simple validations (attributes), no
  composition, no complex conditional validation, no Wolverine
  middleware integration, non-standardized error codes

### Option 3: MiniValidation

- **License**: MIT
- **Advantage**: lightweight, based on DataAnnotations with extensions
- **Disadvantage**: no composable rules, no Wolverine integration,
  limited community, does not cover complex business cases

### Option 4: Custom validation (no framework)

- **Advantage**: full control, no dependency
- **Disadvantage**: considerable development and maintenance effort,
  reinventing the wheel, no standards, no middleware pipeline

## Justification

| Criterion | FluentValidation | DataAnnotations | MiniValidation | Custom |
| --------- | ---------------- | --------------- | -------------- | ------ |
| License | Apache-2.0 | Native .NET | MIT | N/A |
| Composable rules | Yes | No | No | Manual |
| Wolverine middleware | Native | No | No | Manual |
| Conditional validation | Yes (When/Unless) | No | No | Manual |
| Error codes | Yes (WithErrorCode) | Limited | Limited | Manual |
| Community | Very large | Standard | Low | N/A |
| RFC 7807 mapping | Via Granit.AspNetCore | Manual | Manual | Manual |

## Consequences

### Positive

- Declarative and readable validation in each module
- Wolverine integration: commands are validated before execution (DLQ on failure)
- Standardized Granit error codes (e.g. `VALIDATION.EMAIL.INVALID`)
- Reusable validators across packages (`AddressValidator`, `SiretValidator`)
- Automatic mapping to ProblemDetails RFC 7807 via `GranitExceptionHandler`

### Negative

- Third-party dependency for validation (risk of major breaking changes)
- Partial duplication with DataAnnotations for simple cases
  (Granit convention: use FluentValidation even for simple cases,
  for consistency)
