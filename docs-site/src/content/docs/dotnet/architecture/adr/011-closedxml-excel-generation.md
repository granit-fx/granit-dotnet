---
title: "ADR-011: ClosedXML — Excel Spreadsheet Generation"
description: "ClosedXML generates xlsx files from .NET objects with no Excel dependency — chosen for MIT licensing, template-based styling, and OpenXML abstraction."
sidebar:
  order: 11
  label: "011 - ClosedXML"
---

> **Date:** 2026-02-27
> **Authors:** Jean-Francois Meyers
> **Scope:** granit-dotnet (Granit.DocumentGeneration.Excel)

## Context

The `Granit.DocumentGeneration.Excel` module generates `.xlsx` spreadsheets
from Excel templates. Use cases include: data exports, financial reports,
dashboards, and regulatory documents.

The library must support:

- **Templates**: filling named cells in an existing `.xlsx` file
- **Styling**: preserving styles, formulas and page layout from the template
- **Performance**: generating large files (10,000+ rows) with streaming
- **License**: compatible with commercial use without recurring costs

## Decision

**ClosedXML** for Excel file (.xlsx) generation.

## Alternatives considered

### Option 1: ClosedXML (selected)

- **License**: MIT
- **Advantage**: intuitive high-level API, .xlsx template support,
  styles/formulas/layout preserved, active maintenance, MIT
- **Maturity**: 10+ years, large community

### Option 2: EPPlus

- **License**: **Polyform Noncommercial License** (v5+) — commercial use
  requires a **paid license** (~$300/dev/year)
- **Advantage**: very complete API, excellent performance, exhaustive
  documentation
- **Disadvantage**: license change in 2020 (v5), recurring cost
  incompatible with the project's OSS strategy

### Option 3: NPOI

- **License**: Apache-2.0
- **Advantage**: .NET port of Apache POI, supports .xls and .xlsx
- **Disadvantage**: low-level and verbose API (close to Java POI),
  insufficient .NET documentation, lower performance than ClosedXML
  and EPPlus, irregular maintenance

### Option 4: Open XML SDK (Microsoft)

- **License**: MIT
- **Advantage**: official Microsoft SDK, full access to the OOXML format
- **Disadvantage**: **very low-level** API (direct OOXML XML manipulation),
  extreme verbosity for simple operations (filling a cell = ~20 lines),
  no native template support, no high-level style management

## Justification

| Criterion | ClosedXML | EPPlus | NPOI | Open XML SDK |
| --------- | --------- | ------ | ---- | ------------ |
| License | MIT | Commercial (v5+) | Apache-2.0 | MIT |
| Cost | Free | ~$300/dev/year | Free | Free |
| High-level API | Yes | Yes | No | No |
| Template support | Yes | Yes | Partial | No |
| Documentation | Good | Excellent | Low | Good (low-level) |
| Performance | Good | Excellent | Medium | Good |
| Maintenance | Active | Active | Irregular | Active (MS) |

## Consequences

### Positive

- MIT license: no recurring cost, compatible with commercial use
- Intuitive API: `worksheet.Cell("A1").Value = "Hello"` vs ~20 lines Open XML SDK
- Template support: filling named cells in an existing .xlsx
- Style, formula and layout preservation
- Large community and documentation

### Negative

- Slightly lower performance than EPPlus on very large files
- Some advanced features (charts, pivot tables) are less complete
  than in EPPlus
- No native streaming support for very large files (in-memory loading —
  workaround possible with pagination)
