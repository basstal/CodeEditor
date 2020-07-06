using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CodeEditor
{
    public abstract class CodeEditorTreeViewItem : TreeViewItem
    {
        public virtual int moveId { get; set; }
        public abstract CodeEditorTreeViewItem Clone();
        public abstract void Draw();
        // ** 先序遍历
        public void Traverse(Action<CodeEditorTreeViewItem> action)
        {
            action(this);
            if (this.children?.Count > 0)
            {
                foreach (CodeEditorTreeViewItem child in this.children)
                {
                    child.Traverse(action);
                }
            }
        }
        public abstract CodeEditorTreeViewItem Build<T>(T data, int depth = 0);

    }

}
