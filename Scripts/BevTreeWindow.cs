#define ACT91
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Google.Protobuf;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;
namespace CodeEditor
{
    public class BevTreeWindow : EditorWindow
    {
        private BevTreeView m_bevTreeView;
        private Texture2D m_conditionTex;
        private Texture2D m_actionTex;
        private JToken m_bevTreeProperties;
        private Dictionary<string, string[]> m_bevTreeInsertion;
        private Dictionary<string, string> m_explanations;
        private ReorderableList m_fileList;
        private Vector2 m_scrollView1;
        private Vector2 m_scrollView2;
        private Vector2 m_scrollView3;

        private string m_filterPattern = "";
        
        private bool m_isBevTreeDisplayDebugId;
        
        public Texture2D conditionTex
        {
            get => m_conditionTex;
        }

        public Texture2D actionTex
        {
            get => m_actionTex;
        }

        public Dictionary<string, string> explanations
        {
            get => m_explanations;
        }
        public BevTreeView bevTreeView { get => m_bevTreeView; }
        public JToken bevTreeProperties { get => m_bevTreeProperties; }
        public bool isBevTreeDisplayDebugId
        {
            get => m_isBevTreeDisplayDebugId;
        }
        public Dictionary<string, string[]> bevTreeInsertion
        {
            get => m_bevTreeInsertion;
        }
#if ACT91
        [MenuItem("⛵NOAH/Window/Code Editor/BevTree")]
#else
        [MenuItem("Window/BevTree")]
#endif
        static void ShowWindow()
        {
            var window = (BevTreeWindow)GetWindow(typeof(BevTreeWindow), false, "BevTree", true);
            window.Show();
        }

        private void OnFocus()
        {
            m_fileList = null;
            ListReload(m_bevTreeView?.name);
        }

        private void OnEnable()
        {
            // ** 节点图标加载
            m_conditionTex = Resources.Load<Texture2D>("Condition");
            m_actionTex = Resources.Load<Texture2D>("Action");
            
            // ** 节点合法性判断的配置文件读取
            var textAsset = Resources.Load<TextAsset>("BevTreeProperty");
            if (textAsset == null)
            {
                Debug.LogError("BevTreeProperty json 文件丢失??可能导致节点插入删除关系不正确!!请配置正确的 BevTreeProperty 并保存在 Resources 目录中");
                return;
            }
            m_bevTreeProperties = JToken.Parse(textAsset.text);
            
            var insertion = m_bevTreeProperties["insertion"];
            m_bevTreeInsertion = new Dictionary<string, string[]>();
            foreach(JProperty property  in insertion)
            {
                switch (property.Name)
                {
                    case "Condition":
                    case "Action":
                        // ** 所有的condition和action节点都是通过bytes文件名得来的
                        // ** 这里文件名必须与code.proto中定义的message名字一致，否则无法创建对应的proto类型
                        var dir = (string)m_bevTreeProperties[$"{property.Name}sDir"];
                        if (!Directory.Exists(dir))
                        {
                            Debug.LogError($"{property.Name} 节点 指定的查找路径 {dir} 不存在！");
                            continue;
                        }

                        var ext = (string) m_bevTreeProperties["AIExt"];
                        string [] files = Directory.GetFiles(dir, $"*.{ext}", SearchOption.AllDirectories);
                        m_bevTreeInsertion[property.Name] = files.Select(Path.GetFileNameWithoutExtension).ToArray();
                        break;
                    default:
                        if (property.Value is JArray arr)
                        {
                            m_bevTreeInsertion[property.Name] = arr.Values<string>().ToArray(); 
                        }
                        break;
                }
            }
            
            // ** 节点解释文本的读取
            m_explanations = new Dictionary<string, string>();
            var protoDirs = m_bevTreeProperties["BevTreeProto"];
            List<string> contents = new List<string>();
            foreach (string dir in protoDirs)
            {
                contents.AddRange(File.ReadAllLines(dir));
            }

            string classType = null;
            foreach(var line in contents)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    Match match = Regex.Match(line, @"^(enum|message)\s+([^\{\n\t]+)");
                    var isMessageOrEnum = false;
                    if (match.Length > 0)
                    {
                        var groups = match.Groups;
                        classType = groups[2].Value.Trim();
                        isMessageOrEnum = true;
                    }
                    if (line.Contains("//") && !string.IsNullOrEmpty(classType))
                    {
                        var equalSplit = line.Split('=');
                        var slashSplit = Regex.Split(line, "//");
                        var spaceSplit = equalSplit[0].Trim().Split(' ');
                        var rawComment = slashSplit[1].Replace('\\', '/');
                        match = Regex.Match(rawComment, @"help\s*=\s*\[(.*)\]");
                        if (match.Length > 0)
                        {
                            var comment = match.Groups[1].Value;
                            var fieldName = isMessageOrEnum ? "" : spaceSplit[spaceSplit.Length - 1];
                            var fullName = isMessageOrEnum ? classType : string.Format("{0}.{1}", classType, fieldName);
                            m_explanations.Add(fullName, comment);
                        }
                    }
                    if (line.Contains("}"))
                    {
                        classType = null;
                    }
                }
            }
        }
        
        private void ShowNodeExplanation()
        {
            if (m_bevTreeView == null)
            {
                EditorUtility.DisplayDialog("非法操作", "没有打开任意的BevTree文件，请先打开一个文件并选中一些节点再查看说明！", "确认");
                return;
            }
            if (m_bevTreeView.selectedItems?.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                Dictionary<string, bool> added = new Dictionary<string, bool>();
                foreach (BevTreeViewItem item in m_bevTreeView.selectedItems)
                {
                    var type = item.data.Type;
                    if (!added.ContainsKey(type))
                    {
                        sb.AppendLine($"节点{type}:");
                        m_explanations.TryGetValue(type, out string explanation);
                        if (explanation != null)
                        {
                            var splitResult = Regex.Split(explanation, "/n");
                            foreach (var line in splitResult)
                            {
                                sb.AppendLine(line);
                            }
                        }

                        sb.AppendLine("");
                        added.Add(type, true);
                    }
                }
                EditorUtility.DisplayDialog("说明", sb.ToString(), "确认");
            }
            else
            {
                EditorUtility.DisplayDialog("非法操作", "请先选中一个待说明的节点", "确认");
            }
        }
        
        private void OnGUI()
        {
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("新建", EditorStyles.toolbarButton, GUILayout.Width(30f)))
            {
                var newFile = (NewFileWindow)GetWindow(typeof(NewFileWindow));
                newFile.Open(NewFileOption.BevTree, this);
            }
            if (m_bevTreeView != null && m_bevTreeView.changed && GUILayout.Button("保存", EditorStyles.toolbarButton,GUILayout.Width(30f)))
            {
                Save();
                ListReload(m_bevTreeView.name);
            }
            if (GUILayout.Button("说明", EditorStyles.toolbarButton,GUILayout.Width(30f)))
            {
                ShowNodeExplanation();
            }
            if (GUILayout.Button("重启", EditorStyles.toolbarButton,GUILayout.Width(30f)))
            {
                Close();
                ShowWindow();
            }

            m_isBevTreeDisplayDebugId =
                GUILayout.Toggle(m_isBevTreeDisplayDebugId, "显示节点ID", EditorStyles.toolbarButton, GUILayout.MaxWidth(105f));
            Action<CodeEditorTreeViewItem> handler = item =>
            {
                var bevTreeViewItem = (BevTreeViewItem) item;
                bevTreeViewItem.displayName = m_isBevTreeDisplayDebugId
                    ? bevTreeViewItem.nameWithDebugId
                    : bevTreeViewItem.nameNoId;
            };
            m_bevTreeView?.buildRoot.Traverse(handler);
            GUILayout.EndHorizontal();
            
            // ** TreeView 以及节点详细信息
            GUILayout.BeginHorizontal();
            {
                
                GUILayout.BeginVertical(GUILayout.Width(position.width / 6));
                GUILayout.BeginHorizontal();
                // ** todo 搜索框和下面的列表中间有间隙
                GUILayout.Label("搜索", GUILayout.MaxWidth(30f));
                var filter = GUILayout.TextField(m_filterPattern);
                if (!filter.Equals(m_filterPattern))
                {
                    m_filterPattern = filter;
                    m_bevTreeView = null;
                    ListReload("", true);
                }
                GUILayout.EndHorizontal();
                m_scrollView1 = GUILayout.BeginScrollView(m_scrollView1, GUILayout.Width(position.width / 6));
                {
                    m_fileList?.DoLayoutList();
                }
                GUILayout.EndScrollView();
                // ** todo 有时候这里有GUI报错 EndLayoutGroup: BeginLayoutGroup must be called first.
                GUILayout.EndVertical();
                
                GUILayout.BeginVertical();
                m_scrollView2 = GUILayout.BeginScrollView(m_scrollView2, GUILayout.Width(position.width * 5 / 8));
                {
                    
                    m_bevTreeView?.OnGUI(new Rect(0, 0, position.width, position.height - 30));
                }
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
                
                GUILayout.BeginVertical();
                m_scrollView3 = GUILayout.BeginScrollView(m_scrollView3, GUILayout.Width(position.width * 5 / 24));
                {
                    m_bevTreeView?.drawItem?.Draw();
                }
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
            }
            // ** todo 有时候这里有GUI报错 EndLayoutGroup: BeginLayoutGroup must be called first.
            GUILayout.EndHorizontal();
        }

        public void Save()
        {
            var fileName = m_bevTreeView?.name;
            if (!string.IsNullOrEmpty(fileName) && File.Exists(fileName))
            {
                var data = (CodeNode)m_bevTreeView.ToData();
#if ACT91
                BevTreeExtension.WriteCodeNode(fileName, data);
#else
                File.WriteAllBytes(fileName, data.ToByteArray());
#endif
                m_bevTreeView = new BevTreeView(new TreeViewState(), m_bevTreeView.name, data);
                m_bevTreeView.Reload();
                m_bevTreeView.SetExpandedRecursive(-1, true);
                m_bevTreeView.changed = false;
            }
            else
            {
                Debug.LogError($"保存失败：所选的文件[{fileName}]不存在？");
            }
        }
        
        public bool FocusChangedConfirm()
        {
            bool? changed = m_bevTreeView?.changed;
            // ** 当前选中了文件且对应的TreeView没有变更过
            if (changed == null || !changed.Value)
            {
                return true;
            }
            int option = EditorUtility.DisplayDialogComplex("未保存的修改", $"文件[{m_bevTreeView?.name}]有未保存的修改，在继续之前是否保存这些修改？", "保存并继续", "取消", "不保存并继续");
            switch(option)
            {
                case 0:
                    Save();
                    return true;
                case 2:
                    return true;
                default:
                    return false;
            }
        }
        
        public void ListReload(string selectedFile = "", bool clearExist = false)
        {
            if (m_bevTreeProperties == null)
            {
                return;
            }
            m_fileList = clearExist ? null : m_fileList;
            var searchPattern = $"*.{(string) m_bevTreeProperties["AIExt"]}";
            var dir = (string) m_bevTreeProperties["AIDir"];
            var snippetDir = (string) m_bevTreeProperties["AISnippetDir"];
            if (!Directory.Exists(dir) || !Directory.Exists(snippetDir))
            {
                throw new InvalidOperationException($"未找到目录 : {dir} or {snippetDir}");
            }
            var files1 = Directory.GetFiles(dir, searchPattern, SearchOption.TopDirectoryOnly);
            var files2 = Directory.GetFiles(snippetDir, searchPattern, SearchOption.TopDirectoryOnly);
            var allFiles = new List<string>(files1);
            allFiles.AddRange(files2);
            var filterFiles = allFiles.FindAll(file =>
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                return fileName.ToLower().Contains(m_filterPattern.ToLower());
            });
            if (m_fileList == null)
            {
                m_fileList = new ReorderableList(filterFiles, typeof(string), false, false, false, true);
                m_fileList.drawElementCallback += (rect, index, isActive, isFocused) => {
                    
                    var file = filterFiles[index];
                    var fileNameNoExtension = Path.GetFileNameWithoutExtension(file);
                    if (file != null && file.Contains(snippetDir))
                    {
                        fileNameNoExtension = "[S]" + fileNameNoExtension;
                    }
                    EditorGUI.LabelField(rect, fileNameNoExtension + (isActive && m_bevTreeView != null && m_bevTreeView.changed? " *" : ""));
                };
                m_fileList.onSelectCallback += list => {
                    var focusFile = (string)list.list[list.index];
                    if (FocusChangedConfirm() && File.Exists(focusFile))
                    {
#if ACT91
                        CodeNode codeNode = BevTreeExtension.TableFile2CodeNode(focusFile, snippetDir);
#else
                        var parser = new MessageParser<CodeNode>(() => new CodeNode());
                        var codeNode = parser.ParseFrom(File.ReadAllBytes(focusFile));
#endif
                        m_bevTreeView = new BevTreeView(new TreeViewState(), focusFile, codeNode);
                        m_bevTreeView.Reload();
                        m_bevTreeView.SetExpandedRecursive(-1, true);
                    }
                    else
                    {
                        list.index = list.list.IndexOf(m_bevTreeView.name);
                    }
                };
                m_fileList.onRemoveCallback += list =>
                {
                    var files = list.list;
                    var deleteFile = (string)files[list.index];
                    if (EditorUtility.DisplayDialog("删除文件", "确认删除文件[" + deleteFile + "]？", "Yes", "No"))
                    {
                        File.Delete(deleteFile);
                        // ** 删除前列表中有多于1个文件，按下标选中没有被删且【下标 -1】的文件，如果是下标为0的文件被删，则特殊选中下标为1的文件
                        if (files.Count > 1)
                        {
                            ListReload((string)files[list.index > 0 ? list.index - 1 : 1]);
                        }
                        else
                        {
                            // ** 删除前列表中只有1个文件，则整个列表将会为空
                            ListReload();
                        }
                    }
                };
                m_fileList.index = filterFiles.IndexOf(selectedFile);
            }
            else
            {
                m_fileList.list = filterFiles;
                m_fileList.index = filterFiles.IndexOf(selectedFile);
                if (m_fileList.index == -1)
                {
                    // ** 找不到对应文件名的下标位置 则将当前的行为树置空
                    m_bevTreeView = null;
                }
                else
                {
                    // ** 按照找到的下标位置来选中对应的行为树
                    m_fileList.onSelectCallback(m_fileList);
                }
            }
        }
    }

}
