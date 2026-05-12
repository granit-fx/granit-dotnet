# Granit.DataExchange.Excel.Fuzz

SharpFuzz + AFL++ harness around `SylvanExcelFileParser` from `Granit.DataExchange.Excel`.

## What it fuzzes

The Sylvan.Data.Excel-backed parser exposes `ExtractHeadersAsync` and `ParseAsync` for
`.xlsx`, `.xls`, and `.xlsb` inputs. The harness wraps fuzzed bytes in a `MemoryStream`,
calls both methods, and asserts that no unexpected exception escapes.

- Target source: [`../../src/Granit.DataExchange.Excel/Internal/Import/SylvanExcelFileParser.cs`](../../src/Granit.DataExchange.Excel/Internal/Import/SylvanExcelFileParser.cs)
- Type: `internal sealed class SylvanExcelFileParser` (exposed to the harness via
  `InternalsVisibleTo("Granit.DataExchange.Excel.Fuzz")`).

Input is capped at 256 KB. `xlsx` is a zip container, so the parsing pipeline crosses
`System.IO.Compression`, the OPC part reader, and `Sylvan.Data.Excel`'s sheet decoder.

## Seed corpus

`.xlsx` is binary (zip + XML parts), so seeds can't be hand-written. `seeds/01-minimal.xlsx`
was generated once with `ExcelDataWriter.Create(... ExcelXml)` from Sylvan.Data.Excel and
committed. To add more valid seeds, write a one-off generator program (or copy a fixture
out of `tests/Granit.DataExchange.Excel.Tests/TestExcelHelper.cs`) — AFL will mutate them
into adversarial shapes.

## Running locally

```bash
sudo apt-get install -y afl++
dotnet tool install -g SharpFuzz.CommandLine          # one-time
dotnet publish fuzz/Granit.DataExchange.Excel.Fuzz -c Release -o ./out/excel_fuzz
sharpfuzz ./out/excel_fuzz/Granit.DataExchange.Excel.dll
sharpfuzz ./out/excel_fuzz/Sylvan.Data.Excel.dll
AFL_SKIP_CPUFREQ=1 afl-fuzz \
  -i fuzz/Granit.DataExchange.Excel.Fuzz/seeds \
  -o fuzz/Granit.DataExchange.Excel.Fuzz/findings \
  -- dotnet ./out/excel_fuzz/Granit.DataExchange.Excel.Fuzz.dll
```

See <https://github.com/Metalnem/sharpfuzz> for full instructions.

## Finding triage

Anything written under `findings/default/crashes/` is a real bug:

1. Open the crash file as bytes — its content is the offending xlsx payload.
2. Reproduce in a unit test by feeding the bytes to `SylvanExcelFileParser.ParseAsync`.
3. Fix the parser, add the input as a regression seed.

`InvalidDataException`, `FormatException`, `EndOfStreamException`, and exceptions from
the `Sylvan.Data.Excel` namespace are filtered: they represent malformed inputs by
design, not bugs.
