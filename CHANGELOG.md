# Changelog

## [1.0.0] - 2026-08-17

### Added

- Initial public package release.
- Added `BindBuffCallbacks<TBuff>` for cast-free, type-checked game buff integration.
- Added EditMode regression tests for recursive equipment stats and per-part buff callbacks.

### Fixed

- Recursively collect stat modifiers from root parts and every descendant.
- Replace registered equipment's full modifier snapshot once per assembly change, preventing duplicate stat application.
- Keep unregistered assembly edits from mutating live game stats.
