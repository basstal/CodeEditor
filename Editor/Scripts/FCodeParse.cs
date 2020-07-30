using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace CodeEditor
{
    public static class FCodeParse
    {
        public static FCodeLuaFunction GenFunction(string word)
        {
            var func = new FCodeLuaFunction(0) {name = word, shortName = word};
            var functionStart = new List<Token>()
                {new TypedToken(TK.FUNCTION), new LiteralToken('('), new LiteralToken(')')};
            var functionEnd = new List<Token>() {new TypedToken(TK.END)};
            func.line2Tokens.Add(1, functionStart);
            func.line2Tokens.Add(2, functionEnd);
            func.tokens.AddRange(functionStart);
            func.tokens.AddRange(functionEnd);
            return func;
        }
        
        static void Error(string msg)
        {
            Debug.LogWarning($"FCodeParse error : {msg}");
        }
        public static bool ValidSignature(List<Token> lineContent, Dictionary<string, string> fcodeSignature, string signature)
        {
            if (lineContent.Count == 1)
            {
                var ori = lineContent[0].Original();
                ori = Regex.Replace(ori,@"\s+", "");
                return ori == $"--{fcodeSignature[signature]}";
            }

            return false;
        }

        static void ParseSingleLogic(Dictionary<int, List<Token>> line2Tokens, int startLine, int endLine, ref List<FCodeLuaFunction> functions, ref FCodeLogic logic)
        {
            FCodeWindow w = EditorWindow.GetWindow<FCodeWindow>();
            Dictionary<string, string> fcodeSignature = w.fcodeSignature;
            var isSingleLogicStarted = false;
            FCodeTiming timing = 0;
            FCodeLuaFunction command = null, condition = null;
            
            for (int currentLine = startLine + 1; currentLine < endLine; currentLine ++)
            {
                if (line2Tokens.TryGetValue(currentLine, out List<Token> lineContent))
                {
                    // ** 从签名开始处 
                    if (!isSingleLogicStarted)
                    {
                        isSingleLogicStarted = ValidSignature(lineContent, fcodeSignature, "SingleLogicStart");
                        continue;
                    }
                    else if(ValidSignature(lineContent, fcodeSignature, "SingleLogicStart"))
                    {
                        Error("重复的FCodeSignature:SingleLogic:Start签名？？");
                    }
                    
                    // ** 从签名开始的下一行 直到签名结束的行内容都会在此处解析
                    var s = lineContent[0].Original();
                    switch (s)
                    {
                        case "timing":
                            var end = lineContent.Find(item => item.Original() == "FCodeTiming");
                            if(end != null)
                            {
                                var index = lineContent.IndexOf(end);
                                if (!Enum.TryParse(lineContent[index + 2].Original(), out timing))
                                {
                                    Error($"解析FCodeTiming字符串出错 未定义的 {lineContent[index + 2].Original()}？？");
                                }
                            }
                            else
                            {
                                Error("timing 字段中 未找到FCodeTiming关键字？？");
                            }
                            break;
                        case "command":
                            command = functions?.Find(luaFunc => luaFunc.fileStartLine == currentLine);
                            break;
                        case "condition":
                            condition = functions?.Find(luaFunc => luaFunc.fileStartLine == currentLine);
                            break;
                    }

                    // ** 到签名结束
                    if (ValidSignature(lineContent, fcodeSignature, "SingleLogicEnd"))
                    {
                        isSingleLogicStarted = false;
                        var singleLogic = new FCodeLogic.SingleLogic(timing, command ?? GenFunction("command"), condition ?? GenFunction("condition"));
                        command = null;
                        condition = null;
                        logic.logicList.Add(singleLogic);
                    }
                }
            }
        }

        static void ParseVars(Dictionary<int, List<Token>> line2Tokens, int startLine, int endLine,
            ref List<FCodeLuaFunction> functions, ref FCodeLogic logic)
        {
            FCodeWindow w = EditorWindow.GetWindow<FCodeWindow>();
            Dictionary<string, string> fcodeSignature = w.fcodeSignature;
            var isVarsStarted = false;
            for (int currentLine = startLine + 1; currentLine < endLine; currentLine ++)
            {
                if (line2Tokens.TryGetValue(currentLine, out List<Token> lineContent))
                {
                    // ** 从签名开始处 
                    if (!isVarsStarted)
                    {
                        isVarsStarted = ValidSignature(lineContent, fcodeSignature, "VarsStart");
                        continue;
                    }
                    else if(ValidSignature(lineContent, fcodeSignature, "VarsStart"))
                    {
                        Error("重复的FCodeSignature:Vars:Start签名？？");
                    }
                    // ** 把签名中的所有函数都加到vars中
                    var f = functions?.Find(luaFunc => luaFunc.fileStartLine == currentLine);
                    if (f != null)
                    {
                        logic.vars.Add(f);
                    }
                    
                    // ** 到签名结束
                    if (ValidSignature(lineContent, fcodeSignature, "VarsEnd"))
                    {
                        isVarsStarted = false;
                    }
                }
            }
            
        }
        public static FCodeLib TokenizeFCLogic(string content, string fcodePrefix)
        {
            FCodeLLex llex = new FCodeLLex(new StringLoadInfo(content));
            Dictionary<string, object> t = Tokenizing(llex);
            if (t.TryGetValue("functions", out var ot))
            {
                var functions = (List<FCodeLuaFunction>) ot;
                Dictionary<string, string> parametersTypeRef = new Dictionary<string, string>();
                Dictionary<string, FCodeLuaFunction> fcodeFunctions = new Dictionary<string, FCodeLuaFunction>();
                foreach (var fcodeFunction in functions)
                {
                    // ** 如果字段有加xxxxCheck的包装形式，则CodeEditor生成方法调用时会限制参数的填写形式；
                    foreach (var entry in fcodeFunction.line2Tokens)
                    {
                        var tokens = entry.Value;
                        if (tokens.Count > 2)
                        {
                            var first = tokens[0].Original();
                            var varName = tokens[2].Original();
                            if (first.EndsWith("Check") && !parametersTypeRef.ContainsKey(varName))
                            {
                                parametersTypeRef.Add(varName, first.Replace("Check", ""));
                            }
                        }
                    }
                    if (fcodeFunction.name != fcodeFunction.shortName)
                    {
                        if (string.IsNullOrEmpty(fcodeFunction.shortName))
                        {
                            Debug.LogWarning($"fcodeFunction : {fcodeFunction} have no name!");
                        }
                        else
                        {
                            fcodeFunctions.Add(fcodeFunction.shortName, fcodeFunction);
                        }
                    }
                }

                return new FCodeLib(){fcodeFunctions = fcodeFunctions, parametersTypeRef = parametersTypeRef};
            }
            return new FCodeLib();
        }
        public static FCodeLogic Tokenize(string fileName, string content)
        {
            FCodeLLex llex = new FCodeLLex(new StringLoadInfo(content));
            Dictionary<string, object> result = Tokenizing(llex);
            FCodeLogic logic = new FCodeLogic(){name = fileName};
            List<FCodeLuaFunction> functions = null;
            if (result.TryGetValue("functions", out var ot))
            {
                functions = (List<FCodeLuaFunction>) ot;
            }
            
            if (result.TryGetValue("line2Tokens", out var ol))
            {
                var line2Tokens = (Dictionary<int, List<Token>>) ol;
                var entry = line2Tokens.FirstOrDefault(item => item.Value.Select(v => v.Original() == "logicList").Any());
                int startLine = entry.Key;
                if (!result.TryGetValue("maxLine", out var endLine))
                {
                    endLine = startLine;
                    Error("Tokenizing中找不到返回值endLine 使用startLine作为endLine？");
                }
                ParseSingleLogic(line2Tokens, startLine, (int)endLine, ref functions, ref logic);

                // ** 找到有 vars 关键字 且 token 数量为2的行
                var enties = line2Tokens.Where(item => item.Value.Select(v => v.Original() == "vars").Any()).ToArray();
                entry = enties.FirstOrDefault(item => item.Value.Count == 2);
                if (entry.Value != null)
                {
                    startLine = entry.Key;
                    ParseVars(line2Tokens, startLine, (int)endLine, ref functions, ref logic);
                }
            }
            
            return logic;
        }
        
        static Token FindPreviousToken(ref int index, ref List<Token> tokenHistory)
        {
            Token previousToken;
            do
            {
                previousToken = tokenHistory[index--];
            } while (previousToken is JumpToken && index >= 0);

            return previousToken;
        }

        static void HandlerRecord(ref Token token, ref List<Token> history, ref Dictionary<int, List<Token>> lineDict, int lineNumber)
        {
            if (!(token is JumpToken))
            {
                history.Add(token);
                if (lineDict.TryGetValue(lineNumber, out var list))
                {
                    list.Add(token);
                }
                else
                {
                    lineDict.Add(lineNumber, new List<Token>(){token});
                }
            }
        }

        static void HandleRecord(ref Token token, FCodeLuaFunction f, int lineNumber)
        {
            if (f != null && !(token is JumpToken))
            {
                var t = f.tokens;
                var l2t = f.line2Tokens;
                lineNumber = lineNumber - f.fileStartLine + 1;
                f.maxLine = Math.Max(f.maxLine, lineNumber);
                HandlerRecord(ref token, ref t, ref l2t, lineNumber);
            }
        }
        static Dictionary<string, object> Tokenizing(FCodeLLex l)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            Stack<TK> tokenStack = new Stack<TK>();
            List<Token> tokenHistory = new List<Token>();
            Dictionary<int, List<Token>> line2Tokens = new Dictionary<int, List<Token>>();
            List<FCodeLuaFunction> functions = new List<FCodeLuaFunction>();
            result.Add("tokenStack", tokenStack);
            result.Add("functions", functions);
            result.Add("line2Tokens", line2Tokens);
            result.Add("tokenHistory", tokenHistory);
            FCodeLuaFunction func = null;
            TK tokenType;
            do
            {
                var token = l.NextToken();
                HandlerRecord(ref token, ref tokenHistory, ref line2Tokens, l.lineNumber);
                HandleRecord(ref token, func, l.lineNumber);
                tokenType = (TK)token.tokenType;
                
                switch (tokenType)
                {
                    case TK.FUNCTION:
                        tokenStack.Push(tokenType);
                        // ** 函数名 or 函数存放位置
                        StringBuilder functionNameOrRef = new StringBuilder();
                        func = new FCodeLuaFunction(l.lineNumber);
                        HandleRecord(ref token, func, l.lineNumber);
                        var functionTokenType = tokenType;
                        // ** forward 形式 ：xxx = function(xxx)
                        // ** 非 forward 形式 : function xxx(xxx)
                        bool isForward = false;

                        // ** 尚未解析函数名的标记
                        bool isFunctionNameUnparsed = true;
                        var index = tokenHistory.Count - 2;
                        if (index >= 0)
                        {
                            Token previousToken = FindPreviousToken(ref index, ref tokenHistory);
                            isForward = previousToken?.tokenType == '=';
                            // ** 前面还有一个函数名 或函数的存放位置 (例如存放在数组中)
                            if (isForward && index >= 0)
                            {
                                previousToken = FindPreviousToken(ref index, ref tokenHistory);
                                while (previousToken != null && index >= 0)
                                {
                                    if (previousToken is NameToken || previousToken is StringToken ||
                                        previousToken.tokenType == ':' || previousToken.tokenType == '.' ||
                                        previousToken.tokenType == '[' || previousToken.tokenType == ']')
                                    {
                                        functionNameOrRef.Insert(0, previousToken.Original());
                                    }
                                    else
                                    {
                                        break;
                                    }

                                    previousToken = tokenHistory[index--];
                                }
                            }
                        }

                        // ** 解析函数声明部分 函数名以及参数等
                        while (functionTokenType != TK.EOS)
                        {
                            var functionBodyToken = l.NextToken();
                            HandlerRecord(ref functionBodyToken, ref tokenHistory, ref line2Tokens, l.lineNumber);
                            HandleRecord(ref functionBodyToken, func, l.lineNumber);
                            functionTokenType = (TK) functionBodyToken.tokenType;

                            isFunctionNameUnparsed = isFunctionNameUnparsed && functionBodyToken.tokenType != '(';
                            // ** 非 forward形式的函数名
                            if (isFunctionNameUnparsed && !isForward)
                            {
                                if (functionBodyToken is NameToken || functionBodyToken.tokenType == ':' ||
                                    functionBodyToken.tokenType == '.')
                                {
                                    var shortName = functionBodyToken.Original();
                                    functionNameOrRef.Append(shortName);
                                    func.shortName = shortName;
                                }
                            }
                            else if (functionBodyToken is NameToken)
                            {
                                func.functionParameters.Add(functionBodyToken);
                            }
                            else if (functionBodyToken.tokenType == ')')
                            {
                                func.name = functionNameOrRef.ToString();
                                functions.Add(func);
                                break;
                            }
                        }

                        break;
                    case TK.IF:
                    case TK.FOR:
                    case TK.WHILE:
                        tokenStack.Push(tokenType);
                        break;
                    case TK.RETURN:
                        var rTokenType = tokenType;
                        var rToken = token;
                        while (rTokenType != TK.EOS)
                        {
                            // ** 把return 以及其后的一些 token 加到函数的tokens中
                            rToken = l.NextToken();
                            HandlerRecord(ref rToken, ref tokenHistory, ref line2Tokens, l.lineNumber);
                            HandleRecord(ref rToken, func, l.lineNumber);
                            rTokenType = (TK) rToken.tokenType;
                             
                            if (rToken is NameToken)
                            {
                                func?.returnParameters.Add(rToken);
                            }
                            else if (rTokenType == TK.FUNCTION)
                            {
                                // ** todo 在返回值中返回function 这里还没处理
                                Tokenizing(l);
                                rTokenType = (TK) l.token.tokenType;
                            }
                            else if (rTokenType == TK.END)
                            {
                                if (tokenStack.Count > 0)
                                {
                                    var tk = tokenStack.Pop();
                                    func = tk != TK.FUNCTION ? func : null;
                                }

                                // ** todo 在返回值中返回function 这里还没处理
                                break;
                            }
                        }

                        break;
                    case TK.END:
                        if (tokenStack.Count > 0)
                        {
                            var tk = tokenStack.Pop();
                            func = tk != TK.FUNCTION ? func : null;
                        }

                        break;

                }
            } while (tokenType != TK.EOS);

            result.Add("maxLine", l.lineNumber);
            return result;
        }
    }
}