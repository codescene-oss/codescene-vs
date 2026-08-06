# CodeScene Visual Studio Extension Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.7.7] - 2026-08-06

### Added
- add CPU-based throttling to GitChangeLister periodic scan
- remove stale files from Code Health Monitor after GitChangeLister runs
- add CPU monitoring to throttle CLI commands during high load
- cache baseline commit to eliminate redundant git merge-base calls
- cache `GetDefaultBranch` calls to reduce redundant git operations
- skip `GitChangeLister` and `GitChangeObserver` on default branch
- watch global gitignore (`core.excludesfile`) for cache invalidation (#306)
- batch git ignore checks to reduce Repository overhead
- skip GitChangeLister when analyses are running
- dynamically increase GitChangeLister period based on execution time (#303)
- skip periodic GitChangeLister when VS window is not focused

### Fixed
- resolve issues from `CpuSampler` refactoring code review
- filter invalid paths before cache cleanup in GitChangeLister
- check CPU after acquiring semaphore, not before
- catch exceptions in `TakeSample` to ensure graceful fallback
- align CPU throttling with codescene-vscode parity
- use system-wide CPU metrics instead of process-specific
- handle exceptions in CPU monitoring gracefully in `CpuUsageThrottler`
- wait for CPU availability before acquiring semaphore in `CliExecutor`
- synchronize access to `_previousSnapshot` in `CpuMonitor`
- prevent concurrent processing in `FileChangeEventProcessor`
- compute baseline commit once per batch in `OnGitChangeListerFilesDetected`
- thread `baselineCommit` through `GetChangedFilesVsBaselineAsync`
- forward `baselineCommit` to inner reviewer in `ComputeAndCacheDeltaAsync`
- add missing `baselineCommit` parameter to Moq setup expressions
- thread `baselineCommit` through internal delta computation methods
- compute baseline commit once per batch instead of per-event
- add missing `baselineCommit` parameter to `DeltaAsync` Moq expressions
- use `baselineCommit` parameter in `CodeReviewer.GetOrComputeBaselineRawScoreAsync`
- synchronize `DefaultBranchGate` cache access

### Changed
- remove default branch skipping from GitChangeLister
- assert calculated CPU usage in `TakeSampleSync_ReturnsExpectedCpuUsage`
- extract common CPU sampling logic into `CpuSampler`
- add coverage for `CpuMonitor.TakeSampleSync`
- add coverage for stale file removal preservation logic
- consolidate duplicate tests using DataRow
- add comprehensive CPU monitoring threshold and edge case tests
- add edge case coverage for `CpuMonitor`
- cover outer exception handler in `FileChangeEventProcessor`
- reduce cyclomatic complexity in git change detection
- consolidate baseline commit resolution into `IGitService`
- remove redundant FilterIgnoredFiles call in CollectFilesFromRepoState
- add coverage for ignored files not counting toward threshold


## [0.7.6] - 2026-06-09
### Fixed
- use configured baseline exclusively for merge-base resolution (#300)

## [0.7.5] - 2026-06-03
### Added
- improve main branch detection using refs/remotes/origin/HEAD (#279)
### Fixed
- hide ACE ad in code smell docs when no auth token (#299)
- Fixed some unhandled exceptions (#298) (#297) (#296)
- Misc security improvements

## [0.7.4] - 2026-05-12
### Added
- add editor-version to telemetry payload (#277)

## [0.7.3] - 2026-04-22
### Fixed
- solution switch error + improved error logs (#276)
- delta baseline selection for stacked branches (#275)

## [0.7.2] - 2026-04-10
### Fixed
- code-health-rules.json support (#273)

## [0.7.1] - 2026-04-01
### Fixed
- duplicated cli commands on same file (#270)
- editor suggestion bugfix (#269)
- Exception cleanup (#267)
- observer start on solution/project open (#264)
- cleanup status bar messages and logging (#265)
- improved exception management and logging (#263)
  
## [0.7.0] - 2026-03-16
### Fixed
- improved text contrast in Code Health Monitor
- increase document debounce, lower polling (#260)
- stabilize review and delta polling on branch/solution switch (#261) (#259) (#258) (#257) (#255) (#254) (#249) (#247) (#246) (#238)
- refactored caching infrastructure (#251) (#240) (#242) (#244) (#233) (#231)
- margin text for unreviewable file (#252)
- open folder event triggers (#245)
- error list formatting (#243)
- code health editor margin multiple windows (#215)
### Added
- VS2026 theme support (#213)
- git change observer and file change tracking ()
- CodeScene ACE Refactoring (#239) (#214) (#200) (#110) (#99) (#97) (#95) (#88)
- .gitignore change tracker (#253) (#237) (#109)
- solution workspace support (#250)
- Integrate GitChangeLister with periodic scanning into GitChangeObserver (#203)
- editor margin shows delta (#206)
- add CLI request performance telemetry (ace, review, delta)
- error telemetry (#113)
- Set up Stylecop
- Add CodeRabbit
- Add CLAUDE.md
### Changed
- Revamp Makefile (#105)

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




























