﻿namespace Nett.Parser.Productions
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;

    internal static class ValueProduction
    {
        private static readonly char[] WhitspaceCharSet =
        {
            '\u0009', '\u000A', '\u000B', '\u000D', '\u0020', '\u0085', '\u00A0',
            '\u1680', '\u2000', '\u2001', '\u2002', '\u2003', '\u2004', '\u2005', '\u2006',
            '\u2007', '\u2008', '\u2009', '\u200A', '\u2028', '\u2029', '\u202F', '\u205F',
            '\u3000',
        };

        public static TomlObject Apply(ITomlRoot root, TokenBuffer tokens)
        {
            var value = ParseTomlValue(root, tokens);

            if (value == null)
            {
                var t = tokens.Peek();
                if (t.IsEmpty || t.IsEof || t.IsNewLine)
                {
                    throw Parser.CreateParseError(t, "Value is missing.");
                }
                else
                {
                    string msg = $"Expected a TOML value while parsing key value pair."
                        + $" Token of type '{t.type}' with value '{t.value}' is invalid.";
                    throw Parser.CreateParseError(t, msg);
                }
            }

            return value;
        }

        private static int DelimeterBackslashPos(string source, int startIndex)
        {
            int i1 = source.IndexOf("\\\r", startIndex);
            int i2 = source.IndexOf("\\\n", startIndex);

            var minIndex = MinGreaterThan(0, -1, i1, i2);
            return minIndex;
        }

        private static int MinGreaterThan(int absoluteMin, int defaultValue, params int[] values)
        {
            int currenMin = int.MaxValue;

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] >= absoluteMin && values[i] < currenMin)
                {
                    currenMin = values[i];
                }
            }

            return currenMin == int.MaxValue ? defaultValue : currenMin;
        }

        private static int NextNonWhitespaceCharacer(string source, int startIndex)
        {
            for (int i = startIndex; i < source.Length; i++)
            {
                if (!WhitspaceCharSet.Contains(source[i]))
                {
                    return i;
                }
            }

            return source.Length;
        }

        private static TomlString ParseLiteralString(ITomlRoot root, LookaheadBuffer<Token> tokens)
        {
            var t = tokens.Consume();

            Debug.Assert(t.type == TokenType.LiteralString);

            return new TomlString(root, t.value, TomlString.TypeOfString.Literal);
        }

        private static TomlString ParseMultilineLiteralString(ITomlRoot root, LookaheadBuffer<Token> tokens)
        {
            var t = tokens.Consume();
            Debug.Assert(t.type == TokenType.MultilineLiteralString);

            string s = t.value;

            // Trim newline following the """ tag immediately
            if (s.Length > 0 && s[0] == '\r') { s = s.Substring(1); }
            if (s.Length > 0 && s[0] == '\n') { s = s.Substring(1); }

            return new TomlString(root, t.value, TomlString.TypeOfString.MultilineLiteral);
        }

        private static TomlString ParseMultilineString(ITomlRoot root, LookaheadBuffer<Token> tokens)
        {
            var t = tokens.Consume();
            Debug.Assert(t.type == TokenType.MultilineString);

            var s = t.value;

            // Trim newline following the """ tag immediate
            if (s.Length > 0 && s[0] == '\r') { s = s.Substring(1); }
            if (s.Length > 0 && s[0] == '\n') { s = s.Substring(1); }

            s = ReplaceDelimeterBackslash(s);

            return new TomlString(root, s, TomlString.TypeOfString.Multiline);
        }

        private static TomlString ParseStringValue(ITomlRoot root, LookaheadBuffer<Token> tokens)
        {
            var t = tokens.Consume();

            Debug.Assert(t.type == TokenType.String);

            if (t.value.Contains("\n"))
            {
                throw Parser.CreateParseError(t, $"String '{t.value}' is invalid because it contains newlines.");
            }

            var s = t.value.Unescape(t);

            return new TomlString(root, s, TomlString.TypeOfString.Normal);
        }

        private static TomlArray ParseTomlArray(ITomlRoot root, TokenBuffer tokens)
        {
            TomlArray a;

            tokens.ExpectAndConsume(TokenType.LBrac);
            tokens.ConsumeAllNewlines();

            if (tokens.TryExpect(TokenType.RBrac))
            {
                // Empty array handled inside this if, else part can assume the array has values
                tokens.Consume();
                return new TomlArray(root);
            }
            else
            {
                List<TomlValue> values = new List<TomlValue>();
                var errPos = tokens.Peek();
                var v = ParseTomlValue(root, tokens);

                if (v == null)
                {
                    throw Parser.CreateParseError(errPos, $"Array value is missing.");
                }

                values.Add(v);

                while (!tokens.TryExpect(TokenType.RBrac))
                {
                    if (!tokens.TryExpectAndConsume(TokenType.Comma))
                    {
                        throw Parser.CreateParseError(tokens.Peek(), "Array not closed.");
                    }

                    tokens.ConsumeAllNewlines();
                    values.Last().Comments.AddRange(CommentProduction.TryParseComments(tokens, CommentLocation.Append));

                    if (!tokens.TryExpect(TokenType.RBrac))
                    {
                        var et = tokens.Peek();
                        v = ParseTomlValue(root, tokens);

                        if (v == null)
                        {
                            throw Parser.CreateParseError(et, $"Array value is missing.");
                        }

                        if (v.GetType() != values[0].GetType())
                        {
                            throw Parser.CreateParseError(et, $"Expected value of type '{values[0].ReadableTypeName}' but value of type '{v.ReadableTypeName}' was found.");
                        }

                        values.Add(v);
                        tokens.ConsumeAllNewlines();
                    }
                }

                a = new TomlArray(root, values.ToArray());
            }

            a.Last().Comments.AddRange(CommentProduction.TryParseComments(tokens, CommentLocation.Append));
            tokens.ExpectAndConsume(TokenType.RBrac);

            return a;
        }

        private static TomlFloat ParseTomlFloat(ITomlRoot root, LookaheadBuffer<Token> tokens)
        {
            var floatToken = tokens.Consume();

            var check = floatToken.value;
            int startToCheckForZeros = check[0] == '+' || check[0] == '-' ? 1 : 0;

            if (check[startToCheckForZeros] == '0' && check[startToCheckForZeros + 1] != '.')
            {
                throw new Exception($"Failed to parse TOML float with '{floatToken.value}' because it has  a leading '0' which is not allowed by the TOML specification.");
            }

            return new TomlFloat(root, double.Parse(floatToken.value.Replace("_", string.Empty), CultureInfo.InvariantCulture));
        }

        private static TomlInt ParseTomlInt(ITomlRoot root, LookaheadBuffer<Token> tokens)
        {
            var token = tokens.Consume();

            if (token.value.Length > 1 && token.value[0] == '0')
            {
                throw new Exception($"Failed to parse TOML int with '{token.value}' because it has  a leading '0' which is not allowed by the TOML specification.");
            }

            return new TomlInt(root, long.Parse(token.value.Replace("_", string.Empty)));
        }

        private static TomlValue ParseTomlValue(ITomlRoot root, TokenBuffer tokens)
        {
            if (tokens.TryExpect(TokenType.Integer)) { return ParseTomlInt(root, tokens); }
            else if (tokens.TryExpect(TokenType.Float)) { return ParseTomlFloat(root, tokens); }
            else if (tokens.TryExpect(TokenType.DateTime)) { return TomlDateTime.Parse(root, tokens.Consume().value); }
            else if (tokens.TryExpect(TokenType.Timespan)) { return new TomlTimeSpan(root, TimeSpan.Parse(tokens.Consume().value, CultureInfo.InvariantCulture)); }
            else if (tokens.TryExpect(TokenType.String)) { return ParseStringValue(root, tokens); }
            else if (tokens.TryExpect(TokenType.LiteralString)) { return ParseLiteralString(root, tokens); }
            else if (tokens.TryExpect(TokenType.MultilineString)) { return ParseMultilineString(root, tokens); }
            else if (tokens.TryExpect(TokenType.MultilineLiteralString)) { return ParseMultilineLiteralString(root, tokens); }
            else if (tokens.TryExpect(TokenType.Bool)) { return new TomlBool(bool.Parse(tokens.Consume().value)); }
            else if (tokens.TryExpect(TokenType.LBrac)) { return ParseTomlArray(root, tokens); }

            return null;
        }

        private static string ReplaceDelimeterBackslash(string source)
        {
            for (int d = DelimeterBackslashPos(source, 0); d >= 0; d = DelimeterBackslashPos(source, d))
            {
                var nnw = NextNonWhitespaceCharacer(source, d + 1);
                source = source.Remove(d, nnw - d);
            }

            return source;
        }
    }
}
