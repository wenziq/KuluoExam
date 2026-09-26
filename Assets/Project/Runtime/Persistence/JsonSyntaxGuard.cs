using System;
using System.Collections.Generic;
using System.Text;
using Sokoban.Core.Data;

namespace Sokoban.Runtime.Persistence
{
    /// <summary>Rejects Json.NET extensions before it creates a token tree.</summary>
    internal sealed class JsonSyntaxGuard
    {
        internal const int MaxDepth = 32;
        private readonly string text;
        private int position;

        private JsonSyntaxGuard(string text)
        {
            this.text = text;
        }

        internal static void Validate(string text)
        {
            var guard = new JsonSyntaxGuard(text);
            guard.Value(0);
            guard.WhiteSpace();
            if (guard.position != text.Length) guard.Fail("根值后有多余内容。");
        }

        private void Value(int depth)
        {
            WhiteSpace();
            if (position >= text.Length) Fail("缺少 JSON 值。");
            char next = text[position];
            if (next == '{' || next == '[')
            {
                if (depth >= MaxDepth) Fail("JSON 嵌套超过 32 层。");
                if (next == '{') Object(depth + 1);
                else Array(depth + 1);
            }
            else if (next == '"') String(false);
            else if (next == 't') Literal("true");
            else if (next == 'f') Literal("false");
            else if (next == 'n') Literal("null");
            else if (next == '-' || Digit(next)) Number();
            else Fail("无效 JSON 值；不允许注释、单引号或非标准常量。");
        }

        private void Object(int depth)
        {
            position++;
            WhiteSpace();
            if (Take('}')) return;
            var keys = new HashSet<string>(StringComparer.Ordinal);
            while (true)
            {
                WhiteSpace();
                if (position >= text.Length || text[position] != '"') Fail("对象字段名必须使用双引号。");
                string key = String(true);
                if (!keys.Add(key)) Fail("对象包含重复字段。");
                if (keys.Count > 32) Fail("对象字段数量超过上限。");
                WhiteSpace();
                Require(':');
                Value(depth);
                WhiteSpace();
                if (Take('}')) return;
                Require(',');
            }
        }

        private void Array(int depth)
        {
            position++;
            WhiteSpace();
            if (Take(']')) return;
            int count = 0;
            while (true)
            {
                if (++count > ContentLimits.MaxWidth * ContentLimits.MaxHeight) Fail("数组元素超过 400 个。");
                Value(depth);
                WhiteSpace();
                if (Take(']')) return;
                Require(',');
            }
        }

        private string String(bool capture)
        {
            Require('"');
            var decoded = capture ? new StringBuilder() : null;
            bool highSurrogate = false;
            int length = 0;
            while (position < text.Length)
            {
                char value = text[position++];
                if (value == '"')
                {
                    if (highSurrogate) Fail("字符串包含未配对的 Unicode 代理项。");
                    return decoded?.ToString();
                }
                if (value < 0x20) Fail("字符串包含未转义控制字符。");
                if (value == '\\')
                {
                    if (position >= text.Length) Fail("字符串转义未完成。");
                    char escape = text[position++];
                    switch (escape)
                    {
                        case '"': value = '"'; break;
                        case '\\': value = '\\'; break;
                        case '/': value = '/'; break;
                        case 'b': value = '\b'; break;
                        case 'f': value = '\f'; break;
                        case 'n': value = '\n'; break;
                        case 'r': value = '\r'; break;
                        case 't': value = '\t'; break;
                        case 'u': value = UnicodeEscape(); break;
                        default: Fail("无效字符串转义。"); break;
                    }
                }
                if (highSurrogate)
                {
                    if (!char.IsLowSurrogate(value)) Fail("字符串包含未配对的 Unicode 高代理项。");
                    highSurrogate = false;
                }
                else if (char.IsLowSurrogate(value)) Fail("字符串包含未配对的 Unicode 低代理项。");
                else highSurrogate = char.IsHighSurrogate(value);
                if (++length > ContentLimits.MaxWitnessMoves) Fail("字符串超过最大长度 100000。");
                decoded?.Append(value);
            }
            Fail("字符串缺少结束双引号。");
            return null;
        }

        private char UnicodeEscape()
        {
            int value = 0;
            for (int i = 0; i < 4; i++)
            {
                if (position >= text.Length) Fail("Unicode 转义必须有四位十六进制数字。");
                char c = text[position++];
                int digit = c >= '0' && c <= '9' ? c - '0' :
                    c >= 'a' && c <= 'f' ? c - 'a' + 10 :
                    c >= 'A' && c <= 'F' ? c - 'A' + 10 : -1;
                if (digit < 0) Fail("Unicode 转义包含非法数字。");
                value = value * 16 + digit;
            }
            return (char)value;
        }

        private void Number()
        {
            int start = position;
            Take('-');
            if (!Take('0'))
            {
                if (position >= text.Length || text[position] < '1' || text[position] > '9') Fail("无效 JSON 数字。");
                Digits(start);
            }
            if (Take('.'))
            {
                int fraction = position;
                Digits(start);
                if (fraction == position) Fail("小数点后缺少数字。");
            }
            if (Take('e') || Take('E'))
            {
                if (!Take('+')) Take('-');
                int exponent = position;
                Digits(start);
                if (exponent == position) Fail("指数缺少数字。");
            }
            if (position - start > 64) Fail("数字文本超过上限。");
        }

        private void Digits(int start)
        {
            while (position < text.Length && Digit(text[position]))
            {
                position++;
                if (position - start > 64) Fail("数字文本超过上限。");
            }
        }

        private void Literal(string value)
        {
            foreach (char c in value) Require(c);
        }

        private void WhiteSpace()
        {
            while (position < text.Length && (text[position] == ' ' || text[position] == '\t' || text[position] == '\r' || text[position] == '\n')) position++;
        }

        private bool Take(char value)
        {
            if (position >= text.Length || text[position] != value) return false;
            position++;
            return true;
        }

        private void Require(char value)
        {
            if (!Take(value)) Fail("应为 '" + value + "'。");
        }

        private static bool Digit(char value) => value >= '0' && value <= '9';

        private void Fail(string message)
        {
            throw new FormatException("JSON 字符位置 " + position + "：" + message);
        }
    }
}
