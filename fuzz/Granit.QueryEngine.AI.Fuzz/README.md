# Granit.QueryEngine.AI.Fuzz

SharpFuzz + AFL++ harness around the post-LLM JSON deserialization path inside
`LlmNaturalLanguageQueryTranslator` from `Granit.QueryEngine.AI`.

## What it fuzzes

The harness calls the internal entry point
`LlmNaturalLanguageQueryTranslator.TryDeserializeAndConvert(string json, QueryMetadata, out bool)`
with fuzzed UTF-8 input. This is the only attacker-influenced parse step in the AI
module — an attacker who can jailbreak the LLM controls the shape of `json`.

- Target source: [`../../src/Granit.QueryEngine.AI/Internal/LlmNaturalLanguageQueryTranslator.cs`](../../src/Granit.QueryEngine.AI/Internal/LlmNaturalLanguageQueryTranslator.cs)
- Payload DTO: [`../../src/Granit.QueryEngine.AI/Internal/LlmQueryPayload.cs`](../../src/Granit.QueryEngine.AI/Internal/LlmQueryPayload.cs)
- Method: `TryDeserializeAndConvert` — extract-method refactor of the inline
  `JsonSerializer.Deserialize<LlmQueryPayload>(...)` + `ValidateAndConvert(...)` block,
  exposed via `InternalsVisibleTo("Granit.QueryEngine.AI.Fuzz")`.

We do **not** fuzz the LLM call (`IChatClient.GetResponseAsync`) — that's network I/O.

Input is capped at 16 KB.

## Running locally

```bash
sudo apt-get install -y afl++
dotnet tool install -g SharpFuzz.CommandLine          # one-time
dotnet publish fuzz/Granit.QueryEngine.AI.Fuzz -c Release -o ./out/qe_ai_fuzz
sharpfuzz ./out/qe_ai_fuzz/Granit.QueryEngine.AI.dll
sharpfuzz ./out/qe_ai_fuzz/Granit.QueryEngine.Abstractions.dll
AFL_SKIP_CPUFREQ=1 afl-fuzz \
  -i fuzz/Granit.QueryEngine.AI.Fuzz/seeds \
  -o fuzz/Granit.QueryEngine.AI.Fuzz/findings \
  -- dotnet ./out/qe_ai_fuzz/Granit.QueryEngine.AI.Fuzz.dll
```

See <https://github.com/Metalnem/sharpfuzz> for full instructions.

## Finding triage

Anything written under `findings/default/crashes/` is a real bug:

1. Open the crash file as UTF-8 — its content is the offending JSON payload.
2. Reproduce in a unit test by feeding the string to
   `LlmNaturalLanguageQueryTranslator.TryDeserializeAndConvert`.
3. Fix the translator (or the validator), add the input as a regression seed.

`JsonException` is filtered: the production code catches it and returns `null`,
so it is a documented happy-path failure mode.
