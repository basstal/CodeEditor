using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEditor;

namespace CodeEditor
{
    
    public class FCodeLuaFunction
    {
        public string name { get; set; }
        public string shortName { get; set; }
        public List<Token> functionParameters { get; }
        public List<Token> returnParameters { get; }
        public List<Token> tokens { get; }
        public Dictionary<int, List<Token>> line2Tokens { get; }
        public int fileStartLine { get; }
        public int maxLine { get; set; }
        public FCodeLuaFunction(int fileStartLine)
        {
            tokens = new List<Token>();
            functionParameters = new List<Token>();
            returnParameters = new List<Token>();
            line2Tokens = new Dictionary<int, List<Token>>();
            this.fileStartLine = fileStartLine;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"name : {name} , startLine : {fileStartLine} , parameters : ");
            int i = 1;
            foreach (var parameter in functionParameters)
            {
                sb.Append($" p{i++}({parameter.Original()}) ");
            }
            sb.AppendLine();
            i = 1;
            foreach (var entry in line2Tokens)
            {
                sb.Append($" {i++} : ");
                foreach (var token in entry.Value)
                {
                    sb.Append($" {token.Original()} ");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }

    public class FCodeLib
    {
        public Dictionary<string, string> parametersTypeRef { get; set; }
        public Dictionary<string, FCodeLuaFunction> fcodeFunctions { get; set; }


    }
    public class FCodeLogic
    {
        public class SingleLogic
        {
            private FCodeTiming m_timing;
            private FCodeLuaFunction m_command;
            private FCodeLuaFunction m_condition;
            public FCodeTiming timing
            {
                get => m_timing;
                set => m_timing = value;
            }

            public FCodeLuaFunction command => m_command;
            public FCodeLuaFunction condition => m_condition;
            public SingleLogic(FCodeTiming timing, FCodeLuaFunction command, FCodeLuaFunction condition)
            {
                m_timing = timing;
                m_command = command;
                m_condition = condition;
            }

        }
        private List<SingleLogic> m_logicList = new List<SingleLogic>();

        private List<FCodeLuaFunction> m_vars = new List<FCodeLuaFunction>();
        public List<SingleLogic> logicList { get => m_logicList; }
        public List<FCodeLuaFunction> vars
        {
            get => m_vars;
        }
        public string name { get; set; }

    }
}