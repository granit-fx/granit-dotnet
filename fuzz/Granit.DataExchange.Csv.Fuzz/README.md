# Granit.DataExchange.Csv.Fuzz

SharpFuzz + AFL++ harness around `SepCsvFileParser` from `Granit.DataExchange.Csv`.

## What it fuzzes

The Sep-backed CSV parser exposes `ExtractHeadersAsync` and `ParseAsync`. The harness
wraps the fuzzed input bytes in a `MemoryStream`, calls both, and asserts that no
unexpected exception escapes.

- Target source: [`../../src/Granit.DataExchange.Csv/Internal/Import/SepCsvFileParser.cs`](../../src/Granit.DataExchange.Csv/Internal/Import/SepCsvFileParser.cs)
- Type: `internal sealed class SepCsvFileParser` (exposed to the harness via
  `InternalsVisibleTo("Granit.DataExchange.Csv.Fuzz")`).

Input is capped at 64 KB so AFL stays in a tight loop.

## Running locally

```bash
sudo apt-get install -y afl++
dotnet tool install -g SharpFuzz.CommandLine          # one-time
dotnet publish fuzz/Granit.DataExchange.Csv.Fuzz -c Release -o ./out/csv_fuzz
sharpfuzz ./out/csv_fuzz/Granit.DataExchange.Csv.dll
sharpfuzz ./out/csv_fuzz/Sep.dll
AFL_SKIP_CPUFREQ=1 afl-fuzz \
  -i fuzz/Granit.DataExchange.Csv.Fuzz/seeds \
  -o fuzz/Granit.DataExchange.Csv.Fuzz/findings \
  -- dotnet ./out/csv_fuzz/Granit.DataExchange.Csv.Fuzz.dll
```

See <https://github.com/Metalnem/sharpfuzz> for full instructions.

## Finding triage

Anything written under `findings/default/crashes/` is a real bug:

1. Open the crash file as bytes — its content is the offending CSV payload.
2. Reproduce in a unit test by feeding the bytes to `SepCsvFileParser.ParseAsync`.
3. Fix the parser, add the input as a regression seed.

`FormatException`, `InvalidDataException`, `DecoderFallbackException`, and exceptions
thrown from the `nietras.SeparatedValues` namespace are filtered: they represent
malformed inputs by design, not bugs.
