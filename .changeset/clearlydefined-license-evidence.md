---
"bielu-aspnetcore-asyncapi": patch
---

Every package now ships its `LICENSE` file, and the files it produces carry the MIT license and the copyright notice: an `AssemblyMetadata("License", "SPDX-License-Identifier: MIT")` attribute in each assembly, a comment in each XML documentation file, a header in the packed MSBuild and `rd.xml` files, and a copyright line in each `PACKAGE.md`. License scanners such as ClearlyDefined can now find the license in the package files as well as in the nuspec.
