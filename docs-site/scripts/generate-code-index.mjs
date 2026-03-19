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
import { join, relative, basename, dirname, isAbsolute } from 'node:path';
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
      const normalized = m[1].replaceAll('\\', '/');
      const depName = basename(normalized, '.csproj');
      if (depName) deps.push(depName);
    }

    projects.push({ name: projectName, deps, framework });
  }

  return projects.sort((a, b) => a.name.localeCompare(b.name));
}

// ─── C# public type + member extraction ───────────────────────────────────────

// Matches: public [modifiers] (class|interface|record|struct|enum) Name[<T>]
const TYPE_MODIFIERS = /(?:abstract|static|sealed|partial|readonly|ref)\s+/;
const TYPE_RE = new RegExp(
  String.raw`^[ \t]*public\s+(?:${TYPE_MODIFIERS.source})*(?<kind>class|interface|record|struct|enum)\s+(?<name>\w+)(?:<[^>]+>)?`,
  'gm',
);

function extractSymbols(csFile, projectName) {
  const content = readFileSync(csFile, 'utf-8');
  const relFile = relative(REPO_ROOT, csFile).replaceAll('\\', '/');

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

function isSkippableLine(line) {
  const prefixes = ['//', '///', '*', '#', '['];
  return !line || prefixes.some((p) => line.startsWith(p));
}

function isSignatureComplete(pendingLine) {
  const trimmed = pendingLine.trimEnd();
  if (trimmed.endsWith(';') || trimmed.endsWith('{') ||
      trimmed.includes('=>') || trimmed.endsWith(')')) {
    return true;
  }
  const openParens = (pendingLine.match(/\(/g) || []).length;
  const closeParens = (pendingLine.match(/\)/g) || []).length;
  return openParens > 0 && openParens === closeParens;
}

function isPublicMember(fullLine, typeKind) {
  if (fullLine.startsWith('public ')) return true;
  if (typeKind !== 'interface') return false;
  return !fullLine.startsWith('private ') && !fullLine.startsWith('protected ');
}

function extractMembers(content, typeStartOffset, typeKind) {
  const members = [];

  const afterType = content.substring(typeStartOffset);
  const braceIdx = afterType.indexOf('{');
  if (braceIdx === -1) return members;

  const bodyStart = typeStartOffset + braceIdx + 1;
  const lines = content.substring(bodyStart).split('\n');

  let depth = 1;
  let pendingLine = '';

  for (const rawLine of lines) {
    for (const ch of rawLine) {
      if (ch === '{') depth++;
      if (ch === '}') depth--;
    }

    if (depth <= 0) break;
    if (depth !== 1) { pendingLine = ''; continue; }

    const line = rawLine.trim();
    if (isSkippableLine(line)) continue;

    pendingLine = pendingLine ? `${pendingLine} ${line}` : line;

    if (!isSignatureComplete(pendingLine)) continue;

    const fullLine = pendingLine.replaceAll(/\s+/g, ' ').trim();
    pendingLine = '';

    if (isPublicMember(fullLine, typeKind)) {
      const member = parseMemberLine(fullLine, typeKind);
      if (member) members.push(member);
    }
  }

  return members;
}

function tryParseEvent(cleanLine) {
  if (!cleanLine.includes(' event ')) return null;
  const evMatch = cleanLine.match(/event\s+(\S+)\s+(\w+)/);
  if (!evMatch) return null;
  return {
    name: evMatch[2],
    kind: 'event',
    signature: cleanLine.replace(/\s*[{;].*$/, '').replace(/^public\s+/, ''),
  };
}

function tryParseMethod(noAccess) {
  if (!noAccess.includes('(')) return null;
  // Match return type + name + params. Use atomic-like approach: return type
  // is everything before the last identifier before '(', captured greedily
  // up to whitespace + word boundary.
  const parenIdx = noAccess.indexOf('(');
  if (parenIdx === -1) return null;
  const beforeParen = noAccess.substring(0, parenIdx).trimEnd();
  const nameMatch = beforeParen.match(/^(.+)\s+(\w+)$/);
  if (!nameMatch) return null;
  const retType = nameMatch[1].trim();
  const name = nameMatch[2];
  const genericsMatch = noAccess.substring(parenIdx).match(/^(<[^>]+>)?\s*\(([^)]*)\)/);
  if (!genericsMatch) return null;
  const generics = genericsMatch[1] || '';
  const params = genericsMatch[2].trim();
  return { name, kind: 'method', signature: `${name}${generics}(${params})`, returnType: retType };
}

function tryParseProperty(line, noAccess, typeKind) {
  const isGetSet = /\{\s*get/.test(line) || /\{\s*set/.test(line) || /=>\s/.test(line);
  const isInterfaceProp = typeKind === 'interface' && /;\s*}/.test(line);
  if (!isGetSet && !isInterfaceProp) return null;

  // Find the property name (last identifier before { or =)
  const propSigEnd = noAccess.search(/\s*[{=]/);
  if (propSigEnd === -1) return null;
  const propSig = noAccess.substring(0, propSigEnd).trimEnd();
  const propParts = propSig.match(/^(.+)\s+(\w+)$/);
  if (!propParts) return null;
  const retType = propParts[1].trim();
  const name = propParts[2];
  return { name, kind: 'property', signature: `${retType} ${name}`, returnType: retType };
}

function parseMemberLine(line, typeKind) {
  if (line.startsWith('[') || line.startsWith('//')) return null;
  if (/^public\s+(?:class|interface|struct|record|enum)\s/.test(line)) return null;

  const cleanLine = typeKind === 'interface' && !line.startsWith('public ')
    ? `public ${line}`
    : line;

  const noAccess = cleanLine
    .replace(/^public\s+/, '')
    .replaceAll(/(?:new|virtual|abstract|override|static|async|readonly)\s+/g, '');

  return tryParseEvent(cleanLine)
    ?? tryParseMethod(noAccess)
    ?? tryParseProperty(line, noAccess, typeKind);
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
