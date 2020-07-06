using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CodeEditor;
using Google.Protobuf;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CodeEditor
{
    public class BevTreeView : CodeEditorTreeView<CodeNode>
    {
        private BevTreeViewItem m_buildRoot;
        private CodeNode m_rootData;

        public override CodeEditorTreeViewItem buildRoot
        {
            get => m_buildRoot;
        }
        public BevTreeView(TreeViewState state, string name, CodeNode rootData) : base(state, name)
        {
            m_rootData = rootData;
            this.name = name;
            this.changed = false;
            if (BevTreeWindow.instance.savedCopyItems != null)
            {
                copyItems = BevTreeWindow.instance.savedCopyItems;
            }
        }

        public BevTreeView(TreeViewState state, MultiColumnHeader multiColumnHeader) : base(state, multiColumnHeader)
        {
        }

        protected override TreeViewItem BuildRoot()
        {
            if (m_buildRoot == null)
            {
                var root = new BevTreeViewItem(null, -1);
                root.id = -1;
                m_buildRoot = (BevTreeViewItem)root.Build(m_rootData);
                root.AddChild(m_buildRoot);
            }
            ReorderMoveId();

            return m_buildRoot.parent;
        }

        private void ToDataTraverse(CodeNode parent, CodeEditorTreeViewItem child)
        {
            if (child?.children?.Count > 0)
            {
                foreach (BevTreeViewItem item in child.children)
                {
                    var cData = new CodeNode(item.data);
                    cData.Children.Clear();
                    parent.Children.Add(cData);
                    ToDataTraverse(cData, item);
                }
            }
        }
        public override object ToData()
        {
            var output = new CodeNode(m_buildRoot.data);
            output.Children.Clear();
            ToDataTraverse(output, m_buildRoot);
            return output;
        }

        protected override bool ValidCopy(CodeEditorTreeViewItem item)
        {
            // ** 根节点不允许复制
            return ((BevTreeViewItem) item).data.NodeType != BevNodeType.None;
        }

        protected override bool ValidInsert(CodeEditorTreeViewItem t, CodeEditorTreeViewItem v)
        {
            var target = (BevTreeViewItem) t;
            var validate = (BevTreeViewItem) v;
            var result = true;
            string[] value = null;
            // ** todo 这里也许可以继续简化
            switch (target.data.NodeType)
            {
                case BevNodeType.None:
                    BevTreeWindow.instance.bevTreeInsertion.TryGetValue(target.data.Type, out value);
                    result = value?.Contains(validate.data.Type) ?? false;
                    if (!result)
                    {
                        EditorUtility.DisplayDialog("非法的粘贴行为！", "请不要将Action或Condition节点粘贴到BevTree的根节点下!", "确认");
                    }
                    break;
                case BevNodeType.RandomSelector:
                    BevTreeWindow.instance.bevTreeInsertion.TryGetValue(target.data.Type, out value);
                    result = value?.Contains(validate.data.Type) ?? false;
                    if (!result)
                    {
                        EditorUtility.DisplayDialog("非法的粘贴行为！", "请不要将Action或Condition节点粘贴到RandomSelector下!", "确认");
                    }

                    break;
                case BevNodeType.Action:
                case BevNodeType.Condition:
                    result = false;
                    break;
            }

            return result;
        }

        public override void Copy()
        {
            base.Copy();
            BevTreeWindow.instance.savedCopyItems = copyItems;
        }

        protected override void ContextClickedItem(int id)
        {
            itemClicked = (BevTreeViewItem)FindItem(id, rootItem);
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

            var insertion = BevTreeWindow.instance.bevTreeInsertion;
            // ** 这里偷懒只把出现在RandomSelector可插入节点中的节点显示出来
            List<string> all = new List<string>(insertion["RandomSelector"]);
            all.AddRange(insertion["Action"]);
            all.AddRange(insertion["Condition"]);
            foreach (var type in all)
            {
                var content = new GUIContent($"Insert/{type}");
                if (type.StartsWith("Condition"))
                {
                    content = new GUIContent($"Insert/Condition/{type}");
                }
                else if (type.StartsWith("Action"))
                {
                    content = new GUIContent($"Insert/Action/{type}");
                }
                menu.AddItem(content, false, InsertDelegate, type);
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

        void InsertDelegate(object userData)
        {
            var type = (string) userData;
            var newCodeNode = new CodeNode() {Type = type};
            switch (type)
            {
                // ** 如果有新增的待插入的节点 需要在这里新增switch case
                case "Selector":
                case "Sequence":
                    newCodeNode.NodeType = BevNodeType.Composite;
                    Utility.SetIMessageField(newCodeNode, type, new Composite());
                    break;
                case "RandomSelector":
                    newCodeNode.NodeType = BevNodeType.RandomSelector;
                    newCodeNode.RandomSelector = new RandomSelector();
                    break;
                default:
                    if (type.StartsWith("Condition"))
                    {
                        newCodeNode.NodeType = BevNodeType.Condition;
                    }
                    else if(type.StartsWith("Action"))
                    {
                        newCodeNode.NodeType = BevNodeType.Action;
                    }
                    // ** todo 这里可能存在问题？？
                    var message = Utility.CreateIMessage(type);
                    Utility.SetIMessageField(newCodeNode, type, message);
                    break;
            }
            Insert(newCodeNode);
        }
    }
}
