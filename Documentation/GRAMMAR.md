# TXL Grammar and Runtime

TXL is line-oriented. Each non-empty line contains one instruction; indentation is for readability and does not define scope. `#` begins a comment, except hexadecimal colors following `=` or `:`. Single- and double-quoted values are accepted.

## Styles, groups, and layout

`style (` begins a style catalog. A section such as `main:` introduces properties. Groups use `group = name (` and a matching `)`.

```txl
style (
  main:
    font = Arial
    background = #20202A
    color = #F4F7FB
  button:
    background = #0EA5E9
    color = #FFFFFF
    padding = 10
)
group = header (
  layout = vertical
  text(hl) = Welcome to TXL
  text = A native Windows desktop app
)
```

`layout = vertical` stacks direct children from top to bottom (the default); `horizontal` places them side by side. `layout = absolute` uses a WPF Canvas. Set `x = number` and `y = number` on an element for its Canvas coordinates. These are simple fixed coordinates, not responsive constraints. Runtime-applied style properties include font, size, color, background, weight, margin, padding, width, height, and opacity. Gradient backgrounds, shadow, border, radius, theme switching, and spacing tokens are not currently rendered.

## Controls, inputs, and checkboxes

Supported element types include `text`, `button`, `image`, `input`, `checkbox`, `line`, and `space`. Elements may use a variant or class in parentheses, for example `text(hl) = Heading`. For an input, `input = Search users` uses the text as a placeholder shown only while the field is empty. Use `value = actual initial text` on an input to provide real initial content. `bind = stateName` binds an input or checkbox to runtime state. Checkbox interaction uses a native WPF checkbox.

## Runtime state and actions

Declare a state value with `state = name:initial-value`. The initial value is used when no saved value exists. State updates are written to `%LocalAppData%\TXL\<AppName>\state.json` and restored at the next launch. Current action forms include:

```txl
state = left:0
state = right:0
state = result:0
button = Add
 action = math(add,left,right,result)
```

Supported runtime action operations are `set(name,value)`, `append(name,value)`, `clear(name)`, `toggle(name)`, `math(add|subtract|multiply|divide,left,right,result)`, `listAdd(collection,inputState)`, `prompt(state,title)`, `confirm(message)`, `success(message)`, `open(url)`, and `page(path)`. `open` permits HTTP(S) URLs. Invalid calculator input or division by zero produces a user-readable result. Prompt, confirm, and success use basic native dialogs; custom modal layouts, forms, loading overlays, confirmation callbacks, and progress states are not implemented. `page` currently does not render a distinct TXL page.

## Lists, components, and compile-time expansion

A state initialized to `[]` can back `list = stateName`. The built-in todo list renders task rows with completion and removal controls, and persists changes. It is a simple task-list control, not a general-purpose reactive list renderer.

`for = item in collection (` supports compile-time expansion over a declared collection, and `component = name (` defines a reusable block with parameters supplied as `name: value` at the call site. Both features expand while compiling; they do not update if data changes at runtime. They currently provide a basic template facility rather than a fully reactive component system.

`set`, `if`, and `repeat` are also evaluated by the compiler. `set = name:value` declares a compile-time value; `${name}` interpolates the current value. `if` accepts `true`, `false`, a variable name, or a simple `name == value` / `name != value` comparison. `repeat = N (` expands its child block N times (maximum 1,000); `${index}` is zero-based inside the repeated block.

## Class click and hover actions

An element may be assigned a class using parentheses. The `actions (` catalog supports `className(click):` and `className(hover):` sections. Click actions currently support limited URL/message behavior; hover applies and restores a small set of visual properties. This is not a general event scripting system.

```txl
actions (
  card(click):
    open = https://example.com
  card(hover):
    size = 15
    color = red
)
text(card) = Point at or click this text
```

## Projects, CLI, and editor support

A project has an `info.json` with `name`, `version`, `developer`, `description`, and `start`; optional `window` settings include width, height, resizable, and background. `icon` can identify an `.ico` file.

- `txl check <project>` validates metadata and source.
- `txl preview <project>` launches a native WPF app on Windows without publishing an EXE. It is not an embedded editor preview.
- `txl build <project>` publishes a self-contained Windows x64 executable.
- `txl run <project>` builds and launches on Windows.

The VS Code extension provides language support, snippets, completion, formatting, and a command that calls the CLI when it is on `PATH`. Its Explorer file icon theme must be selected manually. Google Fonts entries are completion suggestions only; font installation and bundling are outside the extension.

## Explicitly unsupported developer features

There is no live preview embedded in VS Code, inline preview error panel, component inspector, or automatic hot reload. The native `preview` command is a convenience launch workflow only. Dark/light theme switching, gradients, shadows, border/radius rendering, and a configurable spacing system are also not implemented. Generated WPF apps compile and cross-publish on Linux, but GUI behavior requires Windows visual validation.
