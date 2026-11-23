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
public sealed record Conditional(string Expression, List<Node> Children, List<Node> ElseChildren, int Line) : Node(Line);
public sealed record SetVariable(string Name, string Value, int Line) : Node(Line);
public sealed record StateVariable(string Name, string InitialValue, int Line) : Node(Line);
public sealed record Repeat(string Expression, List<Node> Children, int Line) : Node(Line);
public sealed record ForEach(string Variable, string Collection, List<Node> Children, int Line) : Node(Line);
public sealed record ComponentCall(string Name, Dictionary<string, string> Arguments, int Line) : Node(Line);
public sealed record ActionOperation(string Name, string Value);
public sealed record ProgramAst(List<Node> Nodes);
public sealed record ParseResult(ProgramAst? Program, List<Diagnostic> Diagnostics);
public sealed record WindowInfo(int Width = 900, int Height = 600, bool Resizable = true, string Background = "#20202A", string Mode = "normal", string? Title = null);
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
                    var isNumericColorValue = beforeHash >= start && char.IsWhiteSpace(line[end - 1]) && Regex.IsMatch(line[start..(beforeHash + 1)].Trim(), @"(?:^|\s)(?:\d+(?:\.\d+)?|solid)$", RegexOptions.IgnoreCase);
                    var isHexColor = beforeHash >= start && ((line[beforeHash] is '=' or ':') || isNumericColorValue) && Regex.IsMatch(line[(end + 1)..], @"^(?:[0-9A-Fa-f]{3}|[0-9A-Fa-f]{4}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})(?:\s|$)");
                    if (!isHexColor) break;
                }
                if (c == '"' || (c == '\'' && (inQuote ? quote == c : end == start || char.IsWhiteSpace(line[end - 1]))))
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
    private bool closesWithInlineElse;

    private Parser(List<(Token, string)> tokens, List<Diagnostic> diagnostics)
    {
        lines = tokens;
        this.diagnostics = diagnostics;
    }

    public static ParseResult Parse(string text, string file = "main.txl") => ParseCore(text, file, true);

    public static ParseResult ParseAdditionalPage(string text, string file) => ParseCore(text, file, false);

    private static ParseResult ParseCore(string text, string file, bool resetCatalogs)
    {
        if (resetCatalogs)
        {
            StyleCatalog.Reset();
            ActionCatalog.Reset();
            ComponentCatalog.Reset();
            RuntimeStateCatalog.Reset();
            WindowCatalog.Reset();
        }
        var diagnostics = new List<Diagnostic>();
        var parser = new Parser(Lexer.Lines(text, file, diagnostics), diagnostics);
        var nodes = parser.ReadBlock(false);
        return new ParseResult(diagnostics.Any(d => d.Severity == Severity.Error) ? null : new ProgramAst(nodes), diagnostics);
    }

    private List<Node> ReadBlock(bool nested, Dictionary<string, string>? groupProperties = null, bool allowInlineElse = false)
    {
        var nodes = new List<Node>();
        while (position < lines.Count)
        {
            var (token, file) = lines[position++];
            var source = token.Value.Trim();
            var inlineElse = Regex.IsMatch(source, @"^\)\s*else\s*\($", RegexOptions.IgnoreCase);
            if (source == ")" || source.Equals("end", StringComparison.OrdinalIgnoreCase) || inlineElse)
            {
                if (!nested) diagnostics.Add(new(file, token.Line, token.Column, Severity.Error, $"Unexpected block terminator '{source}'."));
                else if (inlineElse)
                {
                    if (allowInlineElse) closesWithInlineElse = true;
                    else diagnostics.Add(new(file, token.Line, token.Column, Severity.Error, "An 'else (' block must immediately follow an 'if' block."));
                }
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

            if (Regex.IsMatch(source, @"^window(?:s)?\s*\($", RegexOptions.IgnoreCase))
            {
                ReadWindowsBlock(file, token);
                continue;
            }

            if (Regex.IsMatch(source, @"^else\s*\($", RegexOptions.IgnoreCase))
            {
                diagnostics.Add(new(file, token.Line, token.Column, Severity.Error, "An 'else (' block must immediately follow an 'if = condition (' block."));
                ReadBlock(true);
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
                nodes.Add(ReadConditional(conditional.Groups[1].Value.Trim(), token.Line));
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
                nodes.Add(ReadConditional(legacyIf, token.Line));
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
                CheckProperty(file, token, property.Groups[1].Value, Unquote(property.Groups[2].Value));
                if (groupProperties is not null) groupProperties[property.Groups[1].Value] = Unquote(property.Groups[2].Value);
                else diagnostics.Add(new(file, token.Line, token.Column, Severity.Warning, $"Property '{property.Groups[1].Value}' is outside a group or element and will be ignored."));
                continue;
            }

            var element = Regex.Match(source, @"^([A-Za-z][\w-]*)(?:\(([^)]+)\)|\.([A-Za-z][\w-]*))?\s*(?:=\s*(.*))?$");
            if (element.Success)
            {
                var kind = element.Groups[1].Value.ToLowerInvariant();
                if (kind == "embed") kind = "html";
                if (kind is not ("text" or "button" or "image" or "video" or "html" or "embed" or "input" or "checkbox" or "list" or "line" or "space"))
                {
                    diagnostics.Add(new(file, token.Line, token.Column, Severity.Error, $"Unknown command '{kind}'."));
                    continue;
                }
                var variant = element.Groups[2].Success ? element.Groups[2].Value : element.Groups[3].Success ? element.Groups[3].Value : null;
                if (kind == "text" && string.Equals(variant, "headline", StringComparison.OrdinalIgnoreCase)) variant = "headline1";
                if (kind == "text" && variant is not null && Regex.IsMatch(variant, @"^headline[1-5]$", RegexOptions.IgnoreCase)) variant = variant.ToLowerInvariant();
                var value = element.Groups[4].Success ? Unquote(element.Groups[4].Value) : string.Empty;
                var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                while (position < lines.Count)
                {
                    var (propertyToken, propertyFile) = lines[position];
                    var next = propertyToken.Value.Trim();
                    var match = Regex.Match(next, @"^(action|placeholder|bind|value|checked|x|y|position|size|width|height|stretch|autoplay|loop)\s*(?::|=)\s*(.*)$", RegexOptions.IgnoreCase);
                    if (!match.Success) break;
                    CheckProperty(propertyFile, propertyToken, match.Groups[1].Value, Unquote(match.Groups[2].Value));
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

    private Conditional ReadConditional(string expression, int line)
    {
        closesWithInlineElse = false;
        var children = ReadBlock(true, allowInlineElse: true);
        var elseChildren = new List<Node>();
        if (closesWithInlineElse)
        {
            closesWithInlineElse = false;
            elseChildren = ReadBlock(true);
        }
        else if (position < lines.Count && Regex.IsMatch(lines[position].Token.Value.Trim(), @"^else\s*\($", RegexOptions.IgnoreCase))
        {
            position++;
            elseChildren = ReadBlock(true);
        }
        return new Conditional(expression, children, elseChildren, line);
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
                CheckProperty(file, propertyToken, property.Groups[1].Value, Unquote(property.Groups[2].Value));
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
            var heading = Regex.Match(source, @"^([A-Za-z][\w-]*)\((click|hover|keydown)\)\s*:\s*$", RegexOptions.IgnoreCase);
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
                if (Regex.IsMatch(operationLine, @"^[A-Za-z][\w-]*\((click|hover|keydown)\)\s*:\s*$", RegexOptions.IgnoreCase))
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

    private void ReadWindowsBlock(string file, Token opening)
    {
        while (position < lines.Count)
        {
            var (token, _) = lines[position++];
            var source = token.Value.Trim();
            if (source == ")") return;
            var setting = Regex.Match(source, @"^(title|icon|size|mode)\s*(?::|=)\s*(.*)$", RegexOptions.IgnoreCase);
            if (!setting.Success)
            {
                diagnostics.Add(new(file, token.Line, token.Column, Severity.Error, "Expected a windows setting: title, icon, size, or mode."));
                continue;
            }
            var name = setting.Groups[1].Value.ToLowerInvariant();
            var value = Unquote(setting.Groups[2].Value);
            if (string.IsNullOrWhiteSpace(value))
            {
                diagnostics.Add(new(file, token.Line, token.Column, Severity.Error, $"Window setting '{name}' cannot be empty."));
                continue;
            }
            if (name == "size")
            {
                var match = Regex.Match(value, @"^(\d{3,4})\s*[x×]\s*(\d{3,4})$", RegexOptions.IgnoreCase);
                if (!match.Success || !int.TryParse(match.Groups[1].Value, out var width) || !int.TryParse(match.Groups[2].Value, out var height) || width is < 200 or > 7680 || height is < 150 or > 4320)
                {
                    diagnostics.Add(new(file, token.Line, token.Column, Severity.Error, "Window size must be WIDTHxHEIGHT with width 200–7680 and height 150–4320 (for example, 1100x760)."));
                    continue;
                }
                WindowCatalog.Settings[name] = $"{width}x{height}";
                continue;
            }
            if (name == "mode" && value.ToLowerInvariant() is not ("normal" or "maximized" or "fullscreen" or "borderless"))
            {
                diagnostics.Add(new(file, token.Line, token.Column, Severity.Error, "Window mode must be normal, maximized, fullscreen, or borderless."));
                continue;
            }
            if (name == "icon" && (Path.IsPathRooted(value) || value.Split('/', '\\').Any(part => part == "..") || value.Contains(':')))
            {
                diagnostics.Add(new(file, token.Line, token.Column, Severity.Error, "Window icon must be a relative path inside the project, such as assets/app.ico."));
                continue;
            }
            WindowCatalog.Settings[name] = value;
        }
        var (last, lastFile) = lines.Count > 0 ? lines[^1] : (opening, file);
        diagnostics.Add(new(lastFile, last.Line, last.Column, Severity.Error, "Missing closing ')' for the window block."));
    }

    private void CheckProperty(string file, Token token, string name, string value)
    {
        var normalized = name.ToLowerInvariant();
        if (normalized == "position" && !Regex.IsMatch(value, @"^x\s*-?\d+(?:\.\d+)?\s+y\s*-?\d+(?:\.\d+)?$", RegexOptions.IgnoreCase))
            diagnostics.Add(new(file, token.Line, token.Column, Severity.Error, "Position must use 'xNUMBER yNUMBER', for example 'position = x18 y3'."));
        if (normalized is "size" or "width" or "height" or "x" or "y" or "radius")
        {
            var max = normalized == "radius" ? 200 : 10000;
            if (!double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number) || number < 0 || number > max)
                diagnostics.Add(new(file, token.Line, token.Column, Severity.Error, $"Property '{name}' must be a number between 0 and {max}."));
        }
        if (normalized == "font" && value.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            var extension = Uri.TryCreate(value, UriKind.Absolute, out var fontUri) ? Path.GetExtension(fontUri.AbsolutePath) : string.Empty;
            if (fontUri is null || fontUri.Scheme != Uri.UriSchemeHttps || !(extension.Equals(".ttf", StringComparison.OrdinalIgnoreCase) || extension.Equals(".otf", StringComparison.OrdinalIgnoreCase)))
                diagnostics.Add(new(file, token.Line, token.Column, Severity.Error, "Remote fonts must be HTTPS URLs ending in .ttf or .otf. TXL downloads and packages the font during build."));
        }
        if (normalized == "align" && value.ToLowerInvariant() is not ("left" or "center" or "right" or "stretch"))
            diagnostics.Add(new(file, token.Line, token.Column, Severity.Error, "Alignment must be left, center, right, or stretch."));
        if (normalized == "layout" && value.ToLowerInvariant() is not ("vertical" or "horizontal" or "absolute" or "canvas"))
            diagnostics.Add(new(file, token.Line, token.Column, Severity.Error, "Layout must be vertical, horizontal, or absolute."));
        if (normalized is "border" or "stroke" && !Regex.IsMatch(value, @"^(?:(?:\d+(?:\.\d+)?\s+)?(?:solid\s+)?)?(?:#[0-9a-fA-F]{3,8}|[A-Za-z][A-Za-z0-9 _-]*)$"))
            diagnostics.Add(new(file, token.Line, token.Column, Severity.Error, "Border/stroke must be a color or a width and color, such as '1 #333333'."));
        if (normalized == "shadow" && !Regex.IsMatch(value, @"^(?:none|(?:(?:\d+(?:\.\d+)?\s+){0,2})(?:#[0-9a-fA-F]{3,8}|[A-Za-z][A-Za-z0-9 _-]*))$", RegexOptions.IgnoreCase))
            diagnostics.Add(new(file, token.Line, token.Column, Severity.Error, "Shadow must be a color or 'depth blur color', such as '4 12 #00000066'."));
        if ((normalized is "autoplay" or "loop") && !bool.TryParse(value, out _))
            diagnostics.Add(new(file, token.Line, token.Column, Severity.Error, $"Property '{name}' must be true or false."));
    }

    private static bool TryAssignment(string source, string command, out string value)
    {
        var match = Regex.Match(source, "^" + Regex.Escape(command) + @"\s*=\s*(.*)$", RegexOptions.IgnoreCase);
        value = match.Success ? Unquote(match.Groups[1].Value) : string.Empty;
        return match.Success;
    }

    private static bool IsKnownProperty(string name) => name.ToLowerInvariant() is "font" or "size" or "color" or "background" or "weight" or "radius" or "padding" or "margin" or "width" or "height" or "opacity" or "align" or "shadow" or "border" or "stroke" or "hover" or "cursor" or "layout" or "x" or "y" or "position";

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

public static class WindowCatalog
{
    public static readonly Dictionary<string, string> Settings = new(StringComparer.OrdinalIgnoreCase);
    public static void Reset() => Settings.Clear();
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
            var mode = "normal";
            string? windowTitle = null;
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
                    if (window.TryGetProperty("title", out var titleValue))
                    {
                        if (titleValue.ValueKind == JsonValueKind.String) windowTitle = titleValue.GetString();
                        else diagnostics.Add(new(path, 1, 1, Severity.Error, "window.title must be a string."));
                    }
                    if (window.TryGetProperty("mode", out var modeValue))
                    {
                        if (modeValue.ValueKind == JsonValueKind.String) mode = modeValue.GetString()!;
                        else diagnostics.Add(new(path, 1, 1, Severity.Error, "window.mode must be a string."));
                    }
                    if (mode.ToLowerInvariant() is not ("normal" or "maximized" or "fullscreen" or "borderless")) diagnostics.Add(new(path, 1, 1, Severity.Error, "Window mode must be normal, maximized, fullscreen, or borderless."));
                }
            }
            if (!SafeRelative(start) || !File.Exists(Path.Combine(directory, start))) diagnostics.Add(new(path, 1, 1, Severity.Error, $"Start file '{start}' is missing or has an invalid path."));
            if (!string.IsNullOrWhiteSpace(icon) && (!SafeRelative(icon) || !File.Exists(Path.Combine(directory, icon)))) diagnostics.Add(new(path, 1, 1, Severity.Error, $"Icon '{icon}' is missing or has an invalid asset path."));
            info = new ProjectInfo(name, version, developer, description, start, icon, new WindowInfo(width, height, resizable, background, mode, windowTitle));
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
