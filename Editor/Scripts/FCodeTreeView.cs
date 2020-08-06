using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using System.Linq;
using System.Text.RegularExpressions;
using static UnityEditor.GenericMenu;
using System.Reflection.Emit;

namespace CodeEditor
{
    public class FCodeTreeView : CodeEditorTreeView<FCodeLogic>
    {
        private FCodeLogic m_fcodeLogic;
        private FCodeTreeViewItem m_buildRoot;
        public override CodeEditorTreeViewItem buildRoot
        {
            get => m_buildRoot;
        }
        public FCodeTreeView(TreeViewState state, string name, FCodeLogic logic) : base(state, name)
        {
            m_fcodeLogic = logic;
            this.name = name;
            if (FCodeWindow.instance.savedCopyItems != null)
            {
                copyItems = FCodeWindow.instance.savedCopyItems;
            }
        }

        public FCodeTreeView(TreeViewState state, MultiColumnHeader multiColumnHeader) : base(state, multiColumnHeader)
        {
        }

        protected override TreeViewItem BuildRoot()
        {
            if (m_buildRoot == null)
            {
                var root = new FCodeTreeViewItem(-1);
                root.id = -1;
                m_buildRoot = (FCodeTreeViewItem)root.Build(m_fcodeLogic);
                root.AddChild(m_buildRoot);
            }
            ReorderMoveId();
            
            return m_buildRoot.parent;
        }

        public override object ToData()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("local LogicList = {");
            if (buildRoot != null && buildRoot.children != null)
            {
                foreach (FCodeTreeViewItem item in buildRoot.children)
                {
                    if (item.fcodeNodeType == FCodeNodeType.Timing)
                    {
                        var singleLogic = (FCodeLogic.SingleLogic)item.data;
                        sb.AppendLine("\t{");
                        sb.AppendLine($"\t\t-- {FCodeWindow.instance.fcodeSignature["SingleLogicStart"]}");
                        // ** todo 这里根据项目不同有不用的枚举获取方式 先暂时这么写
                        sb.AppendLine($"\t\ttiming = PBEnum.FCodeTiming.{singleLogic.timing},");
                        WriteFunction("condition", item, sb);
                        WriteFunction("command", item, sb);
                        sb.AppendLine($"\t\t-- {FCodeWindow.instance.fcodeSignature["SingleLogicEnd"]}");
                        sb.AppendLine("\t},");
                    }
                }

                sb.AppendLine("\tvars = ");
                sb.AppendLine($"\t-- {FCodeWindow.instance.fcodeSignature["VarsStart"]}");
                sb.AppendLine("\t{");

                List<string> writenVars = new List<string>();
                foreach (FCodeTreeViewItem item in buildRoot.children)
                {
                    if (item.fcodeNodeType == FCodeNodeType.Vars)
                    {
                        var luaFunction = (FCodeLuaFunction)item.data;
                        var isRepeatedName = writenVars.Contains(luaFunction.name);
                        var firstIsLetter = Char.IsLetter(luaFunction.name[0]);
                        if (!isRepeatedName && firstIsLetter)
                        {
                            WriteFunction(luaFunction.name, item, sb);
                            writenVars.Add(luaFunction.name);
                        }
                        else if (isRepeatedName)
                        {
                            Debug.LogWarning($"vars 输出错误：\n有重复的vars变量名 : {luaFunction.name}，仅输出第一个！");
                        }
                        else
                        {
                            Debug.LogWarning($"vars 输出错误：\n变量名不合法，第一个字符必须为字母 [a-z] or [A-Z] ");
                        }
                    }
                }
                sb.AppendLine("\t},");
                sb.AppendLine($"\t-- {FCodeWindow.instance.fcodeSignature["VarsEnd"]}");
            }
            sb.AppendLine("}");
            sb.AppendLine();
            sb.Append("return LogicList");


            return sb.ToString();
        }
        void WriteFunction(string word, FCodeTreeViewItem item, StringBuilder sb)
        {
            FCodeTreeViewItem logicItem = item.displayName == word ? item : null;
            var c1 = item.children?.Find(o =>
            {
                var c = (FCodeTreeViewItem) o;
                return (c.fcodeNodeType == FCodeNodeType.Command || c.fcodeNodeType == FCodeNodeType.Condition || c.fcodeNodeType == FCodeNodeType.Vars) && c.displayName == word;
            });
            logicItem = logicItem ?? (c1 != null ? (FCodeTreeViewItem)c1 : null);
            if (logicItem != null && logicItem.children?.Count > 0)
            {
                sb.AppendLine($"\t\t{word} = function()");
                if (logicItem.fcodeNodeType == FCodeNodeType.Vars)
                {
                    sb.AppendLine($"\t\t\tlocal {word} = nil");
                }
                Stack<int> s = new Stack<int>();
                logicItem.Traverse(child =>
                {
                    if (child != logicItem)
                    {
                        var c = (FCodeTreeViewItem) child;
                        var diff = c.depth - logicItem.depth;
                        var t = new string('\t', diff);
                        if (c.fcodeNodeType == FCodeNodeType.If || c.fcodeNodeType == FCodeNodeType.Foreach)
                        {
                            if (s.Count == 0 || s.Peek() < diff)
                            {
                                s.Push(diff);
                            }
                            else 
                            {
                                while (s.Count > 0 && s.Peek() >= diff)
                                {
                                    var nt = s.Pop();
                                    sb.AppendLine($"\t\t{new string('\t', nt)}end");
                                }
                                s.Push(diff);
                            }
                        }
                        else
                        {
                            while (s.Count > 0 && s.Peek() >= diff)
                            {
                                var nt = s.Pop();
                                sb.AppendLine($"\t\t{new string('\t', nt)}end");
                            }
                        }
                        sb.AppendLine($"\t\t{t}{child.displayName}");
                    }
                });
                while (s.Count > 0)
                {
                    var nt = s.Pop();
                    sb.AppendLine($"\t\t{new string('\t', nt)}end");
                }

                if (logicItem.fcodeNodeType == FCodeNodeType.Vars)
                {
                    sb.AppendLine($"\t\t\treturn {word}");
                }
                sb.AppendLine("\t\tend,");
            }
        }

        protected override bool ValidInsert(CodeEditorTreeViewItem t, CodeEditorTreeViewItem v)
        {
            var target = (FCodeTreeViewItem) t;
            var validate = (FCodeTreeViewItem) v;
            var result = true;
            // ** todo 这里也许可以继续简化
            switch (target.fcodeNodeType)
            {
                case FCodeNodeType.None:
                    result = validate.fcodeNodeType == FCodeNodeType.Timing ||
                             validate.fcodeNodeType == FCodeNodeType.Vars;
                    if (!result)
                    {
                        EditorUtility.DisplayDialog("非法的粘贴行为！", "根节点不允许粘贴除 timing or vars 以外的节点", "确认");
                    }
                    break;
                case FCodeNodeType.Command:
                case FCodeNodeType.Condition:
                case FCodeNodeType.Vars:
                case FCodeNodeType.If:
                case FCodeNodeType.Foreach:
                    result = validate.fcodeNodeType != FCodeNodeType.Timing &&
                             validate.fcodeNodeType != FCodeNodeType.Vars;
                    if (!result)
                    {
                        EditorUtility.DisplayDialog("非法的粘贴行为！", "timing or vars 只允许粘贴在根节点下", "确认");
                    }
                    break;
                default:
                    result = false;
                    break;
            }

            return result;
        }

        protected override bool ValidCopy(CodeEditorTreeViewItem item)
        {
            var nodeType = ((FCodeTreeViewItem) item).fcodeNodeType;
            return nodeType != FCodeNodeType.None && nodeType != FCodeNodeType.Command &&
                   nodeType != FCodeNodeType.Condition;
        }

        protected override void ContextClickedItem(int id)
        {
            var clicked = (FCodeTreeViewItem)FindItem(id, rootItem);
            itemClicked = clicked;
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Copy"), false, Copy);
            if (copyItems?.Count > 0)
            {
                menu.AddItem(new GUIContent("Paste"), false, () => Paste());
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Paste"));
            }
            menu.AddSeparator("");

            if(clicked.fcodeNodeType == FCodeNodeType.None)
            {
                var values = Enum.GetValues(typeof(FCodeTiming));
                List<FCodeTiming> timings = new List<FCodeTiming>();
                foreach(FCodeTiming value in values)
                {
                    if(value != 0)
                    {
                        timings.Add(value);
                    }
                }
                timings.Sort((a, b) => String.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal));
                foreach (var timing in timings)
                {
                    menu.AddItem(new GUIContent($"Insert/Timings/{timing}"), false, ()=>
                    {
                        var command = FCodeParse.GenFunction("command");
                        var condition = FCodeParse.GenFunction("condition");
                        var data = new FCodeLogic.SingleLogic(timing, command, condition);
                        Insert(data);
                    });
                }

                menu.AddItem(new GUIContent("Insert/Vars"), false, () =>
                {
                    // ** todo 先临时写到这里 可以挪到配置中去
                    string defaultName = "default";
                    // ** 找到重复的命名 则在命名后 + 1
                    while (m_buildRoot.children.FindIndex(item =>
                    {
                        var fItem = (FCodeTreeViewItem) item;
                        return fItem.fcodeNodeType == FCodeNodeType.Vars && ((FCodeLuaFunction)fItem.data).name == defaultName;
                    }) != -1)
                    {
                        defaultName += "1";
                    }
                    var data = new FCodeLuaFunction(0) { name = defaultName, shortName = defaultName };
                    Insert(data);
                });
            }
            else if (clicked.fcodeNodeType == FCodeNodeType.Vars || clicked.fcodeNodeType == FCodeNodeType.Condition || clicked.fcodeNodeType == FCodeNodeType.Command || clicked.fcodeNodeType == FCodeNodeType.If || clicked.fcodeNodeType == FCodeNodeType.Foreach)
            {
                var dict = FCodeWindow.instance.fcodeLib.fcodeFunctions.OrderBy(o => o.Key);
                Dictionary<GUIContent, string> others = new Dictionary<GUIContent, string>();

                foreach (var entry in dict)
                {
                    var name = entry.Value.shortName;
                    var prefix = (string)FCodeWindow.instance.fcodeProperties["FCodeFunctionPrefix"];
                    var m = Regex.Match(name, $"{prefix}(set|get|condition|change)");
                    if (m.Groups.Count > 1)
                    {
                        menu.AddItem(new GUIContent($"Insert/Function/{m.Groups[1]}/{name}"), false, () =>
                        {
                            var data = new List<Token>() { new NameToken(name) };
                            Insert(data);
                        });
                    }
                    else
                    {
                        var interval = name.StartsWith(prefix) ? "Function" : "Expression_Math";
                        others.Add(new GUIContent($"Insert/{interval}/{name}"), name);
                    }
                }
                foreach(var entry in others)
                {
                    menu.AddItem(entry.Key, false, () => {
                        var data = new List<Token>() { new NameToken(entry.Value) };
                        Insert(data);
                    });
                }
                menu.AddItem(new GUIContent($"Insert/Expression"), false, () => { Insert(new List<Token>() { new LiteralToken('?') }); });

                menu.AddItem(new GUIContent($"Insert/If"), false, () => {
                    Insert(new List<Token>() { new TypedToken(TK.IF), new LiteralToken('?'), new TypedToken(TK.THEN) });
                });

                menu.AddItem(new GUIContent($"Insert/Foreach"), false, () => { 
                    Insert(new List<Token>() { new TypedToken(TK.FOR), new LiteralToken('?'), new LiteralToken(','), new LiteralToken('?'), new TypedToken(TK.IN) , new NameToken("pairs") , new LiteralToken('('), new LiteralToken('?'),new LiteralToken(')'), new TypedToken(TK.DO) });
                });
            }
            
            menu.AddItem(new GUIContent("Delete"), false, Delete);

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Move ↑"), false, () =>
            {
                Move(MoveOption.Up);
            });
            menu.AddItem(new GUIContent("Move ↓"), false, () =>
            {
                Move(MoveOption.Down);
            });

            menu.AddSeparator("");

            menu.ShowAsContext();
        }

        public override void Copy()
        {
            base.Copy();
            FCodeWindow.instance.savedCopyItems = copyItems;
        }
    }
}