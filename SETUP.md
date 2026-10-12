## Table of contents

- [Getting started](#getting-started)
- [Test releases](#test-releases)
- [Webview Framework (CWF) management](#webview-framework-cwf-management)
- [Feature Flags](#feature-flags)
- [License](#license)

## Getting started

## Test releases

GitHub test builds use a tag that includes the commit SHA; the VSIX keeps a numeric `X.Y.Z` version only.

| | Example |
|---|---------|
| Git tag | `v0.9.0-test.9c31e24` |
| VSIX `Identity` version | `0.9.0` |

Before tagging, set the manifest (and `source.extension.cs`) to the target `X.Y.Z` on the branch you want to ship. Merge `main` into that branch first so the tagged commit includes the current release workflow.

Create and push a test tag from that commit:

```text
make tag-test-release
```

Or dry-run: `pwsh -File tag-test-release.ps1 -WhatIf`. Use `-NoPush` to create the tag locally only.

CI validates that the tag matches `vX.Y.Z-test.<sha>`, that `<sha>` prefixes the tagged commit, and that `X.Y.Z` matches the VSIX manifest. The GitHub release is marked **Pre-release** and attaches the `.vsix`.

Production releases are unchanged: `release.ps1` and tag `vX.Y.Z`. Marketplace publish (`vsix-publish.yml`) uses the latest non–pre-release.

Repeated test tags may reuse the same VSIX version; Visual Studio may not treat that as an upgrade between builds.

## Webview Framework (CWF) management

The Centralized Webviews Framework (CWF) used by the extension, which provides shared webview components across all supported IDEs, is maintained in a private repository.

**Note:** Access to the CWF repository requires appropriate permissions.

A normal build downloads the latest GitHub release. `make updatecwf` builds the sibling checkout at `../cs-webview` and copies it into the extension instead.

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
