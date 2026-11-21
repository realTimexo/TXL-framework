using Txl;

var passed = 0;
var failed = 0;
void Test(string name, Action action)
{
    try { action(); Console.WriteLine($"PASS {name}"); passed++; }
    catch (Exception e) { Console.Error.WriteLine($"FAIL {name}: {e.Message}"); failed++; }
}

Test("colon style and group blocks", () =>
{
    var source = "style (\nmain:\nbackground = #20202A\ncolor = white\n)\ngroup = header (\ntext(hl) = Welcome\n)";
    var result = Parser.Parse(source, "test.txl");
    if (result.Program?.Nodes.Count != 1) throw new Exception(string.Join(";", result.Diagnostics));
    if (StyleCatalog.Styles["main"]["background"] != "#20202A") throw new Exception("hex color was lost");
});
Test("legacy parenthesized style sections", () =>
{
    var result = Parser.Parse("style (\nmain (\ncolor = white\n)\n)");
    if (result.Diagnostics.Any(d => d.Severity == Severity.Error) || StyleCatalog.Styles["main"]["color"] != "white") throw new Exception(string.Join(";", result.Diagnostics));
});
Test("group style reaches generated controls", () =>
{
    var parsed = Parser.Parse("style (\npreferences:\ncolor = #FFFFFF\n)\ngroup = preferences (\ntext = test\n)");
    if (parsed.Program is null) throw new Exception(string.Join(";", parsed.Diagnostics));
    var emitted = WpfEmitter.Emit(new ProjectInfo("Test", "1.2.3", "Timexo", "test", "main.txl", null, new WindowInfo()), parsed.Program, ".");
    if (!emitted.Contains("ApplyStyle(panel_0,\"preferences\")") || !emitted.Contains("Deserialize<string[]>") || !emitted.Contains("\"preferences\"")) throw new Exception("group style was not emitted");
});
Test("EXE file and product versions come from info.json", () =>
{
    var project = WpfEmitter.ProjectFile(new ProjectInfo("TXL Test", "2.3.4-beta", "Timexo", "Version test", "main.txl", "assets/app.ico", new WindowInfo()));
    if (!project.Contains("<FileVersion>2.3.4.0</FileVersion>") || !project.Contains("<InformationalVersion>2.3.4-beta</InformationalVersion>") || !project.Contains("<Product>TXL Test</Product>") || !project.Contains("content/assets/app.ico")) throw new Exception("version or icon metadata missing from generated project");
});
Test("checkbox is emitted enabled and hit-testable", () =>
{
    var parsed = Parser.Parse("group = prefs (\ncheckbox = Remember\n)");
    var output = WpfEmitter.Emit(new ProjectInfo("Test", "1.0.0", "TXL", "test", "main.txl", null, new WindowInfo()), parsed.Program!, ".");
    if (!output.Contains("IsThreeState=false") || !output.Contains("IsEnabled=true,IsHitTestVisible=true,Focusable=true")) throw new Exception("checkbox runtime flags missing");
});
Test("runtime state persists and math handles invalid input and divide by zero", () =>
{
    var parsed = Parser.Parse("state = result:0\nbutton = Calculate\naction = math(divide,left,right,result)");
    if (parsed.Program is null) throw new Exception(string.Join(";", parsed.Diagnostics));
    var output = WpfEmitter.Emit(new ProjectInfo("Test App", "1.0.0", "TXL", "test", "main.txl", null, new WindowInfo()), parsed.Program, ".");
    if (!output.Contains("state.json") || !output.Contains("LocalApplicationData") || !output.Contains("right!=0") || !output.Contains("Invalid input or division by zero")) throw new Exception("persistence or safe math path missing");
});
Test("todo list emits persistent completion and removal controls", () =>
{
    var parsed = Parser.Parse("state = tasks:[]\ninput = New task\nstate = task:\nlist = tasks\nbutton = Add\naction = listAdd(tasks,task)");
    if (parsed.Program is null) throw new Exception(string.Join(";", parsed.Diagnostics));
    var output = WpfEmitter.Emit(new ProjectInfo("Test", "1.0.0", "TXL", "test", "main.txl", null, new WindowInfo()), parsed.Program, ".");
    if (!output.Contains("RenderTodo") || !output.Contains("ChangeTodo") || !output.Contains("Remove") || !output.Contains("listadd") || !output.Contains("SetState(key,JsonSerializer.Serialize(items))")) throw new Exception("todo add/check/remove persistence path missing");
});
Test("input text is placeholder and explicit value is initial content", () =>
{
    var result = Parser.Parse("input = Search\ninput = Name\nvalue = Ada\nplaceholder = Your name");
    if (result.Program?.Nodes is not [Element { Kind: "input", Value: "", Properties: var hint }, Element { Kind: "input", Value: "Ada", Properties: var filled }]) throw new Exception(string.Join(";", result.Diagnostics));
    if (hint["placeholder"] != "Search" || filled["placeholder"] != "Your name") throw new Exception("placeholder semantics mismatch");
    var emitted = WpfEmitter.Emit(new ProjectInfo("Test", "1.0.0", "TXL", "test", "main.txl", null, new WindowInfo()), result.Program, ".");
    if (!emitted.Contains("Grid MakeInput") || !emitted.Contains("TextChanged") || !emitted.Contains("GetValueOrDefault(\"value\",\"\")")) throw new Exception("real hint or initial-value path missing");
});
Test("absolute layout applies explicit x and y coordinates", () =>
{
    var parsed = Parser.Parse("group = canvas (\nlayout = absolute\ntext = Positioned\nx = 42\ny = 18\n)");
    if (parsed.Program is null) throw new Exception(string.Join(";", parsed.Diagnostics));
    var emitted = WpfEmitter.Emit(new ProjectInfo("Test", "1.0.0", "TXL", "test", "main.txl", null, new WindowInfo()), parsed.Program, ".");
    if (!emitted.Contains("new Canvas") || !emitted.Contains("Canvas.SetLeft(child,x)") || !emitted.Contains("Canvas.SetTop(child,y)")) throw new Exception("Canvas positioning not emitted");
});
Test("components and compile-time list loops expand", () =>
{
    var source = "component = metricCard (\ntext = ${title}\ntext = ${value}\n)\nset = users:[Ada,Bob]\nfor = user in users (\nmetricCard(title: ${user}, value: ${index})\n)";
    var parsed = Parser.Parse(source);
    if (parsed.Program is null) throw new Exception(string.Join(";", parsed.Diagnostics));
    var emitted = WpfEmitter.Emit(new ProjectInfo("Test", "1.0.0", "TXL", "test", "main.txl", null, new WindowInfo()), parsed.Program, ".");
    if (!emitted.Contains("Ada") || !emitted.Contains("Bob") || !emitted.Contains("MakeElement")) throw new Exception("component/list expansion missing");
});
Test("elements and action", () =>
{
    var result = Parser.Parse("group = actions (\nbutton = Open\naction = alert(\"Hi\")\n)", "test.txl");
    if (result.Program?.Nodes[0] is not Group { Children: [Element { Kind: "button", Properties: var properties }] } || properties["action"] != "alert(\"Hi\")") throw new Exception(string.Join(";", result.Diagnostics));
});
Test("class click and hover actions parse", () =>
{
    var result = Parser.Parse("actions (\ncard(click):\nopen = https://example.com\ncard(hover):\nsize: 15\ncolor: red\n)\ntext(card) = Click me");
    if (result.Program?.Nodes.Count != 1 || ActionCatalog.Actions["card.click"].Single().Value != "https://example.com" || ActionCatalog.Actions["card.hover"].Count != 2)
        throw new Exception(string.Join(";", result.Diagnostics));
    var input = Parser.Parse("actions (\nfield(hover):\ncolor = red\n)\ninput(field) = Search");
    var output = WpfEmitter.Emit(new ProjectInfo("Test", "1.0.0", "TXL", "test", "main.txl", null, new WindowInfo()), input.Program!, ".");
    if (!output.Contains("hoverTarget=element is Grid inputGrid&&inputGrid.Tag is TextBox wrappedInput?wrappedInput:element")) throw new Exception("input hover style does not target the wrapped TextBox");
});
Test("variables, conditions, interpolation and repeats render", () =>
{
    var source = "set = enabled:true\nset = title:Hello\nif = enabled == true (\ntext = ${title}\n)\nif = false (\ntext = hidden\n)\nrepeat = 3 (\ntext = item-${index}\n)";
    var parsed = Parser.Parse(source);
    if (parsed.Program is null) throw new Exception(string.Join(";", parsed.Diagnostics));
    var output = WpfEmitter.Emit(new ProjectInfo("Test", "1.0.0", "TXL", "test", "main.txl", null, new WindowInfo()), parsed.Program, ".");
    if (!output.Contains("Hello") || output.Contains("hidden") || !output.Contains("item-0") || !output.Contains("item-1") || !output.Contains("item-2")) throw new Exception("compile-time variable/control-flow expansion failed");
});
Test("unknown command diagnostic", () =>
{
    var result = Parser.Parse("buton = nope");
    if (!result.Diagnostics.Any(d => d.Message.Contains("Unknown command"))) throw new Exception("missing diagnostic");
});
Test("missing close parenthesis diagnostic", () =>
{
    var result = Parser.Parse("group = x (\ntext = hi");
    if (!result.Diagnostics.Any(d => d.Message.Contains("Missing closing"))) throw new Exception("missing diagnostic");
});
Test("nested groups and horizontal layout", () =>
{
    var result = Parser.Parse("group = outer (\nlayout = horizontal\ngroup = inner (\ntext = x\n)\n)");
    if (result.Program?.Nodes[0] is not Group { Properties: var props, Children.Count: 1 } || props["layout"] != "horizontal") throw new Exception("nesting or layout lost");
});
Test("hash comments and blank lines", () =>
{
    var result = Parser.Parse("# heading\n\ntext = ok # inline comment");
    if (result.Program?.Nodes.Count != 1 || result.Program.Nodes[0] is not Element { Value: "ok" }) throw new Exception("comment parsing failed");
});
Test("valid info.json", () =>
{
    var directory = TempProject("1.2.3", "main.txl");
    try { var result = ProjectValidator.Validate(directory); if (result.Diagnostics.Any(d => d.Severity == Severity.Error)) throw new Exception(string.Join(";", result.Diagnostics)); }
    finally { Directory.Delete(directory, true); }
});
Test("path traversal rejected", () =>
{
    var directory = TempProject("1.2.3", "../main.txl");
    try { var result = ProjectValidator.Validate(directory); if (!result.Diagnostics.Any(d => d.Message.Contains("path", StringComparison.OrdinalIgnoreCase))) throw new Exception("traversal accepted"); }
    finally { Directory.Delete(directory, true); }
});
Test("invalid semver rejected", () =>
{
    var directory = TempProject("one", "main.txl");
    try { var result = ProjectValidator.Validate(directory); if (!result.Diagnostics.Any(d => d.Message.Contains("Invalid version"))) throw new Exception("version accepted"); }
    finally { Directory.Delete(directory, true); }
});
Console.WriteLine($"{passed} passed, {failed} failed.");
if (failed > 0) Environment.ExitCode = 1;

static string TempProject(string version, string start)
{
    var directory = Path.Combine(Path.GetTempPath(), "txl-test-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    File.WriteAllText(Path.Combine(directory, "main.txl"), "text = test\n");
    File.WriteAllText(Path.Combine(directory, "info.json"), $$"""{"name":"Test","version":"{{version}}","developer":"TXL","description":"test","start":"{{start}}"}""");
    return directory;
}
