---
title: "ADR-008: SmartFormat.NET — CLDR Pluralization"
description: "SmartFormat.NET handles CLDR-compliant pluralization across 18 cultures in Granit's localization system, replacing ICU workarounds with a clean extension model."
sidebar:
  order: 8
  label: "008 - SmartFormat.NET"
---

> **Date:** 2026-02-26
> **Authors:** Jean-Francois Meyers
> **Scope:** granit-dotnet (Granit.Localization)

## Context

The `Granit.Localization` module provides a modular JSON localization system.
Pluralization is a critical feature for multilingual applications: CLDR rules
(Unicode Common Locale Data Repository) define pluralization categories that
vary by language (French: singular/plural, Arabic: 6 forms, etc.).

## Decision

**SmartFormat.NET** for pluralization in the localization system.

## Alternatives considered

### Option 1: SmartFormat.NET (selected)

- **License**: MIT
- **Advantage**: complete CLDR support (all languages), familiar syntax
  (`{0:plural:...}`), lightweight (~50 KB), no native dependency (ICU),
  extensible via custom formatters
- **Maturity**: 12+ years, active

### Option 2: ICU4N

- **Advantage**: official ICU implementation for .NET, MessageFormat support
- **Disadvantage**: native ICU dependency (cross-platform deployment issues,
  ~30 MB size), complex API, limited .NET documentation

### Option 3: MessageFormat.NET

- **Advantage**: implementation of the ICU MessageFormat standard
- **Disadvantage**: poorly maintained project, limited community, insufficient
  documentation, no complete CLDR support

### Option 4: Custom pluralization

- **Advantage**: full control, zero dependency
- **Disadvantage**: reimplementing CLDR rules (200+ languages) is a
  considerable effort and error-prone, long-term maintenance burden

### Option 5: gettext (.po files)

- **Advantage**: industry standard for localization, mature tooling
- **Disadvantage**: .po file format incompatible with Granit's JSON system,
  requires a complete overhaul of the localization pipeline, limited .NET ecosystem

## Justification

| Criterion | SmartFormat | ICU4N | MessageFormat | Custom | gettext |
| --------- | ----------- | ----- | ------------- | ------ | ------- |
| License | MIT | MIT | MIT | N/A | GPL/LGPL |
| Complete CLDR | Yes | Yes | Partial | No | Yes |
| Native dependency | No | Yes (ICU) | No | No | No |
| Size | ~50 KB | ~30 MB | ~20 KB | 0 | Variable |
| .NET documentation | Good | Low | Low | N/A | Low |
| JSON integration | Easy | Possible | Possible | N/A | Incompatible |

## Consequences

### Positive

- Correct pluralization for all languages without native dependency
- Concise and readable syntax in JSON translation files
- Lightweight: no significant impact on package size
- Extensible: custom formatters for specific business cases

### Negative

- SmartFormat has its own syntax (not a pure ICU MessageFormat standard)
- If a switch to ICU MessageFormat becomes necessary, translation file
  migration required
