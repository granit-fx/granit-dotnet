/**
 * Generates a structured search index (search-index.json) from the docs source.
 *
 * Reads all .md/.mdx files under src/content/docs/, extracts frontmatter and
 * content, assigns a category based on path, and writes the result to dist/.
 *
 * This file is served by CF Pages and consumed by the granit-docs-mcp Worker.
 *
 * Run after `pnpm astro build`:
 *   node scripts/generate-search-index.mjs
 */

import { readFileSync, writeFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative, extname, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dir = dirname(fileURLToPath(import.meta.url));
const DOCS_ROOT = join(__dir, '../src/content/docs');
const OUTPUT = join(__dir, '../dist/search-index.json');

// ─── Category mapping from file path ────────────────────────────────────────

const CATEGORY_RULES = [
  { pattern: /\/architecture\/patterns\//, category: 'pattern' },
  { pattern: /\/architecture\/adr\//, category: 'adr' },
  { pattern: /\/guides\//, category: 'guide' },
  { pattern: /\/getting-started\//, category: 'getting-started' },
  { pattern: /\/concepts\//, category: 'concept' },
  { pattern: /\/contributing\//, category: 'community' },
  { pattern: /\/migration\//, category: 'community' },
  { pattern: /\/troubleshooting\//, category: 'community' },
  { pattern: /\/blog\//, category: 'blog' },
  // Module pages: dotnet topic-area pages (core, data, security, api, etc.)
  { pattern: /\/dotnet\/(?:core|data|security|api|infrastructure|business|operations|ai)\//, category: 'module' },
  // Frontend pages
  { pattern: /\/frontend\//, category: 'frontend' },
];

function categorize(relPath) {
  for (const { pattern, category } of CATEGORY_RULES) {
    if (pattern.test(relPath)) return category;
  }
  return 'other';
}

// ─── Frontmatter parser (simple, no dependency) ─────────────────────────────

function parseFrontmatter(raw) {
  const match = raw.match(/^---\n([\s\S]*?)\n---\n([\s\S]*)$/);
  if (!match) return { meta: {}, content: raw };

  const meta = {};
  for (const line of match[1].split('\n')) {
    const kv = line.match(/^(\w+):\s*"?(.+?)"?\s*$/);
    if (kv) meta[kv[1]] = kv[2];
  }

  // Decode YAML unicode escapes (\uXXXX) that appear in double-quoted strings
  for (const key of Object.keys(meta)) {
    meta[key] = meta[key].replace(/\\u([0-9a-fA-F]{4})/g, (_, hex) =>
      String.fromCodePoint(parseInt(hex, 16)),
    );
  }

  return { meta, content: match[2] };
}

// ─── Markdown content cleaner ────────────────────────────────────────────────

function cleanContent(md) {
  return md
    // Remove import statements (MDX)
    .replace(/^import\s+.*$/gm, '')
    // Remove JSX/HTML components
    .replace(/<[A-Z][^>]*>[\s\S]*?<\/[A-Z][^>]*>/g, '')
    .replace(/<[A-Z][^/>]*\/>/g, '')
    .replace(/<(?:Aside|Steps|Tabs|TabItem|FileTree|LinkCard|CardGrid|Card)[^>]*>/gi, '')
    .replace(/<\/(?:Aside|Steps|Tabs|TabItem|FileTree|LinkCard|CardGrid|Card)>/gi, '')
    // Remove code blocks (keep language tag for context)
    .replace(/```\w*\n[\s\S]*?```/g, '[code]')
    // Remove inline code backticks
    .replace(/`([^`]+)`/g, '$1')
    // Convert links to text
    .replace(/\[([^\]]+)\]\([^)]+\)/g, '$1')
    // Remove images
    .replace(/!\[.*?\]\(.*?\)/g, '')
    // Remove heading anchors
    .replace(/\[Section titled ".*?"\]/g, '')
    // Collapse whitespace
    .replace(/\n{3,}/g, '\n\n')
    .trim();
}

// ─── File path to URL ────────────────────────────────────────────────────────

function pathToUrl(relPath) {
  let url = relPath
    .replace(/\.mdx?$/, '')
    .replace(/\/index$/, '');
  return `/${url}/`;
}

// ─── Platform (dotnet or frontend) ───────────────────────────────────────────

function platform(relPath) {
  if (relPath.startsWith('dotnet/')) return 'dotnet';
  if (relPath.startsWith('frontend/')) return 'frontend';
  return 'general';
}

// ─── Walk directory ──────────────────────────────────────────────────────────

function walkDir(dir) {
  const files = [];
  for (const entry of readdirSync(dir)) {
    const full = join(dir, entry);
    const stat = statSync(full);
    if (stat.isDirectory()) {
      files.push(...walkDir(full));
    } else if (/\.mdx?$/.test(entry)) {
      files.push(full);
    }
  }
  return files;
}

// ─── Main ────────────────────────────────────────────────────────────────────

const files = walkDir(DOCS_ROOT);
const index = [];

for (const file of files) {
  const relPath = relative(DOCS_ROOT, file).replace(/\\/g, '/');
  const raw = readFileSync(file, 'utf-8');
  const { meta, content } = parseFrontmatter(raw);

  // Skip pages without a title
  const title = meta.title;
  if (!title) continue;

  const cleaned = cleanContent(content);
  // Skip very short pages (likely index/redirect pages)
  if (cleaned.length < 50) continue;

  index.push({
    title,
    description: meta.description || '',
    url: pathToUrl(relPath),
    category: categorize(`/${relPath}`),
    platform: platform(relPath),
    content: cleaned,
  });
}

// Sort by category then title for deterministic output
index.sort((a, b) => a.category.localeCompare(b.category) || a.title.localeCompare(b.title));

writeFileSync(OUTPUT, JSON.stringify(index, null, 2), 'utf-8');

const byCategory = {};
for (const entry of index) {
  byCategory[entry.category] = (byCategory[entry.category] || 0) + 1;
}

console.log(`✓ search-index.json generated: ${index.length} entries (${(JSON.stringify(index).length / 1024).toFixed(0)} KB)`);
console.log('  Categories:', Object.entries(byCategory).map(([k, v]) => `${k}: ${v}`).join(', '));
