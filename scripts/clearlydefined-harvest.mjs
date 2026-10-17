// Asks ClearlyDefined (https://clearlydefined.io) to harvest every stable version of this
// repository's NuGet packages that it has not indexed yet.
//
// ClearlyDefined does not crawl nuget.org on its own: a package version only gets a definition
// (license, source location, attribution) after someone requests a harvest for it. This script is
// the "someone" — it is idempotent, so the scheduled workflow (.github/workflows/clearlyDefined.yml)
// simply re-runs it daily and picks up whatever has appeared on nuget.org since, including versions
// that were still in NuGet's validation queue on the previous run.
//
//   node scripts/clearlydefined-harvest.mjs [--dry-run] [--include-prerelease]
//
// Package ids come from the packable projects directly under src/ (<PackageId>, else the project
// name), so a new package is covered without touching this file. Prereleases are skipped by
// default: every push to main publishes a beta of all packages, and nobody audits those.
import { readFileSync, readdirSync, existsSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const repoRoot = join(dirname(fileURLToPath(import.meta.url)), "..");
const dryRun = process.argv.includes("--dry-run");
const includePrerelease = process.argv.includes("--include-prerelease");

const NUGET_FLAT = "https://api.nuget.org/v3-flatcontainer";
const CLEARLY_DEFINED = "https://api.clearlydefined.io";
// POST /harvest rejects more than 1000 entries per request.
const HARVEST_BATCH = 500;
// POST /definitions is rate limited to 250 requests a minute; keep each one small and fast.
const DEFINITIONS_BATCH = 100;

function packageIds() {
  const srcDir = join(repoRoot, "src");
  return readdirSync(srcDir, { withFileTypes: true })
    .filter((d) => d.isDirectory())
    .map((d) => join(srcDir, d.name, `${d.name}.csproj`))
    .filter(existsSync)
    .map((path) => {
      const csproj = readFileSync(path, "utf8");
      if (/<IsPackable>\s*false\s*<\/IsPackable>/i.test(csproj)) return null;
      return csproj.match(/<PackageId>\s*([^<\s]+)\s*<\/PackageId>/)?.[1]
        ?? path.match(/([^\\/]+)\.csproj$/)[1];
    })
    .filter(Boolean)
    .sort();
}

async function getJson(url) {
  const response = await fetch(url);
  if (response.status === 404) return null;
  if (!response.ok) throw new Error(`GET ${url} -> ${response.status} ${response.statusText}`);
  return response.json();
}

// Versions nuget.org serves for the id; null when the package has never been published. A version
// still being validated is not listed yet, which is exactly when it should be left for a later run.
async function publishedVersions(id) {
  const index = await getJson(`${NUGET_FLAT}/${id.toLowerCase()}/index.json`);
  if (!index) return null;
  return index.versions.filter((v) => includePrerelease || !v.includes("-"));
}

// Coordinates ClearlyDefined has no tool output for yet. Asked through the batch definitions
// endpoint: the per-package harvest listing (GET /harvest/...?form=list) scans by name prefix and
// regularly times out behind Cloudflare (HTTP 524), whereas this answers in well under a second.
// An unharvested coordinate still comes back, as a placeholder definition without described.tools.
async function unindexed(coordinates) {
  const pending = [];
  for (let i = 0; i < coordinates.length; i += DEFINITIONS_BATCH) {
    const batch = coordinates.slice(i, i + DEFINITIONS_BATCH);
    const response = await fetch(`${CLEARLY_DEFINED}/definitions`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(batch),
    });
    if (!response.ok) throw new Error(`POST /definitions -> ${response.status} ${response.statusText}`);
    const definitions = Object.fromEntries(
      Object.entries(await response.json()).map(([key, value]) => [key.toLowerCase(), value]),
    );
    pending.push(...batch.filter((c) => !definitions[c.toLowerCase()]?.described?.tools?.length));
  }
  return pending;
}

const published = [];
for (const id of packageIds()) {
  const versions = await publishedVersions(id);
  if (!versions) {
    console.log(`${id}: not on nuget.org, skipped`);
    continue;
  }
  published.push(...versions.map((v) => `nuget/nuget/-/${id}/${v}`));
}

const missing = await unindexed(published);
console.log(`${published.length - missing.length}/${published.length} published versions indexed.`);
for (const coordinates of missing) console.log(`  missing: ${coordinates}`);

if (missing.length === 0) {
  console.log("Nothing to harvest.");
} else if (dryRun) {
  console.log(`Dry run: would request ${missing.length} harvest(s).`);
} else {
  for (let i = 0; i < missing.length; i += HARVEST_BATCH) {
    const batch = missing.slice(i, i + HARVEST_BATCH);
    const response = await fetch(`${CLEARLY_DEFINED}/harvest`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(batch.map((coordinates) => ({ tool: "package", coordinates }))),
    });
    if (!response.ok) {
      throw new Error(`POST /harvest -> ${response.status} ${response.statusText}: ${await response.text()}`);
    }
  }
  console.log(`Requested ${missing.length} harvest(s).`);
}
