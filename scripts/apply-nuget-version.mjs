// Projects the changeset-computed version of the placeholder `bielu-aspnetcore-asyncapi` package
// onto the shared NuGet version, and folds its generated changelog into the root CHANGELOG.md.
//
// Run automatically by `npm run version` (i.e. right after `changeset version`). Idempotent:
// re-running with no version change is a no-op.
import { readFileSync, writeFileSync, existsSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const repoRoot = join(dirname(fileURLToPath(import.meta.url)), "..");
const nugetPkgDir = join(repoRoot, "build", "changeset", "nuget-suite");
const versionPropsPath = join(repoRoot, "version.props");
const rootChangelogPath = join(repoRoot, "CHANGELOG.md");

const newVersion = JSON.parse(
  readFileSync(join(nugetPkgDir, "package.json"), "utf8"),
).version;

// 1) Write the shared NuGet version into version.props <VersionPrefix>.
const versionProps = readFileSync(versionPropsPath, "utf8");
const updatedProps = versionProps.replace(
  /(<VersionPrefix>)([^<]*)(<\/VersionPrefix>)/,
  `$1${newVersion}$3`,
);
if (updatedProps === versionProps && !versionProps.includes(`<VersionPrefix>${newVersion}<`)) {
  throw new Error("Could not find <VersionPrefix> in version.props to update.");
}
writeFileSync(versionPropsPath, updatedProps);
console.log(`version.props <VersionPrefix> -> ${newVersion}`);

// 2) Fold the changeset-generated section into the curated root CHANGELOG.md.
const nugetChangelogPath = join(nugetPkgDir, "CHANGELOG.md");
if (!existsSync(nugetChangelogPath)) {
  console.log("No generated nuget-suite CHANGELOG.md yet; skipping changelog fold.");
  process.exit(0);
}

const nugetChangelog = readFileSync(nugetChangelogPath, "utf8");
// The generated file looks like:  # bielu-aspnetcore-asyncapi\n\n## X.Y.Z\n\n### Minor Changes\n...
// Grab the body of the top-most "## X.Y.Z" section for the version we just wrote.
const sectionRe = new RegExp(
  `##\\s+${newVersion.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}\\s*\\n([\\s\\S]*?)(?=\\n##\\s+\\d|$)`,
);
const match = nugetChangelog.match(sectionRe);
const body = (match ? match[1] : "").trim();
if (!body) {
  console.log(`No generated changelog body for ${newVersion}; skipping changelog fold.`);
  process.exit(0);
}

const today = new Date().toISOString().slice(0, 10);
const entry = `## [${newVersion}] - ${today}\n\n${body}\n`;

const rootChangelog = readFileSync(rootChangelogPath, "utf8");
const anchor = "## [Unreleased]";
const idx = rootChangelog.indexOf(anchor);
if (idx === -1) {
  throw new Error(`Could not find "${anchor}" anchor in CHANGELOG.md.`);
}
// Insert the new version section immediately after the end of the Unreleased block
// (i.e. before the next "## " heading that follows it).
const afterAnchor = idx + anchor.length;
const nextHeading = rootChangelog.indexOf("\n## ", afterAnchor);
const insertAt = nextHeading === -1 ? rootChangelog.length : nextHeading + 1;
const updatedChangelog =
  rootChangelog.slice(0, insertAt) + entry + "\n" + rootChangelog.slice(insertAt);
writeFileSync(rootChangelogPath, updatedChangelog);
console.log(`CHANGELOG.md <- section for ${newVersion}`);
