using System.Diagnostics;
using System.Text.Json;
using Txl;

if (args.Length == 0 || args[0] is "help" or "--help" or "-h") { Help(); return 0; }
if (args[0] == "version") { Console.WriteLine("TXL 0.1.0 (Timexo Language)"); return 0; }
var command=args[0]; var project=Path.GetFullPath(args.Length>1?args[1]:".");
if(command is not ("check" or "build" or "run" or "preview")) { Console.Error.WriteLine($"Unknown command: {command}. Use 'txl help'."); return 2; }
var result=ProjectValidator.Validate(project);
foreach(var d in result.Diagnostics) Console.Error.WriteLine(d);
if(result.Diagnostics.Any(x=>x.Severity==Severity.Error)) return 1;
var info=result.Info!; var source=Path.Combine(project, info.Start);
var parse=Parser.Parse(File.ReadAllText(source), source);
foreach(var d in parse.Diagnostics) Console.Error.WriteLine(d);
if(parse.Diagnostics.Any(x=>x.Severity==Severity.Error)) return 1;
if(command=="check") { Console.WriteLine($"TXL project valid: {info.Name} ({parse.Program!.Nodes.Count} top-level nodes)"); return 0; }
var output=Path.Combine(project,"dist"); Directory.CreateDirectory(output);
var exePath=Path.Combine(output,SafeName(info.Name),SafeName(info.Name)+".exe");
var generated=Path.Combine(project,".txl-generated");
try {
 Console.WriteLine("[1/3] Resolving TXL project and assets...");
		 var files=Directory.GetFiles(project,"*",SearchOption.AllDirectories).Where(f=>{var relative=Path.GetRelativePath(project,f).Replace('\\','/');var parts=relative.Split('/');var name=Path.GetFileName(f);return !parts.Any(part=>part is "dist" or ".txl-generated" or ".git" or "bin" or "obj")&&!Path.GetExtension(f).Equals(".txl",StringComparison.OrdinalIgnoreCase)&&name!="info.json"&&name!="README.md"&&name!="build.ps1"&&name!="global.json";}).ToArray();
	 Directory.CreateDirectory(generated); foreach(var f in files){var rel=Path.GetRelativePath(project,f);var dest=Path.Combine(generated,"content",rel);Directory.CreateDirectory(Path.GetDirectoryName(dest)!);File.Copy(f,dest,true);}
		 var iconSource=Path.Combine(AppContext.BaseDirectory,"Assets","txl.ico");var contentIcon=Path.Combine(generated,"content","txl.ico");Directory.CreateDirectory(Path.GetDirectoryName(contentIcon)!);if(File.Exists(iconSource))File.Copy(iconSource,contentIcon,true);
	 var runtime=Path.Combine(generated,"Runtime.cs"); File.WriteAllText(runtime, WpfEmitter.Emit(info, parse.Program!, project));
	 var proj=Path.Combine(generated,"GeneratedApp.csproj"); File.WriteAllText(proj, WpfEmitter.ProjectFile(info));
	 if(command=="preview"){
	  if(!OperatingSystem.IsWindows()){Console.Error.WriteLine("The TXL preview is a native WPF window and can only run on Windows.");return 1;}
	  var preview=new ProcessStartInfo("dotnet"){WorkingDirectory=generated,UseShellExecute=false};preview.ArgumentList.Add("run");preview.ArgumentList.Add("--project");preview.ArgumentList.Add("GeneratedApp.csproj");using var app=Process.Start(preview)!;await app.WaitForExitAsync();return app.ExitCode;
	 }
 Console.WriteLine("[2/3] Publishing self-contained Windows application (win-x64)...");
 var psi=new ProcessStartInfo("dotnet"){WorkingDirectory=generated,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
	 foreach(var a in new[]{"publish","GeneratedApp.csproj","-c","Release","-r","win-x64","--self-contained","true","-p:PublishSingleFile=true","-p:IncludeNativeLibrariesForSelfExtract=true","-p:EnableCompressionInSingleFile=true","-p:DebugSymbols=false","-p:DebugType=None","-o",Path.GetDirectoryName(exePath)!})psi.ArgumentList.Add(a);
 using var proc=Process.Start(psi)!; var stdout=proc.StandardOutput.ReadToEndAsync();var stderr=proc.StandardError.ReadToEndAsync();await proc.WaitForExitAsync();Console.Write(await stdout);Console.Error.Write(await stderr);
 if(proc.ExitCode!=0)return proc.ExitCode;
	 Console.WriteLine($"[3/3] Build complete: {exePath}");
	 if(command=="run"){Console.WriteLine("Native WPF apps can be run only on Windows."); if(OperatingSystem.IsWindows()) Process.Start(new ProcessStartInfo(exePath){UseShellExecute=true});}
 return 0;
} catch(Exception e){Console.Error.WriteLine("TXL build failed: "+e.Message);return 1;}
static string SafeName(string s)=>string.Concat(s.Select(c=>char.IsLetterOrDigit(c)||c is '-' or '_'?c:'_'));
static void Help(){Console.WriteLine("TXL — Timexo Language\nUsage: txl <command> [project]\n\nCommands:\n  check <project>   Validate info.json and TXL source\n  preview <project> Launch a native WPF window without publishing an EXE (Windows only)\n  build <project>   Publish self-contained Windows win-x64 EXE\n  run <project>     Build and launch (Windows only)\n  version           Print version\n  help              Show help");}
