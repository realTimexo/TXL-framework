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
Test("persistent state uses project identity instead of an overridden window title", () =>
{
    var parsed = Parser.Parse("window (\ntitle: Custom Display Title\n)\nstate = visits:0");
    if (parsed.Program is null) throw new Exception(string.Join(";", parsed.Diagnostics));
    var output = WpfEmitter.Emit(new ProjectInfo("Stable Project Name", "1.0.0", "TXL", "test", "main.txl", null, new WindowInfo()), parsed.Program, ".");
    if (!output.Contains("Custom Display Title") || !output.Contains("appName=m.GetProperty(\"Name\").GetString()??\"TXL\"") || !output.Contains("string.Concat(appName.Select") || output.Contains("string.Concat(Title!.Select")) throw new Exception("window title changed the persistent state folder");
});
Test("Enter key filters map to the WPF Return key", () =>
{
    var parsed = Parser.Parse("actions (\nsearch(keydown):\nkey = Enter\nalert = Submitted\n)\ninput(search) = Query");
    if (parsed.Program is null) throw new Exception(string.Join(";", parsed.Diagnostics));
    var output = WpfEmitter.Emit(new ProjectInfo("Test", "1.0.0", "TXL", "test", "main.txl", null, new WindowInfo()), parsed.Program, ".");
    if (!output.Contains("requested.Equals(\"Enter\",StringComparison.OrdinalIgnoreCase))requested=\"Return\"")) throw new Exception("Enter key name is not normalized for WPF");
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
Test("dotted text.headline syntax maps to large text", () =>
{
    var parsed = Parser.Parse("text.headline = TXL headline");
    if (parsed.Program?.Nodes[0] is not Element { Kind: "text", Variant: "headline", Value: "TXL headline" }) throw new Exception(string.Join(";", parsed.Diagnostics));
    var emitted = WpfEmitter.Emit(new ProjectInfo("Test", "1.0.0", "TXL", "test", "main.txl", null, new WindowInfo()), parsed.Program, ".");
    if (!emitted.Contains("variant is") || !emitted.Contains("headline") || !emitted.Contains("?30:16")) throw new Exception("headline default size was not emitted");
});
Test("window block overrides title icon size and mode", () =>
{
    var parsed = Parser.Parse("window (\ntitle: A TXL window\nicon: assets/app.ico\nsize: 1200x800\nmode: fullscreen\n)\ntext = Hello");
    if (parsed.Program is null || WindowCatalog.Settings["size"] != "1200x800") throw new Exception(string.Join(";", parsed.Diagnostics));
    var emitted = WpfEmitter.Emit(new ProjectInfo("Default", "1.0.0", "TXL", "test", "main.txl", null, new WindowInfo(900, 600)), parsed.Program, ".");
    if (!emitted.Contains("WindowStyle.None") || !emitted.Contains("WindowState=WindowState.Maximized") || !emitted.Contains("ResolveContentPath(icon)")) throw new Exception("fullscreen, icon, or title metadata runtime missing");
    if (!WpfEmitter.ProjectFile(new ProjectInfo("Default", "1.0.0", "TXL", "test", "main.txl", null, new WindowInfo())).Contains("content/assets/app.ico")) throw new Exception("windows icon was not packaged as app icon");
});
Test("window block rejects invalid mode and size", () =>
{
    var badMode = Parser.Parse("window (\nmode: tiny\n)\ntext = x");
    var badSize = Parser.Parse("window (\nsize: 100x100\n)\ntext = x");
    if (!badMode.Diagnostics.Any(d => d.Message.Contains("Window mode must")) || !badSize.Diagnostics.Any(d => d.Message.Contains("Window size must"))) throw new Exception("invalid window setting accepted");
});
Test("legacy plural windows block remains accepted", () =>
{
    var parsed = Parser.Parse("windows (\ntitle: Older project\n)\ntext = Compatible");
    if (parsed.Program is null || WindowCatalog.Settings.GetValueOrDefault("title") != "Older project") throw new Exception(string.Join(";", parsed.Diagnostics));
});
Test("image style size applies dimensions and URL is kept as a safe media URI", () =>
{
    var parsed = Parser.Parse("style (\nbrandImage:\nsize = 128\n)\ngroup = canvas (\nlayout = absolute\nimage(brandImage) = https://example.com/logo.png\nposition = x18 y3\n)");
    if (parsed.Program is null || parsed.Program.Nodes[0] is not Group { Children: [Element { Kind: "image", Value: "https://example.com/logo.png", Properties: var props }] } || props["position"] != "x18 y3") throw new Exception(string.Join(";", parsed.Diagnostics));
    var emitted = WpfEmitter.Emit(new ProjectInfo("Test", "1.0.0", "TXL", "test", "main.txl", null, new WindowInfo()), parsed.Program, ".");
    if (!emitted.Contains("image.Width=dimension;image.Height=dimension") || !emitted.Contains("Remote media URLs must use HTTP or HTTPS") || !emitted.Contains("x18 y3")) throw new Exception("image sizing, URL safety, or declarative position missing");
});
Test("video emits native playback controls and supports HTTP(S) assets", () =>
{
    var parsed = Parser.Parse("group = media (\nvideo = https://example.com/demo.mp4\nautoplay = false\nloop = true\n)");
    if (parsed.Program is null) throw new Exception(string.Join(";", parsed.Diagnostics));
    var emitted = WpfEmitter.Emit(new ProjectInfo("Test", "1.0.0", "TXL", "test", "main.txl", null, new WindowInfo()), parsed.Program, ".");
    if (!emitted.Contains("MediaElement") || !emitted.Contains("media.Play()") || !emitted.Contains("media.Pause()") || !emitted.Contains("LoadedBehavior=autoplay?MediaState.Play:MediaState.Manual")) throw new Exception("video player runtime was not generated");
});
Test("keydown class action filters keys and can execute runtime actions", () =>
{
    var parsed = Parser.Parse("actions (\nsearch(keydown):\nkey = Enter\nset = result,submitted\n)\ninput(search) = Search\nstate = result:");
    if (parsed.Program is null || ActionCatalog.Actions["search.keydown"].Count != 2) throw new Exception(string.Join(";", parsed.Diagnostics));
    var emitted = WpfEmitter.Emit(new ProjectInfo("Test", "1.0.0", "TXL", "test", "main.txl", null, new WindowInfo()), parsed.Program, ".");
    if (!emitted.Contains("ApplyKeyboardEvents(element,variant)") || !emitted.Contains("Enum.TryParse<Key>")) throw new Exception("keyboard key event was not wired to the class");
});
Test("position syntax and video flags receive actionable diagnostics", () =>
{
    var badPosition = Parser.Parse("style (\ncard:\nposition = left top\n)\ntext = x");
    var badVideo = Parser.Parse("video = assets/clip.mp4\nautoplay = maybe");
    if (!badPosition.Diagnostics.Any(d => d.Message.Contains("Position must use")) || !badVideo.Diagnostics.Any(d => d.Message.Contains("must be true or false"))) throw new Exception("invalid property was not diagnosed");
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
