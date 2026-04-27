# scripts/

Repository maintenance scripts. Most are pure-stdlib Python and run as part of
git hooks or CI; one (`translate-templates.py`) calls the Anthropic API.

| Script | Purpose | Runs in |
| --- | --- | --- |
| `generate-code-index.py` | Regenerate `.mcp-code-index.json` from `src/`. Auto-invoked by the pre-push hook. | pre-push hook, CI |
| `generate-domain-filters.py` | Generate per-domain `.slnf` filters from `_domain_map.py`. | dev, CI |
| `generate-shard-filters.py` | Generate `.github/shard-filters/*.slnf` from `.github/test-shards.json`. Auto-invoked by the pre-push hook on `.csproj` changes. | pre-push hook, CI |
| `reorganize-slnx.py` | Re-sort projects in `Granit.slnx`. | dev |
| `translate-templates.py` | AI-translate notification HTML templates into target cultures. | dev |
| `_domain_map.py`, `_slnx_utils.py` | Shared helpers (private — leading underscore). | imported only |

---

## `translate-templates.py` — AI translation of HTML notification templates

Why it exists
: Granit notification templates ship in 18 cultures. Hand-translating 13+
  language variants for every template (Privacy, Identity, Webhooks, …) is
  slow and easy to get wrong — especially under the strict Scriban placeholder
  preservation contract. This script industrialises that work while enforcing
  the invariants the template engine and email rendering pipeline require.
: The script is the **deliverable** for [#1311](https://github.com/granit-fx/granit-dotnet/issues/1311).
  Downstream stories (Privacy 13 cultures, regional gaps, future template
  translation) consume it.

### Strict preservation contract

The Claude system prompt enforces, and post-translation validation verifies,
that the output is byte-identical to the source on every non-prose construct:

1. **Scriban placeholders** — every `{{ ... }}` block, every directive
   (`{{ if ... }}`, `{{ for ... }}`, `{{ end }}`), every identifier
   (`model.X`, `privacy.Y`, `app.Z`), every pipe filter
   (`| date.to_string '%B %d, %Y'`) — is preserved exactly.
2. **HTML structure & attributes** — `href`, `style`, `class`, all attribute
   values, tag nesting, tag order, tag count are all preserved.
3. **`<code>` content** — compliance/regulation codes (`EU_GDPR`, request IDs,
   etc.) are never translated.

Only the human-readable text inside `<title>`, `<p>`, `<a>`, `<li>`, `<h1>`–`<h6>`,
`<strong>`, and `<em>` is translated.

### Reproducibility & idempotency

- The system prompt is **stable** and **prompt-cached** (Anthropic ephemeral
  cache, ~0.1× cost on cache reads). Subsequent cultures and subsequent runs
  pay the cache-hit price for the prefix.
- The model defaults are deterministic — the translator does not set
  `temperature` or sampling parameters (Opus 4.7 removes them; using the
  defaults gives the most stable output the API offers).
- Each output file is prefixed with an `<!-- AUTO-TRANSLATED ... -->` marker
  containing the model ID, the run date, and a SHA-256 prefix of the source
  content. Re-running with the same source skips files whose marker still
  matches the source hash.
- Use `--force` to re-translate even when the marker matches.

### Post-validation

Before writing any output, the script verifies:

1. The `{{` / `}}` brace counts match source and output exactly.
2. The multiset of Scriban blocks is identical and in the same order.
3. Opening/closing HTML tag counts balance (rough heuristic — catches the
   common LLM mistakes of dropping a `</p>` or doubling a `<strong>`).

If any check fails, the diagnostic is printed, the run continues to the next
culture, and the script exits non-zero. Output files are written **atomically**
(temp file + `os.replace`), so a partial failure never leaves a half-written
file on disk.

### Usage

```bash
# Required: ANTHROPIC_API_KEY environment variable
export ANTHROPIC_API_KEY=sk-ant-...

# Recommended: use uv — it reads the PEP 723 inline metadata header and
# auto-installs `anthropic` into an isolated environment.
uv run scripts/translate-templates.py \
  --source src/Granit.Privacy.Notifications/Templates/privacy.export_ready.html \
  --cultures de,nl,es,it,pt,zh,ja,pl,tr,ko,sv,cs,hi

# Or with plain python (you must have `anthropic` installed yourself)
pip install 'anthropic>=0.40.0'
python3 scripts/translate-templates.py \
  --source src/Granit.Privacy.Notifications/Templates/privacy.export_ready.html \
  --cultures de

# Preview the plan without calling the API or writing files
python3 scripts/translate-templates.py \
  --source src/Granit.Privacy.Notifications/Templates/privacy.export_ready.html \
  --cultures de,fr-CA,zh \
  --dry-run

# Force re-translation even when the AUTO-TRANSLATED marker matches
python3 scripts/translate-templates.py \
  --source path/to/template.html \
  --cultures de \
  --force
```

### Supported cultures

Granit's 15 base cultures + 3 regional variants (matches
`docs/guide/conventions/langues.md`):

`en`, `fr`, `nl`, `de`, `es`, `it`, `pt`, `zh`, `ja`, `pl`, `tr`, `ko`, `sv`,
`cs`, `hi`, plus regional `fr-CA`, `en-GB`, `pt-BR`.

### Output files always need human review

The injected marker — `<!-- AUTO-TRANSLATED (model, date, src=...) — REVIEW
BEFORE PRODUCTION -->` — is intentional. AI translation is a strong starting
point, not a finished product. A native-speaker review pass is required before
shipping.
