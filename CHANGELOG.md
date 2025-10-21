# CodeScene Visual Studio Extension Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.3.0] - 2025-10-21
### Added
- Bump CLI version to 1.0.14
- Add Code Health Monitor to Freemium

## [0.2.7] - 2025-10-07
### Fixed
- Update extension startup to wait for IDE initialization finish

## [0.2.6] - 2025-09-24
### Changed
- Update ACE wording

## [0.2.5] - 2025-09-18
### Changed
- Improve code health review time by 50% (CLI version 1.0.8)

## [0.2.4] - 2025-09-08
### Fixed
- Add missing tags
### Changed
- Prepare asset files

## [0.2.3] - 2025-09-05
### Fixed
- Corrected publishing manifest and asset handling for Visual Studio Marketplace.

## [0.2.2] - 2025-09-05
### Changed
- Fix for overview gif

## [0.2.1] - 2025-09-04
### Fixed
- VSIX Publish pipeline fixed

## [0.2.0] - 2025-09-04
### Changed
- Updated release script
- Refactor and clean up code
- Removed unnecessary .vscode folder

## [0.1.4] - 2025-08-21
### Changed
- Webview message handling for file focus.
- Review timeout from 10s to 60s.
### Fixed
- Code smell finding tooltip coloring on light themes.

## [0.1.3] - 2025-08-18
### Added
- Introduced a Terms & Policies acceptance step. Users must review and accept before using the extension's analysis capabilities.

## [0.1.2] - 2025-08-14
### Fixed
- VS2022 compatibility.
### Changed
- Updated webview to v1.2.0.
- Bumped cli version to 1.0.5.

## [0.1.1] - 2025-08-12
### Fixed
- Hovering color on Home links
### Changed
- Updated webview to v1.1.1.

## [0.1.0] - 2025-08-11
### Fixed
- Extension installation issues on lower VS2022 versions.
- Device id generation algorithm
- Whitelisted supporthub.codescene domain
### Changed
- Updated webview to v1.1.0.
- Bumped cli version to 1.0.3.

## [0.0.6] - 2025-07-07
### Changed
- Updated webview to v1.0.1.
- Added more styling support to webviews.

## [0.0.5] - 2025-07-04
### Added
- Optional extension usage telemetry.
### Changed
- Updated webview to v1.0.0.

## [0.0.4] - 2025-07-01
### Added
- Option to enable and disable showing CodeScene debug logs for more detailed output.

## [0.0.3] - 2025-06-30
### Added
- Review analysis timeout.

## [0.0.2] - 2025-06-30
### Added
- Documentation for code smells.
- Code Health visibility in a separate editor margin.
### Changed
- Review flow optimization.

## [0.0.1] - 2025-06-10
### Added
- Code Health Review and diagnostics with squiggly lines.
- Visibility of diagnostics in Error List and detailed hover information.
- Initial CodeScene plugin settings.
- Documentation for code smells.
- Code Health visibility in a separate editor margin.
