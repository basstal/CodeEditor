#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEditorInternal;
using XLua;
using NOAH.Lua;

namespace CodeEditor
{
    public class BevRuntimeWindow : EditorWindow
    {
        private LuaTable m_currentAiSubContent;
        private LuaTable m_debugInfo;
        private int m_pressedButtonIndex = -1;
        private ReorderableList m_aiList;
        private LuaTable m_currentBattle;
        private Vector2 m_scrollViewPos;
        private Vector2 m_scrollViewPos1;
        private LuaFunction m_allAiBev;
        private LuaFunction m_targetAiBev;
        private LuaTable m_historyLog;
        private LuaTable m_historyLogCache;
        private string m_aiResName;
        private GUIStyle m_guiStyleBigLabel;
        private GUIStyle m_guiStyleDebugInfo;
        [MenuItem("⛵NOAH/Window/Code Editor/BevRuntime")]
        static void Init()
        {
            BevRuntimeWindow window = (BevRuntimeWindow)EditorWindow.GetWindow(typeof(BevRuntimeWindow), false, "BevRuntime", true);
            window.Show();
        }
        void OnEnable()
        {
            m_guiStyleDebugInfo = new GUIStyle {wordWrap = true, richText = true, normal = {background = null},};
            m_guiStyleBigLabel = new GUIStyle
            {
                fontSize = 26, fontStyle = FontStyle.Bold, normal = {textColor = Color.white},
            };
        }
        void OnFocus()
        {
            ListReload();
        }

        private void ListReload()
        {
            if (Application.isPlaying)
            {
                if (m_currentBattle == null)
                {
                    var luaBridge = LuaManager.Instance.Bridge;
                    var b = luaBridge.DoString("return Battle")[0];
                    if (b != null)
                    {
                        // ** bmg
                        m_currentBattle = (LuaTable)b;
                        m_currentBattle.Get("Instance", out LuaTable instance);
                        if (instance != null)
                        {
                            m_currentBattle = instance;
                        }
                    }
                    if (m_currentBattle == null)
                    {
                        // ** 其他
                        var battle = (LuaTable)luaBridge.DoString(@"return require('Logic/Battle/Battle')")[0];
                        battle.Get("Instance", out LuaTable instance);
                        if (instance != null)
                        {
                            m_currentBattle = instance;
                        }
                        else
                        {
                            m_currentBattle = (LuaTable)luaBridge.DoString("return Battle")[0];
                        }
                    }
                    LuaTable actorsMap = null;
                    m_currentBattle?.Get("actorsMap", out actorsMap);
                    if (actorsMap == null)
                    {
                        m_currentBattle = null;
                    }
                }
                if (m_currentBattle == null) return;
                if (m_targetAiBev == null || m_allAiBev == null)
                {
                    m_currentBattle.Get("TargetAIBev", out m_targetAiBev);
                    m_currentBattle.Get("AllAIBev", out m_allAiBev);
                }
                LuaTable aiBevTable = (LuaTable)m_allAiBev.Call(m_currentBattle)[0];
                List<string> fileNameList = new List<string>();
                aiBevTable.ForEach<string, LuaTable>((fileName, currentData) => {
                    fileNameList.Add(fileName);
                });
                fileNameList.Sort((a, b) => String.Compare(a, b, StringComparison.Ordinal));
                var aiBevNames = fileNameList.ToArray();
                if (m_aiList == null)
                {
                    m_aiList = new ReorderableList(aiBevNames, typeof(string), false, false, false, false);
                    m_aiList.drawElementCallback += (rect, index, isActive, isFocused) => {
                            var currentName = (string)m_aiList.list[index];
                            EditorGUI.LabelField(rect, currentName);
                        };
                    m_aiList.onSelectCallback += list => {
                            m_pressedButtonIndex = -1;
                            m_historyLog = null;
                            m_historyLogCache = null;
                            m_aiResName = null;
                        };
                }
                else
                {
                    m_aiList.list = aiBevNames;
                    var index = m_aiList.index;
                    if (index >= 0 && index < m_aiList.count)
                    {
                        m_aiList.index = Array.FindIndex(aiBevNames, p => p == (string)m_aiList.list[index]);
                    }
                }
            }
        }
        void OnGUI()
        {
            if (Application.isPlaying && m_aiList != null)
            {
                if (m_aiResName != null)
                {
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("行为树资源文件名：" + m_aiResName, m_guiStyleBigLabel);
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.BeginHorizontal();
                {
                    m_scrollViewPos = GUILayout.BeginScrollView(m_scrollViewPos);
                    {
                        m_aiList.DoLayoutList();
                    }
                    GUILayout.EndScrollView();
                    if (m_historyLogCache != null || m_historyLog != null)
                    {
                        m_scrollViewPos1 = GUILayout.BeginScrollView(m_scrollViewPos1, GUILayout.Width(position.width * 4/7));
                        {
                            int logCacheCount = 0;
                            m_historyLogCache?.ForEach<int, LuaTable>((i, cache) => {
                                var str = cache.Get<string, string>("content");
                                logCacheCount ++;
                                if (m_pressedButtonIndex != -1 && m_pressedButtonIndex == i)
                                {
                                    str = string.Format("--->{0}<---", str);
                                    m_currentAiSubContent = cache.Get<string, LuaTable>("subContent");
                                    m_debugInfo = cache.Get<string, LuaTable>("debugInfo");
                                }
                                if (GUILayout.Button(str, m_guiStyleDebugInfo))
                                {
                                    m_pressedButtonIndex = i;
                                }
                            });
                            m_historyLog?.ForEach<int, LuaTable>((i, cache) => {
                                var str = cache.Get<string, string>("content");
                                if (m_pressedButtonIndex != -1 && m_pressedButtonIndex == i + logCacheCount)
                                {
                                    str = string.Format("--->{0}<---", str);
                                    m_currentAiSubContent = cache.Get<string, LuaTable>("subContent");
                                    m_debugInfo = cache.Get<string, LuaTable>("debugInfo");
                                }
                                if (GUILayout.Button(str, m_guiStyleDebugInfo))
                                {
                                    m_pressedButtonIndex = i + logCacheCount;
                                }
                            });

                        }
                        GUILayout.EndScrollView();
                    }
                    if (m_pressedButtonIndex != -1)
                    {
                        GUILayout.BeginVertical(GUILayout.Width(150f));
                        {
                            GUILayout.BeginVertical("box");
                            {
                                GUILayout.Label("详细数据：");
                                m_debugInfo?.ForEach<string, string>((key, value) => {
                                    GUILayout.Label(string.Format("{0} : {1}", key, value));
                                });
                            }
                            GUILayout.EndVertical();
                            GUILayout.BeginVertical("box");
                            {
                                GUILayout.Label("打断信息：");
                                m_currentAiSubContent?.ForEach<int, string>((i, subContent) => {
                                    GUILayout.Label(subContent, m_guiStyleDebugInfo);
                                });
                            }
                            GUILayout.EndVertical();
                        }
                        GUILayout.EndVertical();
                    }
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("本工具需要游戏运行时才有效!", m_guiStyleBigLabel);
            }
        }
        void Update()
        {
            if (Application.isPlaying && m_aiList != null && m_targetAiBev != null)
            {
                if (m_aiList.index >= 0 && m_aiList.index < m_aiList.list.Count)
                {
                    var currentName = (string)m_aiList.list[m_aiList.index];
                    LuaTable currentContext = (LuaTable)m_targetAiBev.Call(m_currentBattle, currentName)[0];
                    currentContext.Get("historyLog", out m_historyLog);
                    currentContext.Get("historyLogCache", out m_historyLogCache);
                    currentContext.Get("aiResName", out m_aiResName);
                    Repaint();
                }
            }
        }
    }
}
#endif