---
---

chore: pause stable releases by entering changesets pre mode (`preview` tag — every Version Packages PR now cuts `x.y.z-preview.N` until the upstream Scalar plugins PR merges and `changeset pre exit` is run), and create GitHub releases automatically: versioned releases get their CHANGELOG.md section as release notes, and interim beta builds are published as GitHub prereleases listing the pending changesets
