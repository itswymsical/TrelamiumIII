using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace TrelamiumIII.Content.DevTools.VfxLab.Generation;

/// <summary> A problem in a stage or host file, in the shape fxc reports its own errors: <c>file(line): message</c>. </summary>
internal sealed class StageFormatException(string source, int line, string message) : Exception($"{source}({line}): {message}");

/// <summary> Reads the <c>//@</c> directives at the top of a stage or host file. Pure text, so it runs without the game. </summary>
internal static class StageParser
{
    public const string StageToken = "Stage";

    private const string Directive = "//@";
    private const string StagesMarker = "//@stages";
    private const string CallsMarker = "//@calls";

    private static readonly Regex TokenPattern = new(@"\$([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);
    private static readonly Regex SamplerPattern = new(@"^\s*sampler2D\s+\w+\s*[:=;]", RegexOptions.Compiled | RegexOptions.Multiline);

    public static IEnumerable<Match> Tokens(string text) => TokenPattern.Matches(text);

    public static string ReplaceTokens(string text, Func<string, string> replace) => TokenPattern.Replace(text, match => replace(match.Groups[1].Value));

    /// <summary> Git may hand either line ending back on Windows, and a generated shader's source has to hash the same either way. </summary>
    public static string Normalize(string text) => text.Replace("\r\n", "\n");

    public static StageDefinition ParseStage(string source, string text)
    {
        string[] lines = Normalize(text).Split('\n');

        string? name = null;
        StagePhase phase = default;
        HashSet<string> needs = new(StringComparer.Ordinal);
        List<ParamDefinition> parameters = [];
        int bodyStart = 0;
        bool inCode = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();

            if (!line.StartsWith(Directive, StringComparison.Ordinal))
            {
                inCode |= line.Length > 0 && !line.StartsWith("//", StringComparison.Ordinal);
                continue;
            }

            if (inCode)
                throw new StageFormatException(source, i + 1, "directives have to come before the code.");

            string[] parts = line[Directive.Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries);

            switch (parts.FirstOrDefault())
            {
                case "stage":
                    (name, phase) = ParseStageDirective(source, i + 1, parts, needs);
                    break;
                case "param":
                    ParamDefinition parameter = ParseParam(source, i + 1, parts);

                    if (parameters.Any(existing => existing.Name == parameter.Name))
                        throw new StageFormatException(source, i + 1, $"the parameter {parameter.Name} is declared twice.");

                    parameters.Add(parameter);
                    break;
                default:
                    throw new StageFormatException(source, i + 1, $"unknown directive '{line}'.");
            }

            bodyStart = i + 1;
        }

        if (name is null)
            throw new StageFormatException(source, 1, "no //@stage directive.");

        while (bodyStart < lines.Length && string.IsNullOrWhiteSpace(lines[bodyStart]))
            bodyStart++;

        string body = string.Join('\n', lines[bodyStart..]).TrimEnd();

        CheckTokens(source, lines, bodyStart, parameters);

        return new StageDefinition(name, phase, needs, parameters, body, source, bodyStart + 1);
    }

    public static HostDefinition ParseHost(string source, string text)
    {
        string[] lines = Normalize(text).Split('\n');

        string? name = null;
        HashSet<string> provides = new(StringComparer.Ordinal);
        int headerEnd = 0;
        int stages = -1;
        int calls = -1;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();

            if (!line.StartsWith(Directive, StringComparison.Ordinal))
                continue;

            if (line == StagesMarker)
                stages = i;
            else if (line == CallsMarker)
                calls = i;
            else
            {
                string[] parts = line[Directive.Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries);

                switch (parts.FirstOrDefault())
                {
                    case "host" when parts.Length == 2:
                        name = parts[1];
                        break;
                    case "provides":
                        provides.UnionWith(parts.Skip(1));
                        break;
                    default:
                        throw new StageFormatException(source, i + 1, $"unknown directive '{line}'.");
                }

                headerEnd = i + 1;
            }
        }

        if (name is null)
            throw new StageFormatException(source, 1, "no //@host directive.");

        if (stages < 0 || calls < stages)
            throw new StageFormatException(source, 1, $"needs {StagesMarker} and then {CallsMarker}.");

        string Slice(int from, int to) => string.Join('\n', lines[from..to]);

        return new HostDefinition(
            name,
            provides,
            Slice(headerEnd, stages),
            headerEnd + 1,
            Slice(stages + 1, calls),
            stages + 2,
            Slice(calls + 1, lines.Length),
            calls + 2,
            source,
            SamplerPattern.Matches(string.Join('\n', lines)).Count);
    }

    private static (string Name, StagePhase Phase) ParseStageDirective(string source, int line, string[] parts, HashSet<string> needs)
    {
        if (parts.Length < 2)
            throw new StageFormatException(source, line, "//@stage needs a name.");

        StagePhase? phase = null;

        foreach (string option in parts.Skip(2))
        {
            (string key, string value) = SplitOption(source, line, option);

            switch (key)
            {
                case "phase":
                    phase = Enum.TryParse(value, out StagePhase parsed) && Enum.IsDefined(parsed)
                        ? parsed
                        : throw new StageFormatException(source, line, $"unknown phase '{value}'.");
                    break;
                case "needs":
                    needs.UnionWith(value.Split(',', StringSplitOptions.RemoveEmptyEntries));
                    break;
                default:
                    throw new StageFormatException(source, line, $"unknown option '{key}'.");
            }
        }

        return (parts[1], phase ?? throw new StageFormatException(source, line, "//@stage needs a phase=."));
    }

    private static ParamDefinition ParseParam(string source, int line, string[] parts)
    {
        if (parts.Length < 4)
            throw new StageFormatException(source, line, "//@param needs a name, a type and a default.");

        string name = parts[1];
        string defaultText = parts[3];

        if (name == StageToken)
            throw new StageFormatException(source, line, $"{StageToken} is the name of the stage's function.");

        float min = float.NegativeInfinity;
        float max = float.PositiveInfinity;

        foreach (string option in parts.Skip(4))
        {
            (string key, string value) = SplitOption(source, line, option);

            if (key != "range")
                throw new StageFormatException(source, line, $"unknown option '{key}'.");

            int separator = value.IndexOf("..", StringComparison.Ordinal);

            if (separator < 0 || !TryFloat(value[..separator], out min) || !TryFloat(value[(separator + 2)..], out max))
                throw new StageFormatException(source, line, $"range '{value}' isn't low..high.");
        }

        return parts[2] switch
        {
            "float" => new ParamDefinition(name, ParamKind.Float, Numbers(source, line, defaultText, 1), null, min, max),
            "float2" => new ParamDefinition(name, ParamKind.Float2, Numbers(source, line, defaultText, 2), null, min, max),
            "color" => new ParamDefinition(name, ParamKind.Color, Numbers(source, line, defaultText, 3), null, min, max),
            "int" => new ParamDefinition(name, ParamKind.Int, Numbers(source, line, defaultText, 1), null, min, max),
            "noise" => new ParamDefinition(name, ParamKind.Noise, [], defaultText, min, max),
            _ => throw new StageFormatException(source, line, $"unknown type '{parts[2]}'."),
        };
    }

    private static (string Key, string Value) SplitOption(string source, int line, string option)
    {
        int separator = option.IndexOf('=');

        return separator > 0
            ? (option[..separator], option[(separator + 1)..])
            : throw new StageFormatException(source, line, $"'{option}' isn't key=value.");
    }

    private static float[] Numbers(string source, int line, string text, int count)
    {
        string[] parts = text.Split(',');
        var values = new float[parts.Length];

        for (int i = 0; i < parts.Length; i++)
        {
            if (!TryFloat(parts[i], out values[i]))
                throw new StageFormatException(source, line, $"'{parts[i]}' isn't a number.");
        }

        return values.Length == count
            ? values
            : throw new StageFormatException(source, line, $"'{text}' should be {count} numbers.");
    }

    private static bool TryFloat(string text, out float value)
        => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    /// <summary> A typo in a token is found here, with its line, rather than as a confusing error from fxc in generated source. </summary>
    private static void CheckTokens(string source, string[] lines, int bodyStart, List<ParamDefinition> parameters)
    {
        bool definesStage = false;

        for (int i = bodyStart; i < lines.Length; i++)
        {
            foreach (Match match in Tokens(lines[i]))
            {
                string token = match.Groups[1].Value;

                if (token == StageToken)
                    definesStage = true;
                else if (parameters.All(parameter => parameter.Name != token))
                    throw new StageFormatException(source, i + 1, $"${token} isn't a parameter of this stage.");
            }
        }

        if (!definesStage)
            throw new StageFormatException(source, bodyStart + 1, $"needs a function named ${StageToken}.");
    }
}
