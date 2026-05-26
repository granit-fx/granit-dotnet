# Granit.Indexing.AI

AI-backed `ISummarizer` for `Granit.Indexing`. Opt-in — generates a SERP-style snippet
for each indexed entry via a one-shot LLM call.

## When to wire it in

The `Granit.Indexing` base contract treats `IndexedEntry.Summary` as optional; consumers
that build their own summaries (manual editor input, first-paragraph heuristic, RAG-style
ground-truth) can skip this package entirely. Reach for it when:

- The corpus is heterogeneous (PDF body, HTML, raw email) and you want consistent
  snippet quality across all rows.
- Search UX needs language-correct one-paragraph snippets — the LLM produces them
  cheaply once content is extracted.
- You already pay for an LLM workspace.

## Registration

```csharp
builder.Services.AddGranitIndexing();
builder.Services.AddGranitIndexingAISummarizer();
// + AI provider package, e.g. AddGranitAIOpenAI(...)
```

The summarizer is registered against `ISummarizer`. Consumers retrieve it from DI and
call `SummarizeAsync(content)` BEFORE building the `IndexedEntry`, then assign the
result to `IndexedEntry.Summary`. The framework does NOT auto-wire a decorator on
`IIndexer<TKey>` — the call cost lives where the consumer can control it.

## Configuration

```json
{
  "Indexing": {
    "AI": {
      "WorkspaceName": "default",
      "MaxAICallsPerHourPerTenant": 1000,
      "RedactPIIBeforeLLMCall": true,
      "MaxContentLength": 8192,
      "MaxSummaryLength": 500,
      "TimeoutSeconds": 20
    }
  }
}
```

## Security baseline (OWASP LLM01 — prompt injection)

- **Instruction isolation.** Content is wrapped in `<untrusted_document>...</untrusted_document>`
  with a system prompt that explicitly forbids the model from treating its content as
  instructions.
- **Structured-output pinning.** `ChatResponseFormat.ForJsonSchema<SummaryResponse>()`
  constrains the model to emit a strict JSON envelope.
- **Length cap.** Responses above `MaxSummaryLength` are truncated and bump the
  `granit.indexing.ai.summarizer.truncated` metric — a stable signal for prompt-adherence
  drift.
- PII redaction over free text is optional via `IAIContentRedactor` (see
  `Granit.AI.Extraction`). The default `NoOpAIContentRedactor` ships unchanged content;
  register a stricter redactor before calling `AddGranitIndexingAISummarizer()`.

## Cost ceiling

`MaxAICallsPerHourPerTenant` (default 1 000) caps outbound LLM calls per tenant via the
sliding-window `IAICallRateLimiter` shared with the rest of the AI-feature family. Above
the cap, `SummarizeAsync` returns `null` — the consumer persists the entry without a
summary, the search pipeline keeps flowing.

## Failure semantics — never throws

Every error path (rate-limit, timeout, transport, schema-reject, empty body) returns
`null` and bumps a tenant-tagged metric. Search ranking falls back to the body alone.
Hosts that depend on a guaranteed summary should add a non-AI fallback (e.g. truncate
the first 300 chars of content) at the call site.
