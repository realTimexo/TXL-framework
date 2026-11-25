using System.Text.Json;
using Txl;

var passed = 0;
var failed = 0;
void Test(string name, Action action)
{
    try { action(); Console.WriteLine($"PASS {name}"); passed++; }
    catch (Exception e) { Console.Error.WriteLine($"FAIL {name}: {e.Message}"); failed++; }
}

Test("updater recognizes denied Windows TXL tool-store deletion and rejects unrelated errors", () =>
{
    const string english = "Failed to uninstall tool package 'txl.cli': Access to the path 'C:\\Users\\Mobile\\.dotnet\\tools\\.store\\txl.cli\\0.7.0' is denied.";
    const string german = "Fehler beim Deinstallieren des Toolpakets 'txl.cli': Der Zugriff auf den Pfad 'C:\\Users\\Mobile\\.dotnet\\tools\\.store\\txl.cli\\0.7.0' wurde verweigert.";
    if (!UpdateRecovery.IsWindowsToolStoreAccessDenied(english, true) || !UpdateRecovery.IsWindowsToolStoreAccessDenied(german, true)) throw new Exception("expected localized access-denied cache error was not recognized");
    if (UpdateRecovery.IsWindowsToolStoreAccessDenied("HTTP 404 from releases API", true) || UpdateRecovery.IsWindowsToolStoreAccessDenied(english, false)) throw new Exception("unrelated or non-Windows failure incorrectly triggered recovery");
});

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
    if (!output.Contains("RenderTodo") || !output.Contains("ChangeTodo") || !output.Contains("Remove") || !output.Contains("Add task") || !output.Contains("AddTodoItem(key,editor)") || !output.Contains("listadd") || !output.Contains("SetState(key,JsonSerializer.Serialize(items))")) throw new Exception("ready-to-use persistent todo add/check/remove path missing");
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
    if (!output.Contains("FrameworkElement target=element is Grid inputGrid&&inputGrid.Tag is TextBox wrappedInput?wrappedInput:element")) throw new Exception("input hover style does not target the wrapped TextBox");
});
Test("dotted headings retain both heading level and class", () =>
{
    var parsed = Parser.Parse("text.headline3(projectsTxt) = Andere Projekte");
    if (parsed.Program?.Nodes[0] is not Element { Kind: "text", Variant: "headline3", ClassName: "projectsTxt", Value: "Andere Projekte" }) throw new Exception(string.Join(";", parsed.Diagnostics));
    var emitted = WpfEmitter.Emit(new ProjectInfo("Test", "1.0.0", "TXL", "test", "main.txl", null, new WindowInfo()), parsed.Program, ".");
    if (!emitted.Contains("MakeElement(\"text\",\"headline3\",\"projectsTxt\"")) throw new Exception("the emitted control lost its independent class name");
});
Test("type-level hover and click actions resolve by control kind", () =>
{
    var parsed = Parser.Parse("actions (\nbutton(hover):\nbackground = #2b2b2b\ncursor = pointer\nbutton(click):\nopen = https://example.com\n)\nbutton = Open");
    if (parsed.Program is null || !ActionCatalog.Actions.ContainsKey("button.click")) throw new Exception(string.Join(";", parsed.Diagnostics));
    var emitted = WpfEmitter.Emit(new ProjectInfo("Test", "1.0.0", "TXL", "test", "main.txl", null, new WindowInfo()), parsed.Program, ".");
    if (!emitted.Contains("ApplyClassEvents(element,className,kind)") || !emitted.Contains("CursorFor(cursor)") || !emitted.Contains("Cursors.Hand") || !emitted.Contains("button.click")) throw new Exception("type action or cursor runtime wiring missing");
    if (!emitted.Contains("\"up\"=>Cursors.UpArrow")) throw new Exception("the valid up cursor did not map to WPF's UpArrow");
});
Test("unsupported cursor names receive a useful diagnostic", () =>
{
    var parsed = Parser.Parse("style (\nbutton:\ncursor = banana\n)\nbutton = Invalid");
    if (!parsed.Diagnostics.Any(d => d.Message.Contains("Cursor must"))) throw new Exception("invalid cursor value was accepted without guidance");
});

Test("functions accept parameters, return values, and run at compile time and in actions", () =>
{
    var source = "function = add(a,b) (\nreturn = ${a} + ${b}\n)\nset = total:add(5,7)\nstate = runtimeTotal:0\nactions (\ncalculate(click):\ncall = add,8,9,runtimeTotal\n)\nbutton(calculate) = Calculate\ntext = ${total}";
    var parsed = Parser.Parse(source);
    if (parsed.Program is null || FunctionCatalog.Definitions.Count != 1) throw new Exception(string.Join(";", parsed.Diagnostics));
    var emitted = WpfEmitter.Emit(new ProjectInfo("Test", "1.0.0", "TXL", "test", "main.txl", null, new WindowInfo()), parsed.Program, ".");
    if (!emitted.Contains("12") || !emitted.Contains("CallFunction(A(0),functionArgs)") || !emitted.Contains("case \"add\"")) throw new Exception("compile-time or runtime function call path missing");
    if (FunctionCatalog.InvokeCompileTime("add", new[] { "11", "4" }) != "15") throw new Exception("function argument binding/math failed");
});

Test("runtime actions preserve multiple arguments and provide JSON array/object operations", () =>
{
    var source = """
state = items:[]
state = profile:{}
state = selected:
actions (
add(click):
arrayAdd = items, "Ada, Lovelace"
objectSet = profile,name,"Ada, Lovelace"
arrayGet = items,0,selected
objectGet = profile,name,selected
fileWrite = notes.txt,Hello
fileRead = notes.txt,selected
fileAppend = notes.txt," world"
fileDelete = notes.txt
httpGet = https://example.com/data,result
httpPost = https://example.com/data,body,result
timerStart = clock,1000,ticks,increment
timerStop = clock
dbExecute = app.db,CREATE TABLE items (id INTEGER PRIMARY KEY, name TEXT)
dbQuery = app.db,SELECT * FROM items,rows
dbScalar = app.db,SELECT COUNT(*) FROM items,count
dbExecute = app.db,"INSERT INTO items(name) VALUES ($name)",name,personName
dbBegin = app.db
dbCommit = app.db
dbRollback = app.db
)
button(add) = Update
""";
    var parsed = Parser.Parse(source);
    if (parsed.Program is null) throw new Exception(string.Join(";", parsed.Diagnostics));
    var output = WpfEmitter.Emit(new ProjectInfo("Test", "1.0.0", "TXL", "test", "main.txl", null, new WindowInfo()), parsed.Program, ".");
    foreach (var fragment in new[] { "Action(name+\"(\"+value+\")\")", "ActionArgs(string body)", "as JsonArray", "as JsonObject", "case \"arrayadd\"", "case \"objectset\"", "DataValue(A(1))", "ResolveDataFile(string relative)", "File.WriteAllText(path", "File.AppendAllText(path", "File.ReadAllText(path)", "File.Delete(path)", "cannot follow symbolic links", "case \"filewrite\":case \"fileappend\"", "case \"timerstart\":case \"timerstop\"", "case \"dbexecute\":case \"dbquery\"", "async void Action(string action)", "async Task ExecuteAction(string action)", "await ExecuteAction(A(0))", "await httpClient.GetAsync(endpoint)", "await httpClient.PostAsync(endpoint,body)", "EnsureSuccessStatusCode()", "DispatcherTimer", "timerStart interval must be 50", "SetState(state,(current+1)", "Microsoft.Data.Sqlite", "SqliteConnection", "case \"dbquery\"", "Database parameters must be name,value pairs", "command.Parameters.Add(parameter)", "parameter.ParameterName=\"$\"+parameterName", "databaseTransactions", "BeginTransaction()", "active.Transaction.Commit()", "active.Transaction.Rollback()", "ExecuteReader()", "ExecuteScalar()" })
        if (!output.Contains(fragment)) throw new Exception("generated runtime is missing: " + fragment);
    if (!output.Contains("void ExecuteOperations(JsonElement operations,bool skipKey=false){foreach(var op in operations.EnumerateArray()){var name=op.GetProperty(\"Name\").GetString()??\"\";if(skipKey&&name.Equals(\"key\",StringComparison.OrdinalIgnoreCase))continue;var value=op.GetProperty(\"Value\").GetString()??\"\";Action(name+\"(\"+value+\")\");}}") || output.Contains("if(name.StartsWith(\"db\",StringComparison.OrdinalIgnoreCase))DatabaseOperation")) throw new Exception("element actions must use the common protected async action dispatcher");
});

Test("try/catch stores a thrown message and renders the catch branch", () =>
{
    var parsed = Parser.Parse("state = error:\ntry (\nthrow = Something went wrong\ntext = This is skipped\n)\ncatch = error (\ntext = Recovery UI\n)");
    if (parsed.Program is null) throw new Exception(string.Join(";", parsed.Diagnostics));
    var output = WpfEmitter.Emit(new ProjectInfo("Test", "1.0.0", "TXL", "test", "main.txl", null, new WindowInfo()), parsed.Program, ".");
    if (!output.Contains("try{throw new InvalidOperationException(\"Something went wrong\")") || !output.Contains("SetState(\"error\",__txlError_") || !output.Contains("Recovery UI")) throw new Exception("runtime try/catch code was not emitted");
    var outside = Parser.Parse("throw = Not allowed");
    if (!outside.Diagnostics.Any(d => d.Message.Contains("only appear inside a `try"))) throw new Exception("throw outside try was not diagnosed");
    var missingCatch = Parser.Parse("try (\ntext = no\n)");
    if (!missingCatch.Diagnostics.Any(d => d.Message.Contains("must be followed immediately"))) throw new Exception("try without catch was accepted");
});

Test("runtime action try/catch awaits HTTP and catches thrown action errors", () =>
{
    var parsed = Parser.Parse("state = response:\nstate = error:\nbutton = Request\naction = try(httpGet(https://example.org/api,response),error)\nbutton = Test throw\naction = try(throw(Test failure),error)");
    if (parsed.Program is null) throw new Exception(string.Join(";", parsed.Diagnostics));
    var output = WpfEmitter.Emit(new ProjectInfo("Test", "1.0.0", "TXL", "test", "main.txl", null, new WindowInfo()), parsed.Program, ".");
    if (!output.Contains("async Task ExecuteAction(string action)") || !output.Contains("await ExecuteAction(A(0))") || !output.Contains("case \"throw\":throw new InvalidOperationException") || !output.Contains("SetState(A(1),error.Message)")) throw new Exception("runtime action-level exception boundary was not generated");
});

Test("project-relative modules load functions before the entry page is parsed", () =>
{
    var directory = TempProject("1.0.0", "main.txl");
    try
    {
        Directory.CreateDirectory(Path.Combine(directory, "modules"));
        File.WriteAllText(Path.Combine(directory, "modules", "math.txl"), "function = double(value) (\nreturn = ${value} * 2\n)\n");
        const string source = "import = modules/math.txl\nset = total:double(21)\ntext = ${total}\n";
        File.WriteAllText(Path.Combine(directory, "main.txl"), source);
        Parser.ResetCatalogs();
        var moduleDiagnostics = ModuleLoader.LoadImports(directory, "main.txl");
        if (moduleDiagnostics.Any(d => d.Severity == Severity.Error)) throw new Exception(string.Join(";", moduleDiagnostics));
        var parsed = Parser.ParseAdditionalPage(source, "main.txl");
        if (parsed.Program is null) throw new Exception(string.Join(";", parsed.Diagnostics));
        var output = WpfEmitter.Emit(new ProjectInfo("Test", "1.0.0", "TXL", "test", "main.txl", null, new WindowInfo()), parsed.Program, directory);
        if (!output.Contains("42") || !output.Contains("case \"double\"")) throw new Exception("the imported function was not available to the entry page");
    }
    finally { Directory.Delete(directory, true); }
});

Test("module loader rejects project-root traversal and detects import cycles", () =>
{
    var directory = TempProject("1.0.0", "main.txl");
    try
    {
        var traversal = ModuleLoader.LoadImports(directory, "../outside.txl");
        if (!traversal.Any(d => d.Severity == Severity.Error && d.Message.Contains("project-relative"))) throw new Exception("project traversal was not rejected");
        var absolute = ModuleLoader.LoadImports(directory, "/outside.txl");
        if (!absolute.Any(d => d.Severity == Severity.Error && d.Message.Contains("project-relative"))) throw new Exception("absolute import was not rejected");
        Directory.CreateDirectory(Path.Combine(directory, "modules"));
        File.WriteAllText(Path.Combine(directory, "modules", "a.txl"), "import = modules/b.txl\n");
        File.WriteAllText(Path.Combine(directory, "modules", "b.txl"), "import = modules/a.txl\n");
        File.WriteAllText(Path.Combine(directory, "main.txl"), "import = modules/a.txl\ntext = ok\n");
        Parser.ResetCatalogs();
        var cyclic = ModuleLoader.LoadImports(directory, "main.txl");
        if (!cyclic.Any(d => d.Severity == Severity.Error && d.Message.Contains("cycle", StringComparison.OrdinalIgnoreCase))) throw new Exception("module cycle was not detected");
        var badSyntax = Parser.Parse("import = C:/outside.txl\ntext = no");
        if (!badSyntax.Diagnostics.Any(d => d.Severity == Severity.Error && d.Message.Contains("project-relative"))) throw new Exception("drive-qualified import was not rejected");
    }
    finally { Directory.Delete(directory, true); }
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
Test("dotted text.headline syntax aliases to headline1", () =>
{
    var parsed = Parser.Parse("text.headline = TXL headline");
    if (parsed.Program?.Nodes[0] is not Element { Kind: "text", Variant: "headline1", Value: "TXL headline" }) throw new Exception(string.Join(";", parsed.Diagnostics));
    var emitted = WpfEmitter.Emit(new ProjectInfo("Test", "1.0.0", "TXL", "test", "main.txl", null, new WindowInfo()), parsed.Program, ".");
    if (!emitted.Contains("\"headline1\"=>38") || !emitted.Contains("\"headline5\"=>18")) throw new Exception("headline level defaults were not emitted");
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
    if (!emitted.Contains("ApplyKeyboardEvents(element,className,kind)") || !emitted.Contains("Enum.TryParse<Key>")) throw new Exception("keyboard key event was not wired to the class");
});
Test("position syntax and video flags receive actionable diagnostics", () =>
{
    var badPosition = Parser.Parse("style (\ncard:\nposition = left top\n)\ntext = x");
    var badVideo = Parser.Parse("video = assets/clip.mp4\nautoplay = maybe");
    if (!badPosition.Diagnostics.Any(d => d.Message.Contains("Position must use")) || !badVideo.Diagnostics.Any(d => d.Message.Contains("must be true or false"))) throw new Exception("invalid property was not diagnosed");
});

Test("apostrophes in ordinary long text do not become quote delimiters", () =>
{
    const string sentence = "Let's build a simple TXL app; it doesn't need quoted text.";
    var parsed = Parser.Parse("text = " + sentence);
    if (parsed.Program?.Nodes[0] is not Element { Value: var value } || value != sentence) throw new Exception(string.Join(";", parsed.Diagnostics));
});
Test("if/else selects the matching compile-time branch", () =>
{
    var parsed = Parser.Parse("if = false (\ntext = wrong\n)\nelse (\ntext = right\n)");
    if (parsed.Program?.Nodes[0] is not Conditional { ElseChildren.Count: 1 }) throw new Exception(string.Join(";", parsed.Diagnostics));
    var emitted = WpfEmitter.Emit(new ProjectInfo("Test", "1.0.0", "TXL", "test", "main.txl", null, new WindowInfo()), parsed.Program, ".");
    if (emitted.Contains("wrong") || !emitted.Contains("right")) throw new Exception("else branch was not selected for false condition");
    var inline = Parser.Parse("if = true (\ntext = yes\n) else (\ntext = no\n)");
    if (inline.Program?.Nodes[0] is not Conditional { ElseChildren.Count: 1 }) throw new Exception("inline `) else (` syntax was not parsed");
    var orphan = Parser.Parse("else (\ntext = wrong\n)");
    if (!orphan.Diagnostics.Any(d => d.Message.Contains("must immediately follow"))) throw new Exception("dangling else was not diagnosed");
});
Test("headline1 through headline5 have ordered generated sizes", () =>
{
    var parsed = Parser.Parse("text.headline1 = A\ntext.headline2 = B\ntext.headline3 = C\ntext.headline4 = D\ntext.headline5 = E");
    if (parsed.Program?.Nodes.OfType<Element>().Select(e => e.Variant).ToArray() is not ["headline1", "headline2", "headline3", "headline4", "headline5"]) throw new Exception(string.Join(";", parsed.Diagnostics));
    var emitted = WpfEmitter.Emit(new ProjectInfo("Test", "1.0.0", "TXL", "test", "main.txl", null, new WindowInfo()), parsed.Program, ".");
    if (!new[] { "\"headline1\"=>38", "\"headline2\"=>32", "\"headline3\"=>26", "\"headline4\"=>21", "\"headline5\"=>18" }.All(emitted.Contains)) throw new Exception("one or more heading levels were omitted");
});
Test("style alignment, vertical group layout, rounded button fill, border and shadow are emitted", () =>
{
    var parsed = Parser.Parse("style (\nmain:\nalign = center\nbutton:\nbackground = red\nradius = 14\nborder = 2 #333333\nshadow = 4 12 #00000066\nmenu:\nlayout = vertical\nalign = center\n)\ngroup = menu (\nlayout = vertical\nalign = right\nbutton = One\nbutton = Two\n)");
    if (parsed.Program is null) throw new Exception(string.Join(";", parsed.Diagnostics));
    var emitted = WpfEmitter.Emit(new ProjectInfo("Test", "1.0.0", "TXL", "test", "main.txl", null, new WindowInfo()), parsed.Program, ".");
    if (!emitted.Contains("RoundedButtonTemplate(r)") || !emitted.Contains("TemplateBindingExtension(Control.BackgroundProperty)") || !emitted.Contains("HorizontalAlignment.Center") || !emitted.Contains("Orientation.Vertical") || !emitted.Contains("DropShadowEffect") || !emitted.Contains("BorderThickness=new Thickness(thickness)") || !emitted.Contains("__groupProps_panel_0")) throw new Exception("one or more style behaviors were not emitted");
});
Test("HTML and embed aliases generate a WPF browser with safe URL handling", () =>
{
    var parsed = Parser.Parse("embed = \"<h1>TXL</h1>\"\nhtml = https://example.com/embed.html");
    if (parsed.Program?.Nodes is not [Element { Kind: "html", Value: var inline }, Element { Kind: "html", Value: var remote }] || inline != "<h1>TXL</h1>" || remote != "https://example.com/embed.html") throw new Exception(string.Join(";", parsed.Diagnostics));
    var emitted = WpfEmitter.Emit(new ProjectInfo("Test", "1.0.0", "TXL", "test", "main.txl", null, new WindowInfo()), parsed.Program, ".");
    if (!emitted.Contains("FrameworkElement MakeHtml") || !emitted.Contains("NavigateToString(source)") || !emitted.Contains("Embedded HTML URLs must use HTTP or HTTPS") || !emitted.Contains("EnsureCoreWebView2Async") || !emitted.Contains("Microsoft Edge WebView2 Runtime")) throw new Exception("modern HTML rendering, URL validation, or missing-runtime guidance is missing");
});
Test("YouTube iframe content is rendered by the modern embedded WebView2 engine", () =>
{
    var parsed = Parser.Parse("html = \"<iframe src='https://www.youtube.com/embed/BSKHG42t89Q'></iframe>\"");
    if (parsed.Program is null) throw new Exception(string.Join(";", parsed.Diagnostics));
    var emitted = WpfEmitter.Emit(new ProjectInfo("Test", "1.0.0", "TXL", "test", "main.txl", null, new WindowInfo()), parsed.Program, ".");
    if (!emitted.Contains("Microsoft.Web.WebView2.Wpf") || !emitted.Contains("new WebView2") || !emitted.Contains("NavigateToString(source)") || emitted.Contains("legacy Windows HTML control cannot play modern YouTube embeds")) throw new Exception("YouTube iframe is not routed to the modern embedded HTML engine");
});

Test("mailto is accepted by the safe open action", () =>
{
    var parsed = Parser.Parse("actions (\ncontact(click):\nopen = mailto:hello@example.com\n)\nbutton(contact) = Email");
    if (parsed.Program is null) throw new Exception(string.Join(";", parsed.Diagnostics));
    var emitted = WpfEmitter.Emit(new ProjectInfo("Test", "1.0.0", "TXL", "test", "main.txl", null, new WindowInfo()), parsed.Program, ".");
    if (!emitted.Contains("uri.Scheme is \"https\" or \"http\" or \"mailto\"") || !emitted.Contains("mailto:hello@example.com")) throw new Exception("mailto scheme not emitted");
});
Test("page navigation compiles distinct page views and pushes current-page history", () =>
{
    var main = Parser.Parse("button = Business\naction = page(business.txl)", "main.txl");
    var business = Parser.ParseAdditionalPage("text.headline = Business page", "business.txl");
    if (main.Program is null || business.Program is null) throw new Exception(string.Join(";", main.Diagnostics.Concat(business.Diagnostics)));
    if (main.Program.Nodes.OfType<Element>().Single().Properties.GetValueOrDefault("action") != "page(business.txl)") throw new Exception("button page action was not attached to the button");
    var pages = new Dictionary<string, ProgramAst>(StringComparer.OrdinalIgnoreCase) { ["main.txl"] = main.Program, ["business.txl"] = business.Program };
    var emitted = WpfEmitter.Emit(new ProjectInfo("Test", "1.0.0", "TXL", "test", "main.txl", null, new WindowInfo()), main.Program, ".", pages);
    if (!emitted.Contains("ShowPage(\"main.txl\",false)") || !emitted.Contains("case \"business.txl\":{") || !emitted.Contains("Business page") || !emitted.Contains("history.Push(currentPage)") || !emitted.Contains("Page must be a project-relative .txl file") || !emitted.Contains("Action(action)")) throw new Exception("main page must be the only startup view; other compiled pages should be reached by action and retain safe back history");
});
Test("remote font URLs require HTTPS and explicit TTF or OTF extensions", () =>
{
    var good = Parser.Parse("style (\nmain:\nfont = https://fonts.example/lilita-one.regular.ttf\n)\ntext = Hi");
    if (good.Program is null) throw new Exception(string.Join(";", good.Diagnostics));
    var badHttp = Parser.Parse("style (\nmain:\nfont = http://fonts.example/font.ttf\n)\ntext = Hi");
    var badType = Parser.Parse("style (\nmain:\nfont = https://fonts.example/font.woff\n)\ntext = Hi");
    if (!badHttp.Diagnostics.Any(d => d.Message.Contains("Remote fonts must be HTTPS")) || !badType.Diagnostics.Any(d => d.Message.Contains("Remote fonts must be HTTPS"))) throw new Exception("unsafe font URL was accepted");
    var project = WpfEmitter.ProjectFile(new ProjectInfo("Test", "1.0.0", "TXL", "test", "main.txl", null, new WindowInfo()));
    if (!project.Contains("Resource Include=\"content/fonts/**/*\"")) throw new Exception("font resource package missing");
});
Test("VS Code starter includes a window block and foundational TXL structure", () =>
{
    var starterFile = Path.Combine(Directory.GetCurrentDirectory(), "VSCodeExtension", "extension.js");
    if (!File.Exists(starterFile)) throw new Exception("run the harness from the repository root");
    var extension = File.ReadAllText(starterFile);
    if (!extension.Contains("'window ('") || !extension.Contains("'style ('") || !extension.Contains("'actions ('") || !extension.Contains("'group = main ('")) throw new Exception("starter structure is incomplete");
    var grammarFile = Path.Combine(Directory.GetCurrentDirectory(), "VSCodeExtension", "syntaxes", "txl.tmLanguage.json");
    using var grammar = JsonDocument.Parse(File.ReadAllText(grammarFile));
    var grammarText = grammar.RootElement.ToString();
    if (!grammarText.Contains("text.html.basic") || !grammarText.Contains("htmlValue") || !grammarText.Contains("html|embed")) throw new Exception("quoted HTML values are missing embedded HTML syntax scopes");
    var packageFile = Path.Combine(Directory.GetCurrentDirectory(), "VSCodeExtension", "package.json");
    using var package = JsonDocument.Parse(File.ReadAllText(packageFile));
    var embedded = package.RootElement.GetProperty("contributes").GetProperty("grammars")[0].GetProperty("embeddedLanguages");
    if (embedded.GetProperty("meta.embedded.html.txl").GetString() != "html") throw new Exception("embedded HTML tokens are not mapped to the HTML language");
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
