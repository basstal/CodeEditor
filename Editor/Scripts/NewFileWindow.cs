#define ACT91
using System;
using System.IO;
using System.Net;
using Google.Protobuf;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CodeEditor
{
    public enum NewFileOption
    {
        BevTree,
        FCode
    };
    
    public class NewFileWindow : EditorWindow
    {
        

        private NewFileOption m_option;
        private string m_newFileName;
        private bool m_isNewFileSnippet;
        private EditorWindow m_callbackWindow;

        public NewFileOption option
        {
            set => m_option = value;
        }

        public void Open(NewFileOption option, EditorWindow callbackWindow)
        {
            m_option = option;
            m_callbackWindow = callbackWindow;
            Focus();
        }
        private void OnEnable()
        {
            m_newFileName = null;
        }

        private bool Confirm()
        {
            switch (m_option)
            {
                case NewFileOption.BevTree:
                    var w = (BevTreeWindow) m_callbackWindow;
                    return w.FocusChangedConfirm();
                case NewFileOption.FCode:
                    var w1 = (FCodeWindow) m_callbackWindow;
                    return w1.FocusChangedConfirm();
            }

            return true;
        }
        private void OnGUI()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("文件名：", GUILayout.MaxWidth(60f));
            m_newFileName = GUILayout.TextField(m_newFileName);
            if (m_option == NewFileOption.BevTree)
            {
                m_isNewFileSnippet = GUILayout.Toggle(m_isNewFileSnippet, "是否为Snippet", GUILayout.MaxWidth(100f));
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(15f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("确认") && !string.IsNullOrEmpty(m_newFileName) && Confirm())
            {
                try{
                    var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(m_newFileName);
                    string newFilePath = null;
                    Action<string, bool> ListReload = null;
                    if (m_option == NewFileOption.BevTree)
                    {
                        var w = (BevTreeWindow) m_callbackWindow;
                        ListReload = w.ListReload;
                        string dir = m_isNewFileSnippet ? (string)w.bevTreeProperties["AISnippetDir"] : (string) w.bevTreeProperties["AIDir"];
                        var ext = (string)w.bevTreeProperties["AIExt"];
                        if (!Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }
                        newFilePath = Path.Combine(dir, $"{fileNameWithoutExtension}.{ext}" );
                        
                    }
                    else if (m_option == NewFileOption.FCode)
                    {
                        
                        var w = (FCodeWindow) m_callbackWindow;
                        ListReload = w.ListReload;
                        var dir = (string)w.fcodeProperties["LogicDir"];
                        var ext = (string)w.fcodeProperties["FCodeExt"];

                        if (!Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }
                        newFilePath = Path.Combine(dir, $"{fileNameWithoutExtension}.{ext}");
                    }
                    if (string.IsNullOrEmpty(newFilePath))
                    {
                        EditorUtility.DisplayDialog("新建文件", "文件名不合法", "ok");
                    }
                    else if(File.Exists(newFilePath))
                    {
                        EditorUtility.DisplayDialog("新建文件", "已存在同名文件，无法新建！将自动跳转到同名文件！", "ok");
                    }
                    else
                    {
                        if (m_option == NewFileOption.BevTree)
                        {
                            var data = new CodeNode() {Name = fileNameWithoutExtension, Type = "BevTree", BevTree = new BevTreeRoot(), NodeType = BevNodeType.None};
#if ACT91
                            BevTreeExtension.WriteCodeNode(newFilePath, data);
#else
                            File.WriteAllBytes(newFilePath, data.ToByteArray());
#endif
                        }
                        else if (m_option == NewFileOption.FCode)
                        {
                            var n = new FCodeTreeView(new TreeViewState(), "", null);
                            File.WriteAllText(newFilePath, (string)n.ToData());
                        }
                    }
                    ListReload?.Invoke(newFilePath, true);
                    Close();
                }
                catch(Exception e){
                    EditorUtility.DisplayDialog("Error!", e.ToString(), "ok");
                }
            }

            if (GUILayout.Button("取消"))
            {
                Close();
            }
            GUILayout.EndHorizontal();

        }
    }
}