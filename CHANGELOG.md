# Changelog

Notable changes to TXL are recorded here. Versions follow semantic versioning where practical. The release page contains distributable packages and release-specific installation links.

## [0.9.0-preview.2] — 2026-11-25

### Fixed

- Replaced the legacy WPF WebBrowser with Microsoft Edge WebView2 so trusted inline HTML, local/remote pages, and YouTube iframe embeds render using Chromium. A clear install link appears if the WebView2 Runtime is missing.
- Added embedded HTML TextMate scopes for quoted `html = "..."` and `embed = "..."` values in VS Code, and registered both names as element syntax.
- Added a dedicated two-page example and regression coverage proving that `info.json`'s start file is the only startup page and a button action navigates to another compiled `.txl` file.

### Validation

- Release validation is in progress for this preview. WPF playback and rendering still need visual testing on Windows with the WebView2 Runtime installed.

## [0.9.0-preview.1] — 2026-11-25

### Added

- Recursive project-relative library imports with traversal, symlink, missing-file, and cycle checks; custom functions with parameters, one return expression, bounded recursion, and compile-time/runtime invocation.
- JSON-backed runtime arrays and objects, plus app-data file read/write/append/delete operations confined to the per-user application data directory.
- HTTP GET and JSON POST actions using asynchronous I/O, repeating WPF timers, and SQLite query/execute/scalar APIs with bound named parameters and begin/commit/rollback transactions.
- Runtime action error boundaries that can await a nested operation, catch its exception, and store the message in state; page-construction try/catch/throw remains available.
- Version-specific isolated updater recovery for the reported Windows `.store\txl.cli` access-denied failure, avoiding deletion or modification of the locked global tool cache.

### Validation and limits

- The solution builds in the Linux sandbox; 50 console regression tests pass; the AllFeatures example passes `txl --check` and cross-publishes as a Windows x64 self-contained application. Its generated WPF runtime includes database parameters/transactions and runtime action error handling.
- This preview does not expose user-written general-purpose `async`/`await`, arbitrary socket networking, database connection pooling, or background worker threads. TXL functions currently have a single return expression and collection loops/components expand at compile time.
- GitHub's Windows release workflow passed: tag/version validation, the 50-test regression harness, CLI package, VSIX, and self-contained Windows AllFeatures app all succeeded; the prerelease was published with CLI, VSIX, app, and source ZIP assets. Visual WPF behavior and the exact access-denied condition on the user's Windows machine still require manual Windows verification.

## [0.8.0] — 2026-11-23

### Added

- `if`/`else` compile-time branch selection with a diagnostic for orphaned `else` blocks; apostrophes in ordinary text no longer trigger quote errors.
- `text.headline1` through `text.headline5`, with `text.headline` retained as an alias.
- Rendered alignment, style-derived group layout, rounded buttons, basic border/stroke and drop-shadow styles, plus deterministic button-background templates.
- `html =` and `embed =` for trusted inline markup, project HTML files, or HTTP(S) pages in a WPF WebBrowser control.
- Safe `mailto:` launch support and project-relative multi-page TXL navigation with Back history.
- Build-time HTTPS TTF/OTF font packaging with size, signature, redirect, and public-host validation.
- GitHub bug-report and feature-request issue forms, plus regression coverage for the new features.

### Validation note

- `dotnet build TXL.slnx` succeeds; all 39 regression tests pass; all five example projects pass `txl --check`; AllFeatures cross-publishes to Windows x64 on Linux; the specified 1001fonts TTF was downloaded, packaged, and cross-published in a temporary test project. WPF visual/runtime behavior still requires a Windows user-session test.
- GitHub Actions v0.8.0 release workflow built and tested on Windows, packaged the CLI, VSIX, Windows AllFeatures app, and clean source ZIP. A versioned PowerShell installer is also attached to the release; future releases will include it automatically.

## [0.7.0] — 2026-11-22

### Added

- `window (` is the canonical spelling for application window settings. Existing `windows (` source remains accepted as a compatibility alias.
- `txl --update` and `txl update` check the latest stable GitHub Release, download its `Txl.Cli` package, verify the published size and SHA-256 digest, and update the installed per-user global tool.

### Changed

- The starter scaffold, AllFeatures sample, VS Code completions/snippets, grammar reference, and website examples now use singular `window (`.
- The installer pins the exact version from the latest stable release when installing or updating the CLI and documents `txl --update` for later updates.

### Upgrade note

- CLI versions before v0.7.0 do not include the updater command. Users on v0.6.0 or earlier should rerun the installer once to bootstrap v0.7.0; subsequent updates can use `txl --update`.

### Validation

- The compiler solution builds on .NET SDK 9.0.200; all 30 regression tests pass, including singular syntax and legacy plural compatibility. AllFeatures passes `txl --check`. The published v0.7.0 tool recognizes `txl --update` and reports the release as current; the pre-release no-downgrade check also passed.
- Generated WPF applications require Windows for visual, keyboard, icon, and codec verification.

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
