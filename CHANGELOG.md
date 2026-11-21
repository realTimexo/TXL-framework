# Changelog

Notable changes to TXL are recorded here. Versions follow semantic versioning where practical. The release page contains distributable packages and release-specific installation links.

## [0.6.0] — 2026-11-21

### Added

- Dotted `text.headline` syntax while preserving `text(hl)` for existing source.
- `windows (` settings for title, project-relative icon, width-by-height size, and normal/maximized/fullscreen/borderless mode, with `info.json` defaults when omitted.
- Uniform image sizing through `size`, separate dimensions, and HTTP(S) image URLs with project-root traversal protection for local assets.
- WPF video playback with Play/Pause buttons, optional autoplay/loop, local media paths, and HTTP(S) sources.
- Class `keydown` actions with optional key-name filtering, including Enter-key handling on focused inputs.
- `position = x18 y3` syntax for absolute Canvas controls and styled groups, with targeted validation.
- Clearer CLI diagnostics with severity, source excerpt, caret, and repair hints where appropriate.
- `txl --build`, `txl --check`, `txl --preview`, `txl --version`, and `txl --help` aliases with current-folder defaults.
- Global .NET tool packaging and a per-user Windows installer that can bootstrap the .NET 9 SDK.
- Updated VS Code completions/snippets, media path highlighting, AllFeatures showcase, docs, and static website/blog source.

### Changed

- The `!` starter scaffold now includes a windows block and actions block.
- Persistent state uses project identity even when a window title is overridden; the common `Enter` key name maps to WPF's `Return` key.
- README and documentation distinguish supported behavior from known limitations.

### Validation

- Compiler solution builds on .NET SDK 9.0.200; 29 regression tests pass in the sandbox.
- Generated WPF applications can be cross-published from Linux, but this release's window appearance, keyboard routing, icon display, and Windows codec playback require Windows validation.

## Earlier history

See the [GitHub release list](https://github.com/realTimexo/TXL-framework/releases) for earlier tagged versions.
