# TXL Starter Showcase
# This file demonstrates the features that are currently rendered by the compiler.
# Edit, save, then run .\build.ps1 from this folder.

style (
  main:
    font = Segoe UI
    background = #111827
    color = #F3F4F6
    size = 16
  text:
    color = #F3F4F6
    size = 16
  button:
    background = #2563EB
    color = #FFFFFF
    weight = bold
    padding = 12
    margin = 6
  input:
    background = #FFFFFF
    color = #111827
    padding = 10
    width = 320
  checkbox:
    color = #F3F4F6
    margin = 8
  hero:
    background = #1F2937
    color = #F9FAFB
    margin = 10
  card:
    background = #182235
    color = #E5E7EB
    margin = 10
)

group = hero (
  layout = vertical
  text(hl) = TXL Desktop Showcase
  text = A native Windows app compiled from a small, readable TXL file.
  image = assets/txl-logo.png
  text = The transparent TXL logo above is loaded from the project assets folder.
)

group = card (
  layout = vertical
  text(hl) = What this example demonstrates
  text = Styles can be assigned by matching a style section to a group name.
  text = Group layout can be vertical or horizontal.
  text = Text, buttons, input boxes, checkboxes, images, lines, and spacing are available.
  text = The app window and project icon are configured in info.json.
)

group = actions (
  layout = horizontal
  button = Show a message
  action = alert("Hello from the TXL Showcase!")
  button = Open TXL example site
  action = open("https://example.com")
)

group = preferences (
  layout = vertical
  text(hl) = Your preferences
  text = The controls below are native Windows controls.
  input = Type your name here
  checkbox = Remember my choice
  text = The checkbox is interactive; its current state is not yet bound to a TXL variable.
)

group = footer (
  layout = vertical
  line
  text = Build target: self-contained Windows x64
  space = 8
  text = Change this file and rebuild to try your own layout.
)


# Reusable class actions. Click currently supports open/alert; hover uses style keys.
actions (
  actionLink(click):
    open = https://example.com
  actionLink(hover):
    size = 18
    color = #38BDF8
)
text(actionLink) = Hover here to preview the class style; click to open the example site.

# Build-time values and control flow (the compiler expands these before generating WPF).
set = showNotice:true
set = owner:TXL
if = showNotice == true (
  text = Hello from ${owner}; this block is included by the compiler.
)
repeat = 3 (
  text = Generated row ${index}
)
