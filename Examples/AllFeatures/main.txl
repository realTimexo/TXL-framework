# TXL All Features Showcase
# This file demonstrates features currently supported by the TXL compiler/runtime.
# Compile and launch on Windows with: .\build.ps1
# Gradient backgrounds, shadows, border/radius, theme switching, loading overlays,
# custom modal forms, live editor inspection, and hot reload are not implemented yet.

windows (
  title: TXL All Features
  icon: assets/app.ico
  size: 1150x820
  mode: normal
)

style (
  main:
    font = Segoe UI
    background = #111827
    color = #F3F4F6
    size = 15
  text:
    color = #F3F4F6
    size = 15
  button:
    background = #2563EB
    color = #FFFFFF
    weight = bold
    padding = 10
    margin = 4
  input:
    background = #FFFFFF
    color = #111827
    padding = 8
    margin = 4
    width = 280
  checkbox:
    color = #E5E7EB
    margin = 4
  section:
    background = #1F2937
    color = #F9FAFB
    padding = 10
    margin = 8
  header:
    background = #0B3B50
    color = #F9FAFB
    padding = 12
    margin = 8
  accent:
    color = #38BDF8
    size = 22
    weight = bold
  eyebrow:
    color = #9CA3AF
    size = 12
  metricNumber:
    color = #38BDF8
    size = 24
    weight = bold
  actionLink:
    background = #0F766E
    color = #FFFFFF
    padding = 10
  brandImage:
    size = 160
)

# Reusable parameterized component; values are substituted at compile time.
component = metricCard (
  text(eyebrow) = ${title}
  text(metricNumber) = ${value}
)

# Class-based events are declared once and attached by class name below.
actions (
  actionLink(click):
    open = https://example.com
  actionLink(hover):
    size = 18
    color = #FDE047
  classAction(click):
    alert = This button uses the class click action.
  classAction(hover):
    size = 19
    color = #A7F3D0
  keyboardField(keydown):
    key = Enter
    alert = Enter key detected on the focused input.
)

# Runtime state declarations seed persistent values on the first launch.
state = personName:
state = rememberPreference:false
state = counter:0
state = note:

# Header, styles, and reusable components.
group = header (
  layout = vertical
  image(brandImage) = assets/txl-logo.png
  text.headline = TXL All Features
  text = One example file for the current language and runtime features.
  metricCard(title: Runtime state, value: Saved locally)
  metricCard(title: Templates, value: Components)
)

# Basic vertical/horizontal groups, styled controls, and the new class event syntax.
group = section (
  layout = vertical
  text.headline = 1. Controls, placeholder inputs, and class actions
  text = The hint disappears as soon as you type. This second field starts with actual text.
  input = Type a name here
  bind = personName
  input = This is real initial input content
  value = Example value
  button(actionLink) = Hover or click to open the example site
  button(classAction) = Hover or click for a class action
  checkbox = Remember this preference
  bind = rememberPreference
)

# Runtime string/boolean state with set, append, clear, and toggle actions.
group = section (
  layout = vertical
  text.headline = 2. Persistent runtime state
  text = Counter value:
  text(accent) = 0
  bind = counter
  group = stateButtons (
    layout = horizontal
    button = Add one
    action = append(counter,1)
    button = Set to 10
    action = set(counter,10)
    button = Clear counter
    action = clear(counter)
    button = Toggle preference
    action = toggle(rememberPreference)
  )
  text = The state values are saved locally and restored on the next launch.
)

# Native prompt, confirmation, and success dialogs (not custom modal forms or loading overlays).
group = section (
  layout = horizontal
  button = Ask for a note
  action = prompt(note,Enter a short note)
  button = Show confirmation
  action = confirm(Continue with this demo?)
  button = Show success message
  action = success(Your changes are saved.)
  text = Last note:
  text = No note entered yet
  bind = note
)

# Four-operation calculator using bound runtime state and a result binding.
state = left:
state = right:
state = calculationResult:Enter two numbers

group = section (
  layout = vertical
  text.headline = 3. Calculator and arithmetic actions
  input = First number
  bind = left
  input = Second number
  bind = right
  group = calculatorButtons (
    layout = horizontal
    button = Add
    action = math(add,left,right,calculationResult)
    button = Subtract
    action = math(subtract,left,right,calculationResult)
    button = Multiply
    action = math(multiply,left,right,calculationResult)
    button = Divide
    action = math(divide,left,right,calculationResult)
  )
  text(accent) = Result appears here
  bind = calculationResult
)

# Persistent runtime collection: add tasks, check/uncheck them, and remove them.
state = newTask:
state = tasks:[]
group = section (
  layout = vertical
  text.headline = 4. Persistent todo list
  input = Add a task to the list
  bind = newTask
  button = Add task
  action = listAdd(tasks,newTask)
  list = tasks
)

# Simple compile-time array iteration and parameterized component calls.
set = users:[Ada,Lin,Sam]
group = section (
  layout = vertical
  text.headline = 5. Compile-time list and for loop
  text = These rows are generated during compilation, not as a reactive list.
  for = user in users (
    metricCard(title: Example user, value: ${user})
  )
)

# Fixed x/y coordinates inside an absolute Canvas layout.
group = section (
  layout = absolute
  position = x10 y8
  text(accent) = 6. Absolute x/y positioning
  text = This label is placed at x=18, y=56 in its Canvas.
  position = x18 y56
  button = Fixed-position button
  position = x18 y94
)

# A local H.264 clip with native play/pause controls. Replace it with a local MP4 or HTTP(S) URL.
group = section (
  layout = vertical
  text.headline = 7. Video playback
  text = This bundled five-second MP4 is a compact playback example. Supported formats depend on Windows codecs.
  video = assets/sample.mp4
  autoplay = false
  loop = true
  width = 480
  height = 270
)

# Enter in this input triggers the class keyboard action; ordinary typing remains in the input.
group = section (
  layout = vertical
  text.headline = 8. Keyboard input
  input(keyboardField) = Press Enter here
)

# Previous compile-time constructs remain available alongside runtime state.
set = showTip:true
if = showTip == true (
  text = Compile-time if: this text is included in the generated app.
)
repeat = 2 (
  text = Compile-time repeat row ${index}
)

# HTTP links are supported by the runtime open action.
group = section (
  layout = horizontal
  text = More:
  button = Open TXL example site
  action = open(https://example.com)
)
