#!/usr/bin/env python3
"""Summarise VSTest .trx result files as GitHub-flavoured Markdown.

Walks one or more roots for *.trx files, aggregates the pass/fail/skip counts,
and emits a Markdown summary on stdout — intended to be redirected into
$GITHUB_STEP_SUMMARY so test outcomes are visible on the job page without
downloading the trx artifacts. Failed tests are listed with their error message.

No external dependencies — runs with Python 3.8+.

Usage:
    python3 scripts/trx-summary.py [ROOT ...] >> "$GITHUB_STEP_SUMMARY"

Exits 0 regardless of test outcome (it only reports); the test step itself is
what fails the job.
"""

from __future__ import annotations

import sys
import xml.etree.ElementTree as ET
from pathlib import Path

# trx files are namespaced; strip the namespace so tag lookups stay readable.
_NS = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"


def _tag(elem: ET.Element) -> str:
    return elem.tag.replace(_NS, "")


def _summarise(trx: Path) -> tuple[int, int, int, list[tuple[str, str]]]:
    """Return (passed, failed, skipped, failures) for a single trx file."""
    passed = failed = skipped = 0
    failures: list[tuple[str, str]] = []
    try:
        root = ET.parse(trx).getroot()
    except ET.ParseError:
        return 0, 0, 0, [(trx.name, "unparseable trx file")]

    for result in root.iter(f"{_NS}UnitTestResult"):
        outcome = result.get("outcome", "")
        if outcome == "Passed":
            passed += 1
        elif outcome == "Failed":
            failed += 1
            name = result.get("testName", "<unknown>")
            message = ""
            output = result.find(f"{_NS}Output")
            if output is not None:
                err = output.find(f"{_NS}ErrorInfo")
                if err is not None:
                    msg = err.find(f"{_NS}Message")
                    if msg is not None and msg.text:
                        message = msg.text.strip().splitlines()[0]
            failures.append((name, message))
        else:
            skipped += 1
    return passed, failed, skipped, failures


def main(argv: list[str]) -> int:
    roots = [Path(a) for a in argv[1:]] or [Path(".")]
    trx_files = sorted({p for root in roots for p in root.rglob("*.trx")})

    total_passed = total_failed = total_skipped = 0
    all_failures: list[tuple[str, str]] = []
    for trx in trx_files:
        p, f, s, fails = _summarise(trx)
        total_passed += p
        total_failed += f
        total_skipped += s
        all_failures.extend(fails)

    out = sys.stdout
    if not trx_files:
        out.write("### Test results\n\n_No .trx files found._\n")
        return 0

    icon = "❌" if total_failed else "✅"
    total = total_passed + total_failed + total_skipped
    out.write(f"### {icon} Test results\n\n")
    out.write("| Total | Passed | Failed | Skipped |\n")
    out.write("| ----: | -----: | -----: | ------: |\n")
    out.write(
        f"| {total} | {total_passed} | {total_failed} | {total_skipped} |\n"
    )

    if all_failures:
        out.write("\n<details><summary>Failed tests</summary>\n\n")
        for name, message in all_failures:
            suffix = f" — {message}" if message else ""
            out.write(f"- `{name}`{suffix}\n")
        out.write("\n</details>\n")

    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
