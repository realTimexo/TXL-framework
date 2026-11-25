using System.Text.Json;
using System.Globalization;

namespace Txl;

public static class WpfEmitter
{
    private static int nodeId;
    public static string ProjectFile(ProjectInfo info)
    {
        var coreVersion = info.Version.Split('-', '+')[0];
        var numericVersion = Version.TryParse(coreVersion, out var parsedVersion)
            ? $"{parsedVersion.Major}.{Math.Max(parsedVersion.Minor, 0)}.{Math.Max(parsedVersion.Build, 0)}.0"
            : "1.0.0.0";
        var configuredIcon = WindowCatalog.Settings.GetValueOrDefault("icon", info.Icon ?? string.Empty);
        var icon = !string.IsNullOrWhiteSpace(configuredIcon) && Path.GetExtension(configuredIcon).Equals(".ico", StringComparison.OrdinalIgnoreCase)
            ? "content/" + configuredIcon.Replace('\\', '/')
            : "content/txl.ico";
        var assemblyName = string.Concat(info.Name.Select(c => char.IsLetterOrDigit(c) ? c : '_'));
        return $"""<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>WinExe</OutputType><TargetFramework>net9.0-windows</TargetFramework><UseWPF>true</UseWPF><EnableWindowsTargeting>true</EnableWindowsTargeting><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><AssemblyName>{Xml(assemblyName)}</AssemblyName><Version>{Xml(info.Version)}</Version><AssemblyVersion>{numericVersion}</AssemblyVersion><FileVersion>{numericVersion}</FileVersion><InformationalVersion>{Xml(info.Version)}</InformationalVersion><Product>{Xml(info.Name)}</Product><Company>{Xml(info.Developer)}</Company><Description>{Xml(info.Description)}</Description><ApplicationIcon Condition="Exists('{Xml(icon)}')">{Xml(icon)}</ApplicationIcon></PropertyGroup><ItemGroup><Content Include="content/**/*" Exclude="content/fonts/**/*" CopyToOutputDirectory="PreserveNewest" /><Content Remove="content/**/*.ico" /><Resource Include="content/fonts/**/*" /><PackageReference Include="Microsoft.Data.Sqlite" Version="9.0.0" /></ItemGroup></Project>""";
    }

    private static string Xml(string value) => System.Security.SecurityElement.Escape(value) ?? string.Empty;

    public static string Emit(ProjectInfo info, ProgramAst ast, string project, IReadOnlyDictionary<string, ProgramAst>? pages = null)
    {
        nodeId = 0;
        pages ??= new Dictionary<string, ProgramAst>(StringComparer.OrdinalIgnoreCase) { [info.Start.Replace('\\', '/')] = ast };
        var pageCases = RenderPageCases(pages);
        var windowSettings = WindowCatalog.Settings;
        var size = windowSettings.GetValueOrDefault("size", $"{info.Window.Width}x{info.Window.Height}").Split('x');
        var width = int.TryParse(size.ElementAtOrDefault(0), out var configuredWidth) ? configuredWidth : info.Window.Width;
        var height = int.TryParse(size.ElementAtOrDefault(1), out var configuredHeight) ? configuredHeight : info.Window.Height;
        var meta = JsonSerializer.Serialize(new { info.Name, Title = windowSettings.GetValueOrDefault("title", info.Window.Title ?? info.Name), Icon = windowSettings.GetValueOrDefault("icon", info.Icon ?? string.Empty), Width = width, Height = height, info.Window.Resizable, info.Window.Background, Mode = windowSettings.GetValueOrDefault("mode", info.Window.Mode) });
        var metaLiteral = JsonSerializer.Serialize(meta);
        var stylesLiteral = JsonSerializer.Serialize(JsonSerializer.Serialize(StyleCatalog.Styles));
        var actionsLiteral = JsonSerializer.Serialize(JsonSerializer.Serialize(ActionCatalog.Actions));
        var initialStateLiteral = JsonSerializer.Serialize(JsonSerializer.Serialize(RuntimeStateCatalog.Initial));
        var functionCases = RenderFunctionCases();
        var template = """
using System.Windows;using System.Windows.Controls;using System.Windows.Controls.Primitives;using System.Windows.Media;using System.Windows.Media.Effects;using System.Windows.Markup;using System.Windows.Input;using System.Windows.Threading;using System.Diagnostics;using System.IO;using System.Net.Http;using System.Text.Json;using System.Text.Json.Nodes;using System.Globalization;using System.Text.RegularExpressions;using Microsoft.Data.Sqlite;
namespace TxGenerated;
public class App:Application { [STAThread] public static void Main(){new App().Run(new MainWindow());} }
public class MainWindow:Window {
 const string Meta={{metaLiteral}}; const string Styles={{stylesLiteral}}; const string Actions={{actionsLiteral}}; const string InitialState={{initialStateLiteral}}; string appName="TXL"; string currentPage=""; readonly HttpClient httpClient=new(){Timeout=TimeSpan.FromSeconds(30)}; readonly Dictionary<string,DispatcherTimer> timers=new(StringComparer.OrdinalIgnoreCase); readonly Dictionary<string,(SqliteConnection Connection,SqliteTransaction Transaction)> databaseTransactions=new(StringComparer.OrdinalIgnoreCase); readonly Stack<string> history=new(); readonly Grid shell=new(); readonly JsonDocument styleDocument=JsonDocument.Parse(Styles); readonly JsonDocument actionDocument=JsonDocument.Parse(Actions); readonly Dictionary<string,string> appState=JsonSerializer.Deserialize<Dictionary<string,string>>(InitialState)!; readonly Dictionary<string,List<Action>> stateWatchers=new(StringComparer.OrdinalIgnoreCase); readonly Dictionary<string,List<StackPanel>> todoViews=new(StringComparer.OrdinalIgnoreCase);
 public MainWindow(){var m=JsonDocument.Parse(Meta).RootElement;appName=m.GetProperty("Name").GetString()??"TXL";Title=m.GetProperty("Title").GetString();Width=m.GetProperty("Width").GetDouble();Height=m.GetProperty("Height").GetDouble();ResizeMode=m.GetProperty("Resizable").GetBoolean()?ResizeMode.CanResize:ResizeMode.NoResize;Background=Brush(m.GetProperty("Background").GetString()!);var mode=m.GetProperty("Mode").GetString()?.ToLowerInvariant()??"normal";if(mode=="maximized")WindowState=WindowState.Maximized;else if(mode=="fullscreen"){WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;WindowState=WindowState.Maximized;}else if(mode=="borderless"){WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;}var icon=m.GetProperty("Icon").GetString();if(!string.IsNullOrWhiteSpace(icon)){try{Icon=new System.Windows.Media.Imaging.BitmapImage(new Uri(ResolveContentPath(icon)));}catch{}}var main=GetStyle("main");if(main.ValueKind==JsonValueKind.Object&&main.TryGetProperty("background",out var bg))Background=Brush(bg.GetString()!);LoadState();Content=shell;ShowPage({{startFile}},false);}
 string StateFile=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TXL",string.Concat(appName.Select(c=>char.IsLetterOrDigit(c)||c is '-' or '_'?c:'_')),"state.json"); string GetState(string key,string fallback="")=>appState.GetValueOrDefault(key,fallback); string ResolveDataFile(string relative){if(string.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative)||relative.Contains(':')||relative.Split('/','\\').Any(part=>part is "" or "." or ".."))throw new InvalidOperationException("File path must be relative and stay inside the app data folder.");var root=Path.GetDirectoryName(StateFile)!;var full=Path.GetFullPath(Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar)));var rel=Path.GetRelativePath(root,full);if(Path.IsPathRooted(rel)||rel==".."||rel.StartsWith(".."+Path.DirectorySeparatorChar,StringComparison.Ordinal))throw new InvalidOperationException("File path escapes the app data folder.");var current=root;foreach(var part in rel.Split(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)){current=Path.Combine(current,part);FileSystemInfo item=Directory.Exists(current)?new DirectoryInfo(current):new FileInfo(current);if(item.Exists&&item.LinkTarget!=null)throw new InvalidOperationException("App data file operations cannot follow symbolic links.");}return full;}
 string CallFunction(string name,string[] args){switch(name.ToLowerInvariant()){ {{functionCases}} default:throw new InvalidOperationException("Unknown TXL function '"+name+"'.");}}
 string EvaluateFunctionExpression(string expression,Dictionary<string,string> locals){var expanded=Regex.Replace(expression,@"\$\{([A-Za-z_][\w]*)\}",match=>locals.GetValueOrDefault(match.Groups[1].Value,""));var match=Regex.Match(expanded.Trim(),@"^(-?(?:\d+(?:\.\d*)?|\.\d+))\s*([+*/-])\s*(-?(?:\d+(?:\.\d*)?|\.\d+))$");if(!match.Success||!double.TryParse(match.Groups[1].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out var left)||!double.TryParse(match.Groups[3].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out var right))return expanded;var result=match.Groups[2].Value switch{"+"=>left+right,"-"=>left-right,"*"=>left*right,"/" when right!=0=>left/right,"/"=>throw new DivideByZeroException("Function divided by zero."),_=>double.NaN};return double.IsFinite(result)?result.ToString("G",CultureInfo.InvariantCulture):throw new InvalidOperationException("Function returned an invalid number.");}
 void SetState(string key,string value){if(GetState(key)==value)return;appState[key]=value;foreach(var update in stateWatchers.GetValueOrDefault(key)??new List<Action>())update();foreach(var view in todoViews.GetValueOrDefault(key)??new List<StackPanel>())RenderTodo(key,view);SaveState();}
 void WatchState(string key,Action update){if(!stateWatchers.TryGetValue(key,out var list))stateWatchers[key]=list=new List<Action>();list.Add(update);update();}
 void LoadState(){try{if(File.Exists(StateFile)){var saved=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(StateFile));if(saved!=null)foreach(var pair in saved)appState[pair.Key]=pair.Value;}}catch{}}
 void SaveState(){try{Directory.CreateDirectory(Path.GetDirectoryName(StateFile)!);File.WriteAllText(StateFile,JsonSerializer.Serialize(appState));}catch(Exception e){MessageBox.Show(this,"Could not save local app data: "+e.Message,Title);}}
 protected override void OnClosed(EventArgs e){foreach(var timer in timers.Values)timer.Stop();foreach(var transaction in databaseTransactions.Values){try{transaction.Transaction.Rollback();}catch{}transaction.Transaction.Dispose();transaction.Connection.Dispose();}databaseTransactions.Clear();SaveState();base.OnClosed(e);}
 Brush Brush(string s){try{return (Brush)new BrushConverter().ConvertFromString(s)!;}catch{return Brushes.Transparent;}}
 Cursor CursorFor(string value)=>value.ToLowerInvariant() switch{"pointer" or "hand"=>Cursors.Hand,"arrow"=>Cursors.Arrow,"appstarting"=>Cursors.AppStarting,"cross"=>Cursors.Cross,"help"=>Cursors.Help,"text" or "ibeam"=>Cursors.IBeam,"no"=>Cursors.No,"sizeall"=>Cursors.SizeAll,"sizenesw"=>Cursors.SizeNESW,"sizens"=>Cursors.SizeNS,"sizenwse"=>Cursors.SizeNWSE,"sizewe"=>Cursors.SizeWE,"up"=>Cursors.UpArrow,"wait"=>Cursors.Wait,"none"=>Cursors.None,"pen"=>Cursors.Pen,"scrollall"=>Cursors.ScrollAll,"scrolln"=>Cursors.ScrollN,"scrollne"=>Cursors.ScrollNE,"scrollns"=>Cursors.ScrollNS,"scrollnw"=>Cursors.ScrollNW,"scrolls"=>Cursors.ScrollS,"scrollse"=>Cursors.ScrollSE,"scrollsw"=>Cursors.ScrollSW,"scrollw"=>Cursors.ScrollW,"scrollwe"=>Cursors.ScrollWE,_=>Cursors.Arrow};
 JsonElement GetStyle(string name)=>styleDocument.RootElement.TryGetProperty(name,out var style)?style:default;
 string ContentRoot=>Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"content"));
 string ResolveContentPath(string file){var root=ContentRoot;var full=Path.GetFullPath(Path.Combine(root,file));var relative=Path.GetRelativePath(root,full);if(Path.IsPathRooted(relative)||relative==".."||relative.StartsWith(".."+Path.DirectorySeparatorChar,StringComparison.Ordinal))throw new InvalidOperationException("TXL content path must stay inside the application content folder.");return full;}
 void ShowPage(string file,bool push=true){try{var key=file.Replace('\\','/').TrimStart('/');if(string.IsNullOrWhiteSpace(key)||key.Split('/').Any(part=>part is "" or "." or "..")||!key.EndsWith(".txl",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Page must be a project-relative .txl file.");key=key.ToLowerInvariant();if(push&&currentPage.Length>0&&!currentPage.Equals(key,StringComparison.OrdinalIgnoreCase))history.Push(currentPage);shell.Children.Clear();var stack=new StackPanel{Margin=new Thickness(20)};switch(key){ {{pageCases}} default:throw new FileNotFoundException("TXL page was not compiled into this project: "+file); }shell.Children.Add(new ScrollViewer{Content=stack});currentPage=key;}catch(Exception ex){MessageBox.Show(ex.Message,"TXL navigation");}}
 FrameworkElement MakeElement(string kind,string variant,string className,string value,Dictionary<string,string> props,string[] groupStyles){FrameworkElement element=kind switch {"text"=>MakeText(value,props,variant),"button"=>MakeButton(value,props),"input"=>MakeInput(value,props),"checkbox"=>MakeCheckbox(value,props),"list"=>MakeTodoList(value),"image"=>new Image{Source=ImageSource(value),MaxHeight=320,MaxWidth=480,Stretch=Stretch.Uniform,Margin=new Thickness(4)},"html"=>MakeHtml(value,props),"video"=>MakeVideo(value,props),"line"=>new Separator{Margin=new Thickness(4)},"space"=>new Border{Height=double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out var h)?h:20},_=>new TextBlock{Text=value}};var styleNames=new[]{"main",kind}.Concat(groupStyles).Concat(string.IsNullOrWhiteSpace(variant)?Array.Empty<string>():new[]{variant}).Concat(string.IsNullOrWhiteSpace(className)?Array.Empty<string>():new[]{className}).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();foreach(var styleName in styleNames)ApplyStyle(element,styleName);if(element is Image image)ApplyImageDimensions(image,props,styleNames);if(element is not Button&&NeedsFrame(styleNames)){var margin=element.Margin;element.Margin=new Thickness(0);var frame=new Border{Child=element,Margin=margin};foreach(var styleName in styleNames)ApplyStyle(frame,styleName);element=frame;}ApplyClassEvents(element,className,kind);ApplyKeyboardEvents(element,className,kind);return element;}
 TextBlock MakeText(string value,Dictionary<string,string> props,string variant){var size=variant switch{"hl" or "headline" or "headline1"=>38,"headline2"=>32,"headline3"=>26,"headline4"=>21,"headline5"=>18,_=>16};var text=new TextBlock{Text=value,FontSize=size,Margin=new Thickness(4)};if(props.TryGetValue("bind",out var key))WatchState(key,()=>text.Text=GetState(key,value));return text;}
 bool Checked(Dictionary<string,string> p)=>p.TryGetValue("checked",out var value)&&bool.TryParse(value,out var result)&&result;
 Grid MakeInput(string placeholder,Dictionary<string,string> props){var key=props.GetValueOrDefault("bind");var box=new TextBox{Text=key!=null?GetState(key,props.GetValueOrDefault("value","")):props.GetValueOrDefault("value",""),MinWidth=220,Margin=new Thickness(4)};var hint=new TextBlock{Text=props.GetValueOrDefault("placeholder",placeholder),Foreground=Brush("#777777"),Margin=new Thickness(9,4,4,4),VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false};var grid=new Grid{Tag=box};grid.Children.Add(box);grid.Children.Add(hint);void Update(){hint.Visibility=box.Text.Length==0&&!box.IsKeyboardFocused?Visibility.Visible:Visibility.Collapsed;}box.TextChanged+=(_,__)=>{Update();if(key!=null)SetState(key,box.Text);};box.GotKeyboardFocus+=(_,__)=>Update();box.LostKeyboardFocus+=(_,__)=>Update();if(key!=null)WatchState(key,()=>{var next=GetState(key);if(box.Text!=next)box.Text=next;});Update();return grid;}
 FrameworkElement MakeVideo(string source,Dictionary<string,string> props){var autoplay=!bool.TryParse(props.GetValueOrDefault("autoplay","true"),out var auto)||auto;var media=new MediaElement{Source=MediaUri(source),Width=double.TryParse(props.GetValueOrDefault("width"),NumberStyles.Float,CultureInfo.InvariantCulture,out var w)?w:480,Height=double.TryParse(props.GetValueOrDefault("height"),NumberStyles.Float,CultureInfo.InvariantCulture,out var h)?h:270,Stretch=Stretch.Uniform,LoadedBehavior=autoplay?MediaState.Play:MediaState.Manual,UnloadedBehavior=MediaState.Stop};if(bool.TryParse(props.GetValueOrDefault("loop","false"),out var loop)&&loop)media.MediaEnded+=(_,__)=>{media.Position=TimeSpan.Zero;media.Play();};var controls=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};var play=new Button{Content="Play",Margin=new Thickness(4)};var pause=new Button{Content="Pause",Margin=new Thickness(4)};play.Click+=(_,__)=>media.Play();pause.Click+=(_,__)=>media.Pause();controls.Children.Add(play);controls.Children.Add(pause);var host=new StackPanel{Margin=new Thickness(4)};host.Children.Add(media);host.Children.Add(controls);return host;}
 FrameworkElement YoutubeFallback(string id,double width,double height){var url="https://www.youtube.com/watch?v="+Uri.EscapeDataString(id);var panel=new StackPanel{VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};panel.Children.Add(new TextBlock{Text="YouTube video",FontSize=22,FontWeight=FontWeights.SemiBold,Foreground=Brush("#F4F7FB"),HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(4)});panel.Children.Add(new TextBlock{Text="This app's legacy Windows HTML control cannot play modern YouTube embeds. Open this video in your default browser.",TextWrapping=TextWrapping.Wrap,Foreground=Brush("#C8CDD8"),MaxWidth=Math.Max(240,width-40),TextAlignment=TextAlignment.Center,Margin=new Thickness(8)});var open=new Button{Content="Open video in browser",Padding=new Thickness(14,8,14,8),Margin=new Thickness(8),Cursor=Cursors.Hand,HorizontalAlignment=HorizontalAlignment.Center};open.Click+=(_,__)=>Process.Start(new ProcessStartInfo(url){UseShellExecute=true});panel.Children.Add(open);return new Border{Width=width,Height=height,Background=Brush("#191B22"),BorderBrush=Brush("#343844"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10),Padding=new Thickness(14),Margin=new Thickness(4),Child=panel};}
 string? YoutubeVideoId(string content){var match=Regex.Match(content,@"(?:youtube(?:-nocookie)?\.com/(?:embed/|watch\?(?:[^#\s]*&)?v=)|youtu\.be/)(?<id>[A-Za-z0-9_-]{6,})",RegexOptions.IgnoreCase);return match.Success?match.Groups["id"].Value:null;}
 FrameworkElement MakeHtml(string source,Dictionary<string,string> props){var width=double.TryParse(props.GetValueOrDefault("width"),NumberStyles.Float,CultureInfo.InvariantCulture,out var w)?w:640;var height=double.TryParse(props.GetValueOrDefault("height"),NumberStyles.Float,CultureInfo.InvariantCulture,out var h)?h:360;if(string.IsNullOrWhiteSpace(source))return new WebBrowser{Width=width,Height=height,Margin=new Thickness(4)};var directId=YoutubeVideoId(source);if(directId!=null)return YoutubeFallback(directId,width,height);if(source.TrimStart().StartsWith("<",StringComparison.Ordinal)){var inlineId=YoutubeVideoId(source);if(inlineId!=null)return YoutubeFallback(inlineId,width,height);var inline=new WebBrowser{Width=width,Height=height,Margin=new Thickness(4)};inline.NavigateToString(source);return inline;}if(Uri.TryCreate(source,UriKind.Absolute,out var uri)){if(uri.Scheme is "http" or "https"){var remoteId=YoutubeVideoId(uri.AbsoluteUri);if(remoteId!=null)return YoutubeFallback(remoteId,width,height);var remote=new WebBrowser{Width=width,Height=height,Margin=new Thickness(4)};remote.Navigate(uri);return remote;}throw new InvalidOperationException("Embedded HTML URLs must use HTTP or HTTPS.");}var file=ResolveContentPath(source);if(File.Exists(file)){var content=File.ReadAllText(file);var fileId=YoutubeVideoId(content);if(fileId!=null)return YoutubeFallback(fileId,width,height);}var browser=new WebBrowser{Width=width,Height=height,Margin=new Thickness(4)};browser.Navigate(new Uri(file));return browser;}
 Uri MediaUri(string path){if(Uri.TryCreate(path,UriKind.Absolute,out var uri)){if(uri.Scheme is "http" or "https")return uri;throw new InvalidOperationException("Remote media URLs must use HTTP or HTTPS.");}return new Uri(ResolveContentPath(path));}
 void ApplyImageDimensions(Image image,Dictionary<string,string> props,string[] styleNames){foreach(var name in styleNames){var style=GetStyle(name);if(style.ValueKind!=JsonValueKind.Object)continue;if(style.TryGetProperty("size",out var size)&&double.TryParse(size.GetString(),NumberStyles.Float,CultureInfo.InvariantCulture,out var dimension)){image.Width=dimension;image.Height=dimension;}if(style.TryGetProperty("width",out var width)&&double.TryParse(width.GetString(),NumberStyles.Float,CultureInfo.InvariantCulture,out var w))image.Width=w;if(style.TryGetProperty("height",out var height)&&double.TryParse(height.GetString(),NumberStyles.Float,CultureInfo.InvariantCulture,out var h))image.Height=h;}if(double.TryParse(props.GetValueOrDefault("width"),NumberStyles.Float,CultureInfo.InvariantCulture,out var propWidth))image.Width=propWidth;if(double.TryParse(props.GetValueOrDefault("height"),NumberStyles.Float,CultureInfo.InvariantCulture,out var propHeight))image.Height=propHeight;if(double.TryParse(props.GetValueOrDefault("size"),NumberStyles.Float,CultureInfo.InvariantCulture,out var propSize)){image.Width=propSize;image.Height=propSize;}}
 CheckBox MakeCheckbox(string label,Dictionary<string,string> props){var key=props.GetValueOrDefault("bind");var box=new CheckBox{Content=label,IsChecked=key!=null?bool.TryParse(GetState(key,"false"),out var state)&&state:Checked(props),IsThreeState=false,ClickMode=ClickMode.Release,Margin=new Thickness(4),IsEnabled=true,IsHitTestVisible=true,Focusable=true,Cursor=Cursors.Hand};if(key!=null){box.Checked+=(_,__)=>SetState(key,"true");box.Unchecked+=(_,__)=>SetState(key,"false");WatchState(key,()=>box.IsChecked=bool.TryParse(GetState(key,"false"),out var current)&&current);}return box;}
 sealed record TodoEntry(string text,bool done);
 List<TodoEntry> ReadTodos(string key){try{return JsonSerializer.Deserialize<List<TodoEntry>>(GetState(key,"[]"))??new();}catch{return new();}}
 StackPanel MakeTodoList(string key){var root=new StackPanel{Margin=new Thickness(4)};var composer=new DockPanel{Margin=new Thickness(2)};var editor=new TextBox{MinWidth=220,Margin=new Thickness(0,0,8,0),VerticalContentAlignment=VerticalAlignment.Center};var hint=new TextBlock{Text="Add a task...",Foreground=Brush("#777777"),Margin=new Thickness(8,4,4,4),VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false};var editorHost=new Grid{Tag=editor};editorHost.Children.Add(editor);editorHost.Children.Add(hint);void UpdateHint(){hint.Visibility=editor.Text.Length==0&&!editor.IsKeyboardFocused?Visibility.Visible:Visibility.Collapsed;}editor.TextChanged+=(_,__)=>UpdateHint();editor.GotKeyboardFocus+=(_,__)=>UpdateHint();editor.LostKeyboardFocus+=(_,__)=>UpdateHint();var add=new Button{Content="Add task",Padding=new Thickness(12,6,12,6),Cursor=Cursors.Hand};add.Click+=(_,__)=>AddTodoItem(key,editor);DockPanel.SetDock(add,Dock.Right);composer.Children.Add(add);composer.Children.Add(editorHost);root.Children.Add(composer);var view=new StackPanel{Margin=new Thickness(0,4,0,0)};if(!todoViews.TryGetValue(key,out var views))todoViews[key]=views=new();views.Add(view);root.Children.Add(view);UpdateHint();RenderTodo(key,view);return root;}
 void AddTodoItem(string key,TextBox editor){var text=editor.Text.Trim();if(text.Length==0)return;var todos=ReadTodos(key);todos.Add(new TodoEntry(text,false));SetState(key,JsonSerializer.Serialize(todos));editor.Clear();}
 void RenderTodo(string key,StackPanel view){view.Children.Clear();var todos=ReadTodos(key);for(var i=0;i<todos.Count;i++){var index=i;var row=new DockPanel{Margin=new Thickness(2)};var check=new CheckBox{Content=todos[i].text,IsChecked=todos[i].done,IsThreeState=false,VerticalAlignment=VerticalAlignment.Center,Focusable=true,IsHitTestVisible=true};check.Checked+=(_,__)=>ChangeTodo(key,index,t=>t with{done=true});check.Unchecked+=(_,__)=>ChangeTodo(key,index,t=>t with{done=false});var remove=new Button{Content="Remove",Margin=new Thickness(6,0,0,0),Padding=new Thickness(8,3,8,3)};DockPanel.SetDock(remove,Dock.Right);remove.Click+=(_,__)=>{var items=ReadTodos(key);if(index<items.Count){items.RemoveAt(index);SetState(key,JsonSerializer.Serialize(items));}};row.Children.Add(remove);row.Children.Add(check);view.Children.Add(row);}}
 void ChangeTodo(string key,int index,Func<TodoEntry,TodoEntry> edit){var items=ReadTodos(key);if(index<items.Count){items[index]=edit(items[index]);SetState(key,JsonSerializer.Serialize(items));}}
 void DatabaseOperation(string name,List<string> args){
  string A(int i)=>i<args.Count?args[i]:"";
  var path=ResolveDataFile(ArgValue(A(0)));
  var key=Path.GetFullPath(path);
  var operation=name.ToLowerInvariant();
  if(operation=="dbbegin"){
   if(databaseTransactions.ContainsKey(key))throw new InvalidOperationException("A transaction is already active for this database.");
   Directory.CreateDirectory(Path.GetDirectoryName(path)!);var txConnection=new SqliteConnection("Data Source="+path);txConnection.Open();databaseTransactions[key]=(txConnection,txConnection.BeginTransaction());return;
  }
  if(operation is "dbcommit" or "dbrollback"){
   if(!databaseTransactions.Remove(key,out var active))throw new InvalidOperationException("No active transaction exists for this database.");
   try{if(operation=="dbcommit")active.Transaction.Commit();else active.Transaction.Rollback();}
   finally{active.Transaction.Dispose();active.Connection.Dispose();}return;
  }
  Directory.CreateDirectory(Path.GetDirectoryName(path)!);
  databaseTransactions.TryGetValue(key,out var transaction);
  SqliteConnection? ownedConnection=null;
  var connection=transaction.Connection;
  if(connection is null){ownedConnection=new SqliteConnection("Data Source="+path);ownedConnection.Open();connection=ownedConnection;}
  try{
   using var command=connection.CreateCommand();command.CommandText=ArgValue(A(1));if(transaction.Transaction is not null)command.Transaction=transaction.Transaction;
   var firstParameterIndex=operation=="dbexecute"?2:3;if((args.Count-firstParameterIndex)%2!=0)throw new InvalidOperationException("Database parameters must be name,value pairs after the SQL statement and result state.");
   for(var parameterIndex=firstParameterIndex;parameterIndex<args.Count;parameterIndex+=2){var parameterName=ArgValue(A(parameterIndex));if(!Regex.IsMatch(parameterName,@"^[A-Za-z_][\w]*$"))throw new InvalidOperationException("Database parameter names must be simple identifiers such as name.");var parameter=command.CreateParameter();parameter.ParameterName="$"+parameterName;parameter.Value=ArgValue(A(parameterIndex+1));command.Parameters.Add(parameter);}
   switch(operation){
    case "dbexecute":command.ExecuteNonQuery();break;
    case "dbscalar":SetState(A(2),command.ExecuteScalar()?.ToString()??"");break;
    case "dbquery":using(var reader=command.ExecuteReader()){var rows=new JsonArray();while(reader.Read()){var row=new JsonObject();for(var column=0;column<reader.FieldCount;column++){var value=reader.IsDBNull(column)?null:reader.GetValue(column);row[reader.GetName(column)]=value is null?null:JsonSerializer.SerializeToNode(value,value.GetType());}rows.Add(row);}SetState(A(2),rows.ToJsonString());}break;
    default:throw new InvalidOperationException("Unknown database operation.");
   }
  }finally{ownedConnection?.Dispose();}
 }
 void TimerOperation(string name,List<string> args){string A(int i)=>i<args.Count?args[i]:"";var key=ArgValue(A(0));if(name.Equals("timerstop",StringComparison.OrdinalIgnoreCase)){if(timers.Remove(key,out var existing))existing.Stop();return;}if(!int.TryParse(ArgValue(A(1)),out var milliseconds)||milliseconds is < 50 or > 86400000)throw new InvalidOperationException("timerStart interval must be 50–86400000 milliseconds.");if(timers.Remove(key,out var old))old.Stop();var state=ArgValue(A(2));var update=ArgValue(A(3));var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(milliseconds)};timer.Tick+=(_,__)=>{if(update.Equals("increment",StringComparison.OrdinalIgnoreCase)){var current=long.TryParse(GetState(state),out var number)?number:0;SetState(state,(current+1).ToString(CultureInfo.InvariantCulture));}else if(update.Equals("toggle",StringComparison.OrdinalIgnoreCase))SetState(state,GetState(state).Equals("true",StringComparison.OrdinalIgnoreCase)?"false":"true");else SetState(state,update);};timers[key]=timer;timer.Start();}
 void FileOperation(string name,List<string> args){string A(int i)=>i<args.Count?args[i]:"";var path=ResolveDataFile(ArgValue(A(0)));switch(name.ToLowerInvariant()){case "filewrite":case "fileappend":Directory.CreateDirectory(Path.GetDirectoryName(path)!);if(name.Equals("filewrite",StringComparison.OrdinalIgnoreCase))File.WriteAllText(path,ArgValue(A(1)));else File.AppendAllText(path,ArgValue(A(1)));break;case "fileread":if(!File.Exists(path))throw new FileNotFoundException("TXL data file does not exist.",path);SetState(A(1),File.ReadAllText(path));break;case "filedelete":if(File.Exists(path))File.Delete(path);break;default:throw new InvalidOperationException("Unknown file operation.");}}
 void ExecuteOperations(JsonElement operations,bool skipKey=false){foreach(var op in operations.EnumerateArray()){var name=op.GetProperty("Name").GetString()??"";if(skipKey&&name.Equals("key",StringComparison.OrdinalIgnoreCase))continue;var value=op.GetProperty("Value").GetString()??"";Action(name+"("+value+")");}}
 void ApplyClassEvents(FrameworkElement element,string className,string kind){var selectors=new[]{kind,className}.Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();var hoverNames=selectors.Select(x=>"__hover_"+x).Where(x=>GetStyle(x).ValueKind==JsonValueKind.Object).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();if(hoverNames.Length>0){FrameworkElement target=element is Grid inputGrid&&inputGrid.Tag is TextBox wrappedInput?wrappedInput:element;var originalCursor=target.Cursor;var originalOpacity=target.Opacity;var originalWidth=target.Width;var originalHeight=target.Height;var originalMargin=target.Margin;var originalEffect=target.Effect;var control=target as Control;var originalForeground=control?.Foreground;var originalBackground=control?.Background;var originalBorderBrush=control?.BorderBrush;var originalBorderThickness=control?.BorderThickness??default;var originalPadding=control?.Padding??default;var originalFontSize=control?.FontSize??0;var originalFontFamily=control?.FontFamily;var originalFontWeight=control?.FontWeight??FontWeights.Normal;var text=target as TextBlock;var originalTextForeground=text?.Foreground;var originalTextSize=text?.FontSize??0;var border=target as Border;var originalCorner=border?.CornerRadius??default;void Restore(){target.Cursor=originalCursor;target.Opacity=originalOpacity;target.Width=originalWidth;target.Height=originalHeight;target.Margin=originalMargin;target.Effect=originalEffect;if(control!=null){control.Foreground=originalForeground;control.Background=originalBackground;control.BorderBrush=originalBorderBrush;control.BorderThickness=originalBorderThickness;control.Padding=originalPadding;control.FontSize=originalFontSize;if(originalFontFamily!=null)control.FontFamily=originalFontFamily;control.FontWeight=originalFontWeight;}if(text!=null){text.Foreground=originalTextForeground;text.FontSize=originalTextSize;text.FontWeight=FontWeights.Normal;}if(border!=null)border.CornerRadius=originalCorner;}element.MouseEnter+=(_,__)=>{foreach(var name in hoverNames)ApplyStyle(target,name);};element.MouseLeave+=(_,__)=>Restore();}
 var clickKeys=selectors.Select(x=>x+".click").Where(x=>actionDocument.RootElement.TryGetProperty(x,out _)).ToArray();if(clickKeys.Length>0){void Invoke(){foreach(var key in clickKeys)if(actionDocument.RootElement.TryGetProperty(key,out var operations))ExecuteOperations(operations);}if(element is ButtonBase button)button.Click+=(_,__)=>Invoke();else element.MouseLeftButtonUp+=(_,__)=>Invoke();}}
 void ApplyKeyboardEvents(FrameworkElement element,string className,string kind){var selectors=new[]{kind,className}.Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();var keys=selectors.Select(x=>x+".keydown").Where(x=>actionDocument.RootElement.TryGetProperty(x,out _)).ToArray();if(keys.Length==0)return;element.PreviewKeyDown+=(_,e)=>{foreach(var key in keys){if(!actionDocument.RootElement.TryGetProperty(key,out var operations))continue;var entries=operations.EnumerateArray().ToArray();var keyName=entries.FirstOrDefault(op=>string.Equals(op.GetProperty("Name").GetString(),"key",StringComparison.OrdinalIgnoreCase));if(keyName.ValueKind!=JsonValueKind.Undefined){var requested=keyName.GetProperty("Value").GetString()??"";if(requested.Equals("Enter",StringComparison.OrdinalIgnoreCase))requested="Return";if(!Enum.TryParse<Key>(requested,true,out var expected)||e.Key!=expected)continue;}ExecuteOperations(operations,true);}};}

 Button MakeButton(string value,Dictionary<string,string> props){var button=new Button{Content=value,Padding=new Thickness(10,6,10,6),Margin=new Thickness(4),Template=RoundedButtonTemplate(0)};if(props.TryGetValue("action",out var action))button.Click+=(_,__)=>Action(action);return button;}
 ControlTemplate RoundedButtonTemplate(double radius){var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.CornerRadiusProperty,new CornerRadius(radius));border.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Control.BackgroundProperty));border.SetValue(Border.BorderBrushProperty,new TemplateBindingExtension(Control.BorderBrushProperty));border.SetValue(Border.BorderThicknessProperty,new TemplateBindingExtension(Control.BorderThicknessProperty));var content=new FrameworkElementFactory(typeof(ContentPresenter));content.SetValue(ContentPresenter.ContentProperty,new TemplateBindingExtension(ContentControl.ContentProperty));content.SetValue(ContentPresenter.ContentTemplateProperty,new TemplateBindingExtension(ContentControl.ContentTemplateProperty));content.SetValue(ContentPresenter.HorizontalAlignmentProperty,new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty));content.SetValue(ContentPresenter.VerticalAlignmentProperty,new TemplateBindingExtension(Control.VerticalContentAlignmentProperty));content.SetValue(ContentPresenter.MarginProperty,new TemplateBindingExtension(Control.PaddingProperty));border.AppendChild(content);return new ControlTemplate(typeof(Button)){VisualTree=border};}
 bool NeedsFrame(string[] names){foreach(var name in names){var style=GetStyle(name);if(style.ValueKind==JsonValueKind.Object&&(style.TryGetProperty("radius",out _)||style.TryGetProperty("border",out _)||style.TryGetProperty("stroke",out _)||style.TryGetProperty("shadow",out _)))return true;}return false;}
 FontFamily Font(string name){if(name.StartsWith("content/fonts/",StringComparison.OrdinalIgnoreCase))return new FontFamily(new Uri("pack://application:,,,/",UriKind.Absolute),name);return new FontFamily(name);}
 void ApplyStyle(FrameworkElement element,string name){if(element is Grid g&&g.Tag is TextBox input)ApplyStyle(input,name);var style=GetStyle(name);if(style.ValueKind!=JsonValueKind.Object)return;string? Get(string key)=>style.TryGetProperty(key,out var value)?value.GetString():null;var color=Get("color");var background=Get("background");var font=Get("font");var size=Get("size");var weight=Get("weight");var margin=Get("margin");var padding=Get("padding");var width=Get("width");var height=Get("height");var opacity=Get("opacity");var align=Get("align");var layout=Get("layout");var cursor=Get("cursor");if(cursor!=null)element.Cursor=CursorFor(cursor);if(color!=null){if(element is Control control1)control1.Foreground=Brush(color);if(element is TextBlock text1)text1.Foreground=Brush(color);}if(background!=null){if(element is Control control2)control2.Background=Brush(background);if(element is Border border1)border1.Background=Brush(background);if(element is Panel panel1)panel1.Background=Brush(background);}if(font!=null){if(element is Control control3)control3.FontFamily=Font(font);if(element is TextBlock text2)text2.FontFamily=Font(font);}if(size!=null&&double.TryParse(size,NumberStyles.Float,CultureInfo.InvariantCulture,out var fs)){if(element is Control control4)control4.FontSize=fs;if(element is TextBlock text3)text3.FontSize=fs;}if(weight!=null){try{var fw=(FontWeight)new FontWeightConverter().ConvertFromString(weight)!;if(element is Control control5)control5.FontWeight=fw;if(element is TextBlock text4)text4.FontWeight=fw;}catch{}}if(margin!=null){try{element.Margin=(Thickness)new ThicknessConverter().ConvertFromString(margin)!;}catch{}}if(padding!=null){try{var th=(Thickness)new ThicknessConverter().ConvertFromString(padding)!;if(element is Control control6)control6.Padding=th;if(element is Border border2)border2.Padding=th;}catch{}}if(width!=null&&double.TryParse(width,NumberStyles.Float,CultureInfo.InvariantCulture,out var w))element.Width=w;if(height!=null&&double.TryParse(height,NumberStyles.Float,CultureInfo.InvariantCulture,out var h))element.Height=h;if(opacity!=null&&double.TryParse(opacity,NumberStyles.Float,CultureInfo.InvariantCulture,out var o))element.Opacity=Math.Clamp(o,0,1);if(align!=null){var horizontal=align.ToLowerInvariant() switch{"left"=>HorizontalAlignment.Left,"center"=>HorizontalAlignment.Center,"right"=>HorizontalAlignment.Right,_=>HorizontalAlignment.Stretch};element.HorizontalAlignment=horizontal;if(element is TextBlock textAlign)textAlign.TextAlignment=align.ToLowerInvariant() switch{"left"=>TextAlignment.Left,"center"=>TextAlignment.Center,"right"=>TextAlignment.Right,_=>TextAlignment.Justify};if(element is Control contentControl)contentControl.HorizontalContentAlignment=horizontal;}if(layout!=null&&element is StackPanel stack)stack.Orientation=layout.Equals("horizontal",StringComparison.OrdinalIgnoreCase)?Orientation.Horizontal:Orientation.Vertical;var borderSpec=Get("border")??Get("stroke");if(borderSpec!=null){var match=Regex.Match(borderSpec,@"^(?:(?<width>\d+(?:\.\d+)?)\s+)?(?:(?:solid)\s+)?(?<color>#[0-9a-fA-F]{3,8}|[A-Za-z][A-Za-z0-9 _-]*)$",RegexOptions.IgnoreCase);if(match.Success){var thickness=double.TryParse(match.Groups["width"].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out var bw)?bw:1;var brush=Brush(match.Groups["color"].Value);if(element is Control ctl){ctl.BorderBrush=brush;ctl.BorderThickness=new Thickness(thickness);}if(element is Border b){b.BorderBrush=brush;b.BorderThickness=new Thickness(thickness);}}}var radius=Get("radius");if(radius!=null&&double.TryParse(radius,NumberStyles.Float,CultureInfo.InvariantCulture,out var r)){if(element is Button rounded)rounded.Template=RoundedButtonTemplate(r);if(element is Border roundedBorder)roundedBorder.CornerRadius=new CornerRadius(r);}var shadow=Get("shadow");if(shadow!=null){if(shadow.Equals("none",StringComparison.OrdinalIgnoreCase))element.Effect=null;else{var match=Regex.Match(shadow,@"^(?:(?<depth>\d+(?:\.\d+)?)\s+)?(?:(?<blur>\d+(?:\.\d+)?)\s+)?(?<color>#[0-9a-fA-F]{3,8}|[A-Za-z][A-Za-z0-9 _-]*)$",RegexOptions.IgnoreCase);if(match.Success){var shadowBrush=Brush(match.Groups["color"].Value) as SolidColorBrush;if(shadowBrush!=null)element.Effect=new DropShadowEffect{Color=shadowBrush.Color,ShadowDepth=double.TryParse(match.Groups["depth"].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out var depth)?Math.Clamp(depth,0,100):4,BlurRadius=double.TryParse(match.Groups["blur"].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out var blur)?Math.Clamp(blur,0,100):12,Opacity=shadowBrush.Opacity};}}}}
 void AddChild(Panel parent,FrameworkElement child,Dictionary<string,string> props){if(parent is Canvas){if(double.TryParse(props.GetValueOrDefault("x","0"),NumberStyles.Float,CultureInfo.InvariantCulture,out var x))Canvas.SetLeft(child,x);if(double.TryParse(props.GetValueOrDefault("y","0"),NumberStyles.Float,CultureInfo.InvariantCulture,out var y))Canvas.SetTop(child,y);}parent.Children.Add(child);}
 bool Confirm(string message)=>MessageBox.Show(this,message,Title,MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes;
 ImageSource? ImageSource(string p){try{return new System.Windows.Media.Imaging.BitmapImage(MediaUri(p));}catch{return null;}}
 List<string> ActionArgs(string body){var result=new List<string>();var start=0;var depth=0;var quote='\0';for(var i=0;i<body.Length;i++){var c=body[i];if(quote!='\0'){if(c==quote&&(i==0||body[i-1]!='\\'))quote='\0';continue;}if(c is '"' or '\'')quote=c;else if(c is '(' or '[' or '{')depth++;else if(c is ')' or ']' or '}')depth--;else if(c==','&&depth==0){result.Add(body[start..i].Trim().Trim('"','\''));start=i+1;}}if(start<body.Length||body.Length>0)result.Add(body[start..].Trim().Trim('"','\''));return result;} string ArgValue(string s)=>appState.TryGetValue(s,out var value)?value:s; JsonNode? DataValue(string s){var value=ArgValue(s);try{return JsonNode.Parse(value);}catch{return JsonValue.Create(value);}} string NodeText(JsonNode? node)=>node is JsonValue value&&value.TryGetValue<string>(out var text)?text:node?.ToJsonString()??"";
 string Prompt(string title){var dialog=new Window{Title=title,Owner=this,Width=360,Height=150,WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize};var panel=new StackPanel{Margin=new Thickness(14)};var input=new TextBox{MinWidth=280};var ok=new Button{Content="OK",IsDefault=true,Width=80,Margin=new Thickness(0,10,0,0),HorizontalAlignment=HorizontalAlignment.Right};ok.Click+=(_,__)=>dialog.DialogResult=true;panel.Children.Add(input);panel.Children.Add(ok);dialog.Content=panel;return dialog.ShowDialog()==true?input.Text:"";}
 async void Action(string action){try{await ExecuteAction(action);}catch(Exception e){MessageBox.Show(this,e.Message,"TXL action");}} async Task ExecuteAction(string action){var m=Regex.Match(action,@"^([A-Za-z][\w]*)\((.*)\)$");if(!m.Success)return;var op=m.Groups[1].Value.ToLowerInvariant();var args=ActionArgs(m.Groups[2].Value);string A(int i)=>i<args.Count?args[i]:"";switch(op){case "open":if(Uri.TryCreate(ArgValue(A(0)),UriKind.Absolute,out var uri)&&(uri.Scheme is "https" or "http" or "mailto"))Process.Start(new ProcessStartInfo(uri.AbsoluteUri){UseShellExecute=true});break;case "alert":case "success":MessageBox.Show(this,ArgValue(A(0)),Title,MessageBoxButton.OK,op=="success"?MessageBoxImage.Information:MessageBoxImage.None);break;case "confirm":Confirm(ArgValue(A(0)));break;case "prompt":if(args.Count>=2){var value=Prompt(ArgValue(A(1)));if(value.Length>0)SetState(A(0),value);}break;case "set":if(args.Count>=2)SetState(A(0),ArgValue(A(1)));break;case "append":if(args.Count>=2)SetState(A(0),GetState(A(0))+ArgValue(A(1)));break;case "clear":if(args.Count>=1)SetState(A(0),"");break;case "toggle":if(args.Count>=1)SetState(A(0),GetState(A(0)).Equals("true",StringComparison.OrdinalIgnoreCase)?"false":"true");break;case "math":if(args.Count>=4){var left=double.TryParse(ArgValue(A(1)),NumberStyles.Float,CultureInfo.InvariantCulture,out var l)?l:double.NaN;var right=double.TryParse(ArgValue(A(2)),NumberStyles.Float,CultureInfo.InvariantCulture,out var r)?r:double.NaN;double result=A(0) switch{"add"=>left+right,"subtract"=>left-right,"multiply"=>left*right,"divide" when right!=0=>left/right,_=>double.NaN};SetState(A(3),double.IsNaN(result)?"Invalid input or division by zero":result.ToString("G",CultureInfo.InvariantCulture));}break;case "listadd":if(args.Count>=2){var list=ReadTodos(A(0));var text=ArgValue(A(1)).Trim();if(text.Length>0){list.Add(new TodoEntry(text,false));SetState(A(0),JsonSerializer.Serialize(list));if(appState.ContainsKey(A(1)))SetState(A(1),"");}}break;case "arrayadd":{var array=JsonNode.Parse(GetState(A(0),"[]")) as JsonArray??throw new InvalidOperationException("arrayAdd needs an array state, initialized with [].");array.Add(DataValue(A(1)));SetState(A(0),array.ToJsonString());break;}case "arrayget":{var array=JsonNode.Parse(GetState(A(0),"[]")) as JsonArray??throw new InvalidOperationException("arrayGet needs an array state.");if(!int.TryParse(A(1),out var index)||index<0||index>=array.Count)throw new IndexOutOfRangeException("arrayGet index is outside the array.");SetState(A(2),NodeText(array[index]));break;}case "arrayremove":{var array=JsonNode.Parse(GetState(A(0),"[]")) as JsonArray??throw new InvalidOperationException("arrayRemove needs an array state.");if(!int.TryParse(A(1),out var index)||index<0||index>=array.Count)throw new IndexOutOfRangeException("arrayRemove index is outside the array.");array.RemoveAt(index);SetState(A(0),array.ToJsonString());break;}case "objectset":{var obj=JsonNode.Parse(GetState(A(0),"{}")) as JsonObject??throw new InvalidOperationException("objectSet needs an object state, initialized with {}.");obj[ArgValue(A(1))]=DataValue(A(2));SetState(A(0),obj.ToJsonString());break;}case "objectget":{var obj=JsonNode.Parse(GetState(A(0),"{}")) as JsonObject??throw new InvalidOperationException("objectGet needs an object state.");if(!obj.TryGetPropertyValue(ArgValue(A(1)),out var value))throw new KeyNotFoundException("objectGet key was not found.");SetState(A(2),NodeText(value));break;}case "objectremove":{var obj=JsonNode.Parse(GetState(A(0),"{}")) as JsonObject??throw new InvalidOperationException("objectRemove needs an object state.");obj.Remove(ArgValue(A(1)));SetState(A(0),obj.ToJsonString());break;}case "httpget":case "httppost":if(!Uri.TryCreate(ArgValue(A(0)),UriKind.Absolute,out var endpoint)||endpoint.Scheme is not ("http" or "https"))throw new InvalidOperationException("HTTP actions require an absolute HTTP or HTTPS URL.");if(op=="httpget"){var response=await httpClient.GetAsync(endpoint);response.EnsureSuccessStatusCode();SetState(A(1),await response.Content.ReadAsStringAsync());}else{using var body=new StringContent(ArgValue(A(1)),System.Text.Encoding.UTF8,"application/json");var response=await httpClient.PostAsync(endpoint,body);response.EnsureSuccessStatusCode();SetState(A(2),await response.Content.ReadAsStringAsync());}break;case "call":if(args.Count>=2){var functionArgs=args.Skip(1).Take(args.Count-2).Select(ArgValue).ToArray();SetState(A(args.Count-1),CallFunction(A(0),functionArgs));}break;case "filewrite":case "fileappend":case "fileread":case "filedelete":FileOperation(op,args);break;case "timerstart":case "timerstop":TimerOperation(op,args);break;case "dbexecute":case "dbquery":case "dbscalar":case "dbbegin":case "dbcommit":case "dbrollback":DatabaseOperation(op,args);break;case "page":ShowPage(ArgValue(A(0)));break;case "throw":throw new InvalidOperationException(ArgValue(A(0)));case "try":if(args.Count>=2){try{await ExecuteAction(A(0));}catch(Exception error){SetState(A(1),error.Message);}}break;}}
 protected override void OnKeyDown(KeyEventArgs e){if(e.Key==Key.Back&&history.Count>0){var previous=history.Pop();ShowPage(previous,false);}base.OnKeyDown(e);}
}
""";
        return template.Replace("{{metaLiteral}}", metaLiteral).Replace("{{stylesLiteral}}", stylesLiteral).Replace("{{actionsLiteral}}", actionsLiteral).Replace("{{initialStateLiteral}}", initialStateLiteral).Replace("{{functionCases}}", functionCases).Replace("{{pageCases}}", pageCases).Replace("{{startFile}}", JsonSerializer.Serialize(info.Start));
    }

    private static string RenderFunctionCases()
    {
        var cases = new System.Text.StringBuilder();
        foreach (var function in FunctionCatalog.Definitions.Values.OrderBy(function => function.Name, StringComparer.OrdinalIgnoreCase))
        {
            cases.Append("case ").Append(JsonSerializer.Serialize(function.Name.ToLowerInvariant())).Append(":{")
                .Append("if(args.Length!=").Append(function.Parameters.Count).Append(")throw new InvalidOperationException(")
                .Append(JsonSerializer.Serialize($"Function '{function.Name}' expects {function.Parameters.Count} argument(s)."))
                .Append(");var locals=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){");
            for (var index = 0; index < function.Parameters.Count; index++)
            {
                if (index > 0) cases.Append(',');
                cases.Append('{').Append(JsonSerializer.Serialize(function.Parameters[index])).Append(",args[").Append(index).Append("]}");
            }
            cases.Append("};return EvaluateFunctionExpression(").Append(JsonSerializer.Serialize(function.ReturnExpression)).Append(",locals);}");
        }
        return cases.ToString();
    }

    private static string RenderPageCases(IReadOnlyDictionary<string, ProgramAst> pages)
    {
        var cases = new System.Text.StringBuilder();
        foreach (var page in pages.OrderBy(page => page.Key, StringComparer.OrdinalIgnoreCase))
        {
            var key = page.Key.Replace('\\', '/').TrimStart('/').ToLowerInvariant();
            cases.Append("case ").Append((char)34).Append(Esc(key)).Append((char)34).Append(":{")
                .Append(RenderNodes(page.Value.Nodes)).Append("break;}");
        }
        return cases.ToString();
    }

    private static string RenderNodes(List<Node> nodes) => RenderInto(nodes, "stack", new List<string>(), new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    private static string RenderInto(List<Node> nodes, string target, List<string> inheritedStyles, Dictionary<string, string> variables, int componentDepth = 0)
    {
        var result = "";
        foreach (var node in nodes)
        {
            if (node is SetVariable set) variables[set.Name] = Resolve(set.Value, variables);
            else if (node is ThrowValue thrown) result += "throw new InvalidOperationException(" + JsonSerializer.Serialize(Resolve(thrown.Message, variables)) + ");";
            else if (node is TryCatch guarded)
            {
                var errorName = "__txlError_" + nodeId++;
                var tryBody = RenderInto(guarded.TryChildren, target, inheritedStyles, new Dictionary<string, string>(variables, StringComparer.OrdinalIgnoreCase), componentDepth);
                var catchBody = RenderInto(guarded.CatchChildren, target, inheritedStyles, new Dictionary<string, string>(variables, StringComparer.OrdinalIgnoreCase), componentDepth);
                result += "try{" + tryBody + "}catch(Exception " + errorName + "){SetState(" + JsonSerializer.Serialize(guarded.ErrorState) + "," + errorName + ".Message);" + catchBody + "}";
            }
            else if (node is Conditional conditional)
            {
                var branch = Evaluate(conditional.Expression, variables) ? conditional.Children : conditional.ElseChildren;
                result += RenderInto(branch, target, inheritedStyles, new Dictionary<string, string>(variables, StringComparer.OrdinalIgnoreCase), componentDepth);
            }
            else if (node is Repeat repeat)
            {
                var countText = Resolve(repeat.Expression, variables);
                if (int.TryParse(countText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
                    for (var i = 0; i < Math.Clamp(count, 0, 1000); i++) { var scope = new Dictionary<string, string>(variables, StringComparer.OrdinalIgnoreCase) { ["index"] = i.ToString(CultureInfo.InvariantCulture) }; result += RenderInto(repeat.Children, target, inheritedStyles, scope); }
            }
            else if (node is ForEach each)
            {
                var raw = variables.GetValueOrDefault(each.Collection, "").Trim();
                if (raw.StartsWith('[') && raw.EndsWith(']')) raw = raw[1..^1];
                var items = raw.Length == 0 ? Array.Empty<string>() : raw.Split(',').Select(x => x.Trim().Trim('"', '\'')).Take(1000).ToArray();
                for (var i = 0; i < items.Length; i++) { var scope = new Dictionary<string, string>(variables, StringComparer.OrdinalIgnoreCase) { [each.Variable] = items[i], ["index"] = i.ToString(CultureInfo.InvariantCulture) }; result += RenderInto(each.Children, target, inheritedStyles, scope); }
            }
            else if (node is ComponentCall call && componentDepth < 32 && ComponentCatalog.Definitions.TryGetValue(call.Name, out var definition))
            {
                var scope = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var argument in call.Arguments) scope[argument.Key] = Resolve(argument.Value, variables);
                result += RenderInto(definition, target, inheritedStyles, scope, componentDepth + 1);
            }
            else if (node is Element element) result += RenderElement(target, element with { Value = Resolve(element.Value, variables) }, inheritedStyles);
            else if (node is Group group)
            {
                var id = "panel_" + nodeId++;
                var styles = new List<string>(inheritedStyles) { group.Name };
                var layout = Layout(group);
                var panel = layout == "Absolute" ? $"new Canvas{{Margin=new Thickness(8)}}" : $"new StackPanel{{Orientation=Orientation.{layout},Margin=new Thickness(8)}}";
                result += $"Panel {id}={panel};ApplyStyle({id},\"{Esc(group.Name)}\");";
                if (group.Properties.Count > 0)
                {
                    var inlineStyle = "__groupProps_" + id;
                    foreach (var property in group.Properties) StyleCatalog.Add(inlineStyle, property.Key, property.Value);
                    result += $"ApplyStyle({id},\"{Esc(inlineStyle)}\");";
                }
                result += RenderInto(group.Children, id, styles, new Dictionary<string, string>(variables, StringComparer.OrdinalIgnoreCase), componentDepth);
                var positionedGroupProperties = PositionProperties(group.Properties, new[] { group.Name });
                result += $"AddChild({target},{id},System.Text.Json.JsonSerializer.Deserialize<Dictionary<string,string>>(\"{Esc(JsonSerializer.Serialize(positionedGroupProperties))}\")!);";
            }
        }
        return result;
    }

    private static string Resolve(string value, Dictionary<string, string> variables)
    {
        string ResolveExpression(string expression)
        {
            expression = expression.Trim();
            if (variables.TryGetValue(expression, out var direct)) return direct;
            if (FunctionCatalog.TryParseCall(expression, out var functionName, out var functionArguments))
            {
                var arguments = functionArguments.Select(argument => Resolve(argument.Trim().Trim('"', '\''), variables)).ToArray();
                return FunctionCatalog.InvokeCompileTime(functionName, arguments);
            }
            var member = System.Text.RegularExpressions.Regex.Match(expression, @"^([A-Za-z_][\w]*)\.([A-Za-z_][\w]*)$");
            if (member.Success && variables.TryGetValue(member.Groups[1].Value, out var json))
            {
                try
                {
                    using var document = JsonDocument.Parse(json);
                    if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty(member.Groups[2].Value, out var property))
                        return property.ValueKind == JsonValueKind.String ? property.GetString() ?? "" : property.ToString();
                }
                catch (JsonException) { }
            }
            return "";
        }

        if (FunctionCatalog.TryParseCall(value.Trim(), out var wholeFunction, out var wholeArguments))
            return FunctionCatalog.InvokeCompileTime(wholeFunction, wholeArguments.Select(argument => Resolve(argument.Trim().Trim('"', '\''), variables)).ToArray());
        return System.Text.RegularExpressions.Regex.Replace(value, @"\$\{([^{}]+)\}", match => ResolveExpression(match.Groups[1].Value));
    }

    private static bool Evaluate(string expression, Dictionary<string, string> variables)
    {
        expression = Resolve(expression.Trim(), variables);
        if (bool.TryParse(expression, out var literal)) return literal;
        var comparison = System.Text.RegularExpressions.Regex.Match(expression, "^([A-Za-z_][\\w]*)\\s*(==|!=)\\s*(.*)$");
        if (comparison.Success)
        {
            var left = variables.GetValueOrDefault(comparison.Groups[1].Value, "");
            var right = comparison.Groups[3].Value.Trim().Trim('"', '\'');
            var equal = string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
            return comparison.Groups[2].Value == "==" ? equal : !equal;
        }
        return !string.IsNullOrWhiteSpace(expression) && expression != "0" && !expression.Equals("false", StringComparison.OrdinalIgnoreCase);
    }

    private static string RenderElement(string target, Element element, List<string> groupStyles)
    {
        var props = JsonSerializer.Serialize(PositionProperties(element.Properties, new[] { "main", element.Kind }.Concat(groupStyles).Concat(string.IsNullOrWhiteSpace(element.Variant) ? Array.Empty<string>() : new[] { element.Variant }).Concat(string.IsNullOrWhiteSpace(element.ClassName) ? Array.Empty<string>() : new[] { element.ClassName })));
        var styles = JsonSerializer.Serialize(groupStyles);
        var selector = element.ClassName ?? (element.Variant is not null && !System.Text.RegularExpressions.Regex.IsMatch(element.Variant, @"^(?:hl|headline|headline[1-5])$", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ? element.Variant : string.Empty);
        return $"AddChild({target},MakeElement(\"{element.Kind}\",\"{Esc(element.Variant ?? string.Empty)}\",\"{Esc(selector)}\",\"{Esc(element.Value)}\",System.Text.Json.JsonSerializer.Deserialize<Dictionary<string,string>>(\"{Esc(props)}\")!,System.Text.Json.JsonSerializer.Deserialize<string[]>(\"{Esc(styles)}\")!),System.Text.Json.JsonSerializer.Deserialize<Dictionary<string,string>>(\"{Esc(props)}\")!);";
    }

    private static string Layout(Group group)
    {
        var value = group.Properties.GetValueOrDefault("layout");
        if (string.IsNullOrWhiteSpace(value) && StyleCatalog.Styles.TryGetValue(group.Name, out var style)) value = style.GetValueOrDefault("layout");
        return (value ?? "vertical").ToLowerInvariant() switch { "horizontal" => "Horizontal", "absolute" or "canvas" => "Absolute", _ => "Vertical" };
    }
    private static Dictionary<string, string> PositionProperties(Dictionary<string, string> properties, IEnumerable<string> styleNames)
    {
        var result = new Dictionary<string, string>(properties, StringComparer.OrdinalIgnoreCase);
        foreach (var name in styleNames)
            if (StyleCatalog.Styles.TryGetValue(name, out var style) && style.TryGetValue("position", out var position)) AddPosition(result, position);
        if (properties.TryGetValue("position", out var explicitPosition)) AddPosition(result, explicitPosition);
        return result;
    }
    private static void AddPosition(Dictionary<string, string> properties, string position)
    {
        var match = System.Text.RegularExpressions.Regex.Match(position, @"^x\s*(-?\d+(?:\.\d+)?)\s+y\s*(-?\d+(?:\.\d+)?)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success) { properties["x"] = match.Groups[1].Value; properties["y"] = match.Groups[2].Value; }
    }
    private static string Esc(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
}
