window (
  title: TXL Page Navigation Demo
  size: 720x460
  mode: normal
)

style (
  main:
    font = Segoe UI
    background = #111827
    color = #F9FAFB
    size = 16
  button:
    background = #2563EB
    color = #FFFFFF
    padding = 12
    radius = 8
)

text.headline = Main page
text = This is the only page opened when the app starts.
text = The test.txl file is compiled as a route, not automatically displayed.
button = Open test.txl
  action = page(test.txl)
