# Contributing to Bielu.AspNetCore.AsyncApi

Thank you for your interest in contributing to Bielu.AspNetCore.AsyncApi! We welcome contributions from the community and are grateful for your help in making this project better.

## Table of Contents

- [Getting Started](#getting-started)
- [Prerequisites](#prerequisites)
- [Development Setup](#development-setup)
- [Building the Project](#building-the-project)
- [Running Tests](#running-tests)
- [Code Style](#code-style)
- [Pull Request Process](#pull-request-process)
- [Release Process](#release-process)
- [Questions and Support](#questions-and-support)

## Getting Started

1. Fork the repository on GitHub
2. Clone your fork locally:
   ```bash
   git clone https://github.com/YOUR_USERNAME/Bielu.AspNetCore.AsyncApi.git
   cd Bielu.AspNetCore.AsyncApi
   ```
3. Add the upstream repository as a remote:
   ```bash
   git remote add upstream https://github.com/bielu/Bielu.AspNetCore.AsyncApi.git
   ```
4. Create a branch for your changes:
   ```bash
   git checkout -b feature/your-feature-name
   ```

## Prerequisites

Before you begin, ensure you have the following installed:

- **.NET SDK 10.0** or later (the project targets .NET 10)
- **Node.js LTS** (required for building the UI components)
- **npm** (comes with Node.js)
- **Git**
- An IDE such as Visual Studio, Visual Studio Code, or JetBrains Rider

## Development Setup

### 1. Restore NuGet Packages

```bash
dotnet restore
```

### 2. Install Node.js Dependencies

The UI components require Node.js packages. Navigate to the UI directory and install dependencies:

```bash
cd src/Bielu.AspNetCore.AsyncApi.UI
npm install
cd ../..
```

### 3. Build the Solution

```bash
dotnet build
```

## Building the Project

### Local Build

Build the entire solution:

```bash
dotnet build src/Bielu.AspNetCore.AsyncApi.slnx
```

Build a specific project:

```bash
dotnet build ./src/Bielu.AspNetCore.AsyncApi/Bielu.AspNetCore.AsyncApi.csproj
```

### Create NuGet Packages Locally

```bash
dotnet pack ./src/Bielu.AspNetCore.AsyncApi/Bielu.AspNetCore.AsyncApi.csproj --configuration Release --output ./build
dotnet pack ./src/Bielu.AspNetCore.AsyncApi.Attributes/Bielu.AspNetCore.AsyncApi.Attributes.csproj --configuration Release --output ./build
dotnet pack ./src/Bielu.AspNetCore.AsyncApi.UI/Bielu.AspNetCore.AsyncApi.UI.csproj --configuration Release --output ./build
```

### Running the Example Application

```bash
cd src/examples/StreetlightsAPI
dotnet run
```

Then visit:
- AsyncAPI JSON: http://localhost:5000/asyncapi/asyncapi.json
- AsyncAPI UI: http://localhost:5000/asyncapi/ui/

## Running Tests

### Run All Tests

```bash
dotnet test
```

### Run Specific Test Projects

```bash
# Core tests
dotnet test ./test/Bielu.AspNetCore.AsyncApi.Tests/Bielu.AspNetCore.AsyncApi.Tests.csproj

# CLI tool tests
dotnet test ./test/Bielu.AspNetCore.AsyncApi.Cli.Tests/Bielu.AspNetCore.AsyncApi.Cli.Tests.csproj
```

### Run Tests with Code Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Code Style

This project uses consistent code formatting enforced by `dotnet format`.

### Check Code Formatting

```bash
dotnet format --verify-no-changes src/Bielu.AspNetCore.AsyncApi.slnx
```

### Apply Code Formatting

```bash
dotnet format src/Bielu.AspNetCore.AsyncApi.slnx
```

### General Guidelines

- Follow existing code conventions in the project
- Use meaningful variable and method names
- Add XML documentation comments for public APIs
- Keep methods focused and concise
- Write unit tests for new functionality

## Pull Request Process

1. **Ensure your code builds** without errors:
   ```bash
   dotnet build --configuration Debug
   ```

2. **Run the tests** and ensure they pass:
   ```bash
   dotnet test
   ```

3. **Check code formatting**:
   ```bash
   dotnet format --verify-no-changes src/Bielu.AspNetCore.AsyncApi.slnx
   ```

4. **Update documentation** if you've changed public APIs

5. **Commit your changes** with a clear, descriptive commit message

6. **Push to your fork** and create a pull request against the `main` branch

7. **Add a changeset** describing your change (see [Changesets](#changesets) below)

8. **Fill out the PR template** with a description of your changes

9. **Wait for CI checks** to pass - the following checks run automatically:
   - Build verification
   - Code formatting check
   - Unit tests

## Changesets

User-facing changes are recorded with [changesets](https://github.com/changesets/changesets) so the
version bump and changelog are generated automatically instead of edited by hand. From the repository
root:

```bash
npx changeset
```

Pick the affected package(s) and a bump level (patch/minor/major), then write a short, user-facing
summary. All NuGet packages in this repo share one version, so for any NuGet change select the single
`bielu-aspnetcore-asyncapi` entry — one bump covers the whole suite. Chore-only PRs (CI, formatting,
tests) can record an empty changeset with `npx changeset add --empty`. See
[`.changeset/README.md`](./.changeset/README.md) for details.

## Release Process

Releases are managed through GitHub Actions and follow semantic versioning. The shared version is
computed from the changesets accumulated on `main`: `npm run version` runs `changeset version` and
then `scripts/apply-nuget-version.mjs`, which writes the new version into `version.props` and folds
the generated notes into [`CHANGELOG.md`](./CHANGELOG.md). Maintainers do not edit `version.props` or
the changelog by hand.

### Version File

The project version is managed centrally in the `version.props` file in the repository root (written
by the changeset `version` step, above):

```xml
<Project>
    <PropertyGroup>
        <VersionPrefix>1.0.0</VersionPrefix>
        <VersionSuffix></VersionSuffix>
    </PropertyGroup>
</Project>
```

- **VersionPrefix**: The base version number (MAJOR.MINOR.PATCH)
- **VersionSuffix**: Optional suffix for pre-release versions (e.g., `beta`, `rc1`)

To update the version, modify this file before creating a release tag.

### CI Pipeline

The CI workflow ([.github/workflows/buildAndPublishPackage.yml](./.github/workflows/buildAndPublishPackage.yml)) runs on:
- Every push to `main`
- Every pull request

It performs:
- Building the solution
- Code format verification
- Running unit tests

### Package Publishing

The build and publish workflow ([.github/workflows/buildAndPublishPackage.yml](./.github/workflows/buildAndPublishPackage.yml)):

- **Pull Requests**: Packages are built with a `-pr` suffix (not published)
- **Pushes to main**: Packages are built with a `-beta` suffix and published to NuGet
- **Releases**: Packages are built without suffix and published to NuGet

### Creating a Release

Releases are created by the repository maintainers:

1. Ensure all changes are merged to `main`
2. Create a tag with semantic versioning format: `v*.*.*` (e.g., `v1.0.0`)
3. Push the tag to trigger the release workflow:
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```
4. The release workflow will:
   - Build the packages
   - Push to NuGet.org

### Version Guidelines

- Use **semantic versioning** (MAJOR.MINOR.PATCH)
- Increment MAJOR for breaking changes
- Increment MINOR for new features (backward compatible)
- Increment PATCH for bug fixes

## Questions and Support

If you have questions or need help:

1. **Check existing issues** for similar questions
2. **Open a new issue** with a clear description
3. Join the [AsyncAPI community slack](https://asyncapi.com/slack-invite) for real-time discussions

---

Thank you for contributing to Bielu.AspNetCore.AsyncApi! 🚀

