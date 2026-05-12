# Granit.QueryEngine.Fuzz

SharpFuzz + AFL++ harness around `QueryRequestBinder.BindAsync` from
`Granit.QueryEngine.AspNetCore`.

## What it fuzzes

The binder parses HTTP query strings into `QueryRequest`, including the bracketed
`filter[field.op]=value` and `presets[group]=name` syntax. The harness mutates the
query-string body and asserts that the binder never throws an unexpected exception.

- Target source: [`../../src/Granit.QueryEngine.AspNetCore/Binding/QueryRequestBinder.cs`](../../src/Granit.QueryEngine.AspNetCore/Binding/QueryRequestBinder.cs)
- Method: `QueryRequestBinder.BindAsync(HttpContext, ParameterInfo)`

## Running locally

```bash
sudo apt-get install -y afl++
dotnet tool install -g SharpFuzz.CommandLine          # one-time
dotnet publish fuzz/Granit.QueryEngine.Fuzz -c Release -o ./out/queryengine_fuzz
sharpfuzz ./out/queryengine_fuzz/Granit.QueryEngine.AspNetCore.dll
sharpfuzz ./out/queryengine_fuzz/Granit.QueryEngine.Abstractions.dll
AFL_SKIP_CPUFREQ=1 afl-fuzz \
  -i fuzz/Granit.QueryEngine.Fuzz/seeds \
  -o fuzz/Granit.QueryEngine.Fuzz/findings \
  -- dotnet ./out/queryengine_fuzz/Granit.QueryEngine.Fuzz.dll
```

See <https://github.com/Metalnem/sharpfuzz> for full instructions.

## Finding triage

Anything written under `findings/default/crashes/` is a real bug:

1. Open the crash file as text — its content is the offending query string body.
2. Reproduce in a unit test by feeding the body to `QueryRequestBinder.BindAsync`.
3. Fix the binder, add the input as a regression seed.

`ArgumentException` from `QueryString.ctor` is filtered: it represents malformed
inputs that Kestrel rejects before any Granit code runs.
