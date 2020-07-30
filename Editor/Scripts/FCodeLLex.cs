using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CodeEditor
{
    public enum TK
    {
        // reserved words
        AND = 257,
        BREAK,
        CONTINUE,
        DO,
        ELSE,
        ELSEIF,
        END,
        FALSE,
        FOR,
        FUNCTION,
        GOTO,
        IF,
        IN,
        LOCAL,
        NIL,
        NOT,
        OR,
        REPEAT,
        RETURN,
        THEN,
        TRUE,
        UNTIL,
        WHILE,
        // other terminal symbols
        CONCAT,
        DOTS,
        EQ,
        GE,
        LE,
        NE,
        DBCOLON,
        NUMBER,
        STRING,
        NAME,
        EOS,
        TABLE,
        COMMENT,
    }
    public class StringLoadInfo
    {
        private StringBuilder m_str;
        public int pos { get; set; }
        public string code => m_str.ToString();
        public StringLoadInfo(string s)
        {
            m_str = new StringBuilder(s);
            pos = 0;
        }

        public int ReadByte()
        {
            if (pos >= m_str.Length)
                return -1;
            else
                return m_str[pos++];
        }
        
    }
    [Serializable]
    public abstract class Token : ICloneable
    {
        public int tokenType;

        public bool EqualsToToken(Token other)
        {
            return tokenType == other.tokenType;
        }

        public bool EqualsToToken(int other)
        {
            return tokenType == other;
        }

        public bool EqualsToToken(TK other)
        {
            return tokenType == (int)other;
        }
        public abstract string Original();
        public abstract void SetOriginal(object value);
        public abstract object Clone();
    }
    [Serializable]
    public class JumpToken : Token
    {
        public int jumpPos;

        public JumpToken(int jumpPos)
        {
            this.jumpPos = jumpPos;
        }

        public override string ToString()
        {
            return $"JumpToken to : {jumpPos}";
        }

        public override string Original()
        {
            throw new NotImplementedException();
        }

        public override void SetOriginal(object value)
        {
            throw new NotImplementedException();
        }

        public override object Clone()
        {
            return new JumpToken(jumpPos);
        }
    }

    [Serializable]
    public class LiteralToken : Token
    {
        public LiteralToken(char literal)
        {
            tokenType = literal;
        }
        
        public override string ToString()
        {
            return $"LiteralToken : {(char) tokenType}";
        }

        public override string Original()
        {
            return $"{(char)tokenType}";
        }

        public override void SetOriginal(object value)
        {
            if (value is char c)
            {
                tokenType = c;
            }
            else
            {
                throw new InvalidCastException($"Try to set value : {value} to LiteralToken ??");
            }
        }

        public override object Clone()
        {
            return new LiteralToken((char)tokenType);
        }
    }

    [Serializable]
    public class TypedToken : Token
    {
        public TypedToken(int tk)
        {
            if (!Enum.IsDefined(typeof(TK), tk))
            {
                throw new InvalidCastException($"Try to cast {tk} to TypedToken, but is not defined in enum TK.");
            }
            tokenType = tk;
        }

        public TypedToken(TK tk)
        {
            tokenType = (int)tk;
        }
        public override string ToString()
        {
            return $"TypedToken : {(TK) tokenType}";
        }

        public override string Original()
        {
            FCodeLLex.typedToken2String.TryGetValue((TK) tokenType, out string str);
            return string.IsNullOrEmpty(str) ? "??" : str;
        }

        public override void SetOriginal(object value)
        {
            if (value is string vStr && Enum.TryParse<TK>(vStr, out var tk))
            {
                tokenType = (int)tk;
            }
            else if (value is int vInt && Enum.IsDefined(typeof(TK), vInt))
            {
                tokenType = vInt;
            }
            else if (value is TK)
            {
                tokenType = (int)value;
            }
            else
            {
                throw new InvalidCastException($"Can not cast {value} to TypedToken!");
            }
        }

        public override object Clone()
        {
            return new TypedToken(tokenType);
        }
    }

    [Serializable]
    public class Comment : TypedToken
    {
        public string info;
        public bool isLongComment;

        public Comment(string info, bool isLongComment = false) : base((int)TK.COMMENT)
        {
            this.info = info;
            this.isLongComment = isLongComment;
        }
        public override string Original()
        {
            if (isLongComment && info.Contains("]=]"))
            {
                Debug.LogWarning("Comment 内容中包含的字符串 -> \"]=]\" <- 将被删除！");
            }
            return isLongComment ? $"--[=[{info}]=]" : $"--{info}";
        }

        public override void SetOriginal(object value)
        {
            info = value.ToString();
        }

        public override object Clone()
        {
            return new Comment(info);
        }

        public override string ToString()
        {
            return $"Comment : {info}";
        }
    }
    [Serializable]
    public class StringToken : TypedToken
    {
        public string info;
        public StringToken(string info = null) : base((int)TK.STRING)
        {
            // ** FCode的字符串首尾都无法带空字符
            this.info = string.IsNullOrEmpty(info) ? "" : info.Trim();
        }

        public override string ToString()
        {
            return $"StringToken : {info}";
        }

        public override string Original()
        {
            return info;
        }

        public override void SetOriginal(object value)
        {
            info = value.ToString();
        }

        public override object Clone()
        {
            return new StringToken(info);
        }
    }

    [Serializable]
    public class NameToken : TypedToken
    {
        public string name;
        public NameToken(string name) : base((int)TK.NAME)
        {
            this.name = name;
        }

        public override string ToString()
        {
            return $"NameToken : {name}";
        }

        public override string Original()
        {
            return name;
        }

        public override void SetOriginal(object value)
        {
            name = value.ToString();
        }

        public override object Clone()
        {
            return new NameToken(name);
        }
    }

    [Serializable]
    public class NumberToken : TypedToken
    {
        public bool isFixed = false;
        public double number;
        public NumberToken(double number, bool isFixed = false) : base((int)TK.NUMBER)
        {
            this.isFixed = isFixed;
            this.number = number;
        }

        public override string ToString()
        {
            return $"NumberToken : {number}, Is fixed : {isFixed}";
        }

        public override object Clone()
        {
            return new NumberToken(number, isFixed);
        }

        public override string Original()
        {
            return number.ToString();
        }

        public override void SetOriginal(object value)
        {
            if (value is string vStr)
            {
                if (vStr.EndsWith("F") && double.TryParse(vStr.Substring(0, vStr.Length - 1), out double d))
                {
                    number = d;
                    isFixed = true;
                }
                else if(double.TryParse(vStr, out double d1))
                {
                    number = d1;
                    isFixed = false;
                }
            }
            else
            {
                number = (double) value;
                isFixed = false;
            }
        }
    }

    public class FCodeLLex
    {
        public static Dictionary<TK, string> typedToken2String = new Dictionary<TK, string>()
        {
            {TK.EQ, "=="},
            {TK.GE, ">="},
            {TK.LE, "<="},
            {TK.NE, "~="},
            {TK.TABLE, "{ }"},
            {TK.CONCAT, ".."},
            {TK.DOTS, "..."},
            {TK.AND, "and"},
            {TK.BREAK, "break"},
            {TK.DO, "do"},
            {TK.ELSE, "else"},
            {TK.ELSEIF, "elseif"},
            {TK.END, "end"},
            {TK.FALSE, "false"},
            {TK.FOR, "for"},
            {TK.FUNCTION, "function"},
            {TK.GOTO, "goto"},
            {TK.IF, "if"},
            {TK.IN, "in"},
            {TK.LOCAL, "local"},
            {TK.NIL, "nil"},
            {TK.NOT, "not"},
            {TK.OR, "or"},
            {TK.REPEAT, "repeat"},
            {TK.RETURN, "return"},
            {TK.THEN, "then"},
            {TK.TRUE, "true"},
            {TK.UNTIL, "until"},
            {TK.WHILE, "while"},
            
        };
        const char EOZ = Char.MaxValue;

        public int pos
        {
            get
            {
                return m_loadInfo.pos;
            }
        }
        public int lineNumber { get; set; }
        public int lastLine { get; set; }
        public Token token { get; set; }
        
        private int m_current;
        private bool m_isHexDigit;
        private StringBuilder m_saved = new StringBuilder();
        private readonly StringLoadInfo m_loadInfo;
        private Token m_lookAhead;

        public FCodeLLex(StringLoadInfo loadInfo)
        {
            this.m_loadInfo = loadInfo;
            this.lineNumber = 1;
            this.lastLine = 1;
            this.token = null;
            this.m_lookAhead = null;
            Next();
        }
        public Token NextToken()
        {
            lastLine = lineNumber;
            if (m_lookAhead != null)
            {
                token = m_lookAhead;
                m_lookAhead = null;
            }
            else
            {
                token = Lex();
            }
            return token;
        }

        public Token LookAhead()
        {
            if (m_lookAhead != null)
            {
                throw new InvalidOperationException("LookAhead should not be called continually");
            }

            m_lookAhead = Lex();
            return m_lookAhead;

        }
        private void Next()
        {
            var c = m_loadInfo.ReadByte();
            m_current = (c == -1) ? EOZ : c;
        }

        private void SaveAndNext()
        {
            m_saved.Append((char)m_current);
            Next();
        }

        private void Save(char c)
        {
            m_saved.Append(c);
        }
        private void Error(string error)
        {
            throw new Exception($"{lineNumber}: {error} \n Code : {m_loadInfo.code}");
        }

        void Warning(string war)
        {
            Debug.LogWarning($"FCodeLLex : {war}");
        }
        private void IncLineNumber()
        {
            var prev = m_current;
            Next();
            // ** 处理\r\n形式的换行
            if (CurrentIsNewLine() && m_current != prev)
            {
                Next();
            }

            if (++lineNumber >= Int32.MaxValue)
            {
                Error("chunk行数超过了Int32.MaxValue!!");
            }
        }
        private bool CurrentIsNewLine()
        {
            return m_current == '\n' || m_current == '\r';
        }
        void ClearSaved()
        {
            m_saved.Clear();
        }
        // ** 跳过 并 统计 [ 与 ] 中的 = 符号 直到下一个 [ or ]
        int SkipSeparator()
        {
            int count = 0;
            var boundary = m_current;
            SaveAndNext();
            while (m_current == '=')
            {
                SaveAndNext();
                count++;
            }
            return m_current == boundary ? count : (- count - 1);
        }
        string GetSaved()
        {
            return m_saved.ToString();
        }

        bool CurrentIsXDigit()
        {
            // ** 大写的F不能用来作为16进制数字的解析 因为其被'误用'为定点数的转型表示
            if (m_current == 'F')
            {
                Warning("大写的F不能用来作为16进制数字的解析，因为其被'误用'为定点数的转型表示");
            }
            return Char.IsDigit((char) m_current) || ('A' <= m_current && m_current <= 'E') || ('a' <= m_current && m_current <= 'f');
        }

        byte ReadHexEscape()
        {
            byte result = 0;
            char[] c = new []{'\\', 'x', '0', '0'};
            for (int i = 2; i < 4; ++i)
            {
                Next();
                c[i] = (char) m_current;
                if (!CurrentIsXDigit())
                {
                    Error($"{new string(c, 0, i + 1)} : hexadecimal digit expected");
                }
                result <<= 4;
                result += byte.Parse(m_current.ToString(), NumberStyles.HexNumber);
            }
            return result;
        }

        byte ReadDecEscape()
        {
            int result = 0;
            var c = new char[3];

            int i;
            for (i = 0; i < 3 && Char.IsDigit((char) m_current); ++i)
            {
                c[i] = (char) m_current;
                result *= 10;
                result += m_current - '0';
                Next();
            }

            if (result > byte.MaxValue)
            {
                Error($"{new string(c, 0, i)} : decimal escape too large");
            }

            return (byte) result;
        }

        bool Str2Decimal(string str, out double d)
        {
            d = 0;
            // ** 过滤 inf or nan
            if (str.Contains("n") || str.Contains("N"))
            {
                return false;
            }

            if (str.Contains("x") || str.Contains("X"))
            {
                // ** 这里不自己做解析 参考代码中使用自己做的解析 from string X 2 number
                return double.TryParse(str, NumberStyles.HexNumber, null, out d);
            }
            // ** 非16进制形式并且以大写F结尾解析为定点数
            return str.EndsWith("F") ? double.TryParse(str.Substring(0, str.Length - 1), out d) : double.TryParse(str, out d);
        }

        NumberToken ReadNumber()
        {
            var exponentSign = new[] {'E', 'e'};
            var firstNumber = m_current;
            var numberToken = new NumberToken(0);
            SaveAndNext();
            if (firstNumber == '0' && (m_current == 'X' || m_current == 'x'))
            {
                exponentSign = new[] {'P', 'p'};
                SaveAndNext();
            }

            // ** 读取number内容
            while (true)
            {
                if (m_current == exponentSign[0] || m_current == exponentSign[1])
                {
                    SaveAndNext();
                    if (m_current == '+' || m_current == '-')
                    {
                        SaveAndNext();
                    }
                }
                // ** 先处理可能的定点数表示
                if (m_current == 'F')
                {
                    Next();
                    numberToken.isFixed = true;
                }
                else if (CurrentIsXDigit() || m_current == '.')
                {
                    SaveAndNext();
                }
                else
                {
                    // ** 其他情况都视为number结束了！
                    break;
                }
            }
            
            // ** 解析number内容
            var numberContent = GetSaved();
            if (Str2Decimal(numberContent, out var d))
            {
                numberToken.number = d;
                return numberToken;
            }
            else
            {
                Error($"malformed number : {numberContent}");
                return numberToken;
            }
        }
        string ReadString()
        {
            var pair = m_current;
            Next();
            while (m_current != pair)
            {
                switch (m_current)
                {
                    case EOZ:
                        Error("unfinished string");
                        continue;
                    case '\n':
                    case '\r':
                        Error("unfinished string");
                        continue;
                    case '\\':
                        byte c;
                        Next();
                        switch (m_current)
                        {
                            case 'a': 
                                c = (byte) '\a';
                                break;
                            case 'b': c = (byte)'\b'; break;
                            case 'f': c = (byte)'\f'; break;
                            case 'n': c = (byte)'\n'; break;
                            case 'r': c = (byte)'\r'; break;
                            case 't': c = (byte)'\t'; break;
                            case 'v': c = (byte)'\v'; break;
                            case 'x': 
                                c = ReadHexEscape();
                                break;
                            case '\n':
                            case '\r':
                                Save('\n');
                                IncLineNumber();
                                continue;
                            case 'u':
                                Error("unsupported unicode string format");
                                continue;
                            case '\\':
                            case '\"':
                            case '\'':
                                c = (byte) m_current;
                                break;
                            case EOZ:
                                continue;
                            case 'z':
                                Next();
                                while (Char.IsWhiteSpace((char)m_current))
                                {
                                    if (CurrentIsNewLine())
                                    {
                                        IncLineNumber();
                                    }
                                    else
                                    {
                                        Next();
                                    }
                                }
                                continue;
                            default:
                                if (!Char.IsDigit((char) m_current))
                                {
                                    Error($"{m_current} : invalid escape sequence");
                                }

                                c = ReadDecEscape();
                                break;
                        }
                        Save((char)c);
                        Next();
                        continue;
                    default:
                        SaveAndNext();
                        continue;
                }
            }
            Next();
            return GetSaved();
        }
        string ReadLongString(int sepCount)
        {
            // ** 略过开始的 [
            Next();

            var isLongStringEnd = false;
            while (!isLongStringEnd)
            {
                switch (m_current)
                {
                    case EOZ:
                        Error($"{GetSaved()} : unfinished long string/comment");
                        break;
                    case ']':
                        var saveCache = GetSaved();
                        ClearSaved();
                        if (SkipSeparator() == sepCount)
                        {
                            // ** 是结束
                            ClearSaved();
                            Next();
                            isLongStringEnd = true;
                        }
                        else
                        {
                            // ** 依然是内容
                            m_saved.Insert(0, GetSaved());
                            m_saved.Insert(0, saveCache);
                        }
                        break;
                    case '\n':
                    case '\r':
                        Save('\n');
                        IncLineNumber();
                        break;
                    default:
                        SaveAndNext();
                        break;
                }
            }

            var content = GetSaved();
            return content;
        }
        private Token Lex()
        {
            ClearSaved();
            int sepCount = 0;
            while (true)
            {
                switch (m_current)
                {
                    case '\n':
                    case '\r':
                        var jTk = new JumpToken(pos);
                        IncLineNumber();
                        return jTk;
                    case '-':
                        Next();
                        if (m_current != '-')
                        {
                            return new LiteralToken('-');
                        }
                        
                        Next();
                        // ** 是一个 长 注释
                        if (m_current == '[')
                        {
                            sepCount = SkipSeparator();
                            ClearSaved();
                            if (sepCount >= 0)
                            {
                                var comment = ReadLongString(sepCount);
                                ClearSaved();
                                return new Comment(comment, true);
                            }
                        }
                        // 短注释
                        while (!CurrentIsNewLine() && m_current != EOZ)
                        {
                            SaveAndNext();
                        }

                        var saved = GetSaved();
                        ClearSaved();
                        return new Comment(saved);
                    case '[':
                        sepCount = SkipSeparator();
                        if (sepCount >= 0)
                        {
                            string longString = ReadLongString(sepCount);
                            return new StringToken(longString);
                        }
                        else if (sepCount == -1)
                        {
                            return new LiteralToken('[');
                        }
                        else
                        {
                            Error("invalid long string delimiter");
                            continue;
                        }
                    case '=':
                        Next();
                        if (m_current != '=')
                        {
                            return new LiteralToken('=');
                        }
                        Next();
                        return new TypedToken(TK.EQ);
                    case '<':
                        Next();
                        if (m_current != '=')
                        {
                            return new LiteralToken('<');
                        }
                        Next();
                        return new TypedToken(TK.LE);
                    case '>':
                        Next();
                        if (m_current != '=')
                        {
                            return new LiteralToken('>');
                        }
                        Next();
                        return new TypedToken(TK.GE);
                    case '~':
                        Next();
                        if (m_current != '=')
                        {
                            return new LiteralToken('~');
                        }
                        Next();
                        return new TypedToken(TK.NE);
                    case ':':
                        Next();
                        if (m_current != ':')
                        {
                            return new LiteralToken(':');
                        }
                        Next();
                        Error("goto is not allowed!");
                        continue;
                    case '"':
                    case '\'':
                        // ** 字符串token
                        return new StringToken(ReadString());
                    case '.':
                        SaveAndNext();
                        if (m_current == '.')
                        {
                            SaveAndNext();
                            if (m_current == '.')
                            {
                                SaveAndNext();
                                return new TypedToken(TK.DOTS);
                            }
                            else
                            {
                                return new TypedToken(TK.CONCAT);
                            }
                        }
                        else if (!Char.IsDigit((char) m_current))
                        {
                            return new LiteralToken('.');
                        }
                        else
                        {
                            return ReadNumber();
                        }
                    case EOZ:
                        return new TypedToken(TK.EOS);
                    default:
                        if (Char.IsWhiteSpace((char) m_current) || m_current == ';')
                        {
                            var jTK = new JumpToken(pos);
                            Next();
                            return jTK;
                        }
                        else if (Char.IsDigit((char) m_current))
                        {
                            return ReadNumber();
                        }
                        else if (Char.IsLetter((char) m_current) || m_current == '_')
                        {
                            do
                            {
                                SaveAndNext();
                            } while (Char.IsLetterOrDigit((char)m_current) || m_current == '_');

                            string identifier = GetSaved();

                            var tk = typedToken2String.FirstOrDefault(x => x.Value == identifier).Key;
                            return tk != 0 ? new TypedToken(tk) : new NameToken(identifier);
                        }
                        else
                        {
                            var c = (char)m_current;
                            Next();
                            return new LiteralToken(c);
                        }
                        
                }
            }
        }
    }
}