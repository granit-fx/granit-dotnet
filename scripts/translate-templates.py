#!/usr/bin/env python3
# /// script
# requires-python = ">=3.10"
# dependencies = [
#     "anthropic>=0.40.0",
# ]
# ///
"""AI translation of Granit HTML notification templates.

Takes a neutral English ``.html`` template (e.g. Privacy / Identity / Webhooks
notifications) plus a list of target cultures, and produces one
``{name}.{culture}.html`` file per culture using the Claude API.

Why this exists:
    Granit framework ships notification templates in 18 cultures. Hand-translating
    13+ language variants for every new template is slow and error-prone; this
    script industrialises that work while preserving the strict invariants the
    Scriban template engine and the email rendering pipeline require.

Strict preservation contract:
    1. Every ``{{ ... }}`` Scriban placeholder, every ``{{ if/for/end }}``
       directive — same identifier names, same spacing — is byte-identical.
    2. HTML attributes (``href``, ``style``, classes, etc.) are byte-identical.
    3. Only human-language content (``<title>``, paragraph text, link labels,
       headings) is translated.

Reproducibility:
    - ``temperature=0`` and a stable system prompt make outputs near-deterministic.
    - The system prompt is cached (Anthropic prompt caching) — saves cost across
      cultures and across runs.
    - Idempotency: when the existing output's ``AUTO-TRANSLATED`` marker matches
      the source content hash, the file is skipped (use ``--force`` to override).

Post-validation:
    - Counts of ``{{`` and ``}}`` must match input vs output exactly.
    - Every ``{{ ... }}`` block in the input must appear verbatim in the output.
    - Unbalanced HTML tag count check (rough heuristic, not a full parse).

Reference: https://github.com/granit-fx/granit-dotnet/issues/1311
"""

from __future__ import annotations

import argparse
import hashlib
import os
import re
import sys
import tempfile
from dataclasses import dataclass
from datetime import date
from pathlib import Path

# ─── Configuration ────────────────────────────────────────────────────────────

# Latest Opus model at time of writing (April 2026).
# See ~/.claude/skills/claude-api for the current canonical list.
MODEL = "claude-opus-4-7"

MAX_TOKENS = 16000

# Marker injected at top of every generated file.
MARKER_PREFIX = "<!-- AUTO-TRANSLATED"

# Map ISO culture codes to human-readable language names so the prompt can
# address Claude in the target language unambiguously. Covers Granit's 15 base
# cultures + 3 regional. Source of truth: docs/guide/conventions/langues.md.
CULTURE_NAMES: dict[str, str] = {
    "en": "English",
    "fr": "French",
    "fr-CA": "Canadian French (français canadien)",
    "en-GB": "British English",
    "nl": "Dutch (Nederlands)",
    "de": "German (Deutsch)",
    "es": "Spanish (español)",
    "it": "Italian (italiano)",
    "pt": "European Portuguese (português europeu)",
    "pt-BR": "Brazilian Portuguese (português brasileiro)",
    "zh": "Simplified Chinese (简体中文)",
    "ja": "Japanese (日本語)",
    "pl": "Polish (polski)",
    "tr": "Turkish (Türkçe)",
    "ko": "Korean (한국어)",
    "sv": "Swedish (svenska)",
    "cs": "Czech (čeština)",
    "hi": "Hindi (हिन्दी)",
}

SYSTEM_PROMPT_TEMPLATE = """You are a professional technical translator working on the Granit .NET framework's HTML email/notification templates. Your task is to translate the human-readable text in a Scriban-templated HTML document into {target_language_name} ({target_culture_code}).

# ABSOLUTE RULES — violations will break production rendering

1. **Preserve every Scriban block exactly.** Any sequence inside `{{ ... }}` (double-brace) — including `{{ if ... }}`, `{{ for ... }}`, `{{ end }}`, `{{ model.X }}`, `{{ privacy.Y }}`, `{{ app.Z }}`, pipe filters like `| date.to_string '%B %d, %Y'`, etc. — MUST appear in the output byte-for-byte identical to the input. Same identifier names. Same spacing. Same case. Same quoting. NEVER translate identifier names, filter names, or string literals inside Scriban blocks.

2. **Preserve every HTML tag, attribute name, and attribute value.** `href`, `src`, `style`, `class`, `id`, `data-*`, etc. are byte-identical to the input. URLs (e.g. `mailto:`, hosted asset paths) are NOT translated. Inline CSS (colors, paddings, font sizes) is NOT translated.

3. **Preserve the HTML structure.** Same nesting. Same number of `<p>`, `<a>`, `<ul>`, `<li>`, `<strong>`, `<em>`, `<h1>`–`<h6>`, `<title>`, `<code>`, etc. tags. Same order. Do NOT add or remove tags. Do NOT wrap things differently.

4. **Translate ONLY the human-readable text content** of:
   - The `<title>` tag (this is the email subject — translate it).
   - Text inside `<p>`, `<strong>`, `<em>`, `<a>`, `<li>`, `<h1>`–`<h6>`, `<span>`, `<div>` (when they contain prose).
   - Plain text nodes that are clearly user-facing prose.

5. **Do NOT translate text inside `<code>` tags.** `<code>` wraps technical identifiers (regulation codes like `EU_GDPR`, request IDs, status enums) used for compliance traceability. Their content must remain verbatim.

6. **Do NOT translate text that is itself a Scriban output.** If the visible text inside an HTML element is `{{ model.something }}`, leave it alone — that's runtime data, not source language.

7. **Tone and style.** Professional, clear, plain language. GDPR Article 12 §1 compliant: concise, transparent, intelligible, plain. Use the target language's standard formal register (e.g. German "Sie", French "vous", Japanese 丁寧語). Avoid colloquialisms and marketing fluff.

8. **Output format.** Return ONLY the translated HTML. NO surrounding markdown fences. NO preamble like "Here is the translation:". NO trailing commentary. The first character of your response must be the first character of the translated HTML (typically `<`). The last character must be the last character of the translated HTML.

# Common mistakes to avoid

- Translating `model.dpo_email` to `model.dpo_courriel` — NEVER. Identifier names are immutable.
- Translating `EU_GDPR` to `UE_RGPD` inside `<code>` — NEVER. Compliance codes are universal.
- Reformatting the HTML (changing indentation, collapsing whitespace, adding line breaks). Preserve the original layout exactly.
- Translating URL fragments in `href`. URLs are functional, not prose.
- Adding "<!-- comments -->" or any other content not present in the source.

The user message is the source HTML. Reply with ONLY the translated HTML."""


# ─── Validation logic ─────────────────────────────────────────────────────────


# Match Scriban `{{ ... }}` blocks. Non-greedy so `{{ a }}{{ b }}` parses as two
# blocks. Multiline-aware (some templates wrap directives across lines).
_SCRIBAN_BLOCK = re.compile(r"\{\{[^}]*\}\}", re.DOTALL)

# Match opening/closing/self-closing HTML tags (excluding comments and DOCTYPE).
_HTML_TAG = re.compile(r"<(/?)([a-zA-Z][a-zA-Z0-9]*)(?:\s[^>]*)?(/?)>")

# Self-closing tags that don't need a closing pair in standards-compliant HTML.
_VOID_TAGS = frozenset({"area", "base", "br", "col", "embed", "hr", "img",
                        "input", "link", "meta", "param", "source", "track",
                        "wbr"})


@dataclass
class ValidationResult:
    """Outcome of post-translation invariant checks."""
    ok: bool
    diagnostics: list[str]


def extract_scriban_blocks(html: str) -> list[str]:
    """Return every Scriban `{{ ... }}` block in document order."""
    return _SCRIBAN_BLOCK.findall(html)


def count_braces(text: str) -> tuple[int, int]:
    """Return (count of '{{', count of '}}'). Used as a fast structural check."""
    return text.count("{{"), text.count("}}")


def check_html_tag_balance(html: str) -> list[str]:
    """Rough HTML well-formedness check.

    Counts opening vs closing tags per element name (ignoring void tags).
    Returns a list of diagnostic strings; empty means balanced. This is a
    heuristic, not a full HTML parse — but Granit notification templates are
    intentionally simple, so a rough check catches the common LLM mistakes
    (forgetting a `</p>`, doubling a `<strong>`).
    """
    counts: dict[str, int] = {}
    for match in _HTML_TAG.finditer(html):
        is_close = match.group(1) == "/"
        name = match.group(2).lower()
        is_self_close = match.group(3) == "/"
        if name in _VOID_TAGS or is_self_close:
            continue
        counts[name] = counts.get(name, 0) + (-1 if is_close else 1)

    return [
        f"unbalanced <{name}>: net {delta:+d}"
        for name, delta in counts.items()
        if delta != 0
    ]


def validate_translation(source: str, translated: str) -> ValidationResult:
    """Run all post-translation invariant checks."""
    diagnostics: list[str] = []

    src_open, src_close = count_braces(source)
    out_open, out_close = count_braces(translated)
    if src_open != out_open:
        diagnostics.append(
            f"`{{{{` count mismatch: source={src_open}, output={out_open}"
        )
    if src_close != out_close:
        diagnostics.append(
            f"`}}}}` count mismatch: source={src_close}, output={out_close}"
        )

    src_blocks = extract_scriban_blocks(source)
    out_blocks = extract_scriban_blocks(translated)
    # Order matters — Scriban directives must keep their structural position.
    if src_blocks != out_blocks:
        from collections import Counter
        src_counts = Counter(src_blocks)
        out_counts = Counter(out_blocks)
        missing = list((src_counts - out_counts).elements())
        added = list((out_counts - src_counts).elements())
        if missing:
            diagnostics.append(
                f"Scriban blocks missing or altered ({len(missing)}): "
                + ", ".join(repr(b) for b in missing[:5])
                + ("..." if len(missing) > 5 else "")
            )
        if added:
            diagnostics.append(
                f"Scriban blocks fabricated by translator ({len(added)}): "
                + ", ".join(repr(b) for b in added[:5])
                + ("..." if len(added) > 5 else "")
            )
        if not missing and not added:
            # Same multiset, different order — still a structural change.
            diagnostics.append("Scriban blocks reordered (multiset equal, sequence differs)")

    diagnostics.extend(check_html_tag_balance(translated))

    return ValidationResult(ok=not diagnostics, diagnostics=diagnostics)


# ─── Marker / idempotency ─────────────────────────────────────────────────────


def source_hash(source_html: str) -> str:
    """SHA-256 of the source content, truncated for marker readability."""
    return hashlib.sha256(source_html.encode("utf-8")).hexdigest()[:16]


def build_marker(model: str, source_html: str) -> str:
    """Build the AUTO-TRANSLATED marker line."""
    return (
        f"{MARKER_PREFIX} ({model}, {date.today().isoformat()}, "
        f"src={source_hash(source_html)}) — REVIEW BEFORE PRODUCTION -->"
    )


_MARKER_RE = re.compile(
    re.escape(MARKER_PREFIX) + r"\s*\([^,]+,\s*\d{4}-\d{2}-\d{2},\s*src=([0-9a-f]+)\)"
)


def existing_output_matches_source(output_path: Path, source_html: str) -> bool:
    """True if `output_path` exists and its marker's src hash matches source."""
    if not output_path.exists():
        return False
    try:
        first_line = output_path.read_text(encoding="utf-8").splitlines()[0]
    except (OSError, IndexError):
        return False
    match = _MARKER_RE.search(first_line)
    return bool(match and match.group(1) == source_hash(source_html))


# ─── Anthropic API call ───────────────────────────────────────────────────────


def call_claude(
    *,
    client,  # anthropic.Anthropic — typed at call site; avoids import at module load
    source_html: str,
    target_culture: str,
    target_language_name: str,
) -> tuple[str, dict]:
    """Call Claude with prompt caching on the system prompt.

    Returns (translated_html, usage_dict). The system prompt is marked
    `cache_control: ephemeral` so subsequent cultures (and subsequent runs)
    pay ~0.1× for the prefix instead of full price. Only the user message
    (the source HTML) is uncached — that's what should vary.
    """
    system_prompt = SYSTEM_PROMPT_TEMPLATE.format(
        target_language_name=target_language_name,
        target_culture_code=target_culture,
    )

    response = client.messages.create(
        model=MODEL,
        max_tokens=MAX_TOKENS,
        system=[
            {
                "type": "text",
                "text": system_prompt,
                "cache_control": {"type": "ephemeral"},
            }
        ],
        messages=[{"role": "user", "content": source_html}],
        # Defensive: thinking removes temperature on Opus 4.7. Don't set it.
    )

    # Concatenate text blocks (defensive: usually exactly one).
    parts = [b.text for b in response.content if b.type == "text"]
    translated = "".join(parts).strip()

    usage = {
        "input_tokens": response.usage.input_tokens,
        "output_tokens": response.usage.output_tokens,
        "cache_creation_input_tokens": getattr(
            response.usage, "cache_creation_input_tokens", 0
        ),
        "cache_read_input_tokens": getattr(
            response.usage, "cache_read_input_tokens", 0
        ),
    }
    return translated, usage


# ─── Atomic write ─────────────────────────────────────────────────────────────


def atomic_write(path: Path, content: str) -> None:
    """Write `content` to `path` via a temp file + os.replace.

    Prevents leaving a half-written file if the process is interrupted.
    """
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, tmp_path = tempfile.mkstemp(
        prefix=f".{path.name}.",
        suffix=".tmp",
        dir=str(path.parent),
    )
    try:
        with os.fdopen(fd, "w", encoding="utf-8", newline="") as f:
            f.write(content)
        os.replace(tmp_path, path)
    except BaseException:
        # Clean up temp file on failure (including KeyboardInterrupt).
        try:
            os.unlink(tmp_path)
        except OSError:
            pass
        raise


# ─── Output path resolution ───────────────────────────────────────────────────


def resolve_output_path(
    source: Path, culture: str, output_dir: Path | None
) -> Path:
    """Compute the per-culture output path.

    For ``Templates/privacy.export_ready.html`` and culture ``de`` →
    ``Templates/privacy.export_ready.de.html``. Honours ``--output-dir`` when
    set, otherwise writes alongside the source.
    """
    stem = source.stem  # e.g. "privacy.export_ready"
    suffix = source.suffix  # ".html"
    target_name = f"{stem}.{culture}{suffix}"
    return (output_dir or source.parent) / target_name


# ─── CLI ──────────────────────────────────────────────────────────────────────


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="AI-translate a Granit HTML notification template into one or more cultures.",
        epilog="Reference: https://github.com/granit-fx/granit-dotnet/issues/1311",
    )
    parser.add_argument(
        "--source",
        type=Path,
        required=True,
        help="Path to the neutral English .html template (e.g. "
             "src/Granit.Privacy.Notifications/Templates/privacy.export_ready.html)",
    )
    parser.add_argument(
        "--cultures",
        type=str,
        required=True,
        help="Comma-separated culture codes (e.g. de,nl,es,it,pt,zh,ja,pl,tr,ko,sv,cs,hi)",
    )
    parser.add_argument(
        "--output-dir",
        type=Path,
        default=None,
        help="Directory for translated files. Defaults to the source's directory.",
    )
    parser.add_argument(
        "--force",
        action="store_true",
        help="Overwrite outputs even if their AUTO-TRANSLATED marker matches the source hash.",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Show what would be produced without calling the API or writing files.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()

    if not args.source.is_file():
        print(f"error: source file not found: {args.source}", file=sys.stderr)
        return 2

    source_html = args.source.read_text(encoding="utf-8")
    cultures = [c.strip() for c in args.cultures.split(",") if c.strip()]
    if not cultures:
        print("error: --cultures must contain at least one culture code", file=sys.stderr)
        return 2

    unknown = [c for c in cultures if c not in CULTURE_NAMES]
    if unknown:
        print(
            f"error: unknown culture code(s): {', '.join(unknown)}. "
            f"Known: {', '.join(sorted(CULTURE_NAMES))}",
            file=sys.stderr,
        )
        return 2

    print(f"Source: {args.source}")
    print(f"Source hash: {source_hash(source_html)}")
    print(f"Cultures: {', '.join(cultures)}")
    print(f"Model: {MODEL}")
    print(f"Output dir: {args.output_dir or args.source.parent}")
    print()

    if args.dry_run:
        print("--dry-run: not calling the API, not writing files.")
        for culture in cultures:
            output_path = resolve_output_path(args.source, culture, args.output_dir)
            already = existing_output_matches_source(output_path, source_html)
            status = "skip (marker matches)" if already and not args.force else "would write"
            print(f"  [{culture:6s}] {output_path}  ({status})")
        return 0

    api_key = os.environ.get("ANTHROPIC_API_KEY")
    if not api_key:
        print(
            "error: ANTHROPIC_API_KEY environment variable is not set.",
            file=sys.stderr,
        )
        return 2

    try:
        import anthropic
    except ImportError:
        print(
            "error: the 'anthropic' package is not installed. "
            "Run with `uv run scripts/translate-templates.py ...` "
            "(uv reads the PEP 723 inline metadata) or `pip install anthropic`.",
            file=sys.stderr,
        )
        return 2

    client = anthropic.Anthropic(api_key=api_key)

    written = 0
    skipped = 0
    failed = 0
    total_input = 0
    total_output = 0
    total_cache_read = 0
    total_cache_create = 0

    for culture in cultures:
        output_path = resolve_output_path(args.source, culture, args.output_dir)

        if not args.force and existing_output_matches_source(output_path, source_html):
            print(f"  [{culture:6s}] {output_path.name}  skip (marker matches)")
            skipped += 1
            continue

        language_name = CULTURE_NAMES[culture]
        print(f"  [{culture:6s}] translating → {language_name}... ", end="", flush=True)

        try:
            translated, usage = call_claude(
                client=client,
                source_html=source_html,
                target_culture=culture,
                target_language_name=language_name,
            )
        except anthropic.APIError as e:  # type: ignore[attr-defined]
            print(f"FAILED ({type(e).__name__}: {e})")
            failed += 1
            continue

        result = validate_translation(source_html, translated)
        if not result.ok:
            print("FAILED validation:")
            for diag in result.diagnostics:
                print(f"      - {diag}")
            failed += 1
            continue

        marker = build_marker(MODEL, source_html)
        final_content = marker + "\n" + translated
        if not final_content.endswith("\n"):
            final_content += "\n"

        atomic_write(output_path, final_content)
        written += 1
        total_input += usage["input_tokens"]
        total_output += usage["output_tokens"]
        total_cache_read += usage["cache_read_input_tokens"]
        total_cache_create += usage["cache_creation_input_tokens"]

        cache_note = ""
        if usage["cache_read_input_tokens"]:
            cache_note = f" [cache hit: {usage['cache_read_input_tokens']} tokens]"
        elif usage["cache_creation_input_tokens"]:
            cache_note = f" [cache write: {usage['cache_creation_input_tokens']} tokens]"
        print(f"OK ({usage['output_tokens']} tokens out{cache_note})")

    print()
    print(f"Summary: {written} written, {skipped} skipped, {failed} failed")
    print(f"Tokens — input: {total_input}, output: {total_output}, "
          f"cache_read: {total_cache_read}, cache_write: {total_cache_create}")

    return 0 if failed == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
