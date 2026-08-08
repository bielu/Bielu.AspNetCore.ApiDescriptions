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

// 1) Write the shared NuGet version into version.props. A prerelease version (e.g. from changesets
// pre mode: 1.0.1-preview.0) is split into <VersionPrefix>1.0.1</VersionPrefix> +
// <VersionSuffix>preview.0</VersionSuffix>; the .NET SDK recombines them at build/pack time. A
// stable version clears the suffix.
const dash = newVersion.indexOf("-");
const versionPrefix = dash === -1 ? newVersion : newVersion.slice(0, dash);
const versionSuffix = dash === -1 ? "" : newVersion.slice(dash + 1);

const versionProps = readFileSync(versionPropsPath, "utf8");
if (!/<VersionPrefix>[^<]*<\/VersionPrefix>/.test(versionProps)) {
  throw new Error("Could not find <VersionPrefix> in version.props to update.");
}
if (!/<VersionSuffix>[^<]*<\/VersionSuffix>/.test(versionProps)) {
  throw new Error("Could not find <VersionSuffix> in version.props to update.");
}
const updatedProps = versionProps
  .replace(/(<VersionPrefix>)[^<]*(<\/VersionPrefix>)/, `$1${versionPrefix}$2`)
  .replace(/(<VersionSuffix>)[^<]*(<\/VersionSuffix>)/, `$1${versionSuffix}$2`);
writeFileSync(versionPropsPath, updatedProps);
console.log(
  `version.props <VersionPrefix> -> ${versionPrefix}` +
    (versionSuffix ? `, <VersionSuffix> -> ${versionSuffix}` : ", <VersionSuffix> cleared"),
);

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

const rootChangelog = readFileSync(rootChangelogPath, "utf8");
const anchor = "## [Unreleased]";
const idx = rootChangelog.indexOf(anchor);
if (idx === -1) {
  throw new Error(`Could not find "${anchor}" anchor in CHANGELOG.md.`);
}
if (rootChangelog.includes(`## [${newVersion}]`)) {
  console.log(`CHANGELOG.md already has a section for ${newVersion}; skipping changelog fold.`);
  process.exit(0);
}

// The new version section goes immediately after the Unreleased block, i.e. before the next "## "
// heading that follows it.
const afterAnchor = idx + anchor.length;
const nextHeading = rootChangelog.indexOf("\n## ", afterAnchor);
const unreleasedEnd = nextHeading === -1 ? rootChangelog.length : nextHeading + 1;

// Drain the curated [Unreleased] block into this release rather than leaving it above the new
// section. Everything a contributor hand-wrote there has just shipped, so leaving it in place would
// keep advertising released work as unreleased — and any item that never got a changeset (the
// generated `body` above only covers those that did) would otherwise sit under [Unreleased] forever.
const EMPTY_UNRELEASED = "_Nothing yet._";
const curated = rootChangelog.slice(afterAnchor, unreleasedEnd).trim();
// Demote the curated block's own "### Added"/"### Fixed" headings so they nest under the
// "### Additional Changes" wrapper below instead of becoming siblings of it.
const carried =
  curated === EMPTY_UNRELEASED ? "" : curated.replace(/^### /gm, "#### ");

const entry =
  `## [${newVersion}] - ${today}\n\n${body}\n` +
  (carried
    ? "\n### Additional Changes\n\n" +
      "These shipped in this release but have no changeset, so they have no generated entry above.\n\n" +
      `${carried}\n`
    : "");

const updatedChangelog =
  rootChangelog.slice(0, afterAnchor) +
  `\n\n${EMPTY_UNRELEASED}\n\n` +
  entry +
  "\n" +
  rootChangelog.slice(unreleasedEnd);
writeFileSync(rootChangelogPath, updatedChangelog);
console.log(
  `CHANGELOG.md <- section for ${newVersion}` +
    (carried ? " (carried the [Unreleased] block into it)" : ""),
);
