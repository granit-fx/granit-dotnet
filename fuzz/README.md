# Fuzzing

Granit ships continuous fuzzing harnesses against components that parse
untrusted input. Each harness lives in its own subdirectory and uses
[SharpFuzz](https://github.com/Metalnem/sharpfuzz) on top of libFuzzer.

## Current targets

| Harness | Module | Surface |
| --- | --- | --- |
| [`Granit.QueryEngine.Fuzz`](./Granit.QueryEngine.Fuzz/) | [`Granit.QueryEngine.AspNetCore`](../src/Granit.QueryEngine.AspNetCore/) | HTTP query-string binder (`QueryRequestBinder.BindAsync`) |

The fuzz projects are deliberately **excluded from `Granit.slnx` and from
`test-shards.json`**: the normal CI build and unit-test shards never touch them.
They are built and exercised exclusively by ClusterFuzzLite (see
[`.clusterfuzzlite/`](../.clusterfuzzlite/) and the `cifuzz-*` workflows).

## Running a harness locally

```bash
dotnet publish fuzz/Granit.QueryEngine.Fuzz -c Release -o ./out/queryengine_fuzz
dotnet tool install -g SharpFuzz.CommandLine   # one-time
sharpfuzz ./out/queryengine_fuzz/Granit.QueryEngine.AspNetCore.dll
afl-fuzz -i fuzz/Granit.QueryEngine.Fuzz/seeds \
         -o fuzz/Granit.QueryEngine.Fuzz/findings \
         ./out/queryengine_fuzz/Granit.QueryEngine.Fuzz
```

Full instructions: <https://github.com/Metalnem/sharpfuzz>.

## CI

- `cifuzz-pr.yml` runs the `code-change` mode on every PR touching the fuzzed
  modules or the harness itself (5 minutes per fuzzer).
- `cifuzz-batch.yml` runs the `batch` mode nightly at 03:00 UTC (1 hour per
  fuzzer) and can be triggered manually via `workflow_dispatch`.

Findings are uploaded as SARIF to the repository's Security tab.

## Motivation

OpenSSF Scorecard `Fuzzing` check: any continuous fuzzing pipeline integrated
with the repository raises the score from 0 to 10 on that criterion.
