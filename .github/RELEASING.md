# Releasing

## Stable releases

Run the existing interactive release flow from a clean `main` branch:

```powershell
pwsh ./release.ps1
```

Choose `patch`, `minor`, or `major`. The script updates the VSIX manifest, generated version source, and changelog, creates `vX.Y.Z`, then pushes the commit and tag. The tag workflow builds the VSIX and creates a normal GitHub release.

The manual `Publish latest VSIX release` workflow publishes the latest stable GitHub release to the Visual Studio Marketplace.

## Test releases

Test releases do not change committed package metadata.

Create a patch test tag:

```powershell
make test-release
```

Create a minor or major test tag:

```powershell
make test-release BUMP=minor
make test-release BUMP=major
```

The command requires a clean worktree and creates an annotated tag without pushing it. Push the exact tag using the command printed by the script.

A test tag has the form `vX.Y.Z-test.N`, where `N` is the tagged commit count. Visual Studio requires numeric VSIX versions, so the corresponding package version is `X.Y.Z.N`. For example, `v0.9.0-test.325` produces a VSIX whose installed version is `0.9.0.325`.

The tag workflow validates the tag, injects the numeric version into its temporary checkout, builds `codescene-vs-vX.Y.Z-test.N.vsix`, and creates a GitHub prerelease. Test releases never become the latest GitHub release and are never selected by the Marketplace publishing workflow.

Visual Studio considers `X.Y.Z.N` newer than stable `X.Y.Z`. Uninstall the test build before installing the final stable release with the same `X.Y.Z` version. The tag helper rejects revisions above `65534`, which is the maximum assembly-version component.

## Release notes

GitHub generates release notes using these comparison points:

- Stable release: the previous reachable stable tag.
- First test for a base version: the latest reachable stable tag.
- Later test for the same base version: the previous valid same-base test tag.

Each test prerelease also links to the cumulative changes from the latest stable tag.

## Install and verify a test build

1. Download the VSIX from the GitHub prerelease.
2. Close Visual Studio.
3. Run the downloaded VSIX and complete the VSIX Installer flow.
4. Open Visual Studio and find CodeScene under `Extensions > Manage Extensions > Installed`.
5. Confirm the displayed version is `X.Y.Z.N` from the test tag.

Test builds must be installed from GitHub. They are not published to the Visual Studio Marketplace.
