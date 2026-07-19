// Prints the release notes for the current shared NuGet version (version.props) by extracting
// that version's "## [x.y.z] - date" section from the root CHANGELOG.md — i.e. the section
// scripts/apply-nuget-version.mjs spliced in when the Version Packages PR was created.
//
// With --pending it instead lists the summaries of the not-yet-versioned .changeset/*.md files
// (skipping any already consumed into changesets pre mode via pre.json) — the notes for an interim
// beta build, whose changes have no CHANGELOG.md section yet.
//
// Used by the tagRelease/tagBeta CI jobs: `node scripts/release-notes.mjs [--pending] > notes.md`
// feeds `gh release create --notes-file`. Falls back to a generic pointer at CHANGELOG.md (still
// exit 0) so a missing section never blocks the release.
import { readFileSync, readdirSync, existsSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const repoRoot = join(dirname(fileURLToPath(import.meta.url)), "..");

if (process.argv.includes("--pending")) {
  const changesetDir = join(repoRoot, ".changeset");
  const preJsonPath = join(changesetDir, "pre.json");
  const consumed = new Set(
    existsSync(preJsonPath) ? JSON.parse(readFileSync(preJsonPath, "utf8")).changesets : [],
  );
  const entries = readdirSync(changesetDir)
    .filter((f) => f.endsWith(".md") && f !== "README.md" && !consumed.has(f.replace(/\.md$/, "")))
    .map((f) => {
      const raw = readFileSync(join(changesetDir, f), "utf8");
      // Frontmatter ("---\n<pkg>: <bump>\n---") followed by the summary; the frontmatter body is
      // empty for `changeset add --empty` files ("---\n---"), so the closing newline is optional.
      const fm = raw.match(/^---\r?\n([\s\S]*?)\r?\n?---\r?\n?([\s\S]*)$/);
      const bumps = (fm?.[1] ?? "")
        .split(/\r?\n/)
        .map((l) => l.trim())
        .filter(Boolean)
        .join(", ");
      const summary = (fm?.[2] ?? raw).trim();
      if (!summary) return null;
      return `- ${summary.replace(/\r?\n/g, "\n  ")}${bumps ? `\n  *(${bumps})*` : ""}`;
    })
    .filter(Boolean);
  process.stdout.write(
    "Interim beta build of the changes pending for the next release.\n\n" +
      (entries.length
        ? `### Pending changes\n\n${entries.join("\n")}\n`
        : "See the open Version Packages PR for details.\n"),
  );
  process.exit(0);
}

const versionProps = readFileSync(join(repoRoot, "version.props"), "utf8");
const prefix = versionProps.match(/<VersionPrefix>([^<]*)<\/VersionPrefix>/)?.[1]?.trim();
const suffix = versionProps.match(/<VersionSuffix>([^<]*)<\/VersionSuffix>/)?.[1]?.trim();
if (!prefix) {
  console.error("Could not read <VersionPrefix> from version.props.");
  process.exit(1);
}
const version = suffix ? `${prefix}-${suffix}` : prefix;

const changelog = readFileSync(join(repoRoot, "CHANGELOG.md"), "utf8");
const escaped = version.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
// Section shape: "## [x.y.z] - YYYY-MM-DD\n...body...\n" up to the next "## " heading (or the end
// of the file — `(?![\s\S])`, since `$` under the m flag would stop at the first line break).
const match = changelog.match(
  new RegExp(`^##\\s+\\[${escaped}\\][^\\n]*\\n([\\s\\S]*?)(?=\\n##\\s|(?![\\s\\S]))`, "m"),
);
const body = match?.[1]?.trim();

if (body) {
  process.stdout.write(`${body}\n`);
} else {
  console.error(`No CHANGELOG.md section found for ${version}; using fallback notes.`);
  process.stdout.write(
    `Automated release of \`${version}\`. See [CHANGELOG.md](https://github.com/bielu/Bielu.AspNetCore.AsyncApi/blob/main/CHANGELOG.md) for details.\n`,
  );
}
