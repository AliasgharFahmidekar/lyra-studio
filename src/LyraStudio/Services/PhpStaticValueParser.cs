using System.Globalization;
using System.Text;

namespace LyraStudio.Services;

/// <summary>
/// Reads a deliberately small, non-executing subset of PHP expressions used
/// by Elementor widget metadata and Lyra Page Builder recipe files.
/// Unknown expressions are preserved as strings/null; no PHP is evaluated.
/// </summary>
public static class PhpStaticValueParser
{
    public static object? ParseExpression(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return null;

        var parser = new Parser(expression);
        return parser.ParseValue();
    }

    public static object? ParseReturnExpression(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var expressionStart = FindFirstReturnExpression(source);
        return expressionStart < 0 ? null : ParseExpression(source[expressionStart..]);
    }

    private static int FindFirstReturnExpression(string source)
    {
        var quote = (char)0;
        var escaped = false;
        var lineComment = false;
        var blockComment = false;

        for (var index = 0; index < source.Length; index++)
        {
            var current = source[index];
            var next = index + 1 < source.Length ? source[index + 1] : (char)0;

            if (lineComment)
            {
                if (current == (char)13 || current == (char)10) lineComment = false;
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

            if (quote != (char)0)
            {
                if (escaped) { escaped = false; continue; }
                if (current == (char)92) { escaped = true; continue; }
                if (current == quote) quote = (char)0;
                continue;
            }

            if (current == (char)39 || current == '"')
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
            if (current == '#')
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

            if (index + 6 > source.Length ||
                !source.AsSpan(index, 6).Equals("return", StringComparison.Ordinal))
                continue;

            var beforeIsIdentifier = index > 0 &&
                (char.IsLetterOrDigit(source[index - 1]) || source[index - 1] == '_');
            var afterIndex = index + 6;
            var afterIsIdentifier = afterIndex < source.Length &&
                (char.IsLetterOrDigit(source[afterIndex]) || source[afterIndex] == '_');
            if (beforeIsIdentifier || afterIsIdentifier)
                continue;

            while (afterIndex < source.Length && char.IsWhiteSpace(source[afterIndex]))
                afterIndex++;

            if (afterIndex < source.Length)
                return afterIndex;
        }

        return -1;
    }

    private sealed class Parser
    {
        private readonly string _source;
        private int _position;

        public Parser(string source) => _source = source;

        public object? ParseValue()
        {
            SkipTrivia();
            var value = ParsePrimary();

            while (true)
            {
                SkipTrivia();
                if (!Consume('.'))
                    break;

                var right = ParsePrimary();
                value = Convert.ToString(value, CultureInfo.InvariantCulture) +
                        Convert.ToString(right, CultureInfo.InvariantCulture);
            }

            return value;
        }

        private object? ParsePrimary()
        {
            SkipTrivia();
            if (End) return null;

            if (Peek('\'') || Peek('"'))
                return ParseString();

            if (Peek('['))
                return ParseArray(']');

            if (Peek('$'))
            {
                _position++;
                var variable = "$" + ReadIdentifier();
                SkipTrivia();
                if (Consume("::"))
                    return ParseStaticCall(variable, ReadIdentifier());
                if (Consume("->"))
                {
                    var method = ReadIdentifier();
                    SkipTrivia();
                    if (Peek('('))
                        _ = ParseFunctionCall("->" + method);
                    return variable + "->" + method + "()";
                }
                return variable;
            }

            if (ConsumeWord("array"))
                return ParseArray(')');

            if (char.IsDigit(Current) || (Current == '-' && HasNext && char.IsDigit(_source[_position + 1])))
                return ParseNumber();

            if (IsIdentifierStart(Current) || Current == '\\')
            {
                var identifier = ReadIdentifier();
                SkipTrivia();

                if (Consume("::"))
                {
                    var member = ReadIdentifier();
                    SkipTrivia();
                    if (Peek('('))
                        return ParseFunctionCall(identifier + "::" + member);
                    return member;
                }

                if (Peek('('))
                    return ParseFunctionCall(identifier);

                return identifier switch
                {
                    "true" or "TRUE" => true,
                    "false" or "FALSE" => false,
                    "null" or "NULL" => null,
                    "LYRA_URL" => "asset://",
                    _ => identifier
                };
            }

            return null;
        }

        private object? ParseArray(char closing)
        {
            if (closing == ')')
            {
                if (!Consume('(')) return null;
            }
            else if (!Consume('['))
            {
                return null;
            }

            var values = new List<object?>();
            var keyed = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            var hasKeys = false;

            SkipTrivia();
            while (!End && !Peek(closing))
            {
                var item = ParseValue();
                SkipTrivia();
                if (Consume("=>"))
                {
                    var value = ParseValue();
                    var key = Convert.ToString(item, CultureInfo.InvariantCulture) ?? "";
                    keyed[key] = value;
                    hasKeys = true;
                }
                else
                {
                    values.Add(item);
                }

                SkipTrivia();
                if (Consume(','))
                {
                    SkipTrivia();
                    continue;
                }

                break;
            }

            Consume(closing);
            if (!hasKeys)
                return values;

            for (var index = 0; index < values.Count; index++)
                keyed[index.ToString(CultureInfo.InvariantCulture)] = values[index];
            return keyed;
        }

        private object? ParseFunctionCall(string function)
        {
            if (!Consume('(')) return function;

            var args = new List<object?>();
            SkipTrivia();
            while (!End && !Peek(')'))
            {
                args.Add(ParseValue());
                SkipTrivia();
                if (Consume(','))
                {
                    SkipTrivia();
                    continue;
                }
                break;
            }
            Consume(')');

            var name = function.Split("::", StringSplitOptions.None)[^1];
            return EvaluateCall(name, args);
        }

        private object? ParseStaticCall(string variable, string method)
        {
            SkipTrivia();
            if (!Peek('('))
                return method;

            var result = ParseFunctionCall(variable + "::" + method);
            return result;
        }

        private static object? EvaluateCall(string method, IReadOnlyList<object?> args)
        {
            object? Arg(int index) => index < args.Count ? args[index] : null;

            if (method is "__" or "_x" or "_n" or "esc_html__" or "esc_attr__" or "sanitize_key" or "home_url" or "site_url")
                return Arg(0);

            if (method.Equals("container", StringComparison.OrdinalIgnoreCase))
            {
                return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["id"] = "",
                    ["elType"] = "container",
                    ["isInner"] = false,
                    ["settings"] = Arg(0) ?? new Dictionary<string, object?>(),
                    ["elements"] = Arg(1) ?? new List<object?>()
                };
            }

            if (method.Equals("widget", StringComparison.OrdinalIgnoreCase))
            {
                return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["id"] = "",
                    ["elType"] = "widget",
                    ["widgetType"] = Arg(0),
                    ["settings"] = Arg(1) ?? new Dictionary<string, object?>(),
                    ["elements"] = new List<object?>()
                };
            }

            if (method.Equals("item", StringComparison.OrdinalIgnoreCase))
                return Arg(0) ?? new Dictionary<string, object?>();

            if (method.Equals("gap", StringComparison.OrdinalIgnoreCase))
            {
                var size = Arg(0) ?? 0;
                return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["unit"] = "px",
                    ["size"] = size,
                    ["column"] = Convert.ToString(size, CultureInfo.InvariantCulture) ?? "0",
                    ["row"] = Convert.ToString(size, CultureInfo.InvariantCulture) ?? "0",
                    ["isLinked"] = true
                };
            }

            if (method.Equals("dims", StringComparison.OrdinalIgnoreCase))
            {
                var numbers = args.Select(x => Convert.ToString(x, CultureInfo.InvariantCulture) ?? "0").ToList();
                string top, right, bottom, left;
                if (numbers.Count <= 1)
                    top = right = bottom = left = numbers.Count == 0 ? "0" : numbers[0];
                else if (numbers.Count == 2)
                {
                    top = bottom = numbers[0];
                    right = left = numbers[1];
                }
                else
                {
                    top = numbers.ElementAtOrDefault(0) ?? "0";
                    right = numbers.ElementAtOrDefault(1) ?? "0";
                    bottom = numbers.ElementAtOrDefault(2) ?? "0";
                    left = numbers.ElementAtOrDefault(3) ?? "0";
                }

                return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["unit"] = "px",
                    ["top"] = top,
                    ["right"] = right,
                    ["bottom"] = bottom,
                    ["left"] = left,
                    ["isLinked"] = top == right && right == bottom && bottom == left
                };
            }

            if (method.Equals("dynamic", StringComparison.OrdinalIgnoreCase))
                return $"[dynamic:{Convert.ToString(Arg(0), CultureInfo.InvariantCulture)}]";

            return null;
        }

        private string ParseString()
        {
            var quote = Current;
            _position++;
            var result = new StringBuilder();

            while (!End)
            {
                var character = Current;
                _position++;

                if (character == quote)
                    break;

                if (character == '\\' && !End)
                {
                    var escaped = Current;
                    _position++;
                    if (quote == '\'')
                    {
                        result.Append(escaped is '\\' or '\'' ? escaped : '\\');
                        if (escaped is not '\\' and not '\'')
                            result.Append(escaped);
                    }
                    else
                    {
                        result.Append(escaped switch
                        {
                            'n' => '\n',
                            'r' => '\r',
                            't' => '\t',
                            '\\' => '\\',
                            '"' => '"',
                            '$' => '$',
                            _ => escaped
                        });
                    }
                    continue;
                }

                result.Append(character);
            }

            return result.ToString();
        }

        private object ParseNumber()
        {
            var start = _position;
            if (Peek('-')) _position++;
            while (!End && (char.IsDigit(Current) || Current == '.'))
                _position++;

            var text = _source[start.._position];
            if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                return integer;
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                return number;
            return 0;
        }

        private string ReadIdentifier()
        {
            var start = _position;
            while (!End && (char.IsLetterOrDigit(Current) || Current is '_' or '\\'))
                _position++;
            return _source[start.._position].TrimStart('\\');
        }

        private bool ConsumeWord(string value)
        {
            SkipTrivia();
            if (!_source.AsSpan(_position).StartsWith(value, StringComparison.Ordinal))
                return false;

            var end = _position + value.Length;
            if (end < _source.Length && (char.IsLetterOrDigit(_source[end]) || _source[end] == '_'))
                return false;

            _position = end;
            SkipTrivia();
            return true;
        }

        private void SkipTrivia()
        {
            while (!End)
            {
                if (char.IsWhiteSpace(Current))
                {
                    _position++;
                    continue;
                }

                if (Current == '/' && HasNext && _source[_position + 1] == '/')
                {
                    _position += 2;
                    while (!End && Current is not '\r' and not '\n') _position++;
                    continue;
                }

                if (Current == '#')
                {
                    while (!End && Current is not '\r' and not '\n') _position++;
                    continue;
                }

                if (Current == '/' && HasNext && _source[_position + 1] == '*')
                {
                    _position += 2;
                    while (_position + 1 < _source.Length &&
                           !(_source[_position] == '*' && _source[_position + 1] == '/'))
                        _position++;
                    if (_position + 1 < _source.Length) _position += 2;
                    continue;
                }

                break;
            }
        }

        private bool Consume(string token)
        {
            SkipTrivia();
            if (!_source.AsSpan(_position).StartsWith(token, StringComparison.Ordinal))
                return false;
            _position += token.Length;
            return true;
        }

        private bool Consume(char character)
        {
            SkipTrivia();
            if (!Peek(character)) return false;
            _position++;
            return true;
        }

        private bool Peek(char character) => !End && Current == character;
        private bool End => _position >= _source.Length;
        private bool HasNext => _position + 1 < _source.Length;
        private char Current => End ? '\0' : _source[_position];
        private static bool IsIdentifierStart(char value) => char.IsLetter(value) || value == '_';
    }
}
