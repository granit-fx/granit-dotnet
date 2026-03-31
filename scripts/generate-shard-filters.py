#!/usr/bin/env python3
"""Generate solution filter (.slnf) files for CI.

Generates two types of filters:
- Per-shard filters for test jobs (from .github/test-shards.json)
- A src-only filter for the build gate job (all src/ projects, no tests)

No external dependencies — runs with Python 3.8+.

Usage (from granit-dotnet repo root):
    python3 scripts/generate-shard-filters.py
"""

from __future__ import annotations

import json
import sys

from _slnx_utils import (
    REPO_ROOT,
    SLNX_PATH,
    load_slnx_projects,
    resolve_csproj,
    generate_slnf,
    write_slnf,
    to_slnx_path,
)

SHARDS_PATH = REPO_ROOT / ".github" / "test-shards.json"
FILTERS_DIR = REPO_ROOT / ".github" / "shard-filters"
SOLUTION_PATH = "../../Granit.slnx"


def dirs_to_csproj_paths(project_dirs: list[str]) -> list[str]:
    """Convert project directory names to slnx-relative .csproj paths."""
    paths: list[str] = []
    for d in project_dirs:
        csproj = resolve_csproj(d)
        if csproj is None:
            print(f"  WARNING: no .csproj found in {d}, skipping", file=sys.stderr)
            continue
        paths.append(to_slnx_path(csproj))
    return paths


def main() -> None:
    if not SHARDS_PATH.is_file():
        print(f"ERROR: {SHARDS_PATH} not found. Run from repo root.", file=sys.stderr)
        sys.exit(1)

    if not SLNX_PATH.is_file():
        print(f"ERROR: {SLNX_PATH} not found.", file=sys.stderr)
        sys.exit(1)

    shards = json.loads(SHARDS_PATH.read_text(encoding="utf-8"))["shards"]
    slnx_projects = load_slnx_projects()
    print(f"Loaded {len(slnx_projects)} projects from {SLNX_PATH.name}")
    FILTERS_DIR.mkdir(parents=True, exist_ok=True)

    # ── Shard filters (test jobs) ───────────────────────────────────────────
    for shard in shards:
        name = shard["name"]
        projects = shard["projects"]
        print(f"Generating {name}.slnf ({len(projects)} test projects)...")
        csproj_paths = dirs_to_csproj_paths(projects)
        slnf = generate_slnf(csproj_paths, slnx_projects, SOLUTION_PATH)
        out = FILTERS_DIR / f"{name}.slnf"
        write_slnf(out, slnf)
        print(f"  -> {out.relative_to(REPO_ROOT)} ({len(slnf['solution']['projects'])} projects)")

    # ── src-only filter (build gate job) ──────────────────────────────────
    src_projects = sorted(p for p in slnx_projects if p.startswith("src/"))
    src_slnf = {
        "solution": {
            "path": SOLUTION_PATH,
            "projects": src_projects,
        }
    }
    src_out = FILTERS_DIR / "src-only.slnf"
    write_slnf(src_out, src_slnf)
    print("Generating src-only.slnf...")
    print(f"  -> {src_out.relative_to(REPO_ROOT)} ({len(src_projects)} projects)")

    total = len(shards) + 1
    print(f"\nDone. Generated {total} solution filters in {FILTERS_DIR.relative_to(REPO_ROOT)}/")


if __name__ == "__main__":
    main()
