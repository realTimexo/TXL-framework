# TXL Todo List
# Tasks are stored as local JSON state under the current Windows user's LocalAppData.
style (
  main:
    font = Segoe UI
    background = #111827
    color = #F3F4F6
    size = 16
  button:
    background = #2563EB
    color = #FFFFFF
    padding = 10
    margin = 4
  input:
    background = #FFFFFF
    color = #111827
    padding = 10
    width = 360
)

state = task:
state = todos:[]

group = todoApp (
  layout = vertical
  text(hl) = My Todo List
  text = Add a task, tick it when complete, or remove it.
  input = What needs to be done?
  bind = task
  group = addRow (
    layout = horizontal
    button = Add task
    action = listAdd(todos,task)
    button = Clear input
    action = clear(task)
  )
  text = Tasks
  list = todos
  line
  text = Your tasks are saved locally and restored the next time you open this app.
)
