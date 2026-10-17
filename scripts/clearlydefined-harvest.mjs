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
// POST /definitions is rate limited to 250 requests a minute, and it computes a definition on the
// spot for every coordinate it has not cached, so a large batch of never-seen versions outlives
// Cloudflare's gateway timeout (HTTP 502/524). Keep each request small.
const DEFINITIONS_BATCH = 20;
const MAX_ATTEMPTS = 4;

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
    pending.push(...(await unindexedBatch(coordinates.slice(i, i + DEFINITIONS_BATCH))));
  }
  return pending;
}

// A batch that keeps failing on a transient status is split in half, since one slow coordinate is
// enough to time the whole request out. A single coordinate that still cannot be answered counts as
// unindexed: requesting a harvest for something already harvested is a harmless no-op.
async function unindexedBatch(batch) {
  const definitions = await fetchDefinitions(batch);
  if (definitions) {
    return batch.filter((c) => !definitions[c.toLowerCase()]?.described?.tools?.length);
  }
  if (batch.length === 1) {
    console.log(`  no definition answer for ${batch[0]}, treating it as unindexed`);
    return batch;
  }
  const half = Math.ceil(batch.length / 2);
  return [...(await unindexedBatch(batch.slice(0, half))), ...(await unindexedBatch(batch.slice(half)))];
}

// Definitions keyed by lower-cased coordinates, or null when the service kept answering with a
// transient status (429 or 5xx). Any other failure is a real error and aborts the run.
async function fetchDefinitions(batch) {
  for (let attempt = 1; attempt <= MAX_ATTEMPTS; attempt++) {
    const response = await fetch(`${CLEARLY_DEFINED}/definitions`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(batch),
    });
    if (response.ok) {
      return Object.fromEntries(
        Object.entries(await response.json()).map(([key, value]) => [key.toLowerCase(), value]),
      );
    }
    if (response.status !== 429 && response.status < 500) {
      throw new Error(`POST /definitions -> ${response.status} ${response.statusText}`);
    }
    console.log(`  POST /definitions (${batch.length}) -> ${response.status}, attempt ${attempt}/${MAX_ATTEMPTS}`);
    if (attempt < MAX_ATTEMPTS) await new Promise((resolve) => setTimeout(resolve, 2 ** attempt * 1000));
  }
  return null;
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
