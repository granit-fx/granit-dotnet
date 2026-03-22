#!/usr/bin/env python3
"""Generate code-index.json from Granit .NET source.

Parses all public types and their public members from .cs files under src/,
plus the project dependency graph from .csproj files.

No external dependencies — runs with Python 3.8+.

Usage (from granit-dotnet repo root):
    python3 scripts/generate-code-index.py [--src ./src] [--out code-index.json]
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys

from pathlib import Path


PUBLIC_PREFIX = "public "

# ─── CLI ──────────────────────────────────────────────────────────────────────


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Generate code-index.json")
    parser.add_argument("--src", default="src", help="Source root (default: src)")
    parser.add_argument("--out", default=".mcp-code-index.json", help="Output path")
    return parser.parse_args()


# ─── Walk helpers ─────────────────────────────────────────────────────────────

SKIP_DIRS = {"bin", "obj", "node_modules", "bundles"}


def walk_files(directory: Path, ext: str) -> list[Path]:
    files: list[Path] = []
    for entry in sorted(directory.iterdir()):
        if entry.name in SKIP_DIRS:
            continue
        if entry.is_dir():
            files.extend(walk_files(entry, ext))
        elif entry.suffix == ext:
            files.append(entry)
    return files


# ─── Project graph from .csproj ──────────────────────────────────────────────


def read_default_framework(src_root: Path) -> str:
    """Read TargetFramework from Directory.Build.props (walks up from src_root)."""
    for directory in [src_root, src_root.parent]:
        props = directory / "Directory.Build.props"
        if props.is_file():
            m = re.search(r"<TargetFrameworks?>(.*?)</TargetFrameworks?>", props.read_text(encoding="utf-8"))
            if m:
                return m.group(1)
    return ""


def parse_project_graph(src_root: Path) -> list[dict]:
    default_fw = read_default_framework(src_root)
    csproj_files = walk_files(src_root, ".csproj")
    projects = []

    for file in csproj_files:
        name = file.stem
        if any(skip in name for skip in ("Analyzers", "CodeFixes", "SourceGenerator")):
            continue

        xml = file.read_text(encoding="utf-8")

        # Extract TargetFramework(s) — fall back to Directory.Build.props
        fw_match = re.search(r"<TargetFrameworks?>(.*?)</TargetFrameworks?>", xml)
        framework = fw_match.group(1) if fw_match else default_fw

        # Extract ProjectReference dependencies
        deps = []
        for m in re.finditer(r'<ProjectReference\s+Include="([^"]+)"', xml):
            normalized = m.group(1).replace("\\", "/")
            dep_name = Path(normalized).stem
            if dep_name:
                deps.append(dep_name)

        projects.append({"name": name, "deps": deps, "framework": framework})

    return sorted(projects, key=lambda p: p["name"])


# ─── C# type extraction ─────────────────────────────────────────────────────

TYPE_RE = re.compile(
    r"^[ \t]*public\s+(?:(?:abstract|static|sealed|partial|readonly|ref)\s+)*"
    r"(?P<kind>class|interface|record|struct|enum)\s+(?P<name>\w+)(?:<[^>]+>)?",
    re.MULTILINE,
)


def extract_symbols(cs_file: Path, project_name: str, repo_root: Path) -> list[dict]:
    content = cs_file.read_text(encoding="utf-8")
    rel_file = str(cs_file.relative_to(repo_root)).replace("\\", "/")

    ns_match = re.search(r"^namespace\s+([\w.]+)", content, re.MULTILINE)
    namespace = ns_match.group(1) if ns_match else project_name

    symbols = []
    for match in TYPE_RE.finditer(content):
        kind = match.group("kind")
        name = match.group("name")
        fqn = f"{namespace}.{name}"
        line_idx = content[: match.start()].count("\n") + 1
        members = extract_members(content, match.start(), kind)

        symbols.append({
            "name": name,
            "fqn": fqn,
            "kind": kind,
            "project": project_name,
            "file": rel_file,
            "line": line_idx,
            "members": members,
        })

    return symbols


# ─── Member extraction ───────────────────────────────────────────────────────


def is_word_char(ch: str) -> bool:
    return ch.isalnum() or ch == "_"


def split_last_word(s: str) -> tuple[str, str] | None:
    t = s.rstrip()
    i = len(t) - 1
    while i >= 0 and is_word_char(t[i]):
        i -= 1
    if i < 0 or i == len(t) - 1:
        return None
    word = t[i + 1 :]
    before = t[: i + 1].rstrip()
    return (before, word) if before else None


def truncate_at(s: str, chars: str) -> str:
    for i, ch in enumerate(s):
        if ch in chars:
            return s[:i].rstrip()
    return s


def index_of_any(s: str, chars: str) -> int:
    for i, ch in enumerate(s):
        if ch in chars:
            return i
    return -1


def strip_public_prefix(s: str) -> str:
    if not s.startswith(PUBLIC_PREFIX):
        return s
    return s[len(PUBLIC_PREFIX):].lstrip()


def is_skippable_line(line: str) -> bool:
    if not line:
        return True
    return any(line.startswith(p) for p in ("//", "///", "*", "#", "["))


def is_signature_complete(pending: str) -> bool:
    trimmed = pending.rstrip()
    if any(trimmed.endswith(c) for c in (";", "{", ")")):
        return True
    if "=>" in trimmed:
        return True
    return pending.count("(") > 0 and pending.count("(") == pending.count(")")


def is_public_member(full_line: str, type_kind: str) -> bool:
    if full_line.startswith(PUBLIC_PREFIX):
        return True
    if type_kind != "interface":
        return False
    return not full_line.startswith("private ") and not full_line.startswith("protected ")


def count_brace_depth(line: str, depth: int) -> int:
    for ch in line:
        if ch == "{":
            depth += 1
        elif ch == "}":
            depth -= 1
    return depth


def try_parse_event(clean_line: str) -> dict | None:
    idx = clean_line.find(" event ")
    if idx == -1:
        return None
    after_event = clean_line[idx + 7 :].strip()
    space_idx = after_event.find(" ")
    if space_idx == -1:
        return None
    rest = after_event[space_idx + 1 :].strip()
    end = 0
    while end < len(rest) and is_word_char(rest[end]):
        end += 1
    if end == 0:
        return None
    return {
        "name": rest[:end],
        "kind": "event",
        "signature": strip_public_prefix(truncate_at(clean_line, "{;")),
    }


def try_parse_method(no_access: str) -> dict | None:
    paren_idx = no_access.find("(")
    if paren_idx == -1:
        return None
    before_paren = no_access[:paren_idx].rstrip()
    parts = split_last_word(before_paren)
    if not parts:
        return None
    ret_type, name = parts

    after = no_access[paren_idx:]
    generics = ""
    search_from = 0
    if after.startswith("<"):
        gt_idx = after.find(">")
        if gt_idx == -1:
            return None
        generics = after[: gt_idx + 1]
        search_from = gt_idx + 1

    open_idx = after.find("(", search_from)
    if open_idx == -1:
        return None
    close_idx = after.find(")", open_idx)
    if close_idx == -1:
        return None
    params = after[open_idx + 1 : close_idx].strip()
    return {
        "name": name,
        "kind": "method",
        "signature": f"{name}{generics}({params})",
        "returnType": ret_type,
    }


def try_parse_property(line: str, no_access: str, type_kind: str) -> dict | None:
    brace_idx = line.find("{")
    is_get_set = "=>" in line
    if not is_get_set and brace_idx != -1:
        after_brace = line[brace_idx + 1 :].lstrip()
        is_get_set = after_brace.startswith("get") or after_brace.startswith("set")
    is_interface_prop = type_kind == "interface" and ";" in line and "}" in line
    if not is_get_set and not is_interface_prop:
        return None

    char_idx = index_of_any(no_access, "{=")
    if char_idx == -1:
        return None
    prop_sig = no_access[:char_idx].rstrip()
    prop_parts = split_last_word(prop_sig)
    if not prop_parts:
        return None
    ret_type, name = prop_parts
    return {
        "name": name,
        "kind": "property",
        "signature": f"{ret_type} {name}",
        "returnType": ret_type,
    }


def parse_member_line(line: str, type_kind: str) -> dict | None:
    if line.startswith("[") or line.startswith("//"):
        return None
    if re.match(r"^public\s+(?:class|interface|struct|record|enum)\s", line):
        return None

    clean_line = f"{PUBLIC_PREFIX}{line}" if type_kind == "interface" and not line.startswith(PUBLIC_PREFIX) else line

    no_access = re.sub(r"^public\s+", "", clean_line)
    no_access = re.sub(r"(?:new|virtual|abstract|override|static|async|readonly)\s+", "", no_access)

    result = try_parse_event(clean_line)
    if result:
        return result
    result = try_parse_method(no_access)
    if result:
        return result
    return try_parse_property(line, no_access, type_kind)


def _try_collect_member(pending_line: str, type_kind: str) -> dict | None:
    full_line = re.sub(r"\s+", " ", pending_line).strip()
    if not is_public_member(full_line, type_kind):
        return None
    return parse_member_line(full_line, type_kind)


def extract_members(content: str, type_start_offset: int, type_kind: str) -> list[dict]:
    after_type = content[type_start_offset:]
    brace_idx = after_type.find("{")
    if brace_idx == -1:
        return []

    body_start = type_start_offset + brace_idx + 1
    lines = content[body_start:].split("\n")

    members: list[dict] = []
    depth = 1
    pending_line = ""

    for raw_line in lines:
        depth = count_brace_depth(raw_line, depth)
        if depth <= 0:
            break
        if depth != 1:
            pending_line = ""
            continue

        line = raw_line.strip()
        if is_skippable_line(line):
            continue

        pending_line = f"{pending_line} {line}" if pending_line else line
        if not is_signature_complete(pending_line):
            continue

        member = _try_collect_member(pending_line, type_kind)
        if member:
            members.append(member)
        pending_line = ""

    return members


# ─── Main ────────────────────────────────────────────────────────────────────


def main() -> None:
    args = parse_args()
    src_root = Path(args.src).resolve()
    repo_root = src_root.parent
    output = Path(args.out) if os.path.isabs(args.out) else repo_root / args.out

    if not src_root.is_dir():
        print(f"Source directory not found: {src_root}", file=sys.stderr)
        sys.exit(1)

    print("Parsing project graph...")
    project_graph = parse_project_graph(src_root)
    print(f"  Found {len(project_graph)} projects")

    print("Extracting public symbols...")
    cs_files = walk_files(src_root, ".cs")

    source_files = [
        f
        for f in cs_files
        if "/obj/" not in str(f)
        and "/bin/" not in str(f)
        and not f.name.endswith(".g.cs")
        and not f.name.endswith(".generated.cs")
    ]
    print(f"  Scanning {len(source_files)} .cs files...")

    all_symbols: list[dict] = []
    for file in source_files:
        rel = file.relative_to(src_root)
        project_name = rel.parts[0]
        all_symbols.extend(extract_symbols(file, project_name, repo_root))

    # Deduplicate by fqn (partial classes)
    by_fqn: dict[str, dict] = {}
    for sym in all_symbols:
        existing = by_fqn.get(sym["fqn"])
        if existing:
            existing["members"].extend(sym["members"])
        else:
            by_fqn[sym["fqn"]] = sym

    symbols = sorted(by_fqn.values(), key=lambda s: s["fqn"])

    index = {
        "repo": "granit-dotnet",
        "projectGraph": project_graph,
        "symbols": symbols,
    }

    output.write_text(json.dumps(index, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    total_members = sum(len(s["members"]) for s in symbols)
    size_kb = len(json.dumps(index)) // 1024

    print("\ncode-index.json generated:")
    print(f"  {len(symbols)} types, {total_members} members")
    print(f"  {len(project_graph)} projects in dependency graph")
    print(f"  {size_kb} KB")


if __name__ == "__main__":
    main()
