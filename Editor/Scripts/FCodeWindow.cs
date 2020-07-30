#define ACT91
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;

namespace CodeEditor
{
    public class FCodeWindow : EditorWindow, IHasCustomMenu
    {
        private JToken m_fcodeProperties;
        private Dictionary<string, string> m_fcodeSignature = new Dictionary<string, string>();
        private Dictionary<string, Texture2D> m_icons = new Dictionary<string, Texture2D>();
        private FCodeTreeView m_fcodeTreeView;
        private List<string> m_fCodeLogicFiles;
        private ReorderableList m_fileList;
        private Vector2 m_scrollView1;
        private Vector2 m_scrollView2;
        private Vector2 m_scrollView3;
        private List<string> m_fcodeConfig = new List<string>();
        private FCodeLib m_fcodeLib;
        public static FCodeWindow instance { get; private set; }

        private string m_filterPattern = "";
        public Dictionary<string, string> fcodeSignature
        {
            get => m_fcodeSignature;
        }

        public FCodeLib fcodeLib => m_fcodeLib;

        public FCodeTreeView fcodeTreeView => m_fcodeTreeView;
        public List<string> fcodeConfig
        {
            get => m_fcodeConfig;
        }
        public Dictionary<string, Texture2D>  icons => m_icons;
        public JToken fcodeProperties { get => m_fcodeProperties; }
#if ACT91
        [MenuItem("⛵NOAH/Window/Code Editor/FCode")]
#else
        [MenuItem("Window/CodeEditor/FCode", false, 1)]
#endif
        static void ShowWindow()
        {
            instance = GetWindow<FCodeWindow>("FCode编辑器", typeof(SceneView));
            instance.Show();
            instance.Focus();
        }

        private void OnFocus()
        {
            if (instance == null)
            {
                instance = GetWindow<FCodeWindow>();
            }
            ListReload(m_fcodeTreeView?.name, true);
        }

        private void OnEnable()
        {
            m_icons.Clear();
            m_icons = new Dictionary<string, Texture2D>
            {
                {"Condition", Resources.Load<Texture2D>("Condition")},
                {"Command", Resources.Load<Texture2D>("Action")},
                {"Foreach", Resources.Load<Texture2D>("Foreach")},
                {"Function", Resources.Load<Texture2D>("Function")},
                {"If", Resources.Load<Texture2D>("If")},
                {"Expression", Resources.Load<Texture2D>("Expression")},
                {"Vars", Resources.Load<Texture2D>("Vars")}
            };
            
            // ** 节点合法性判断的配置文件读取
            var textAsset = Resources.Load<TextAsset>("FCodeProperty");
            if (textAsset == null)
            {
                Debug.LogError("FCodeProperty.json 文件丢失??可能导致节点插入删除关系不正确!!请配置正确的 FCodeProperty 并保存在 Resources 目录中");
                return;
            }
            m_fcodeProperties = JToken.Parse(textAsset.text);
            var fcodeSignature = m_fcodeProperties["FCodeSignature"];
            m_fcodeSignature.Clear();
            foreach (JProperty entry in fcodeSignature)
            {
                m_fcodeSignature.Add(entry.Name, (string)entry.Value);
            }

            
            var file = (string)m_fcodeProperties["FCodeFunctionFile"];
            if(!File.Exists(file))
            {
                Debug.LogWarning($"FCodeFunctionFile not found ! path : {file}. You can change its path in FCodeProperty.json");
            }
            else
            {
                var content = File.ReadAllText(file);
                m_fcodeLib = FCodeParse.TokenizeFCLogic(content, (string) m_fcodeProperties["FCodeFunctionPrefix"]);
            }

            m_fcodeConfig.Clear();
            var type = typeof(FCodeConfig);
            var properties = type.GetProperties();
            foreach( var property in properties)
            {
                var descriptor = Utility.GetDescriptor(type);
                var name = Char.ToLower(property.Name[0]) + property.Name.Substring(1);
                var fieldDescriptor = descriptor?.FindFieldByName(name);
                if (fieldDescriptor == null) continue;
                m_fcodeConfig.Add(name);
            }
            m_fcodeConfig.Sort((a, b) => String.Compare(a, b, StringComparison.Ordinal));
        }

        private void OnGUI()
        {
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("新建", EditorStyles.toolbarButton, GUILayout.Width(30f)))
            {
                var newFile = GetWindow<NewFileWindow>("新建FCode脚本");
                newFile.Open(NewFileOption.FCode, this);
            }
            if (m_fcodeTreeView != null && m_fcodeTreeView.changed && GUILayout.Button("保存", EditorStyles.toolbarButton,GUILayout.Width(30f)))
            {
                Save();
                ListReload(m_fcodeTreeView.name);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                
                GUILayout.BeginVertical(GUILayout.Width(position.width / 6));
                GUILayout.BeginHorizontal();
                // ** todo 搜索框和下面的列表中间有间隙
                GUILayout.Label("搜索", GUILayout.MaxWidth(30f));
                var filter = GUILayout.TextField(m_filterPattern);
                if (filter != m_filterPattern)
                {
                    m_filterPattern = filter;
                    m_fcodeTreeView = null;
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
                    
                    m_fcodeTreeView?.OnGUI(new Rect(0, 0, position.width, position.height - 30));
                }
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
                
                GUILayout.BeginVertical();
                m_scrollView3 = GUILayout.BeginScrollView(m_scrollView3, GUILayout.Width(position.width * 5 / 24));
                {
                    m_fcodeTreeView?.drawItem?.Draw();
                }
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
            }
            // ** todo 有时候这里有GUI报错 EndLayoutGroup: BeginLayoutGroup must be called first.
            GUILayout.EndHorizontal();
        }
        public void Save()
        {
            var fileName = m_fcodeTreeView?.name;
            if (!string.IsNullOrEmpty(fileName) && File.Exists(fileName))
            {
                var data = (string)m_fcodeTreeView.ToData();
                File.WriteAllText(fileName, data);
                m_fcodeTreeView.changed = false;
            }
            else
            {
                Debug.LogError($"保存失败：所选的文件[{fileName}]不存在？");
            }
        }
        public bool FocusChangedConfirm()
        {
            bool? changed = m_fcodeTreeView?.changed;
            // ** 当前选中了文件且对应的TreeView没有变更过
            if (changed == null || !changed.Value)
            {
                return true;
            }
            int option = EditorUtility.DisplayDialogComplex("未保存的修改", $"文件[{m_fcodeTreeView?.name}]有未保存的修改，在继续之前是否保存这些修改？", "保存并继续", "取消", "不保存并继续");
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
            if (m_fcodeProperties == null)
            {
                return;
            }

            m_fileList = clearExist ? null : m_fileList;
            var searchPattern = $"*.{(string) m_fcodeProperties["FCodeExt"]}";
            var dir = (string) m_fcodeProperties["LogicDir"];
            if (!Directory.Exists(dir))
            {
                Debug.LogWarning($"LogicDir not found ! path : {dir}. You can change its path in FCodeProperty.json");
                return;
            }
            var allFiles = Directory.GetFiles(dir, searchPattern, SearchOption.TopDirectoryOnly);
            var filterFiles = new List<string>(allFiles).FindAll(file =>
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
                    EditorGUI.LabelField(rect, fileNameNoExtension + (isActive && m_fcodeTreeView != null && m_fcodeTreeView.changed? " *" : ""));
                };
                m_fileList.onSelectCallback += list => {
                    var focusFile = (string)list.list[list.index];
                    if (m_fcodeTreeView != null && focusFile == m_fcodeTreeView.name)
                    {
                        return;
                    }
                    if (FocusChangedConfirm() && File.Exists(focusFile))
                    {
                        var content = File.ReadAllText(focusFile);
                        FCodeLogic logic = FCodeParse.Tokenize(focusFile, content);
                        m_fcodeTreeView = new FCodeTreeView(new TreeViewState(), focusFile, logic);
                        m_fcodeTreeView.Reload();
                        m_fcodeTreeView.SetExpandedRecursive(-1, true);
                    }
                    else if (m_fcodeTreeView != null)
                    {
                        list.index = list.list.IndexOf(m_fcodeTreeView.name);
                    }
                };
                m_fileList.onRemoveCallback += list =>
                {
                    var files = list.list;
                    var deleteFile = (string)files[list.index];
                    if (EditorUtility.DisplayDialog("删除文件", "确认删除文件[" + deleteFile + "]？", "Yes", "No"))
                    {
                        File.Delete(deleteFile);
                        m_fileList = null;
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
            }
            if (m_fileList.index == -1)
            {
                // ** 找不到对应文件名的下标位置 则将当前的行为树置空
                m_fcodeTreeView = null;
            }
            else
            {
                // ** 按照找到的下标位置来选中对应的行为树
                m_fileList.onSelectCallback(m_fileList);
            }
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("重启"), false, ()=> {
                Close();
                ShowWindow();
            });
        }
    }
}