---
title: "ADR-016: Sylvan.Data.Excel — Streaming Excel File Reading"
description: "Selection of Sylvan.Data.Excel for zero-dependency streaming Excel file reading in the DataExchange pipeline"
sidebar:
  order: 16
  label: "016 - Sylvan Excel Reading"
---

> **Date:** 2026-03-01
> **Authors:** Jean-Francois Meyers
> **Scope:** granit-dotnet (Granit.DataExchange.Excel)

## Context

The `Granit.DataExchange.Excel` module requires an Excel parser capable of reading
`.xlsx`, `.xlsb` and `.xls` files in streaming, without loading the entire
workbook into memory (DOM model). Use cases include: patient data import,
roundtrip reimport, initial loading from legacy `.xls` files.

The framework already uses **ClosedXML** for Excel **generation**
(`Granit.DocumentGeneration.Excel`). For **reading** (import), ClosedXML
is unsuitable as it loads the complete DOM into memory (hundreds of MB for
100K+ rows).

The library must support:

- **Streaming**: forward-only reading without DOM loading
- **Formats**: `.xlsx`, `.xlsb`, `.xls` (legacy files)
- **Performance**: 100K+ rows with minimal memory footprint
- **Async**: non-blocking support for the DataExchange pipeline
- **License**: compatible with commercial use without recurring costs
- **Dependencies**: minimal (avoid conflicts with ClosedXML)

## Decision

**Sylvan.Data.Excel** for Excel file reading in `Granit.DataExchange.Excel`.

> ClosedXML remains for **generation** (`Granit.DocumentGeneration.Excel`).

## Alternatives considered

### Option 1: Sylvan.Data.Excel (selected)

- **License**: MIT
- **Advantage**: zero transitive dependencies (pure managed), `DbDataReader`
  forward-only streaming, `.xlsx`/`.xlsb`/`.xls` support, lowest memory
  footprint in the ecosystem, native async (`CreateAsync`, `ReadAsync`)
- **Maturity**: Sylvan ecosystem (Csv at 3.1M downloads), version 0.5.2
- **Disadvantage**: smaller community (867K downloads), no native
  `IAsyncEnumerable` (wrapping required)

### Option 2: ClosedXML (already used for generation)

- **License**: MIT
- **Advantage**: rich API, already in the dependency graph, same
  library for reading and writing
- **Disadvantage**: **DOM model** — loads the entire workbook into memory.
  For 100K rows, consumption of hundreds of MB (each `XLCell` has its
  own `XLStyle`). Unsuitable for large file imports.

### Option 3: MiniExcel

- **License**: Apache-2.0
- **Advantage**: very simple API (`Query<T>()` in one line), SAX-like
  streaming (~17 MB for 1M rows), native `IAsyncEnumerable` (v2 preview)
- **Disadvantage**: transitive dependency on `DocumentFormat.OpenXml`
  (version conflict risk with ClosedXML which depends on the same package),
  no `.xls` or `.xlsb` support, typed access via dynamic/Dictionary
  (possible runtime errors)

### Option 4: ExcelDataReader

- **License**: MIT
- **Advantage**: most popular (92M downloads), `IDataReader` forward-only,
  `.xls`/`.xlsx`/`.xlsb` support, battle-tested
- **Disadvantage**: **no async support** (no `async`, no `ReadAsync`,
  no `IAsyncEnumerable`), targets only netstandard2.0 (no modern .NET
  optimizations), basic typed accessors

### Option 5: Open XML SDK (Microsoft)

- **License**: MIT
- **Advantage**: official SDK, SAX mode (`OpenXmlReader`) for ultimate streaming
- **Disadvantage**: **very low-level** API — direct XML element manipulation,
  manual shared string table management, cell reference interpretation,
  style index management. Hundreds of lines for what other libraries
  accomplish in one line.

## Justification

| Criterion | Sylvan.Data.Excel | ClosedXML | MiniExcel | ExcelDataReader | Open XML SDK |
| --------- | ----------------- | --------- | --------- | --------------- | ------------ |
| License | MIT | MIT | Apache-2.0 | MIT | MIT |
| Reading model | **Forward-only** | DOM (all in RAM) | SAX streaming | Forward-only | DOM or SAX |
| Memory 100K rows | **Very low** | Hundreds MB | ~17 MB | Low-medium | SAX: low |
| Formats | .xlsx/.xlsb/.xls | .xlsx | .xlsx/.csv | .xlsx/.xlsb/.xls | .xlsx/.xlsb |
| Async | **Yes** | No | Yes | **No** | No |
| Transitive deps | **Zero** | OpenXml | OpenXml | None | N/A |
| API | DbDataReader | Rich object model | dynamic/Dictionary | IDataReader | XML nodes |
| NuGet downloads | ~867K | ~45M | ~10.1M | ~92M | ~250M+ |

The decisive criterion is the combination of **zero transitive dependencies** +
**forward-only streaming** + **async support** + **legacy .xls support**.

Sylvan.Data.Excel is the only one to check all four boxes. The **zero
dependencies** point is critical: `Granit.DocumentGeneration.Excel` already
pulls `ClosedXML` -> `DocumentFormat.OpenXml`. Adding MiniExcel would bring a
second transitive dependency on `DocumentFormat.OpenXml` with a version
conflict risk. Sylvan.Data.Excel avoids this problem entirely.

## Consequences

### Positive

- Lowest memory footprint for Excel file reading in .NET
- Zero transitive dependencies (no conflict with ClosedXML/OpenXml)
- Support for all 3 common formats: `.xlsx`, `.xlsb`, `.xls` (legacy)
- Familiar and strongly-typed `DbDataReader` API
- Native async (`CreateAsync`, `ReadAsync`)
- MIT: no cost, compatible with commercial use

### Negative

- Smaller community than ExcelDataReader or MiniExcel
- Version 0.5.x (stable Sylvan ecosystem but conservative versioning)
- No native `IAsyncEnumerable` — requires a wrapper in
  `SylvanExcelFileParser` (trivial: `while ReadAsync yield return` loop)
- No support for password-protected files (rare case for import)
