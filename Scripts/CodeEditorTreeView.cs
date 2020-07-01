using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.Tilemaps;
namespace CodeEditor
{

    public abstract class CodeEditorTreeView<T> : TreeView
    {
        public virtual CodeEditorTreeViewItem buildRoot { get; }
        public enum InsertOption{ Up, Down };
        public enum MoveOption { Up, Down};

        // 当前已选中的Items
        private List<CodeEditorTreeViewItem> m_selectedItems = new List<CodeEditorTreeViewItem>();
        // 当前正在处理的Item
        private CodeEditorTreeViewItem m_drawItem;
        private List<CodeEditorTreeViewItem> m_copyItems = new List<CodeEditorTreeViewItem>();
    
        public List<CodeEditorTreeViewItem> selectedItems
        {
            get => m_selectedItems;
        }
        public CodeEditorTreeViewItem itemClicked { get; set; }
        public List<CodeEditorTreeViewItem> copyItems
        {
            get => m_copyItems;
            set => m_copyItems = value;
        }

        public CodeEditorTreeViewItem drawItem
        {
            get => m_drawItem;
        }

        public bool changed { get; set; }
        public string name { get; set; }
        protected CodeEditorTreeView(TreeViewState state, string name) : base(state)
        {
        }

        protected CodeEditorTreeView(TreeViewState state, MultiColumnHeader multiColumnHeader) : base(state, multiColumnHeader)
        {
        }

        protected virtual bool ValidBuild(T data) => true;
        public abstract object ToData();
        protected virtual bool ValidCopy(CodeEditorTreeViewItem item) => true;
        protected override void SelectionChanged(IList<int> selectedIds)
        {
            m_selectedItems.Clear();
            foreach (var id in selectedIds)
            {
                var item = FindItem(id, rootItem);
                m_selectedItems.Add((CodeEditorTreeViewItem)item);
            }

            if (m_selectedItems.Count > 0)
            {
                m_drawItem = m_selectedItems[m_selectedItems.Count - 1];
            }
        }

        public override void OnGUI(Rect rect)
        {
            base.OnGUI(rect);
            if (GUI.changed)
            {
                Reload();
            }
        }

        public virtual void Copy()
        {
            m_copyItems.Clear();
            foreach (var item in m_selectedItems)
            {
                if (ValidCopy(item))
                {
                    var cloneItem = item.Clone();
                    m_copyItems.Add(cloneItem);
                }
            }
            Reload();
        }
        protected virtual bool ValidInsert(CodeEditorTreeViewItem target, CodeEditorTreeViewItem validate) => true;
        public virtual void Paste(InsertOption option = InsertOption.Down)
        {
            if (itemClicked == null)
            {
                throw new InvalidOperationException("没有粘贴操作的目标对象！itemClicked == null??");
            }

            List<int> ids = new List<int>();
            foreach(var item in m_copyItems)
            {
                if(ValidInsert(itemClicked, item))
                {
                    var realInsertion = item.Clone();
                    InsertOperation(itemClicked, realInsertion, option);
                    ids.Add(realInsertion.id);
                    changed = true;
                }
            }
            Reload();
            if (ids.Count > 0)
            {
                SetExpanded(itemClicked.id, true);
                SetSelection(ids, TreeViewSelectionOptions.FireSelectionChanged);
            }
        }
        public virtual void Delete()
        {
            foreach(var item in m_selectedItems)
            {
                item.parent.children.Remove(item);
                item.parent = null;
                changed = true;
            }
            Reload();
        }
        public virtual void Insert(object data, InsertOption option = InsertOption.Down)
        {
            if (itemClicked == null)
            {
                throw new InvalidOperationException("没有插入操作的目标对象！itemClicked == null??");
            }
            var insertion = itemClicked.Build(data);
            List<int> ids = new List<int>();
            if (ValidInsert(itemClicked, insertion))
            {
                InsertOperation(itemClicked, insertion, option);
                ids.Add(insertion.id);
                changed = true;
            }
            Reload();
            if (ids.Count > 0)
            {
                SetExpanded(itemClicked.id, true);
                SetSelection(ids, TreeViewSelectionOptions.FireSelectionChanged);
            }
        }
        private void InsertOperation(CodeEditorTreeViewItem target, CodeEditorTreeViewItem item, InsertOption option)
        {
            switch (option)
            {
                case InsertOption.Up:
                    target.children.Insert(0, item);
                    item.parent = target;
                    break;
                case InsertOption.Down:
                    target.AddChild(item);
                    break;
            }
            item.depth = target.depth + 1;
            item.Traverse(child => child.depth = child.parent.depth + 1);
        }
        protected virtual bool ValidMove(CodeEditorTreeViewItem item, MoveOption option) => true;

        protected virtual void ReorderMoveId()
        {
            var debugIdRange = 1;
            buildRoot.Traverse(item =>
            {
                item.moveId = debugIdRange++;
            });
        }
        public virtual void Move(MoveOption option)
        {
            List<int> ids = new List<int>();
            m_selectedItems.Sort((a, b) =>
            {
                var itemLeft = a;
                var itemRight = b;
                return option == MoveOption.Up ? itemLeft.moveId - itemRight.moveId : itemRight.moveId - itemLeft.moveId; 
            });
            foreach(TreeViewItem item in m_selectedItems)
            {
                var parent = item.parent;
                var index = parent.children.IndexOf(item);
                if (index > 0 && option == MoveOption.Up)
                {
                    parent.children.RemoveAt(index);
                    parent.children.Insert(index - 1, item);
                    ids.Add(item.id);
                    changed = true;
                }
                else if (index < parent.children.Count - 1 && option == MoveOption.Down)
                {
                    parent.children.RemoveAt(index);
                    parent.children.Insert(index + 1, item);
                    ids.Add(item.id);
                    changed = true;
                }
            }
            Reload();
            if (ids.Count > 0)
            {
                SetSelection(ids, TreeViewSelectionOptions.FireSelectionChanged);
            }
        }
    }
}
