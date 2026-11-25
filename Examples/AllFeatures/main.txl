# TXL All Features Showcase
# This file demonstrates features currently supported by the TXL compiler/runtime.
# Compile and launch on Windows with: .\build.ps1
# Gradients, theme switching, loading overlays, custom modal forms, live editor inspection,
# and hot reload are not implemented yet.

import = modules/text.txl
set = welcomeText:welcome(Ada)

window (
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
    radius = 10
    border = 1 #60A5FA
    shadow = 4 12 #00000055
  calculatorButtons:
    layout = horizontal
    align = center
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
  businessPage(click):
    page = business.txl
  mainPage(click):
    page = main.txl
  contact(click):
    open = mailto:hello@example.com
)

# Runtime state declarations seed persistent values on the first launch.
state = personName:
state = rememberPreference:false
state = counter:0
state = note:
state = names:["Ada"]
state = profile:{}
state = savedText:
state = apiResponse:
state = timerTicks:0
state = databaseResult:
state = databaseName:Grace

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
) else (
  text = This alternate branch is selected when showTip is false.
)
text.headline2 = Heading level two
text.headline3 = Heading level three
text.headline4 = Heading level four
text.headline5 = Heading level five
embed = "<p><strong>Trusted inline HTML</strong> rendered by WPF WebBrowser.</p>"
repeat = 2 (
  text = Compile-time repeat row ${index}
)

# HTTP links are supported by the runtime open action.
group = section (
  layout = horizontal
  text = More:
  button = Open TXL example site
  action = open(https://example.com)
  button(businessPage) = Open the compiled business page
  button(contact) = Email the author
)

# Custom compile-time function imported from a library module.
group = section (
  layout = vertical
  text.headline = 9. Functions, modules, collections, and local files
  text = Imported function output: ${welcomeText}
  button = Add Grace to names
  action = arrayAdd(names,Grace)
  button = Save profile name
  action = objectSet(profile,name,Ada)
  button = Read profile name
  action = objectGet(profile,name,note)
  text = Profile name:
  text = No name saved
  bind = note
  button = Save a local note
  action = fileWrite(notes.txt,Hello from TXL)
  button = Read the local note
  action = fileRead(notes.txt,savedText)
  text = Saved note:
  text = Nothing read yet
  bind = savedText
)

# Network, timer, and SQLite calls only run when their buttons are clicked.
group = section (
  layout = vertical
  text.headline = 10. HTTP, timers, and SQLite
  button = Request example JSON
  action = httpGet(https://httpbin.org/json,apiResponse)
  text = HTTP response:
  text = Click the request button
  bind = apiResponse
  button = Start 1-second timer
  action = timerStart(demoClock,1000,timerTicks,increment)
  button = Stop timer
  action = timerStop(demoClock)
  text = Timer ticks:
  text = 0
  bind = timerTicks
  button = Create SQLite demo table
  action = dbExecute(app.db,CREATE TABLE IF NOT EXISTS demo (id INTEGER PRIMARY KEY, name TEXT))
  button = Begin SQLite transaction
  action = dbBegin(app.db)
  button = Insert name in current transaction
  action = dbExecute(app.db,"INSERT INTO demo(name) VALUES ($name)",name,databaseName)
  button = Commit transaction
  action = dbCommit(app.db)
  button = Roll back transaction
  action = dbRollback(app.db)
  button = Count SQLite rows
  action = dbScalar(app.db,SELECT COUNT(*) FROM demo,databaseResult)
  text = Database result:
  text = Click Count SQLite rows
  bind = databaseResult
)
