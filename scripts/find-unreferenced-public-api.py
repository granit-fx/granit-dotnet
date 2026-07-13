#!/usr/bin/env python3
"""Informational sweep: public types under src/ whose name never appears outside
their own project. Candidates for dead framework surface (the 2026-07 audit found
one: MinimumResponseTimeGuard shipped with zero consumers).

Heuristic (textual, not semantic): a type consumed only via reflection, DI
open-generics, or from consuming apps outside this repo will show up as a false
positive — this script REPORTS, it never gates CI. For live analysis prefer
roslyn-lens `find_isolated_symbols`.

Usage: python3 scripts/find-unreferenced-public-api.py [--json]
"""

from __future__ import annotations

import json
import re
import sys
from collections import defaultdict
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
SRC = REPO_ROOT / "src"

PUBLIC_TYPE = re.compile(
    r"^\s*public\s+(?:sealed\s+|abstract\s+|static\s+|partial\s+|readonly\s+)*"
    r"(?:class|record|interface|struct|enum)\s+([A-Za-z_][A-Za-z0-9_]*)",
    re.MULTILINE,
)

# Names conventionally never referenced by type name: modules (assembly-scanned),
# attributes (used via [Name]), extension classes (invoked as methods), options
# (resolved via IOptions<T> inside their own project is fine), localization markers.
IGNORED_SUFFIXES = (
    "Module",
    "Attribute",
    "Extensions",
    "LocalizationResource",
    "Migration",
)


def source_files() -> list[Path]:
    return [
        f
        for f in SRC.rglob("*.cs")
        if "/bin/" not in f.as_posix() and "/obj/" not in f.as_posix()
    ]


def main() -> int:
    files = source_files()
    contents = {f: f.read_text(encoding="utf-8", errors="replace") for f in files}

    declarations: dict[str, Path] = {}
    for f, text in contents.items():
        for match in PUBLIC_TYPE.finditer(text):
            name = match.group(1)
            if not name.endswith(IGNORED_SUFFIXES):
                declarations.setdefault(name, f)

    references: dict[str, set[str]] = defaultdict(set)
    for f, text in contents.items():
        project = f.relative_to(SRC).parts[0]
        for name in declarations:
            if name in text:
                references[name].add(project)

    orphans = []
    for name, decl_file in sorted(declarations.items()):
        home = decl_file.relative_to(SRC).parts[0]
        if references[name] <= {home}:
            orphans.append(
                {"type": name, "file": str(decl_file.relative_to(REPO_ROOT)), "project": home}
            )

    try:
        if "--json" in sys.argv:
            print(json.dumps(orphans, indent=2))
        else:
            print(f"{len(orphans)} public type(s) referenced only inside their own project:")
            for o in orphans:
                print(f"  {o['type']}  ({o['file']})")
            print("\nHeuristic report — verify with roslyn-lens find_isolated_symbols before deleting.")
    except BrokenPipeError:  # piped through head — not an error
        pass

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
