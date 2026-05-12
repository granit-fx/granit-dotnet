# Granit.QueryEngine.Fuzz

LibFuzzer-style harness around `QueryRequestBinder.BindAsync` from
`Granit.QueryEngine.AspNetCore`.

## What it fuzzes

The binder parses HTTP query strings into `QueryRequest`, including the bracketed
`filter[field.op]=value` and `presets[group]=name` syntax. The harness mutates the
query-string body and asserts that the binder never throws an unexpected exception.

- Target source: [`../../src/Granit.QueryEngine.AspNetCore/Binding/QueryRequestBinder.cs`](../../src/Granit.QueryEngine.AspNetCore/Binding/QueryRequestBinder.cs)
- Method: `QueryRequestBinder.BindAsync(HttpContext, ParameterInfo)`

## Running locally

```bash
dotnet publish fuzz/Granit.QueryEngine.Fuzz -c Release -o ./out/queryengine_fuzz
dotnet tool install -g SharpFuzz.CommandLine          # one-time
sharpfuzz ./out/queryengine_fuzz/Granit.QueryEngine.AspNetCore.dll
afl-fuzz -i fuzz/Granit.QueryEngine.Fuzz/seeds \
         -o fuzz/Granit.QueryEngine.Fuzz/findings \
         ./out/queryengine_fuzz/Granit.QueryEngine.Fuzz
```

See <https://github.com/Metalnem/sharpfuzz> for full instructions.

## Finding triage

Anything written under `findings/crashes/` is a real bug:

1. Open the crash file as text — its content is the offending query string body.
2. Reproduce in a unit test by feeding the body to `QueryRequestBinder.BindAsync`.
3. Fix the binder, add the input as a regression seed.

`ArgumentException` from `QueryString.ctor` is filtered: it represents malformed
inputs that Kestrel rejects before any Granit code runs.
