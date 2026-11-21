# TXL Language Support for VS Code

Provides TXL syntax highlighting, snippets, contextual completions, hover help, formatting, and a native preview command.

## Preview

Run **TXL: Preview Active Project** from the Command Palette while a `.txl` source file is active, or with a project folder open. The extension launches the existing `txl preview <project>` CLI command using the active TXL file's containing directory (or the workspace folder when no TXL file is active). The `txl` CLI must be installed and available on `PATH`; if it is missing, the extension reports that clearly. The preview is the CLI's native Windows WPF window, not an embedded VS Code preview. The CLI only knows build-time/source constructs; this extension does not present static constructs as live runtime state.

## Editor features

Completions are context-specific: style blocks suggest style properties, style value positions suggest valid values, and element/group positions suggest element or group starters. Text after `text =` is treated as content and does not offer TXL command completions. Font values include a broad set of Google Fonts family names. In a blank TXL file, type `!` and accept the starter completion to insert a useful app structure.

The TXL language icon is contributed by the extension. An optional **TXL File Icons** Explorer icon theme is also included, but VS Code's active file icon theme is user-controlled: users must explicitly select it in VS Code's File Icon Theme picker. Installing the extension does not change the user's theme selection.

## Packaging

The release workflow uses Node 22 and `@vscode/vsce` 4.1.1 (`npm ci`, then `npx vsce package --no-dependencies`). On 2026-11-21, `npm audit` reported two moderate upstream advisories in the VSIX packaging tool's transitive `minimatch` dependency and no high or critical findings. These packages are build-time tooling and are not bundled into the VSIX. Recheck the audit when updating the packer; npm's suggested forced major downgrade should not be applied without retesting the release workflow.
