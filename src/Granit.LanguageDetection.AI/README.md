# Granit.LanguageDetection.AI

AI-backed `ILanguageDetectorProvider` for `Granit.LanguageDetection`. Priority 200 — runs
before the pure-managed Trigram detector (priority 100) when both are registered.

## When to wire it in

The default Trigram detector covers 390+ languages with sub-millisecond latency and zero
network cost. Reach for the AI detector when:

- The corpus mixes very-short snippets where Trigram's 10-char minimum returns `null` too
  often.
- You need high precision on rare scripts not in the Franc dataset.
- You already pay for an LLM workspace and the marginal cost of one detection call per
  indexed entry is acceptable.

## Registration

```csharp
builder.Services.AddGranitLanguageDetection();
builder.Services.AddGranitLanguageDetectionTrigram();   // ← keep this — see below
builder.Services.AddGranitLanguageDetectionAI();
// + AI provider package, e.g. AddGranitAIOpenAI(...)
```

The AI detector (priority 200) wins over Trigram (priority 100) at the composite
resolution step. When the AI call fails (rate-limited, timeout, transport,
schema-reject, ISO-639-1 mismatch, empty body), the detector returns `null` and the
composite gracefully falls through to the next provider.

### ⚠ Always keep the Trigram detector registered

Every AI failure path is handled by **falling through the composite chain to the next
provider** — never by retrying or throwing. That contract is only useful when at least
one non-AI provider is also registered. If you wire `AddGranitLanguageDetectionAI()`
**alone**, every AI failure resolves to `null` for the whole call.

The framework intentionally does NOT `[DependsOn]` the Trigram module here: an advanced
host may want to substitute its own pure-managed provider (e.g. a domain-specific
trigram corpus, an enterprise lexer). Whatever you choose, **always keep at least one
non-AI `ILanguageDetectorProvider` registered** so the composite has a fallback path.

Every failure path bumps a tenant-tagged metric on the
`Granit.LanguageDetection.AI` meter so you can alert on excessive fallthrough without
parsing logs.

## Configuration

```json
{
  "LanguageDetection": {
    "AI": {
      "WorkspaceName": "default",
      "MaxAICallsPerHourPerTenant": 1000,
      "RedactPIIBeforeLLMCall": true,
      "MaxContentLength": 2048,
      "TimeoutSeconds": 10
    }
  }
}
```

## Security baseline (OWASP LLM01 — prompt injection)

Three layers of defence apply to every call:

1. **Instruction isolation.** Content is wrapped in `<untrusted_document>...</untrusted_document>`
   with a system prompt that explicitly forbids the model from treating its content as
   instructions.
2. **Structured-output pinning.** `ChatResponseFormat.ForJsonSchema<LanguageDetectionResponse>()`
   constrains the model to emit a strict JSON envelope.
3. **ISO 639-1 validation.** The response must match `^[a-z]{2}$`. Non-conforming
   responses bump the `granit.language_detection.ai.injection_attempt` metric
   (tenant-tagged, never content-tagged) and the call returns `null`.

PII redaction over free text is optional via `IAIContentRedactor` (see
`Granit.AI.Extraction`). The default `NoOpAIContentRedactor` ships unchanged content; hosts
register stricter redactors (NER, regex, composite) before calling
`AddGranitLanguageDetectionAI()`.

## Cost ceiling

`MaxAICallsPerHourPerTenant` (default 1 000) caps outbound LLM calls per tenant via the
sliding-window `IAICallRateLimiter` shared with the other AI feature packages. The
1 001st call within the trailing hour returns `null` — no exception, the composite
falls through to Trigram.
