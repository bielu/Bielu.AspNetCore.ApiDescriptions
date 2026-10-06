# Supported .NET versions

This policy applies to the AsyncAPI, Arazzo, Overlay, and shared runtime libraries in this repository.
Support for each .NET version ends when Microsoft ends support for that version. Use the latest servicing patch
of a supported runtime.

Last reviewed: 14 September 2026. Planned entries below describe future work, not support available in a released
package.

| .NET version | Target framework | Release track | Package support | Microsoft end of support | Target removal |
| --- | --- | --- | --- | --- | --- |
| .NET 10 | `net10.0` | LTS | Supported by the current runtime libraries | **14 November 2028** | Major package release scheduled for this EOL |
| .NET 11 | `net11.0` | STS | Planned for the next minor release; prerelease validation first | Final-release EOL to be confirmed by Microsoft | Major package release scheduled for its official EOL |
| .NET 12 | `net12.0` | Expected LTS under Microsoft's release cadence | Planned future support; not implemented | To be confirmed by Microsoft | Major package release scheduled for its official EOL |
| .NET 9 and earlier | No runtime-library target | Varies | Not supported by this package family | Not applicable to this package policy | Not applicable |

The .NET 10 date comes from [Microsoft's official .NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).
Microsoft's current cadence assigns even-numbered releases to LTS and odd-numbered releases to STS; the .NET 12 track
above is an expectation based on that cadence. [Microsoft lifecycle FAQ](https://learn.microsoft.com/en-us/lifecycle/faq/dotnet-core)

.NET 11 is currently at RC1. Its short RC support window is separate from the final .NET 11 lifecycle and must not be
used as the target-removal date. Record final .NET 11 and .NET 12 EOL dates here when Microsoft publishes them.
[Microsoft prerelease support dates](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core#go-live-releases)

## Adding supported versions

New .NET support is a backward-compatible feature delivered in a minor package release. The planned .NET 11 release
will contain both `net10.0` and `net11.0` assets in each runtime-library package, preserving existing .NET 10 APIs and
behavior. Consumers reference the same package ID and select their application's target framework.

Multi-targeting is not limited to two frameworks. When .NET 12 support is added, retain .NET 10 and .NET 11 if they
have not reached EOL, producing `net10.0;net11.0;net12.0`. Apply the same rule to later releases. Adding a newer target
does not retire an older supported target.

Mark a version as supported only after its packaged libraries, runtime tests, documented tooling, and representative
applications pass validation. Until then, keep it marked planned or prerelease. Compiler analyzers and source
generators retain their compiler-compatible `netstandard2.0` target; this does not extend runtime-library support to
older .NET versions.

## End of support and removal

Maintain each supported target in the actively maintained package line until its official Microsoft EOL date.
On that date, support ends, including compatibility fixes and security servicing for that target. Apply this policy
equally to .NET 10, .NET 11, .NET 12, and subsequent versions.

Schedule removal of an EOL target in a **major package release at EOL**. Removing a target breaks consumers of that
framework, so it must never happen in a patch or minor release. If the major release ships after the EOL date, support
still ends on the published date and the first subsequent major release removes the target. Announce the removal
release and the last package version containing the target in these docs and the release notes.

Previously published packages remain available and retain their original target assets. Reaching EOL does not delete
or modify those packages, and their availability does not imply continued support. Upgrade the application's .NET
target before adopting a package major version that removes it. This policy applies to the actively maintained
package line; it does not promise maintenance of every historical package major version.

For .NET 10, schedule the removal major for **14 November 2028**. Use Microsoft's published final-release dates to
schedule the equivalent work for .NET 11, .NET 12, and later versions. If Microsoft changes an EOL date, update this
matrix and the removal schedule to match the official policy.

## Release maintenance

For each framework addition or retirement, update this matrix, central target-framework configuration, dependency
conditions, CI coverage, templates, CLI/MSBuild compatibility checks, and examples together. Retain every supported
target when adding a new one, and remove only targets whose official EOL has been reached.

The SDK needed to build this repository may be newer than an application's runtime. Consumers using the .NET 10
package assets must continue to work with the .NET 10 SDK and runtime throughout its supported lifecycle.
