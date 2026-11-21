# TXL Language and Runtime Guide

TXL is a line-oriented language for compiling native Windows desktop interfaces with WPF. Each non-empty line is one statement; indentation is for readability and does not determine scope. `#` starts a comment, except when it begins a hexadecimal color after `=` or `:`. Values can be quoted with single or double quotes. A block opens with `(` and closes with a standalone `)`.

## A small application

```txl
windows (
  title: Reading List
  icon: assets/app.ico
  size: 1100x760
  mode: normal
)

style (
  main:
    font = Segoe UI
    background = #172033
    color = #F4F7FB
  button:
    background = #1677D2
    color = white
    padding = 10
)

actions (
  sayHello(click):
    alert = Hello from TXL.
)

group = home (
  layout = vertical
  text.headline = Keep reading.
  text = A simple native Windows app.
  button(sayHello) = Show a message
)
```

`text.headline = ...` creates a large text label. The older `text(hl) = ...` form remains accepted for existing projects, but new code should use the dotted form.

## Windows and project defaults

Use one optional `windows (` block near the top of `main.txl` to override the project defaults:

```txl
windows (
  title: My application
  icon: assets/app.ico
  size: 1280x800
  mode: maximized
)
```

`title` is the visible window title. `icon` is a local, project-relative image path; an `.ico` file is also used as the generated executable's application icon. `size` is `WIDTHxHEIGHT` (width 200–7680, height 150–4320). `mode` is `normal`, `maximized`, `fullscreen`, or `borderless`. Fullscreen removes the window border and maximizes the window. Borderless removes the border but keeps the configured window dimensions.

Omitted values inherit project defaults: the title falls back to `info.json`'s `name` (or its optional `window.title`), the icon falls back to `info.json`'s `icon`, the size falls back to `info.json`'s `window.width` and `window.height`, and the mode falls back to `info.json`'s optional `window.mode` (default `normal`). Other existing defaults such as `window.resizable` and `window.background` are preserved. Example `info.json` window settings:

```json
{
  "name": "Reading List",
  "version": "1.0.0",
  "developer": "Your name",
  "description": "A native TXL app",
  "start": "main.txl",
  "icon": "assets/app.ico",
  "window": {
    "title": "Reading List",
    "width": 1100,
    "height": 760,
    "resizable": true,
    "background": "#172033",
    "mode": "normal"
  }
}
```

## Styles, classes, and layout

A style section uses `name:` and a property assignment. Supported rendered style values include `font`, `size`, `color`, `background`, `weight`, `margin`, `padding`, `width`, `height`, and `opacity`. For an image, `size = 160` makes a 160-by-160 uniform-fit box; `width` or `height` can set one dimension independently. The image keeps its proportions.

A parenthesized class such as `image(brandImage)` or `text(note)` selects a matching style section and can also receive class actions. You can define reusable styling for classes already used in the source:

```txl
style (
  brandImage:
    size = 160
  note:
    color = #A7F3D0
)

group = panel (
  layout = vertical
  image(brandImage) = assets/logo.png
  text(note) = This label uses the note style.
)
```

A `group = name (` creates a layout group. `layout = vertical` stacks direct children top-to-bottom (the default); `horizontal` places them side-by-side. `absolute` creates a WPF Canvas. In an absolute group, position a child or the group itself using `position = x18 y3`. Values are fixed coordinates, not responsive constraints. `x = 18` and `y = 3` remain supported for older source files.

Gradients, shadows, border/radius rendering, responsive layout constraints, theme switching, and spacing tokens are not currently implemented.

## Images and videos

Local media paths are relative to the project folder and copied into the generated application's `content` folder. Project-root traversal such as `../outside.png` is rejected by the runtime path resolver. HTTP and HTTPS URLs are also accepted for images and videos; other remote URL schemes are rejected. Remote loading needs a working network connection at runtime.

```txl
style (
  logo:
    size = 120
)

image(logo) = assets/logo.png
image = https://example.org/images/banner.png

video = assets/clip.mp4
autoplay = false
loop = true
width = 480
height = 270
```

`video` creates a WPF media player with Play and Pause buttons. `autoplay` defaults to `true`; `loop` defaults to `false`. Video and audio format support depends on installed Windows codecs and the URL/file encoding. WPF media playback, streaming URLs, and codec behavior need to be tested on the target Windows version. A project can pass `txl --check` on Linux, but Linux cannot run or visually validate the native WPF window.

## Variables and `if` statements

TXL has two simple kinds of values. `set` defines a compile-time value used while the compiler expands the source. `${name}` inserts it into text. A compile-time `if` includes its block when its expression is true:

```txl
set = username:Ada
set = isAdmin:true

if = isAdmin == true (
  text = Welcome, ${username}.
)

if = isAdmin != true (
  text = Ask an administrator for access.
)
```

`if` accepts `true`, `false`, a variable name, or one `name == value` / `name != value` comparison. Comparisons are case-insensitive strings. This is evaluated during compilation; it is not a live runtime condition.

For values that change while the app is running, use `state` instead. State is stored as text and persisted as JSON under `%LocalAppData%\TXL\<AppName>\state.json` on Windows:

```txl
state = userName:Ada
state = remember:false

group = profile (
  input = Your name
  bind = userName
  checkbox = Remember me
  bind = remember
  text = Current value:
  text = Ada
  bind = userName
)
```

Inputs bind to a state key with `bind = userName`; checkboxes bind to a boolean string in the same way. Use action operations such as `set`, `append`, `toggle`, and `clear` to update runtime state. A persisted value replaces the initial value on later launches.

## Windows message boxes and dialogs

A button's `action = alert(...)` displays a native Windows message box. `success(...)` is an information-style message. `confirm(...)` displays Yes/No buttons, and `prompt(state,title)` opens a simple text-entry dialog:

```txl
group = dialogExample (
  button = Show an alert
  action = alert("The file was saved.")
  button = Ask for confirmation
  action = confirm("Continue?")
  button = Enter a name
  action = prompt(userName,Enter your name)
)
```

Class actions can run the same message operations:

```txl
actions (
  alertButton(click):
    alert = This is a native Windows message box.
)

button(alertButton) = Show alert
```

These are basic native dialogs. Custom modal form layouts, loading overlays, progress states, and confirmation callbacks are not included.

## Actions and keyboard input

An element can use a class in parentheses. Define `click`, `hover`, or `keydown` under `actions (`. A `key` line filters keyboard events; other action lines run when the key matches. Key actions attach to the focused element and do not replace normal text entry.

```txl
actions (
  searchBox(keydown):
    key = Enter
    alert = Search submitted
)

input(searchBox) = Type a search, then press Enter
```

The current keyboard event set is a simple WPF key-down event with an optional key-name filter. This is not a general shortcut-command system. `hover` temporarily applies a limited subset of visual styles and restores the previous appearance when the pointer leaves.

## Other language features

- `list = stateName` shows a persistent task list with add, check/uncheck, and remove actions when paired with a state and `listAdd(collection,inputState)`.
- `component = name (` defines a reusable compile-time block. Call it as `name(title: value)`; `${title}` is substituted during compilation.
- `for = item in collection (` expands a compile-time list. `repeat = N (` expands at most 1,000 times and provides zero-based `${index}`.
- Calculator math supports `math(add|subtract|multiply|divide,left,right,result)` and writes a readable result for invalid input or division by zero.
- `open = https://example.org` opens only HTTP(S) links. `page(path)` is retained but does not yet render a separate TXL page.

These are intentionally small compile-time and runtime features, not a reactive application framework.

## CLI, preview, and installation

When a TXL project folder is the current directory, the global CLI defaults to that folder:

```powershell
txl --version
txl --help
txl --check
txl --build
```

The command aliases `txl check`, `txl build`, `txl preview`, and `txl run` are also available. `txl --build .\MyProject` accepts an explicit project path. `--build` publishes a self-contained Windows x64 app into the project's `dist` folder; `--preview` builds and opens a native WPF window on Windows without publishing the standalone EXE. The preview is not an embedded VS Code window.

The supported global installation uses the .NET tool system and is global to the current Windows user, not machine-wide. It does not require administrator rights. Building projects requires the .NET 9 SDK, which the included `Installer/install.ps1` can install to the user profile when missing. The installer downloads the latest TXL CLI NuGet package from the GitHub Releases page. Add `%USERPROFILE%\.dotnet\tools` to PATH (the installer does this) and open a new terminal after installation. Building still needs network access for NuGet restore if packages are not already cached.

The standalone generated app runs on Windows. Cross-publishing on Linux is supported, but WPF appearance, keyboard events, icons, and media playback must be checked on Windows. See [the Windows build guide](BUILD-WINDOWS.md) for setup and troubleshooting.
