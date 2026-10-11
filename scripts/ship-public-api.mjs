// Marks the tracked public API as shipped: moves every entry of each src/*/PublicAPI.Unshipped.txt
// into the PublicAPI.Shipped.txt next to it, which is what Microsoft.CodeAnalysis.PublicApiAnalyzers
// expects once a version carrying those APIs has been released.
//
// Run automatically by `npm run version` (i.e. right after `changeset version`), so the move lands in
// the "Version Packages" PR together with the version bump it belongs to. Idempotent: re-running with
// nothing unshipped is a no-op.
//
// Only a stable version ships an API. A prerelease (changesets pre mode: 1.2.0-preview.0) leaves the
// files alone, because an API that only appeared in a preview may still change before the release.
import { readFileSync, writeFileSync, readdirSync, existsSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join, relative } from "node:path";

const repoRoot = join(dirname(fileURLToPath(import.meta.url)), "..");
const srcDir = join(repoRoot, "src");
const nugetPkgDir = join(repoRoot, "build", "changeset", "nuget-suite");

const HEADER = "#nullable enable";
const REMOVED_PREFIX = "*REMOVED*";
const BOM = "﻿";

const version = JSON.parse(readFileSync(join(nugetPkgDir, "package.json"), "utf8")).version;
if (version.includes("-")) {
  console.log(`${version} is a prerelease; leaving the public API files unshipped.`);
  process.exit(0);
}

// Splits a PublicAPI file into its parts, remembering how it was encoded so it is written back the
// same way (the repository's .editorconfig asks for CRLF, and the analyzer reads either).
function read(path) {
  const raw = readFileSync(path, "utf8");
  const hasBom = raw.startsWith(BOM);
  const text = hasBom ? raw.slice(1) : raw;
  const eol = text.includes("\r\n") ? "\r\n" : "\n";
  const lines = text.split(/\r?\n/).filter((line) => line.trim() !== "");
  return {
    hasBom,
    eol,
    hasHeader: lines.includes(HEADER),
    entries: lines.filter((line) => line !== HEADER),
  };
}

function write(path, file, entries) {
  const lines = file.hasHeader ? [HEADER, ...entries] : entries;
  const text = lines.length > 0 ? lines.join(file.eol) + file.eol : "";
  writeFileSync(path, (file.hasBom ? BOM : "") + text);
}

let shippedProjects = 0;
for (const project of readdirSync(srcDir, { withFileTypes: true })) {
  if (!project.isDirectory()) {
    continue;
  }

  const unshippedPath = join(srcDir, project.name, "PublicAPI.Unshipped.txt");
  const shippedPath = join(srcDir, project.name, "PublicAPI.Shipped.txt");
  if (!existsSync(unshippedPath)) {
    continue;
  }
  if (!existsSync(shippedPath)) {
    throw new Error(`${relative(repoRoot, unshippedPath)} has no PublicAPI.Shipped.txt next to it.`);
  }

  const unshipped = read(unshippedPath);
  if (unshipped.entries.length === 0) {
    continue;
  }

  const shipped = read(shippedPath);
  // A "*REMOVED*" entry records that a shipped API went away (or changed signature, when it is
  // paired with the new signature). Shipping it means dropping the old line from the shipped file.
  const removed = new Set(
    unshipped.entries
      .filter((entry) => entry.startsWith(REMOVED_PREFIX))
      .map((entry) => entry.slice(REMOVED_PREFIX.length)),
  );
  const added = unshipped.entries.filter((entry) => !entry.startsWith(REMOVED_PREFIX));

  const kept = shipped.entries.filter((entry) => !removed.has(entry));
  const known = new Set(kept);
  const fresh = [...new Set(added)].filter((entry) => !known.has(entry)).sort();

  write(shippedPath, shipped, [...kept, ...fresh]);
  write(unshippedPath, unshipped, []);
  shippedProjects++;
  console.log(
    `${project.name}: shipped ${fresh.length} API entr${fresh.length === 1 ? "y" : "ies"}` +
      (removed.size > 0 ? `, removed ${shipped.entries.length - kept.length}` : ""),
  );
}

if (shippedProjects === 0) {
  console.log("No unshipped public API entries; nothing to ship.");
}
