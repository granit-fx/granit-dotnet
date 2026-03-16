---
title: "ADR-015: Sep — High-Performance CSV Parsing"
description: "Selection of Sep for SIMD-vectorized zero-allocation CSV parsing in the DataExchange pipeline"
sidebar:
  order: 15
  label: "015 - Sep CSV Parsing"
---

> **Date:** 2026-03-01
> **Authors:** Jean-Francois Meyers
> **Scope:** granit-dotnet (Granit.DataExchange.Csv)

## Context

The `Granit.DataExchange.Csv` module requires a CSV parser capable of processing
files with 100,000+ rows in streaming, without loading the entire file into
memory. Use cases include: patient data import, roundtrip reimport, initial
reference data loading.

The library must support:

- **Streaming**: native `IAsyncEnumerable` for the DataExchange pipeline
- **Performance**: large files without degradation
- **Encodings**: UTF-8, UTF-8 BOM, Latin-1, Windows-1252
- **RFC 4180**: quoted fields, configurable separators
- **License**: compatible with commercial use without recurring costs
- **Target**: explicit .NET 10

## Decision

**Sep** (nietras) for CSV parsing in `Granit.DataExchange.Csv`.

## Alternatives considered

### Option 1: Sep (selected)

- **License**: MIT
- **Advantage**: zero-allocation after warmup, SIMD vectorization (AVX-512,
  SSE, ARM NEON), explicit net10.0 target, native `IAsyncEnumerable` (.NET 9+),
  `Span<T>` / `ISpanParsable<T>`, 9-35x faster than CsvHelper, AOT-compatible
- **Maturity**: active releases in 2025 (0.9.0 to 0.12.2), extensive tests
- **Disadvantage**: lower-level API (Span-oriented), 0.x version number
  (conservative versioning by the author, not a sign of instability)

### Option 2: CsvHelper

- **License**: MS-PL / Apache-2.0
- **Advantage**: de facto standard (508M NuGet downloads), high-level API
  (`GetRecordsAsync<T>()`, `ClassMap`, `TypeConverter`), native `IAsyncEnumerable`,
  excellent documentation, error callbacks (`BadDataFound`)
- **Disadvantage**: one `string` allocation per column (significant at
  100K+ rows), no SIMD vectorization, no explicit net10.0 target
  (via netstandard2.0), 9-35x slower than Sep

### Option 3: Sylvan.Data.Csv

- **License**: MIT
- **Advantage**: 2-3x faster than CsvHelper, familiar `DbDataReader` API,
  auto-detection of delimiter, Lax mode for malformed data
- **Disadvantage**: no explicit net10.0 target, no SIMD, no
  `IAsyncEnumerable` (only `ReadAsync()`), intermediate performance
  without decisive advantage over Sep or CsvHelper

### Option 4: RecordParser

- **License**: MIT
- **Advantage**: near-zero allocation via expression trees, `Span<char>`
- **Disadvantage**: last release November 2023 (18+ months), 116K downloads,
  no .NET 8/9/10 target, no `IAsyncEnumerable`, minimal documentation,
  stagnant maintenance

## Justification

| Criterion | Sep | CsvHelper | Sylvan.Data.Csv | RecordParser |
| --------- | --- | --------- | --------------- | ------------ |
| License | MIT | MS-PL/Apache-2.0 | MIT | MIT |
| Performance vs CsvHelper | **9-35x** | 1x (baseline) | 2-3x | ~2x |
| Zero-allocation | **Yes** | No | Low-alloc | Near-zero |
| SIMD (AVX-512/NEON) | **Yes** | No | No | No |
| Target net10.0 | **Yes** | No (netstandard) | No (net6.0) | No |
| IAsyncEnumerable | **Yes** (.NET 9+) | Yes | No | No |
| Span/Memory API | **Yes** | No | Partial | Yes |
| NuGet downloads | ~1.4M | ~508M | ~3.1M | ~116K |
| Active maintenance | **Yes** (2025) | Yes | Yes | No (2023) |
| AOT/Trimming | **Yes** | Partial | Partial | Unknown |
| API ergonomics | Medium | Excellent | Good | Low |

The decisive criterion is **streaming performance** for large files
(100K+ rows). Sep is 9-35x faster than CsvHelper thanks to SIMD
vectorization and zero allocations. The lower-level API is not a disadvantage
as it is encapsulated behind the `IFileParser` interface — consumers never
see the Sep API directly.

## Consequences

### Positive

- Fastest CSV parsing in the .NET ecosystem (SIMD vectorized)
- Zero-allocation: no GC pressure on large imports
- Explicit net10.0 target: runtime optimizations leveraged
- Native `IAsyncEnumerable`: natural integration with the DataExchange pipeline
- MIT: no cost, compatible with commercial use
- AOT-compatible: no runtime reflection

### Negative

- Span-oriented API more verbose than CsvHelper for internal code
- 0.x version (though stable and actively maintained)
- Smaller community than CsvHelper (1.4M vs 508M downloads)
- No native `ClassMap` — mapping is done by `IDataMapper<T>` (by design)
- No built-in error callbacks (`BadDataFound`) — handled via
  `try/catch` in the `SepCsvFileParser` implementation
