# release-pipeline Specification

## Purpose
Ship tested builds automatically and keep installed apps up to date.

## Requirements

### Requirement: Pull request validation
Every pull request SHALL build the solution and run all tests on Windows.

#### Scenario: Failing test blocks merge
- **WHEN** a test fails in a pull request
- **THEN** the CI check fails

### Requirement: Nightly pre-release
A scheduled workflow SHALL build and test `develop` daily and, when it changed in the last 24 hours, publish a Velopack pre-release `vX.Y.Z-nightly.<run_number>` on GitHub Releases.

#### Scenario: Quiet day
- **WHEN** `develop` has no new commits in 24 hours
- **THEN** no nightly release is created

### Requirement: Stable release
Pushing a `vMAJOR.MINOR.PATCH` tag SHALL validate that the tag is strict SemVer and points to a commit on `main`, build and test, sign the binaries when a certificate secret is configured, and publish a stable GitHub Release with all Velopack assets.

#### Scenario: Invalid tag
- **WHEN** the tag `v1.2` is pushed
- **THEN** the release workflow fails before building

### Requirement: Auto-update
The installed app SHALL check GitHub Releases through Velopack at startup, download a newer release in the background and offer "Restart now" to apply it.

#### Scenario: Update ready
- **WHEN** a newer stable release exists
- **THEN** the sidebar shows that an update is ready with a restart action
