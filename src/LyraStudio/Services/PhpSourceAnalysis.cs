using System.Text;
using System.Text.RegularExpressions;

namespace LyraStudio.Services;

/// <summary>
/// Balanced-delimiter helpers for read-only inspection of PHP source.
/// These routines locate declarations/calls; they do not evaluate PHP code.
/// </summary>
internal static class PhpSourceAnalysis
{
    public static string? ExtractMethodBody(string source, string methodName)
    {
        var pattern = $@"\bfunction\s+{Regex.Escape(methodName)}\s*\([^)]*\)[^{{;]*\{{";
        var match = Regex.Match(source, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success) return null;

        var open = source.IndexOf('{', match.Index + match.Length - 1);
        if (open < 0) return null;
        var close = FindMatching(source, open, '{', '}');
        return close < 0 ? null : source[(open + 1)..close];
    }

    public static int FindMatching(string source, int openIndex, char open, char close)
    {
        if (openIndex < 0 || openIndex >= source.Length || source[openIndex] != open)
            return -1;

        var depth = 0;
        var quote = '\0';
        var lineComment = false;
        var blockComment = false;
        var escaped = false;

        for (var index = openIndex; index < source.Length; index++)
        {
            var current = source[index];
            var next = index + 1 < source.Length ? source[index + 1] : '\0';

            if (lineComment)
            {
                if (current is '\r' or '\n') lineComment = false;
                continue;
            }

            if (blockComment)
            {
                if (current == '*' && next == '/')
                {
                    blockComment = false;
                    index++;
                }
                continue;
            }

            if (quote != '\0')
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }
                if (current == '\\')
                {
                    escaped = true;
                    continue;
                }
                if (current == quote) quote = '\0';
                continue;
            }

            if (current is '\'' or '"')
            {
                quote = current;
                continue;
            }
            if (current == '/' && next == '/')
            {
                lineComment = true;
                index++;
                continue;
            }
            if (current == '#' )
            {
                lineComment = true;
                continue;
            }
            if (current == '/' && next == '*')
            {
                blockComment = true;
                index++;
                continue;
            }

            if (current == open) depth++;
            else if (current == close)
            {
                depth--;
                if (depth == 0) return index;
            }
        }

        return -1;
    }

    public static IReadOnlyList<string> SplitTopLevelArguments(string source)
    {
        var result = new List<string>();
        var start = 0;
        var round = 0;
        var square = 0;
        var curly = 0;
        var quote = '\0';
        var escaped = false;
        var lineComment = false;
        var blockComment = false;

        for (var index = 0; index < source.Length; index++)
        {
            var current = source[index];
            var next = index + 1 < source.Length ? source[index + 1] : '\0';

            if (lineComment)
            {
                if (current is '\r' or '\n') lineComment = false;
                continue;
            }
            if (blockComment)
            {
                if (current == '*' && next == '/')
                {
                    blockComment = false;
                    index++;
                }
                continue;
            }
            if (quote != '\0')
            {
                if (escaped) { escaped = false; continue; }
                if (current == '\\') { escaped = true; continue; }
                if (current == quote) quote = '\0';
                continue;
            }

            if (current is '\'' or '"') { quote = current; continue; }
            if (current == '/' && next == '/') { lineComment = true; index++; continue; }
            if (current == '#') { lineComment = true; continue; }
            if (current == '/' && next == '*') { blockComment = true; index++; continue; }

            switch (current)
            {
                case '(': round++; break;
                case ')': round--; break;
                case '[': square++; break;
                case ']': square--; break;
                case '{': curly++; break;
                case '}': curly--; break;
                case ',' when round == 0 && square == 0 && curly == 0:
                    result.Add(source[start..index].Trim());
                    start = index + 1;
                    break;
            }
        }

        var final = source[start..].Trim();
        if (final.Length > 0) result.Add(final);
        return result;
    }

    public static IReadOnlyList<PhpControlCall> FindControlCalls(string body)
    {
        var result = new List<PhpControlCall>();
        var pattern = new Regex(
            @"(?<target>\$this|\$[a-zA-Z_][a-zA-Z0-9_]*)\s*->\s*(?<method>add_control|add_responsive_control)\s*\(",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        foreach (Match match in pattern.Matches(body))
        {
            var open = body.IndexOf('(', match.Index + match.Length - 1);
            var close = FindMatching(body, open, '(', ')');
            if (close < 0) continue;

            var argsText = body[(open + 1)..close];
            var args = SplitTopLevelArguments(argsText);
            if (args.Count < 2) continue;

            var idValue = PhpStaticValueParser.ParseExpression(args[0]);
            var id = Convert.ToString(idValue) ?? "";
            if (string.IsNullOrWhiteSpace(id)) continue;

            var config = PhpStaticValueParser.ParseExpression(args[1]) as IDictionary<string, object?>;
            if (config is null)
                continue;

            var fieldsVariable = Regex.Match(args[1],
                @"['""]fields['""]\s*=>\s*\$(?<name>[a-zA-Z_][a-zA-Z0-9_]*)\s*->\s*get_controls\s*\(",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Groups["name"].Value;

            result.Add(new PhpControlCall(
                match.Groups["target"].Value,
                match.Groups["method"].Value,
                id,
                config,
                fieldsVariable,
                args[1]));
        }

        return result;
    }

    public static object? GetValue(IDictionary<string, object?> values, string key)
    {
        foreach (var pair in values)
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                return pair.Value;
        return null;
    }

    public static string? GetString(IDictionary<string, object?> values, string key)
    {
        var value = GetValue(values, key);
        return value is null ? null : Convert.ToString(value);
    }

    public static bool GetBoolean(IDictionary<string, object?> values, string key, bool fallback = false)
    {
        var value = GetValue(values, key);
        if (value is bool boolean) return boolean;
        return bool.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    }
}

internal sealed record PhpControlCall(
    string Target,
    string Method,
    string Id,
    IDictionary<string, object?> Config,
    string FieldsVariable,
    string RawConfig);
