## Table of contents

- [Getting started](#getting-started)
- [Test releases](#test-releases)
- [Webview Framework (CWF) management](#webview-framework-cwf-management)
- [Feature Flags](#feature-flags)
- [License](#license)

## Getting started

## Test releases

GitHub test builds use a tag that includes the commit SHA; the VSIX keeps a numeric `X.Y.Z` version only, which is the version in the tag.

| | Example |
|---|---------|
| Git tag | `v0.9.2-test.9c31e24` |
| VSIX `Identity` version | `0.9.2` |

The tagged commit does not need a manifest edit: the workflow sets the VSIX version (and `source.extension.cs`) from the tag. Merge `main` into the branch you want to ship first so the tagged commit includes the current release workflow.

Create and push a test tag from that commit:

```text
make tag-test-release BUMP=patch
```

`BUMP` is `patch`, `minor` or `major` (default `patch`) and is applied to the highest published stable release to get the test version. Or dry-run: `pwsh -File tag-test-release.ps1 -WhatIf`. Use `-NoPush` to create the tag locally only.

Prerequisites: the GitHub CLI (`gh`) installed and authenticated (`gh auth login`), and a clean git worktree.

The workflow validates that the tag matches `vX.Y.Z-test.<sha>`, that `<sha>` prefixes the tagged commit, and that the base version exceeds the previous stable release. It creates a **Pre-release** with the `.vsix` attached and never publishes to the Marketplace.

Production releases are described in [.github/RELEASING.md](.github/RELEASING.md).

Repeated test tags may reuse the same VSIX version; Visual Studio may not treat that as an upgrade between builds.

## Webview Framework (CWF) management

The Centralized Webviews Framework (CWF) used by the extension, which provides shared webview components across all supported IDEs, is maintained in a private repository.

**Note:** Access to the CWF repository requires appropriate permissions.

A normal build downloads the release pinned in `cwf.version` (at the repository root). Bumping the pin is a one-line commit to that file.

`make updatecwf` builds the sibling checkout at `../cs-webview` and copies it into the extension instead.

Limitations to be aware of:

- `CWF_VERSION` overrides the pin only when `.github/cwf.ps1` is run directly (for example `$env:CWF_VERSION = '2.5.9'; pwsh .github/cwf.ps1`). Make does not see environment overrides, so it will not re-run the download when only the variable changes.
- After `make updatecwf`, returning to the pinned release needs `.github/cwf.ps1` run directly, or `cs-cwf.zip` deleted so Make downloads it again.

## Feature Flags

This plugin supports simple feature flags that can be controlled at build time using environment variables. 
Feature flags allow you to enable or disable experimental functionality without modifying the source code.

### How feature flags work

Feature flags are defined in `Codescene.VSExtension.VS2022.csproj` using environment variables and compilation symbols, for example:

Example:
```
<EnableAceRefactoring Condition="'$(FEATURE_ACE)' == 'true'">true</EnableAceRefactoring>
<EnableAceRefactoring Condition="'$(FEATURE_ACE)' != 'true'">false</EnableAceRefactoring>
...
<!-- Add conditional compilation symbol for features -->
<PropertyGroup>
  <DefineConstants Condition="'$(EnableAceRefactoring)' == 'true'">$(DefineConstants);FEATURE_ACE</DefineConstants>
</PropertyGroup>
```

During the build, compilation symbols (using same names as env variables) are being created with appropriate value according to env variable.
They are then used in the codebase to conditionally include or exclude feature-specific code blocks, for example:

```
#if FEATURE_ACE
                    await AceUtils.CheckContainsRefactorableFunctionsAsync(result, code);
#endif
```

### Using feature flags when building the plugin locally

To test feature flags locally set the corresponding env variable before building the extension.
Also make sure to close the IDE before setting the env variable to ensure that the build process picks up the new value.


If a property is not provided, it defaults to false.

## License

See LICENSE file.
