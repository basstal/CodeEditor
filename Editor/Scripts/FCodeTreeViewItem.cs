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
    public class ParsedTokens : ICloneable
    {
        public FCodeNodeType pType { get; set; }
        public List<ParameterConfig> parameterConfigs { get; set; }
        public bool isLocalReturn { get; set; }
        public bool isReturnValue { get; set; }
        public List<Token> returnTokens { get; set; }
        public FCodeLuaFunction fcodeFunction { get; set; }
        public string fcodeFunctionName { get; set; }
        public string expression { get; set; }
        public string foreachPairsKey { get; set; } 
        public string foreachPairsVal { get; set; }
        public object Clone()
        {
            var c = new ParsedTokens()
            {
                pType = pType,
                isLocalReturn = isLocalReturn,
                isReturnValue = isReturnValue,
                fcodeFunction = fcodeFunction,
                fcodeFunctionName = fcodeFunctionName,
                expression = expression,
                foreachPairsKey = foreachPairsKey,
                foreachPairsVal = foreachPairsVal,
            };
            if (returnTokens != null)
            {
                c.returnTokens = new List<Token>();
                foreach(var token in returnTokens)
                {
                    c.returnTokens.Add((Token)token.Clone());
                }
            }
            if (parameterConfigs != null)
            {
                c.parameterConfigs = new List<ParameterConfig>();
                foreach(var parameterConfig in parameterConfigs)
                {
                    c.parameterConfigs.Add((ParameterConfig)parameterConfig.Clone());
                }
            }
            return c;
        }
        void AppendLocal(StringBuilder sb)
        {
            if (returnTokens != null && returnTokens.Count > 0 && !string.IsNullOrWhiteSpace(returnTokens[0].Original()))
            {
                if (isLocalReturn)
                {
                    sb.Append("local ");
                }
                // ** todo 目前仅能有一个返回值
                sb.Append(returnTokens[0].Original());
                sb.Append(" = ");
            }
        }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            switch (pType)
            {
                case FCodeNodeType.Function:
                    AppendLocal(sb);
                    sb.Append(fcodeFunctionName);
                    sb.Append('(');
                    if (parameterConfigs != null && parameterConfigs.Count > 0)
                    {
                        parameterConfigs.Sort((a, b) => a.index - b.index);
                        ParameterConfig prev = null;
                        foreach (var pc in parameterConfigs)
                        {
                            if (pc.fields == null || pc.fields.Count == 0 || pc.index - prev?.index > 1 || (prev == null && pc.index > 0))
                            {
                                continue;
                            }
                            var varSb = new StringBuilder();
                            foreach (var fieldToken in pc.fields)
                            {
                                varSb.Append(fieldToken.Original());
                            }
                            if (string.IsNullOrWhiteSpace(varSb.ToString()))
                            {
                                continue;
                            }
                            if (pc.index > 0)
                            {
                                sb.Append(", ");
                            }
                            var configPrefix = pc.isConfig? "config.": "";
                            if (pc.isString)
                            {
                                sb.Append('\'');
                            }
                            sb.Append(configPrefix);
                            sb.Append(varSb);
                            if (pc.isString)
                            {
                                sb.Append('\'');
                            }
                            prev = pc;
                        }
                    }
                    sb.Append(')');
                    break;
                case FCodeNodeType.If:
                    sb.Append($"if {expression} then");
                    break;
                case FCodeNodeType.Expression:
                    if (isReturnValue)
                    { 
                        AppendLocal(sb);
                    }
                    sb.Append(expression);
                    break;
                case FCodeNodeType.Foreach:
                    sb.Append($"for {foreachPairsKey}, {foreachPairsVal} in pairs({expression}) do");
                    break;
                case FCodeNodeType.End:
                    sb.Append("end");
                    break;
                default:
                    Debug.LogWarning($"DisplayName for FCodeNodeType : {pType.ToString()} not found !");
                    break;
            }

            return sb.ToString();
        }

        
    }
    public class ParameterConfig : ICloneable
    {
        public int index { get; set; }
        // ** 是否为配置表字段
        public bool isConfig { get; set; }
        // ** 是否为字符串
        public bool isString { get; set; }
        // ** 是否为定点数表示
        public bool isFixedNumber { get; set; }
        public bool isTable { get; set; }
        public List<Token> fields { get; set; }
        public ParameterConfig(int i)
        {
            index = i;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var field in fields)
            {
                sb.Append(field);
                sb.Append(' ');
            }
            return
                $"index : {index}, isConfig : {isConfig}, isString : {isString}, isFixedNumber : {isFixedNumber}, isTable : {isTable}, content : {sb}";
        }

        public object Clone()
        {
            var c = new ParameterConfig(index)
            {
                isConfig = isConfig,
                isString = isString,
                isFixedNumber = isFixedNumber,
                isTable = isTable,
            };
            if (fields != null)
            {
                c.fields = new List<Token>();
                foreach(var field in fields)
                {
                    c.fields.Add((Token)field.Clone());
                }
            }
            return c;
        }
    };
    public class FCodeTreeViewItem : CodeEditorTreeViewItem
    {
        static string[] Actors = {"empty", "self", "target", "creator"};
        private static int m_itemId = 1;
        private FCodeNodeType m_fcodeNodeType = FCodeNodeType.None;
        public object data { get; set; }
        public FCodeNodeType fcodeNodeType
        {
            get => m_fcodeNodeType;
            set => m_fcodeNodeType = value;
        }
        public Action DisplayNameRefresh { get; set; }

        public FCodeTreeViewItem(int depth = 0)
        {
            this.id = m_itemId ++;
            this.depth = depth;
        }
        public FCodeTreeViewItem(FCodeLogic data, int depth) : this(depth)
        {
            // ** 根节点
            // ** 不能被复制 构造时对源数据没有做拷贝
            this.data = data;
            DisplayNameRefresh = () => displayName = $"{data?.name}";
            DisplayNameRefresh();
        }

        public FCodeTreeViewItem(FCodeLogic.SingleLogic data, int depth) : this(depth)
        {
            // ** SingleLogic节点 顺便用于展示和修改timing
            // ** 不能被复制 构造时对源数据没有做拷贝
            this.data = data;
            m_fcodeNodeType = FCodeNodeType.Timing;
            DisplayNameRefresh = () => displayName = $"{data?.timing.ToString()}";
            DisplayNameRefresh();
        }

        public FCodeTreeViewItem(FCodeLuaFunction data, int depth) : this(depth)
        {
            // ** Vars or Command or Condition 根节点 function的根节点
            // ** 不能被复制 构造时对源数据没有做拷贝
            this.data = data;
            
            DisplayNameRefresh = () => displayName = $"{data?.name}";
            DisplayNameRefresh();
            switch (displayName)
            {
                case "command":
                    m_fcodeNodeType = FCodeNodeType.Command;
                    break;
                case "condition":
                    m_fcodeNodeType = FCodeNodeType.Condition;
                    break;
                default:
                    m_fcodeNodeType = FCodeNodeType.Vars;
                    break;
            }
            FCodeWindow.instance.icons.TryGetValue(m_fcodeNodeType.ToString(), out var itemIcon);
            icon = itemIcon;
        }

        public FCodeTreeViewItem(List<Token> data, int depth) : this(depth)
        {
            this.data = ParseTokens(data);
            ConstructByParsedTokens();
        }
        public FCodeTreeViewItem(ParsedTokens data, int depth) : this(depth)
        {
            this.data = data;
            ConstructByParsedTokens();
        }
        void ConstructByParsedTokens()
        {
            m_fcodeNodeType = ((ParsedTokens)this.data).pType;
            DisplayNameRefresh = () => displayName = $"{this.data}";
            DisplayNameRefresh();
            FCodeWindow.instance.icons.TryGetValue(m_fcodeNodeType.ToString(), out var itemIcon);
            icon = itemIcon;
        }

        int ParseReturn(List<Token> tokens, ref ParsedTokens result)
        {
            var localIndex = tokens.FindIndex(t => t.tokenType == (int)TK.LOCAL);
            if (localIndex == -1)
            {
                result.isLocalReturn = false;
            }
            else
            {
                localIndex ++;
                result.isLocalReturn = true;
            }
            var expressionWithReturn = tokens.FindIndex(t => t.tokenType == '=');
            result.isReturnValue = expressionWithReturn != -1;
            if (result.isReturnValue)
            {
                result.returnTokens = new List<Token>();
                localIndex = localIndex != -1 ? localIndex : 0;
                while(localIndex < expressionWithReturn)
                {
                    if (tokens[localIndex] is NameToken)
                    {
                        result.returnTokens.Add(tokens[localIndex]);
                    }
                    localIndex ++;
                }
            }
            return localIndex;
        }
        ParsedTokens ParseTokens(List<Token> tokens)
        {
            var result = new ParsedTokens();
            if (tokens == null || tokens.Count == 0)
            {
                return result;
            }
            var firstToken = tokens[0];
            var lastToken = tokens[tokens.Count - 1];
            var isFCodeFunction = false;
            foreach(var token in tokens)
            {
                var prefix = (string)FCodeWindow.instance.fcodeProperties["FCodeFunctionPrefix"];
                if (token.Original().StartsWith(prefix))
                {
                    isFCodeFunction = true;
                    firstToken = token;
                    break;
                }
            }
            var first = firstToken.Original();
            var last = lastToken.Original();
            if (isFCodeFunction)
            {
                // ** 解析 fcode function
                var startIndex = tokens.FindIndex(t => t.Original() == first);
                if (startIndex ++ >= 0 && FCodeWindow.instance.fcodeLib.fcodeFunctions.TryGetValue(first, out var function))
                {
                    result.pType = FCodeNodeType.Function;
                    result.fcodeFunctionName = first;
                    result.fcodeFunction = function;
                    // ** todo 这里加参数类型检查
                    if (function.functionParameters.Count > 0)
                    {
                        var parameterConfigs = new List<ParameterConfig>();
                        int varIndex = 0;
                        while(startIndex < tokens.Count)
                        {
                            var currentToken = tokens[startIndex];
                            Token nextToken = null;
                            if (startIndex + 1 < tokens.Count)
                            {
                                nextToken = tokens[startIndex + 1];
                            }
                            if (currentToken is NameToken nameToken )
                            {
                                // ** 当前是一个变量名
                                // ** 解析为 config.xxxx
                                if (nextToken is LiteralToken token && token.tokenType == '.' && nameToken.Original() == "config" )
                                {
                                    var pc = new ParameterConfig(varIndex ++){isConfig = true, fields = new List<Token>{tokens[startIndex + 2]}};
                                    parameterConfigs.Add(pc);
                                    startIndex += 2;
                                }
                                else
                                {
                                    // ** 解析为一般的变量
                                    // ** todo 这里可能导致了不能在参数中填函数的问题
                                    var pc = new ParameterConfig(varIndex ++){fields = new List<Token>{currentToken}};
                                    parameterConfigs.Add(pc);
                                }
                            }
                            else if(currentToken is TypedToken)
                            {
                                var pc = new ParameterConfig(varIndex ++){fields = new List<Token>{currentToken}};
                                if (currentToken is NumberToken number)
                                {
                                    pc.isFixedNumber = number.isFixed;
                                }
                                else if (currentToken is StringToken)
                                {
                                    pc.isString = true;
                                }
                                parameterConfigs.Add(pc);
                            }
                            else if(currentToken is LiteralToken)
                            {
                                // ** todo 这里导致参数中不能使用函数
                                if (currentToken.tokenType == ')')
                                {
                                    break;
                                }
                                else if (currentToken.tokenType == '{')
                                {
                                    // ** 参数为table
                                    var pc = new ParameterConfig(varIndex ++);
                                    var fields = new List<Token>();
                                    while (!(nextToken is LiteralToken l && l.tokenType == '}'))
                                    {
                                        // ** 目前仅支持单个nameToken的table
                                        if (nextToken is NameToken name)
                                        {
                                            fields.Add(name);
                                        }
                                        nextToken = tokens[++startIndex + 1];
                                    }
                                    if (fields.Count > 0)
                                    {
                                        pc.fields = fields;
                                        pc.isTable = true;
                                        parameterConfigs.Add(pc);
                                    }
                                }
                                else if (currentToken.tokenType == '-' && nextToken is NumberToken number)
                                {
                                    var fieldNumber = (NumberToken) number.Clone();
                                    fieldNumber.number = -fieldNumber.number;
                                    var pc = new ParameterConfig(varIndex ++){isFixedNumber = number.isFixed, fields = new List<Token>{fieldNumber}};
                                    parameterConfigs.Add(pc);
                                    ++startIndex;
                                }
                            }
                            startIndex ++;
                        }
                        result.parameterConfigs = parameterConfigs;
                    }
                    if (function.returnParameters.Count > 0)
                    {
                        ParseReturn(tokens, ref result);
                    }
                }
                else
                {
                    Debug.LogWarning($"未找到 FCodeFunction : {first}");
                }
            }
            else if (first.StartsWith("if") && last.EndsWith("then"))
            {
                // ** 解析if
                result.pType = FCodeNodeType.If;
                var startIndex = 1;
                StringBuilder sb = new StringBuilder();
                while(startIndex < tokens.Count && tokens[startIndex] != lastToken)
                {
                    var token = tokens[startIndex];
                    Token nextToken = tokens[startIndex + 1];
                    Token2Expression(sb, token, nextToken);
                    startIndex ++;
                }
                result.expression = sb.ToString();
            }
            else if (firstToken.tokenType == (int)TK.END)
            {
                Debug.LogWarning("逻辑不应该走到这里？？!");
            }
            else if (first.StartsWith("for") && last.EndsWith("do"))
            {
                // ** 解析foreach
                result.pType = FCodeNodeType.Foreach;
                var pairsBegin = tokens.FindIndex(t => t.Original() == "pairs");
                if (pairsBegin == -1)
                {
                    Debug.LogWarning("lua for 没找到 pairs关键字？？！ 无法正确解析");
                    return result;
                }
                StringBuilder sb = new StringBuilder();
                pairsBegin += 2;
                while(pairsBegin < tokens.Count && tokens[pairsBegin].tokenType != ')')
                {
                    var token = tokens[pairsBegin];
                    Token nextToken = tokens[pairsBegin + 1];
                    Token2Expression(sb, token, nextToken);
                    pairsBegin ++;
                }
                result.expression = sb.ToString();
                var valueIndex = tokens.FindIndex(t => t.tokenType == ',');
                result.foreachPairsKey = "_";
                result.foreachPairsVal = "_";
                if (valueIndex != -1)
                {
                    if (tokens[valueIndex - 1] is NameToken keyName)
                    {
                        result.foreachPairsKey = keyName.Original();
                    }

                    if (tokens[valueIndex + 1] is NameToken valueName)
                    {
                        result.foreachPairsVal = valueName.Original();
                    }
                }
            }
            else
            {
                // ** 解析表达式
                result.pType = FCodeNodeType.Expression;
                var startIndex = ParseReturn(tokens, ref result);
                ++startIndex;
                StringBuilder sb = new StringBuilder();
                while(startIndex < tokens.Count)
                {
                    var token = tokens[startIndex];
                    Token nextToken = null;
                    if (startIndex + 1 < tokens.Count)
                    {
                        nextToken = tokens[startIndex + 1];
                    }
                    Token2Expression(sb, token, nextToken);
                    ++startIndex;
                }
                result.expression = sb.ToString();
            }

            return result;
        }
        void Token2Expression(StringBuilder expression, Token token, Token nextToken)
        {
            if (token == null)
            {
                return;
            }
            var o = token.Original();
            if(token is NumberToken number)
            {
                o += number.isFixed ? "F" : "";
            }
            else if (token is StringToken)
            {
                o = $"\'{o}\'";
            }

            var nextTokenType = nextToken?.tokenType;
            var tokenType = token.tokenType;
            var noPostSpace = nextTokenType == '.' || nextTokenType == ':' || nextTokenType == '(' ||
                              tokenType == '@' || tokenType == '.' || tokenType == '(' || tokenType == ':';
            if (noPostSpace)
            {
                expression.Append(o);
            }
            else
            {
                // ** 规范空格
                expression.Append(o);
                expression.Append(' ');
            }
        }

        private FCodeTreeViewItem BuildRecursive(FCodeTreeViewItem root)
        {
            FCodeTreeViewItem copyRoot = (FCodeTreeViewItem)Build(root.data);
            if (root.children != null)
            {
                foreach (FCodeTreeViewItem child in root.children)
                {
                    var copyChild = BuildRecursive(child);
                    copyRoot.AddChild(copyChild);
                }
            }
            return copyRoot;
        }
        
        public override CodeEditorTreeViewItem Clone()
        {
            if (fcodeNodeType == FCodeNodeType.Timing || fcodeNodeType == FCodeNodeType.Vars)
            {
                return Build(data);
            }
            else if (fcodeNodeType == FCodeNodeType.If || fcodeNodeType == FCodeNodeType.Foreach)
            {
                return BuildRecursive(this);
            }
            else
            {
                return Build(data);
            }
        }

        public override void Draw()
        {
            GUI.changed = false;
            GUILayout.BeginVertical("box");
            switch (m_fcodeNodeType)
            {
                case FCodeNodeType.Timing:
                    GUILayout.Label("时间点");
                    var values = Enum.GetNames(typeof(FCodeTiming));
                    var singleLogic = (FCodeLogic.SingleLogic)data;
                    var selectedIndex = Array.FindIndex(values, v => v == singleLogic.timing.ToString());
                    selectedIndex = EditorGUILayout.Popup(selectedIndex, values);
                    if (FCodeTiming.TryParse(values[selectedIndex], out FCodeTiming timing))
                    {
                        singleLogic.timing = timing;
                    }
                    break;
                case FCodeNodeType.Vars:
                    GUILayout.Label("变量命名");
                    FCodeLuaFunction var = (FCodeLuaFunction) data;
                    var newName = EditorGUILayout.TextField(var.name);
                    var index = FCodeWindow.instance.fcodeTreeView.buildRoot.children.FindIndex(item =>
                    {
                        var fItem = (FCodeTreeViewItem) item;
                        return fItem.fcodeNodeType == FCodeNodeType.Vars &&
                               ((FCodeLuaFunction) fItem.data).name == newName;
                    });
                    if (newName != var.name && index == -1)
                    {
                        var.name = newName;
                    }
                    break;
                case FCodeNodeType.Function:
                    GUILayout.Label("调用参数");
                    ParsedTokens parsedTokens = (ParsedTokens) data;
                    var fcodeFunction = parsedTokens.fcodeFunction;
                    for(int parameterIndex = 0; parameterIndex < fcodeFunction.functionParameters.Count; ++parameterIndex)
                    {
                        var paramName = fcodeFunction.functionParameters[parameterIndex].Original();
                        var parameterConfig = parsedTokens.parameterConfigs.Find(item => item.index == parameterIndex);
                        if (parameterConfig == null)
                        {
                            parameterConfig = parameterConfig ?? new ParameterConfig(parameterIndex);
                            parsedTokens.parameterConfigs.Add(parameterConfig);
                        }


                        FCodeWindow.instance.fcodeLib.parametersTypeRef.TryGetValue(paramName, out string parameterTypeRef);
                        // ** 找不到 or 没有就都认为是String
                        parameterTypeRef = parameterTypeRef ?? "String";
                        GUILayout.Space(7f);
                        GUILayout.Label($"参数{parameterConfig.index + 1} : {paramName}");

                        // ** 通用的参数处理方式，先处理配置表字段
                        var isConfig = parameterConfig.isConfig;
                        if (parameterTypeRef != "Actor")
                        {
                            isConfig = GUILayout.Toggle(isConfig, "是否为配置表字段");
                        }
                        if (parameterConfig.isConfig != isConfig)
                        {
                            parameterConfig.isConfig = isConfig;
                            if (!isConfig)
                            {
                                parameterConfig.fields = null;
                            }
                        }
                        if(isConfig)
                        {
                            parameterConfig.isString = false;
                            int i = -1;
                            if (parameterConfig.fields != null)
                            {
                                i = FCodeWindow.instance.fcodeConfig.FindIndex(p => p == parameterConfig.fields[0].Original());
                            }
                            var ni = EditorGUILayout.Popup(i, FCodeWindow.instance.fcodeConfig.ToArray());
                            if (ni > -1)
                            {
                                if (ni != i)
                                {
                                    parameterConfig.fields = parameterConfig.fields ?? new List<Token>() { new NameToken("?") };
                                    parameterConfig.fields[0].SetOriginal(FCodeWindow.instance.fcodeConfig[ni]);
                                }
                            }
                            else
                            {
                                parameterConfig.fields = null;
                            }
                        }
                        // ** 再处理其他填值类型的字段
                        else
                        {
                            switch (parameterTypeRef)
                            {
                                case "Boolean":
                                    TypedToken boolToken = null;
                                    if (parameterConfig.fields != null)
                                    {
                                        boolToken = (TypedToken)parameterConfig.fields[0];
                                    }
                                    else
                                    {
                                        boolToken = new TypedToken(TK.FALSE); 
                                    }

                                    var flag = boolToken.tokenType == (int) TK.TRUE;
                                    var nFlag = GUILayout.Toggle(flag, paramName);
                                    if (nFlag != flag)
                                    {
                                        parameterConfig.fields = parameterConfig.fields??new List<Token>() { boolToken };
                                        parameterConfig.fields[0].SetOriginal(nFlag?TK.TRUE :TK.FALSE);
                                    }
                                    break;
                                case "Literal":
                                case "Nil":
                                case "String":
                                    parameterConfig.isString = GUILayout.Toggle(parameterConfig.isString, "是否为字符串");
                                    string str = "";
                                    if (parameterConfig.fields != null)
                                    {
                                        str = parameterConfig.fields[0].Original();
                                    }
                                    var nv = GUILayout.TextField(str);
                                    if (nv != str)
                                    {
                                        nv = Regex.Replace(nv, "['\"]", "");
                                        parameterConfig.fields = parameterConfig.fields??new List<Token>() { new StringToken() };
                                        parameterConfig.fields[0].SetOriginal(nv);
                                    }
                                    break;
                                case "Number":
                                    Token number = new NameToken("");
                                    if (parameterConfig.fields != null)
                                    {
                                        number = parameterConfig.fields[0];
                                    }

                                    var n = number.Original();
                                    var nn = EditorGUILayout.TextField(n);
                                    if (nn != n)
                                    {
                                        parameterConfig.fields = parameterConfig.fields ??new List<Token>() { number };
                                        number.SetOriginal(nn);
                                    }
                                    break;
                                case "Actor":
                                    int i = 0;
                                    if (parameterConfig.fields != null)
                                    {
                                        i = Array.FindIndex(Actors, p => p == parameterConfig.fields[0].Original());
                                    }
                                    
                                    var ni = EditorGUILayout.Popup(i, Actors);
                                    if (ni > 0)
                                    {
                                        if (ni != i)
                                        {
                                            parameterConfig.fields = parameterConfig.fields??new List<Token>() { new NameToken(Actors[ni]) };
                                            parameterConfig.fields[0].SetOriginal(Actors[ni]);
                                        }
                                    }
                                    else
                                    {
                                        parameterConfig.fields = null;
                                    }
                                    break;
                            }
                        }
                    }
                    if (fcodeFunction.returnParameters.Count > 0)
                    {
                        GUILayout.Space(15f);
                        GUILayout.Label("返回值");
                        parsedTokens.isLocalReturn = GUILayout.Toggle(parsedTokens.isLocalReturn, "是否为新定义变量");
                        for(int rCount = 0; rCount < fcodeFunction.returnParameters.Count; ++rCount)
                        {
                            var token = fcodeFunction.returnParameters[rCount];
                            GUILayout.Space(7f);
                            GUILayout.Label($"值{rCount + 1} : {token.Original()}");
                            parsedTokens.returnTokens = parsedTokens.returnTokens ?? new List<Token>();
                            Token rToken = rCount < parsedTokens.returnTokens.Count
                                ? parsedTokens.returnTokens[rCount]
                                : new NameToken(""); 
                            

                            var v = rToken.Original();
                            var nv = GUILayout.TextField(v);
                            if (nv != v)
                            {
                                while (rCount >= parsedTokens.returnTokens.Count)
                                {
                                    parsedTokens.returnTokens.Add(new NameToken(""));
                                }
                                parsedTokens.returnTokens[rCount] = rToken;
                                rToken.SetOriginal(nv);
                            }
                        }
                    }
                    break;
                case FCodeNodeType.Expression:
                    ParsedTokens parsedTokens1 = (ParsedTokens) data;
                    GUILayout.Label("表达式");
                    parsedTokens1.expression = GUILayout.TextArea(parsedTokens1.expression);
                    GUILayout.Space(15f);
                    
                    parsedTokens1.isReturnValue = GUILayout.Toggle(parsedTokens1.isReturnValue, "是否有返回变量");
                    if (parsedTokens1.isReturnValue)
                    {
                        GUILayout.Space(15f);
                        GUILayout.Label("变量名");
                        parsedTokens1.isLocalReturn = GUILayout.Toggle(parsedTokens1.isLocalReturn, "是否为新定义变量");
                        parsedTokens1.returnTokens = parsedTokens1.returnTokens ?? new List<Token>();
                        Token rToken = parsedTokens1.returnTokens.Count > 0 ? parsedTokens1.returnTokens[0] : new NameToken("");
                        if (parsedTokens1.returnTokens.Count == 0)
                        {
                            parsedTokens1.returnTokens.Add(rToken);
                        }

                        var v = rToken.Original();
                        var nv = GUILayout.TextField(v);
                        if (nv != v)
                        {
                            rToken.SetOriginal(nv);
                        }
                    }
                    
                    break;
                case FCodeNodeType.If:
                    GUILayout.Label("条件表达式");
                    ParsedTokens parsedTokens2 = (ParsedTokens) data;
                    parsedTokens2.expression = GUILayout.TextField(parsedTokens2.expression);
                    break;
                case FCodeNodeType.Foreach:
                    GUILayout.Label("键");
                    ParsedTokens parsedTokens3 = (ParsedTokens) data;

                    var nk = GUILayout.TextField(parsedTokens3.foreachPairsKey);
                    if (nk != parsedTokens3.foreachPairsKey)
                    {
                        parsedTokens3.foreachPairsKey = string.IsNullOrWhiteSpace(nk) ? "_" : nk;
                    }
                    GUILayout.Label("值");
                    var npv = GUILayout.TextField(parsedTokens3.foreachPairsVal);
                    if (npv != parsedTokens3.foreachPairsVal)
                    {
                        parsedTokens3.foreachPairsVal = string.IsNullOrWhiteSpace(npv) ? "_" : npv;
                    }
                    GUILayout.Label("表");
                    parsedTokens3.expression = GUILayout.TextField(parsedTokens3.expression);
                    break;
            }
            GUILayout.EndVertical();
            if (GUI.changed)
            {
                FCodeWindow.instance.fcodeTreeView.changed = true;
                DisplayNameRefresh();
            }
        }

        public int ParseLuaFunction2Tree(FCodeLuaFunction func, int startLine, int endLine, FCodeTreeViewItem parent, int depth)
        {
            for (int i = startLine; i <= endLine; ++i)
            {
                if (func.line2Tokens.TryGetValue(i, out var tokens))
                {
                    if (tokens.Count == 1 && tokens[0].tokenType == (int) TK.END)
                    {
                        // ** 遇到end 则返回end 的行数，同时略过end的treeViewItem构造
                        return i;
                    }
                    var item = new FCodeTreeViewItem(tokens, depth);
                    parent.AddChild(item);
                    if (item.fcodeNodeType == FCodeNodeType.If || item.fcodeNodeType == FCodeNodeType.Foreach)
                    {
                        // ** 从返回值end的行数开始继续构造当前的tree
                        var ri = ParseLuaFunction2Tree(func, i + 1, endLine, item, depth + 1);
                        if (ri < i + 1)
                        {
                            throw new InvalidOperationException("待解析的lua内容存在语法错误，请检查后重试");
                        }
                        i = ri;
                    }
                }
            }
            // ** 全部构造完不需要返回任何值
            return 0;
        }
        public override CodeEditorTreeViewItem Build<T>(T data, int depth = 0)
        {
            if (data == null || depth > 10)
            {
                Debug.LogError("Build data is null?? or Depth is too deep");
                return null;
            }

            if (data is FCodeLogic fcodeLogic)
            {
                var item = new FCodeTreeViewItem(fcodeLogic, depth);
                foreach (var singleLogic in fcodeLogic.logicList)
                {
                    var singleLogicItem = Build(singleLogic, depth + 1);
                    item.AddChild(singleLogicItem);
                }
                foreach (var luaFunction in fcodeLogic.vars)
                {
                    var singleLogicItem = Build(luaFunction, depth + 1);
                    item.AddChild(singleLogicItem);
                }
                return item;
            }
            else if (data is FCodeLogic.SingleLogic singleLogic)
            {
                var item = new FCodeTreeViewItem(singleLogic, depth);

                if (singleLogic.condition != null)
                {
                    var conditionItem = (FCodeTreeViewItem)Build(singleLogic.condition, depth + 1);
                    conditionItem.fcodeNodeType = FCodeNodeType.Condition;
                    item.AddChild(conditionItem);
                }
                if (singleLogic.command != null)
                {
                    var commandItem = (FCodeTreeViewItem)Build(singleLogic.command, depth + 1);
                    commandItem.fcodeNodeType = FCodeNodeType.Command;
                    item.AddChild(commandItem);
                }
                return item;
            }
            else if (data is FCodeLuaFunction fcodeLuaFunction)
            {
                var item = new FCodeTreeViewItem(fcodeLuaFunction, depth);
                // ** 这里第一行默认为function() 所以直接跳过
                if (item.fcodeNodeType == FCodeNodeType.Vars)
                {
                    ParseLuaFunction2Tree(fcodeLuaFunction, 3, fcodeLuaFunction.maxLine - 2, item, depth + 1);
                }
                else
                {
                    ParseLuaFunction2Tree(fcodeLuaFunction, 2, fcodeLuaFunction.maxLine, item, depth + 1);
                }
                return item;
            }
            else if (data is List<Token> tokens)
            {
                var item = new FCodeTreeViewItem(tokens, depth);
                return item;
            }
            else if (data is ParsedTokens parsedTokens)
            {
                var item = new FCodeTreeViewItem((ParsedTokens)parsedTokens.Clone(), depth);
                return item;
            }
            throw new InvalidCastException($"BevTreeViewItem cannot build data type {typeof(T).Name}");
        }
    }
}