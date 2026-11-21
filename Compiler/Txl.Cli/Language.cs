using System.Text.Json;
using System.Text.RegularExpressions;

namespace Txl;

public enum Severity { Info, Warning, Error }
public sealed record Diagnostic(string File, int Line, int Column, Severity Severity, string Message)
{
    public override string ToString() => $"{File}:{Line}:{Column}: {Severity.ToString().ToLowerInvariant()}: {Message}";
}
public sealed record Token(string Value, int Line, int Column, bool Quoted = false);
public abstract record Node(int Line);
public sealed record Element(string Kind, string? Variant, string Value, Dictionary<string, string> Properties, int Line) : Node(Line);
public sealed record Group(string Name, Dictionary<string, string> Properties, List<Node> Children, int Line) : Node(Line);
public sealed record Conditional(string Expression, List<Node> Children, int Line) : Node(Line);
public sealed record SetVariable(string Name, string Value, int Line) : Node(Line);
public sealed record StateVariable(string Name, string InitialValue, int Line) : Node(Line);
public sealed record Repeat(string Expression, List<Node> Children, int Line) : Node(Line);
public sealed record ForEach(string Variable, string Collection, List<Node> Children, int Line) : Node(Line);
public sealed record ComponentCall(string Name, Dictionary<string, string> Arguments, int Line) : Node(Line);
public sealed record ActionOperation(string Name, string Value);
public sealed record ProgramAst(List<Node> Nodes);
public sealed record ParseResult(ProgramAst? Program, List<Diagnostic> Diagnostics);
public sealed record WindowInfo(int Width = 900, int Height = 600, bool Resizable = true, string Background = "#20202A");
public sealed record ProjectInfo(string Name, string Version, string Developer, string Description, string Start, string? Icon, WindowInfo Window);
public sealed record ValidationResult(ProjectInfo? Info, List<Diagnostic> Diagnostics);

public static class Lexer
{
    public static List<(Token Token, string File)> Lines(string text, string file, List<Diagnostic> diagnostics)
    {
        var output = new List<(Token, string)>();
        var source = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var n = 0; n < source.Length; n++)
        {
            var line = source[n];
            var start = 0;
            while (start < line.Length && char.IsWhiteSpace(line[start])) start++;
            if (start == line.Length || line[start] == '#') continue;
            var inQuote = false;
            var quote = '\0';
            var end = start;
            while (end < line.Length)
            {
                var c = line[end];
                if (!inQuote && c == '#')
                {
                    var beforeHash = end - 1;
                    while (beforeHash >= start && char.IsWhiteSpace(line[beforeHash])) beforeHash--;
                    var isHexColor = beforeHash >= start && (line[beforeHash] is '=' or ':') && Regex.IsMatch(line[(end + 1)..], @"^(?:[0-9A-Fa-f]{3}|[0-9A-Fa-f]{4}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})(?:\s|$)");
                    if (!isHexColor) break;
                }
                if (c is '"' or '\'')
                {
                    if (!inQuote) { inQuote = true; quote = c; }
                    else if (quote == c) inQuote = false;
                }
                end++;
            }
            if (inQuote)
            {
                diagnostics.Add(new(file, n + 1, start + 1, Severity.Error, "Unterminated quoted string."));
                continue;
            }
            var value = line[start..end].TrimEnd();
            if (value.Length > 0) output.Add((new Token(value, n + 1, start + 1), file));
        }
        return output;
    }
}

public sealed class Parser
{
    private readonly List<(Token Token, string File)> lines;
    private readonly List<Diagnostic> diagnostics;
    private int position;

    private Parser(List<(Token, string)> tokens, List<Diagnostic> diagnostics)
    {
        lines = tokens;
        this.diagnostics = diagnostics;
    }

    public static ParseResult Parse(string text, string file = "main.txl")
    {
        StyleCatalog.Reset();
        ActionCatalog.Reset();
        ComponentCatalog.Reset();
        RuntimeStateCatalog.Reset();
        var diagnostics = new List<Diagnostic>();
        var parser = new Parser(Lexer.Lines(text, file, diagnostics), diagnostics);
        var nodes = parser.ReadBlock(false);
        return new ParseResult(diagnostics.Any(d => d.Severity == Severity.Error) ? null : new ProgramAst(nodes), diagnostics);
    }

    private List<Node> ReadBlock(bool nested, Dictionary<string, string>? groupProperties = null)
    {
        var nodes = new List<Node>();
        while (position < lines.Count)
        {
            var (token, file) = lines[position++];
            var source = token.Value.Trim();
            if (source == ")" || source.Equals("end", StringComparison.OrdinalIgnoreCase))
            {
                if (!nested) diagnostics.Add(new(file, token.Line, token.Column, Severity.Error, $"Unexpected block terminator '{source}'."));
                return nodes;
            }

            if (Regex.IsMatch(source, @"^style\s*\($", RegexOptions.IgnoreCase))
            {
                ReadStyleBlock(file, token);
                continue;
            }

            if (Regex.IsMatch(source, @"^actions\s*\($", RegexOptions.IgnoreCase))
            {
                ReadActionsBlock(file, token);
                continue;
            }

            var component = Regex.Match(source, @"^component\s*=\s*([A-Za-z][\w-]*)\s*\($", RegexOptions.IgnoreCase);
            if (component.Success)
            {
                var name = component.Groups[1].Value;
                var body = ReadBlock(true);
                if (ComponentCatalog.Definitions.ContainsKey(name)) diagnostics.Add(new(file, token.Line, token.Column, Severity.Error, $"Component '{name}' is already defined."));
                else ComponentCatalog.Definitions[name] = body;
                continue;
            }

            var forEach = Regex.Match(source, @"^for\s*=\s*([A-Za-z_][\w]*)\s+in\s+([A-Za-z_][\w]*)\s*\($", RegexOptions.IgnoreCase);
            if (forEach.Success)
            {
                nodes.Add(new ForEach(forEach.Groups[1].Value, forEach.Groups[2].Value, ReadBlock(true), token.Line));
                continue;
            }

            var componentCall = Regex.Match(source, @"^([A-Za-z][\w-]*)\((.*)\)\s*$");
            if (componentCall.Success && !source.Contains('='))
            {
                if (!ComponentCatalog.Definitions.ContainsKey(componentCall.Groups[1].Value)) diagnostics.Add(new(file, token.Line, token.Column, Severity.Error, $"Unknown component '{componentCall.Groups[1].Value}'. Define it with 'component = name ('."));
                else nodes.Add(new ComponentCall(componentCall.Groups[1].Value, ParseArguments(componentCall.Groups[2].Value), token.Line));
                continue;
            }

            var group = Regex.Match(source, @"^group\s*=\s*(.*?)\s*\($", RegexOptions.IgnoreCase);
            if (group.Success)
            {
                var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                nodes.Add(new Group(Unquote(group.Groups[1].Value), properties, ReadBlock(true, properties), token.Line));
                continue;
            }

            var conditional = Regex.Match(source, @"^if\s*=\s*(.*?)\s*\($", RegexOptions.IgnoreCase);
            if (conditional.Success)
            {
                nodes.Add(new Conditional(conditional.Groups[1].Value.Trim(), ReadBlock(true), token.Line));
                continue;
            }

            var repeat = Regex.Match(source, @"^repeat\s*=\s*(.*?)\s*\($", RegexOptions.IgnoreCase);
            if (repeat.Success)
            {
                nodes.Add(new Repeat(repeat.Groups[1].Value.Trim(), ReadBlock(true), token.Line));
                continue;
            }

            if (TryAssignment(source, "group", out var legacyGroup))
            {
                var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                nodes.Add(new Group(legacyGroup, properties, ReadBlock(true, properties), token.Line));
                continue;
            }
            if (TryAssignment(source, "if", out var legacyIf))
            {
                nodes.Add(new Conditional(legacyIf, ReadBlock(true), token.Line));
                continue;
            }
            if (TryAssignment(source, "repeat", out var legacyRepeat))
            {
                nodes.Add(new Repeat(legacyRepeat, ReadBlock(true), token.Line));
                continue;
            }
            if (TryAssignment(source, "set", out var set))
            {
                var colon = set.IndexOf(':');
                var equals = set.IndexOf('=');
                var separator = colon >= 0 ? colon : equals;
                if (separator < 1) diagnostics.Add(new(file, token.Line, token.Column, Severity.Error, "Invalid set syntax; expected 'set = name:value'."));
                else nodes.Add(new SetVariable(set[..separator].Trim(), set[(separator + 1)..].Trim(), token.Line));
                continue;
            }

            if (TryAssignment(source, "state", out var state))
            {
                var separator = state.IndexOf(':');
                if (separator < 1) diagnostics.Add(new(file, token.Line, token.Column, Severity.Error, "Invalid state syntax; expected 'state = name:initial-value'."));
                else
                {
                    var name = state[..separator].Trim(); var initial = state[(separator + 1)..].Trim();
                    if (!Regex.IsMatch(name, @"^[A-Za-z_][\w]*$")) diagnostics.Add(new(file, token.Line, token.Column, Severity.Error, $"Invalid state name '{name}'."));
                    else if (RuntimeStateCatalog.Initial.ContainsKey(name)) diagnostics.Add(new(file, token.Line, token.Column, Severity.Error, $"State '{name}' is declared more than once."));
                    else { RuntimeStateCatalog.Initial[name] = Unquote(initial); nodes.Add(new StateVariable(name, Unquote(initial), token.Line)); }
                }
                continue;
            }

            var property = Regex.Match(source, @"^([A-Za-z][\w-]*)\s*(?::|=)\s*(.*)$");
            if (property.Success && IsKnownProperty(property.Groups[1].Value))
            {
                if (groupProperties is not null) groupProperties[property.Groups[1].Value] = Unquote(property.Groups[2].Value);
                else diagnostics.Add(new(file, token.Line, token.Column, Severity.Warning, $"Property '{property.Groups[1].Value}' is outside a group or element and will be ignored."));
                continue;
            }

            var element = Regex.Match(source, @"^([A-Za-z][\w-]*)(?:\(([^)]+)\))?\s*(?:=\s*(.*))?$");
            if (element.Success)
            {
                var kind = element.Groups[1].Value.ToLowerInvariant();
                if (kind is not ("text" or "button" or "image" or "input" or "checkbox" or "list" or "line" or "space"))
                {
                    diagnostics.Add(new(file, token.Line, token.Column, Severity.Error, $"Unknown command '{kind}'."));
                    continue;
                }
                var variant = element.Groups[2].Success ? element.Groups[2].Value : null;
                var value = element.Groups[3].Success ? Unquote(element.Groups[3].Value) : string.Empty;
                var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                while (position < lines.Count)
                {
                    var next = lines[position].Token.Value.Trim();
                    var match = Regex.Match(next, @"^(action|placeholder|bind|value|checked|x|y)\s*(?::|=)\s*(.*)$", RegexOptions.IgnoreCase);
                    if (!match.Success) break;
                    properties[match.Groups[1].Value] = Unquote(match.Groups[2].Value);
                    position++;
                }
                if (kind == "input")
                {
                    // The assigned input text is hint text, never the actual submitted value.
                    if (!properties.ContainsKey("placeholder") && !string.IsNullOrWhiteSpace(value)) properties["placeholder"] = value;
                    value = properties.GetValueOrDefault("value", string.Empty);
                }
                if (kind == "checkbox" && properties.TryGetValue("checked", out var checkedValue) && !bool.TryParse(checkedValue, out _))
                    diagnostics.Add(new(file, token.Line, token.Column, Severity.Error, "Checkbox property 'checked' must be true or false."));
                nodes.Add(new Element(kind, variant, value, properties, token.Line));
                continue;
            }
            diagnostics.Add(new(file, token.Line, token.Column, Severity.Error, $"Cannot parse TXL statement: '{source}'."));
        }

        if (nested)
        {
            var (last, file) = lines.Count > 0 ? lines[^1] : (new Token(string.Empty, 1, 1), "main.txl");
            diagnostics.Add(new(file, last.Line, last.Column, Severity.Error, "Missing closing ')' for this block."));
        }
        return nodes;
    }

    private void ReadStyleBlock(string file, Token opening)
    {
        while (position < lines.Count)
        {
            var (token, _) = lines[position++];
            var source = token.Value.Trim();
            if (source == ")" || source.Equals("end", StringComparison.OrdinalIgnoreCase)) return;
            var parenthesized = Regex.Match(source, @"^([A-Za-z][\w-]*)\s*\($");
            var colonHeading = Regex.Match(source, @"^([A-Za-z][\w-]*)\s*:\s*$");
            if (!parenthesized.Success && !colonHeading.Success)
            {
                diagnostics.Add(new(file, token.Line, token.Column, Severity.Error, "Expected a style section such as 'main:' or 'main ('."));
                continue;
            }
            var styleName = parenthesized.Success ? parenthesized.Groups[1].Value : colonHeading.Groups[1].Value;
            var sectionClosed = !parenthesized.Success;
            while (position < lines.Count)
            {
                var (propertyToken, _) = lines[position++];
                var propertyLine = propertyToken.Value.Trim();
                if (propertyLine == ")")
                {
                    if (!parenthesized.Success) return;
                    sectionClosed = true;
                    break;
                }
                if (!parenthesized.Success && (Regex.IsMatch(propertyLine, @"^[A-Za-z][\w-]*\s*:$") || Regex.IsMatch(propertyLine, @"^[A-Za-z][\w-]*\s*\($")))
                {
                    position--;
                    break;
                }
                var property = Regex.Match(propertyLine, @"^([A-Za-z][\w-]*)\s*(?::|=)\s*(.*)$");
                if (!property.Success)
                {
                    diagnostics.Add(new(file, propertyToken.Line, propertyToken.Column, Severity.Error, "Expected a style property such as 'color = white'."));
                    continue;
                }
                StyleCatalog.Add(styleName, property.Groups[1].Value, Unquote(property.Groups[2].Value));
            }
            if (!sectionClosed)
            {
                if (parenthesized.Success) diagnostics.Add(new(file, opening.Line, opening.Column, Severity.Error, $"Missing closing ')' for style '{styleName}'."));
                else diagnostics.Add(new(file, opening.Line, opening.Column, Severity.Error, "Missing closing ')' for the style block."));
                return;
            }
        }
        diagnostics.Add(new(file, opening.Line, opening.Column, Severity.Error, "Missing closing ')' for the style block."));
    }

    private void ReadActionsBlock(string file, Token opening)
    {
        while (position < lines.Count)
        {
            var (token, _) = lines[position++];
            var source = token.Value.Trim();
            if (source == ")") return;
            var heading = Regex.Match(source, @"^([A-Za-z][\w-]*)\((click|hover)\)\s*:\s*$", RegexOptions.IgnoreCase);
            if (!heading.Success)
            {
                diagnostics.Add(new(file, token.Line, token.Column, Severity.Error, "Expected an event section such as 'card(click):' or 'card(hover):'."));
                continue;
            }
            var className = heading.Groups[1].Value;
            var eventName = heading.Groups[2].Value.ToLowerInvariant();
            while (position < lines.Count)
            {
                var (operationToken, _) = lines[position++];
                var operationLine = operationToken.Value.Trim();
                if (operationLine == ")") return;
                if (Regex.IsMatch(operationLine, @"^[A-Za-z][\w-]*\((click|hover)\)\s*:\s*$", RegexOptions.IgnoreCase))
                {
                    position--;
                    break;
                }
                var operation = Regex.Match(operationLine, @"^([A-Za-z][\w-]*)\s*(?:=|:)\s*(.*)$");
                if (!operation.Success)
                {
                    diagnostics.Add(new(file, operationToken.Line, operationToken.Column, Severity.Error, "Expected an action or hover style such as 'open = https://example.com' or 'color = red'."));
                    continue;
                }
                var operationName = operation.Groups[1].Value;
                var operationValue = Unquote(operation.Groups[2].Value);
                ActionCatalog.Add(className, eventName, operationName, operationValue);
                if (eventName == "hover") StyleCatalog.Add("__hover_" + className, operationName, operationValue);
            }
        }
        diagnostics.Add(new(file, opening.Line, opening.Column, Severity.Error, "Missing closing ')' for the actions block."));
    }

    private static Dictionary<string, string> ParseArguments(string input)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var parts = new List<string>(); var start = 0; var quote = '\0';
        for (var i = 0; i < input.Length; i++)
        {
            if (input[i] is '"' or '\'') { if (quote == '\0') quote = input[i]; else if (quote == input[i]) quote = '\0'; }
            else if (input[i] == ',' && quote == '\0') { parts.Add(input[start..i]); start = i + 1; }
        }
        if (start < input.Length) parts.Add(input[start..]);
        foreach (var part in parts)
        {
            var match = Regex.Match(part.Trim(), @"^([A-Za-z_][\w]*)\s*:\s*(.*)$");
            if (match.Success) result[match.Groups[1].Value] = Unquote(match.Groups[2].Value);
        }
        return result;
    }

    private static bool TryAssignment(string source, string command, out string value)
    {
        var match = Regex.Match(source, "^" + Regex.Escape(command) + @"\s*=\s*(.*)$", RegexOptions.IgnoreCase);
        value = match.Success ? Unquote(match.Groups[1].Value) : string.Empty;
        return match.Success;
    }

    private static bool IsKnownProperty(string name) => name.ToLowerInvariant() is "font" or "size" or "color" or "background" or "weight" or "radius" or "padding" or "margin" or "width" or "height" or "opacity" or "align" or "shadow" or "border" or "hover" or "cursor" or "layout" or "x" or "y";

    private static string Unquote(string value)
    {
        value = value.Trim();
        return value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')) ? value[1..^1] : value;
    }
}

public static class StyleCatalog
{
    public static readonly Dictionary<string, Dictionary<string, string>> Styles = new(StringComparer.OrdinalIgnoreCase);
    public static void Add(string group, string key, string value)
    {
        if (!Styles.TryGetValue(group, out var style)) Styles[group] = style = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        style[key] = value;
    }
    public static void Reset() => Styles.Clear();
}

public static class ActionCatalog
{
    public static readonly Dictionary<string, List<ActionOperation>> Actions = new(StringComparer.OrdinalIgnoreCase);
    public static void Add(string className, string eventName, string name, string value)
    {
        var key = className + "." + eventName;
        if (!Actions.TryGetValue(key, out var operations)) Actions[key] = operations = new List<ActionOperation>();
        operations.Add(new ActionOperation(name, value));
    }
    public static void Reset() => Actions.Clear();
}

public static class ComponentCatalog
{
    public static readonly Dictionary<string, List<Node>> Definitions = new(StringComparer.OrdinalIgnoreCase);
    public static void Reset() => Definitions.Clear();
}

public static class RuntimeStateCatalog
{
    public static readonly Dictionary<string, string> Initial = new(StringComparer.OrdinalIgnoreCase);
    public static void Reset() => Initial.Clear();
}

public static class ProjectValidator
{
    public static ValidationResult Validate(string directory)
    {
        var diagnostics = new List<Diagnostic>();
        var path = Path.Combine(directory, "info.json");
        if (!File.Exists(path))
        {
            diagnostics.Add(new(path, 1, 1, Severity.Error, "Required file info.json is missing."));
            return new(null, diagnostics);
        }
        ProjectInfo? info = null;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new JsonException("The JSON root must be an object.");
            string Get(string name, bool required = true)
            {
                if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String) return value.GetString()!;
                if (required) diagnostics.Add(new(path, 1, 1, Severity.Error, $"Required field '{name}' is missing or is not a string."));
                return string.Empty;
            }
            var name = Get("name");
            var version = Get("version");
            var developer = Get("developer");
            var description = Get("description");
            var start = Get("start");
            var icon = Get("icon", false);
            if (!Regex.IsMatch(version, @"^\d+\.\d+\.\d+(?:-[\w.-]+)?$")) diagnostics.Add(new(path, 1, 1, Severity.Error, "Invalid version; expected a value such as 1.0.0."));
            var width = 900;
            var height = 600;
            var resizable = true;
            var background = "#20202A";
            if (root.TryGetProperty("window", out var window))
            {
                if (window.ValueKind != JsonValueKind.Object) diagnostics.Add(new(path, 1, 1, Severity.Error, "window must be an object."));
                else
                {
                    width = ReadInt(window, "width", 900);
                    height = ReadInt(window, "height", 600);
                    if (width is < 200 or > 7680 || height is < 150 or > 4320) diagnostics.Add(new(path, 1, 1, Severity.Error, "Window size must have width 200–7680 and height 150–4320."));
                    if (window.TryGetProperty("resizable", out var resize) && resize.ValueKind is JsonValueKind.True or JsonValueKind.False) resizable = resize.GetBoolean();
                    if (window.TryGetProperty("background", out var color) && color.ValueKind == JsonValueKind.String) background = color.GetString()!;
                }
            }
            if (!SafeRelative(start) || !File.Exists(Path.Combine(directory, start))) diagnostics.Add(new(path, 1, 1, Severity.Error, $"Start file '{start}' is missing or has an invalid path."));
            if (!string.IsNullOrWhiteSpace(icon) && (!SafeRelative(icon) || !File.Exists(Path.Combine(directory, icon)))) diagnostics.Add(new(path, 1, 1, Severity.Error, $"Icon '{icon}' is missing or has an invalid asset path."));
            info = new ProjectInfo(name, version, developer, description, start, icon, new WindowInfo(width, height, resizable, background));
        }
        catch (JsonException error) { diagnostics.Add(new(path, 1, 1, Severity.Error, "Invalid JSON: " + error.Message)); }
        return new(info, diagnostics);
    }

    private static int ReadInt(JsonElement element, string name, int fallback)
    {
        if (!element.TryGetProperty(name, out var value)) return fallback;
        return value.TryGetInt32(out var result) ? result : -1;
    }

    private static bool SafeRelative(string value) => !string.IsNullOrWhiteSpace(value) && !Path.IsPathRooted(value) && !value.Split('/', '\\').Any(part => part == "..") && !value.Contains(':');
}
