# TXL Language and Runtime Guide

TXL is a line-oriented language for compiling native Windows desktop interfaces with WPF. Each non-empty line is one statement; indentation is for readability and does not determine scope. `#` starts a comment, except when it begins a hexadecimal color after `=` or `:`, or follows a numeric width/depth in a stroke or shadow value. Apostrophes inside ordinary unquoted text are preserved. Values can be quoted with single or double quotes. A block opens with `(` and closes with a standalone `)`.

## A small application

```txl
window (
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

`text.headline = ...` aliases `text.headline1 = ...`. The five heading variants are `text.headline1` through `text.headline5`, progressively decreasing from 38 to 18 WPF device-independent pixels. The older `text(hl) = ...` form remains accepted.

## Windows and project defaults

Use one optional `window (` block near the top of `main.txl` to override the project defaults. The older `windows (` spelling remains accepted as a compatibility alias, but new source should use `window (`:

```txl
window (
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

A style section uses `name:` and a property assignment. Rendered style values include `font`, `size`, `color`, `background`, `weight`, `margin`, `padding`, `width`, `height`, `opacity`, `align`, `layout`, `radius`, `border`/`stroke`, and `shadow`. `align` accepts `left`, `center`, `right`, or `stretch`; `layout` accepts `vertical`, `horizontal`, or `absolute`. A style section matching a group name can set that group’s default layout and alignment. For an image, `size = 160` makes a 160-by-160 uniform-fit box; `width` or `height` can set one dimension independently. The image keeps its proportions.

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

A `group = name (` creates a layout group. `layout = vertical` stacks direct children top-to-bottom (the default); `horizontal` places them side-by-side; `absolute` creates a WPF Canvas. A matching group style can set the layout, for example `style (
  buttons:
    layout = vertical
    align = center
)`. In an absolute group, position a child or the group itself using `position = x18 y3`. Values are fixed coordinates, not responsive constraints. `x = 18` and `y = 3` remain supported for older source files.

`align` applies horizontal alignment to text, controls, or a group. `radius = 12` rounds button corners and decorates framed text/image elements. `border = 2 #333333` or `stroke = 1 red` draws a stroke. `shadow = 4 12 #00000066` sets shadow depth, blur, and color; a color alone uses default depth/blur, and `shadow = none` clears it. Button background color is painted through a template binding so it remains visible with the rounded template. WPF layout is not responsive; shadows/borders are basic WPF effects rather than a full design system.

## Images and videos

Local media paths are relative to the project folder and copied into the generated application's `content` folder. Project-root traversal such as `../outside.png` is rejected by the runtime path resolver. HTTP and HTTPS URLs are also accepted for images and videos; other remote URL schemes are rejected. Remote loading needs a working network connection at runtime. `font = https://host/font.ttf` or `.otf` downloads the font during `txl --build`/`txl --preview` and embeds it in the generated application; remote font builds require an internet connection. TXL restricts downloads to HTTPS on the standard port, validates public DNS addresses and font signatures, follows only a few HTTPS redirects, and caps downloads at 20 MiB. The family name is inferred from the filename, so descriptive filenames are recommended. `txl --check` validates the URL form without downloading it.

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

## HTML embedding

Use `html =` or its `embed =` alias with a single-line HTML string, a project-relative `.html` file, or an HTTP(S) URL. Project files are copied into the app content folder. Generated apps use Microsoft Edge WebView2 (Chromium), so inline HTML, scripts, CSS, remote pages, and YouTube iframe embeds use a modern engine instead of the legacy WPF WebBrowser control. The Microsoft Edge WebView2 Runtime must be installed; Windows 11 includes it, while some Windows 10 devices may need the [Evergreen Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) installed. If it is missing, the app displays an install link rather than a script-error dialog. Treat embedded HTML and remote pages as trusted content because page JavaScript runs with the app user's network access. Remote embedding accepts only HTTP(S). VS Code syntax highlighting embeds its HTML grammar inside quoted `html = "..."` and `embed = "..."` values.

```txl
embed = "<h2>Welcome</h2><p>Embedded markup</p>"
html = assets/about.html
```

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
) else (
  text = Access granted.
)
```

`if` accepts `true`, `false`, a variable name, or one `name == value` / `name != value` comparison. An optional `else (` block must immediately follow the closing `)` of its `if`. Comparisons are case-insensitive strings. Both branches are selected during compilation; this is not a live runtime condition.

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

- `import = modules/name.txl` loads a project-relative library before the entry page is parsed. Imported files can define functions, components, styles, and actions; keep UI/runtime statements in pages. Import paths must end in `.txl`, cannot traverse with `..`, and cannot follow symbolic links. Cycles and missing modules are errors.
- Example library: `modules/math.txl` can contain `function = double(value) (` followed by `return = ${value} * 2` and `)`. In `main.txl`, write `import = modules/math.txl` before calling `double(21)` in a compile-time value or `call = double,21,result` in a runtime action.
- State values can hold JSON arrays or objects, for example `state = names:["Ada"]` and `state = profile:{}`. In actions use `arrayAdd = names,Grace`, `arrayGet = names,0,result`, `arrayRemove = names,0`, `objectSet = profile,city,London`, `objectGet = profile,city,result`, or `objectRemove = profile,city`. Array/object mutations persist through the same local JSON state file. Values that contain commas can be quoted; nested brackets/braces and quotes are respected by the action argument parser. Retrieved scalar strings are placed into state as plain text; retrieved arrays/objects remain JSON text.
- File actions are `fileWrite = notes.txt,text`, `fileAppend = notes.txt,more`, `fileRead = notes.txt,resultState`, and `fileDelete = notes.txt`. Files are confined to the app's per-user TXL data directory under LocalAppData; absolute paths, traversal, and symbolic links are rejected. These operations are synchronous and intended for small local app data, not large files or arbitrary project file access.
- `httpGet = https://api.example.org/data,responseState` and `httpPost = https://api.example.org/data,jsonBodyState,responseState` perform asynchronous HTTP requests and store response text in state. POST sends the supplied body as `application/json`. Only absolute HTTP(S) URLs are accepted and requests time out after 30 seconds. This is asynchronous action I/O; TXL does not yet expose general-purpose `async`/`await`, cancellation, or arbitrary networking sockets.
- `timerStart = clock,1000,seconds,increment` starts a named 1-second repeating timer that increments the `seconds` state; the update can also be `toggle` or a fixed value. Use `timerStop = clock` to stop it. Intervals are limited to 50 ms–24 hours, and all active timers stop when the app closes. These are UI-thread WPF timers, not background worker threads.
- SQLite uses `dbExecute = app.db,SQL`, `dbQuery = app.db,SQL,resultState`, and `dbScalar = app.db,SQL,resultState`. Database files are confined to the app data folder; query rows are stored as a JSON array of row objects. Bind parameters using a quoted SQL argument and subsequent name/value pairs: `dbExecute = app.db,"INSERT INTO people(name) VALUES ($name)",name,userName`. For `dbQuery` and `dbScalar`, place the output state immediately after SQL, then add parameter name/value pairs. Values are passed as bound data, not spliced into SQL. Use `dbBegin = app.db`, then one or more execute/query/scalar actions, then `dbCommit = app.db` or `dbRollback = app.db`. An unclosed active transaction is rolled back when the window closes. Async database actions are not currently provided.
- Runtime error handling has two forms. During page construction, `try (` must be followed by `catch = errorState (`; `throw = message` throws within that boundary, the catch stores the error message and renders its body. For runtime action failures, wrap an action call as `try(httpGet(https://api.example.org/data,result),errorState)`; the wrapped action is awaited, and any exception message is saved in `errorState` instead of opening the default error dialog. `throw(message)` raises an error that can be caught by this action wrapper. This is exception handling for page construction or a single action call, not a typed exception system or a universal boundary around every event callback.
- `list = stateName` shows a persistent task list with add, check/uncheck, and remove actions when paired with a state and `listAdd(collection,inputState)`.
- `component = name (` defines a reusable compile-time block. Call it as `name(title: value)`; `${title}` is substituted during compilation.
- `for = item in collection (` expands a compile-time list. `repeat = N (` expands at most 1,000 times and provides zero-based `${index}`.
- Calculator math supports `math(add|subtract|multiply|divide,left,right,result)` and writes a readable result for invalid input or division by zero.
- `open = https://example.org` opens HTTP(S) links; `open = mailto:hello@example.org` launches the system mail handler. Other URI schemes are ignored.
- `page = business.txl` or `page(business.txl)` navigates to another `.txl` file compiled from within the project. The `info.json` `start` file is the only view shown at launch; other root or nested `.txl` files are compiled as routes and do not render until a page action runs. For example, put `button = Open test page` followed by `action = page(test.txl)` in `main.txl`, set `"start":"main.txl"` in `info.json`, and put the destination UI in `test.txl`. Paths are normalized and traversal is rejected; Back returns to the previous compiled page.

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

The standalone generated app runs on Windows. Cross-publishing on Linux is supported, but WPF appearance, alignment, rounded button templates, shadows/borders, HTML behavior, navigation, keyboard events, icons, and media playback must be checked on Windows. See [the Windows build guide](BUILD-WINDOWS.md) for setup and troubleshooting.
