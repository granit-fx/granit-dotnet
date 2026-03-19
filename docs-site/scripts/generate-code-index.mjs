/**
 * Generates a structured code index (code-index.json) from the Granit .NET source.
 *
 * Parses all public types and their public members from .cs files under src/,
 * plus the project dependency graph from .csproj files.
 *
 * Output is published as a GitHub Release asset and consumed by the granit-mcp Worker.
 *
 * Usage (from granit-dotnet repo root):
 *   node <path-to>/generate-code-index.mjs [--src ./src] [--out ./code-index.json]
 *
 * This script lives in granit-docs-mcp but runs against granit-dotnet source.
 * Copy it to granit-dotnet/docs-site/scripts/ or reference it directly.
 */

import { readFileSync, writeFileSync, readdirSync, statSync } from 'node:fs';
import { join, resolve, relative, basename, dirname, isAbsolute } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dir = dirname(fileURLToPath(import.meta.url));

// ─── CLI args ─────────────────────────────────────────────────────────────────

function parseArgs() {
  const args = process.argv.slice(2);
  let srcRoot = join(process.cwd(), 'src');
  let output = join(process.cwd(), 'code-index.json');
  let repoRoot = process.cwd();

  for (let i = 0; i < args.length; i++) {
    if (args[i] === '--src' && args[i + 1]) { const v = args[++i]; srcRoot = isAbsolute(v) ? v : join(process.cwd(), v); }
    if (args[i] === '--out' && args[i + 1]) { const v = args[++i]; output = isAbsolute(v) ? v : join(process.cwd(), v); }
  }

  return { srcRoot, output, repoRoot };
}

const { srcRoot: SRC_ROOT, output: OUTPUT, repoRoot: REPO_ROOT } = parseArgs();

// ─── Walk helpers ─────────────────────────────────────────────────────────────

function walkDir(dir, ext) {
  const files = [];
  for (const entry of readdirSync(dir)) {
    if (entry === 'bin' || entry === 'obj' || entry === 'node_modules' || entry === 'bundles') continue;
    const full = join(dir, entry);
    const stat = statSync(full);
    if (stat.isDirectory()) {
      files.push(...walkDir(full, ext));
    } else if (full.endsWith(ext)) {
      files.push(full);
    }
  }
  return files;
}

// ─── Project graph from .csproj (regex-based XML parsing) ─────────────────────

function parseProjectGraph() {
  const csprojFiles = walkDir(SRC_ROOT, '.csproj');
  const projects = [];

  for (const file of csprojFiles) {
    const projectName = basename(file, '.csproj');
    if (projectName.includes('Analyzers') || projectName.includes('CodeFixes')) continue;
    if (projectName.includes('SourceGenerator')) continue;

    const xml = readFileSync(file, 'utf-8');

    // Extract TargetFramework(s)
    const fwMatch = xml.match(/<TargetFramework(?:s)?>(.*?)<\/TargetFramework(?:s)?>/);
    const framework = fwMatch ? fwMatch[1] : '';

    // Extract ProjectReference dependencies
    const deps = [];
    const refRe = /<ProjectReference\s+Include="([^"]+)"/g;
    let m;
    while ((m = refRe.exec(xml)) !== null) {
      // Normalize Windows backslashes before extracting basename
      const normalized = m[1].replace(/\\/g, '/');
      const depName = basename(normalized, '.csproj');
      if (depName) deps.push(depName);
    }

    projects.push({ name: projectName, deps, framework });
  }

  return projects.sort((a, b) => a.name.localeCompare(b.name));
}

// ─── C# public type + member extraction ───────────────────────────────────────

// Matches: public [modifiers] (class|interface|record|struct|enum) Name[<T>]
const TYPE_RE = /^[ \t]*public\s+(?:(?:abstract|static|sealed|partial|readonly|ref)\s+)*(?<kind>class|interface|record|struct|enum)\s+(?<name>\w+)(?:<[^>]+>)?/gm;

function extractSymbols(csFile, projectName) {
  const content = readFileSync(csFile, 'utf-8');
  const relFile = relative(REPO_ROOT, csFile).replace(/\\/g, '/');

  const nsMatch = content.match(/^namespace\s+([\w.]+)/m);
  const namespace = nsMatch ? nsMatch[1] : projectName;

  const symbols = [];

  for (const match of content.matchAll(TYPE_RE)) {
    const kind = match.groups.kind;
    const name = match.groups.name;
    const fqn = `${namespace}.${name}`;
    const lineIdx = content.substring(0, match.index).split('\n').length;

    const members = extractMembers(content, match.index, kind);

    symbols.push({
      name,
      fqn,
      kind,
      project: projectName,
      file: relFile,
      line: lineIdx,
      members,
    });
  }

  return symbols;
}

function extractMembers(content, typeStartOffset, typeKind) {
  const members = [];

  const afterType = content.substring(typeStartOffset);
  const braceIdx = afterType.indexOf('{');
  if (braceIdx === -1) return members;

  // Walk line-by-line from the opening brace, tracking brace depth
  const bodyStart = typeStartOffset + braceIdx + 1;
  const body = content.substring(bodyStart);
  const lines = body.split('\n');

  let depth = 1; // We're already inside the type's { }
  let pendingLine = ''; // Buffer for multi-line signatures

  for (const rawLine of lines) {
    // Count braces on this line (ignoring strings/chars for simplicity)
    for (const ch of rawLine) {
      if (ch === '{') depth++;
      if (ch === '}') depth--;
    }

    if (depth <= 0) break; // End of type
    if (depth !== 1) { pendingLine = ''; continue; } // Inside a nested block

    const line = rawLine.trim();
    if (!line || line.startsWith('//') || line.startsWith('///') || line.startsWith('*') || line.startsWith('#') || line.startsWith('[')) continue;

    // Handle multi-line signatures: accumulate until we see ';' or '{'  or '=>'
    if (pendingLine) {
      pendingLine += ' ' + line;
    } else {
      pendingLine = line;
    }

    // Check if the statement is complete (ends with ; or { or =>)
    const trimmed = pendingLine.trimEnd();
    const isComplete = trimmed.endsWith(';') || trimmed.endsWith('{') ||
                       trimmed.includes('=>') || trimmed.endsWith(')');
    // Also complete if balanced parens
    const openParens = (pendingLine.match(/\(/g) || []).length;
    const closeParens = (pendingLine.match(/\)/g) || []).length;
    const parensBalanced = openParens === 0 || openParens === closeParens;

    if (!isComplete && !parensBalanced) continue; // Keep accumulating

    const fullLine = pendingLine.replace(/\s+/g, ' ').trim();
    pendingLine = '';

    // At depth 1: direct members of the type
    const isPublic = fullLine.startsWith('public ');
    const isInterfaceMember = typeKind === 'interface' && !fullLine.startsWith('private ') && !fullLine.startsWith('protected ');

    if (isPublic || isInterfaceMember) {
      const member = parseMemberLine(fullLine, typeKind);
      if (member) members.push(member);
    }
  }

  return members;
}

function parseMemberLine(line, typeKind) {
  if (/^\[/.test(line)) return null;
  if (/^\/\//.test(line)) return null;
  if (/^public\s+(?:class|interface|struct|record|enum)\s/.test(line)) return null;

  const cleanLine = typeKind === 'interface' && !line.startsWith('public ')
    ? `public ${line}`
    : line;

  // Remove leading 'public' and modifiers for parsing
  const noAccess = cleanLine
    .replace(/^public\s+/, '')
    .replace(/(?:new|virtual|abstract|override|static|async|readonly)\s+/g, '');

  // Event
  if (cleanLine.includes(' event ')) {
    const evMatch = cleanLine.match(/event\s+(\S+)\s+(\w+)/);
    if (evMatch) {
      return {
        name: evMatch[2],
        kind: 'event',
        signature: cleanLine.replace(/\s*[{;].*$/, '').replace(/^public\s+/, ''),
      };
    }
  }

  // Method: has parentheses
  if (noAccess.includes('(')) {
    const methodMatch = noAccess.match(/^([\w<>\[\]?,.\s]+?)\s+(\w+)\s*(<[^>]+>)?\s*\(([^)]*)\)/);
    if (methodMatch) {
      const retType = methodMatch[1].trim();
      const name = methodMatch[2];
      const generics = methodMatch[3] || '';
      const params = methodMatch[4].trim();
      return { name, kind: 'method', signature: `${name}${generics}(${params})`, returnType: retType };
    }
  }

  // Property: { get/set or =>
  if (/\{\s*get/.test(line) || /\{\s*set/.test(line) || /=>\s/.test(line)) {
    const propMatch = noAccess.match(/^([\w<>\[\]?,.\s]+?)\s+(\w+)\s*[{=]/);
    if (propMatch) {
      const retType = propMatch[1].trim();
      const name = propMatch[2];
      return { name, kind: 'property', signature: `${retType} ${name}`, returnType: retType };
    }
  }

  // Interface property shorthand: Type Name { get; set; }
  if (typeKind === 'interface' && /;\s*}/.test(line)) {
    const propMatch = noAccess.match(/^([\w<>\[\]?,.\s]+?)\s+(\w+)\s*\{/);
    if (propMatch) {
      const retType = propMatch[1].trim();
      const name = propMatch[2];
      return { name, kind: 'property', signature: `${retType} ${name}`, returnType: retType };
    }
  }

  return null;
}

// ─── Main ─────────────────────────────────────────────────────────────────────

console.log('Parsing project graph...');
const projectGraph = parseProjectGraph();
console.log(`  Found ${projectGraph.length} projects`);

console.log('Extracting public symbols...');
const csFiles = walkDir(SRC_ROOT, '.cs');

const sourceFiles = csFiles.filter(f => {
  const rel = relative(REPO_ROOT, f);
  if (rel.includes('/obj/') || rel.includes('/bin/')) return false;
  if (f.endsWith('.g.cs') || f.endsWith('.generated.cs')) return false;
  return true;
});

console.log(`  Scanning ${sourceFiles.length} .cs files...`);

const allSymbols = [];
for (const file of sourceFiles) {
  const rel = relative(SRC_ROOT, file);
  const projectName = rel.split('/')[0];
  const symbols = extractSymbols(file, projectName);
  allSymbols.push(...symbols);
}

// Deduplicate by fqn (partial classes)
const byFqn = new Map();
for (const sym of allSymbols) {
  const existing = byFqn.get(sym.fqn);
  if (existing) {
    existing.members.push(...sym.members);
  } else {
    byFqn.set(sym.fqn, sym);
  }
}

const symbols = [...byFqn.values()].sort((a, b) => a.fqn.localeCompare(b.fqn));

const index = {
  generatedAt: new Date().toISOString(),
  repo: 'granit-dotnet',
  projectGraph,
  symbols,
};

writeFileSync(OUTPUT, JSON.stringify(index, null, 2), 'utf-8');

const sizeKb = (JSON.stringify(index).length / 1024).toFixed(0);
const totalMembers = symbols.reduce((n, s) => n + s.members.length, 0);
console.log(`\n✓ code-index.json generated:`);
console.log(`  ${symbols.length} types, ${totalMembers} members`);
console.log(`  ${projectGraph.length} projects in dependency graph`);
console.log(`  ${sizeKb} KB`);
