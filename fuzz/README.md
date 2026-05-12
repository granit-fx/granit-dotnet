# Fuzzing

Granit ships continuous fuzzing harnesses against components that parse
untrusted input. Each harness lives in its own subdirectory and uses
[SharpFuzz](https://github.com/Metalnem/sharpfuzz) driven by [AFL++](https://aflplus.plus/).

## Current targets

| Harness | Module | Surface |
| --- | --- | --- |
| [`Granit.QueryEngine.Fuzz`](./Granit.QueryEngine.Fuzz/) | [`Granit.QueryEngine.AspNetCore`](../src/Granit.QueryEngine.AspNetCore/) | HTTP query-string binder (`QueryRequestBinder.BindAsync`) |
| [`Granit.DataExchange.Csv.Fuzz`](./Granit.DataExchange.Csv.Fuzz/) | [`Granit.DataExchange.Csv`](../src/Granit.DataExchange.Csv/) | Sep-backed CSV parser (`SepCsvFileParser.ExtractHeadersAsync` + `ParseAsync`) |
| [`Granit.DataExchange.Excel.Fuzz`](./Granit.DataExchange.Excel.Fuzz/) | [`Granit.DataExchange.Excel`](../src/Granit.DataExchange.Excel/) | Sylvan-backed Excel parser (`SylvanExcelFileParser.ExtractHeadersAsync` + `ParseAsync`) |
| [`Granit.QueryEngine.AI.Fuzz`](./Granit.QueryEngine.AI.Fuzz/) | [`Granit.QueryEngine.AI`](../src/Granit.QueryEngine.AI/) | Post-LLM JSON deserialization (`LlmNaturalLanguageQueryTranslator.TryDeserializeAndConvert`) |

The fuzz projects are deliberately **excluded from `Granit.slnx` and from
`test-shards.json`**: the normal CI build and unit-test shards never touch them.
They are built and exercised exclusively by the `cifuzz-*` GitHub workflows.

## Running a harness locally

```bash
sudo apt-get install -y afl++
dotnet tool install -g SharpFuzz.CommandLine   # one-time
dotnet publish fuzz/Granit.QueryEngine.Fuzz -c Release -o ./out/queryengine_fuzz
sharpfuzz ./out/queryengine_fuzz/Granit.QueryEngine.AspNetCore.dll
sharpfuzz ./out/queryengine_fuzz/Granit.QueryEngine.Abstractions.dll
AFL_SKIP_CPUFREQ=1 afl-fuzz \
  -i fuzz/Granit.QueryEngine.Fuzz/seeds \
  -o fuzz/Granit.QueryEngine.Fuzz/findings \
  -- dotnet ./out/queryengine_fuzz/Granit.QueryEngine.Fuzz.dll
```

Full instructions: <https://github.com/Metalnem/sharpfuzz>.

## CI

- `cifuzz-pr.yml` runs the harness on every PR touching the fuzzed modules or
  the harness itself (5 minutes per fuzzer). Job fails if AFL++ finds a crash.
- `cifuzz-batch.yml` runs nightly at 03:00 UTC (1 hour per fuzzer) and is
  triggerable manually via `workflow_dispatch`.

Findings (corpus, crashes, hangs) are uploaded as workflow artifacts.

## Motivation

OpenSSF Scorecard `Fuzzing` check: any continuous fuzzing pipeline integrated
with the repository raises the score from 0 to 10 on that criterion.
