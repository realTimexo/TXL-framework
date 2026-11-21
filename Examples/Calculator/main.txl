# TXL Calculator
# Runtime state, input binding and arithmetic are persisted locally by the generated app.
style (
  main:
    font = Segoe UI
    background = #111827
    color = #F3F4F6
    size = 16
  button:
    background = #2563EB
    color = #FFFFFF
    padding = 12
    margin = 4
  input:
    background = #FFFFFF
    color = #111827
    padding = 10
    width = 320
  result:
    color = #38BDF8
    size = 26
    weight = bold
)

state = left:0
state = right:0
state = result:0

group = calculator (
  layout = vertical
  text(hl) = Simple Calculator
  text = Enter two numbers, then choose an operation.
  input = First number
  bind = left
  input = Second number
  bind = right
  text = Result
  text(result) = 0
  bind = result
  group = operations (
    layout = horizontal
    button = Add
    action = math(add,left,right,result)
    button = Subtract
    action = math(subtract,left,right,result)
    button = Multiply
    action = math(multiply,left,right,result)
    button = Divide
    action = math(divide,left,right,result)
  )
  button = Clear result
  action = clear(result)
  text = A non-number or division by zero displays a helpful result message.
)
