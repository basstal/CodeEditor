using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Reflection;
using CodeEditor;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Google.Protobuf.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CodeEditor
{
    public enum BevTreeDisplayType{
        FIELD_NAME,
        FIELD_NAME_AND_EXPLANATION,
        EXPLANATION,
    }
    public class BevTreeViewItem : CodeEditorTreeViewItem
    {
        private static int m_itemId = 1;
        
        private static GUIStyle m_GUIStyle = new GUIStyle()
        {
            wordWrap = true,
            richText = true,
        };
        private static BevTreeDisplayType m_displayType = BevTreeDisplayType.FIELD_NAME_AND_EXPLANATION;

        
        private int m_debugId;
        public override int moveId
        {
            get => m_debugId;
            set
            {
                m_debugId = value;
                DisplayNameRefresh();
            } 
        }
        public CodeNode data { get; set; }

        public string nameWithDebugId { get; set; }
        public string nameNoId { get; set; }
        public BevTreeViewItem(CodeNode data, int depth)
        {
            this.id = m_itemId ++;
            this.data = data;
            this.depth = depth;

            DisplayNameRefresh();
            switch (data?.NodeType)
            {
                case BevNodeType.Action:
                    icon = BevTreeWindow.instance.actionTex;
                    break;
                case BevNodeType.Condition:
                    icon = BevTreeWindow.instance.conditionTex;
                    break;
            }
        }

        void DisplayNameRefresh()
        {
            var displayName = $"{data?.Type}";
            displayName += string.IsNullOrEmpty(data?.Name) ? "" : $"({data?.Name})";
            Composite composite = null;
            switch(data?.Type)
            {
                case "Selector":
                    composite = data.Selector;
                    break;
                case "Sequence":
                    composite = data.Sequence;
                    break;
            }
            if (composite != null && composite.AbortMode != BevAbortMode.None)
            {
                displayName += $"[{composite.AbortMode}]";
            }

            this.nameNoId = $"{displayName}";
            this.nameWithDebugId = $"{displayName} : {this.moveId}";
        }
        
        public override CodeEditorTreeViewItem Clone()
        {
            return Build(data.Clone());
        }

        public override CodeEditorTreeViewItem Build<T>(T data, int depth = 0)
        {
            if (data == null)
            {
                Debug.LogError("Build data is null??");
                return null;
            }

            if (data is CodeNode codeNode)
            {
                
                var item = new BevTreeViewItem(codeNode, depth);
                // ** todo 这里有时候会报空异常
                item.displayName = BevTreeWindow.instance.isBevTreeDisplayDebugId ? item.nameWithDebugId : item.nameNoId; 
                    
                if (codeNode.Children?.Count > 0)
                {
                    for(var i = 0; i < codeNode.Children.Count; ++i)
                    {
                        var childItem = Build(codeNode.Children[i], depth + 1);
                        item.AddChild(childItem);
                    }
                }
                return item;
            }
            throw new InvalidCastException($"BevTreeViewItem cannot build data type {typeof(T).Name}");
        }
        public override void Draw()
        {
            GUILayout.Label("<color=cyan>字段显示方式</color>", m_GUIStyle);
            
            m_displayType = (BevTreeDisplayType)EditorGUILayout.EnumPopup(m_displayType);

            var type = data.GetType();
            var propertyInfoList = type.GetProperties();
            
            var noPropertyInfo = true;
            foreach(var propertyInfo in propertyInfoList)
            {
                switch (propertyInfo.Name)
                {
                    case "Name":
                        GUILayout.BeginVertical("box");
                        {
                            var descriptor = Utility.GetDescriptor(type);
                            DrawPropertyName(propertyInfo, descriptor);
                            DrawProperty(propertyInfo, data, descriptor);
                        }
                        GUILayout.EndVertical();
                        break;
                    case "Decorates":
                        if (data.NodeType == BevNodeType.Condition)
                        {
                            GUILayout.BeginVertical("box");
                            GUILayout.Label("装饰器");
                            // ** todo 这里或许能简化
                            foreach (var decorate in data.Decorates)
                            {
                                var decorateType = decorate.GetType();
                                var descriptor = Utility.GetDescriptor(decorateType); 
                                var innerPropertyInfoList = decorateType.GetProperties();
                                foreach (var innerPropertyInfo in innerPropertyInfoList)
                                {
                                    if (innerPropertyInfo.Name == "Parser" ||
                                        innerPropertyInfo.Name == "Descriptor")
                                    {
                                        continue;
                                    }
                                    DrawPropertyName(innerPropertyInfo, descriptor);
                                    DrawProperty(innerPropertyInfo, decorate, descriptor);
                                }
                            }

                            // ** 从这里开始记录值是否发生变更
                            GUI.changed = false;
                            GUILayout.BeginHorizontal();
                            {
                                if (GUILayout.Button("+"))
                                {
                                    data.Decorates.Add(new Decorate());
                                }

                                if (GUILayout.Button("-"))
                                {
                                    data.Decorates.RemoveAt(data.Decorates.Count - 1);
                                }
                            }
                            if (GUI.changed)
                            {
                                BevTreeWindow.instance.bevTreeView.changed = true;
                            }
                            GUILayout.EndHorizontal();
                            GUILayout.EndVertical();
                        }
                        break;
                    default:
                        var innerType = propertyInfo.PropertyType;
                        // 内部的IMessage
                        if (typeof(IMessage).IsAssignableFrom(innerType))
                        {
                            var descriptor = Utility.GetDescriptor(innerType); 
                            var innerPropertyInfoList = innerType.GetProperties();
                            var innerNode = propertyInfo.GetValue(data);
                            // ** 这个与2比较是因为最少都有2个属性 parser 和 descriptor
                            if (innerNode != null && innerPropertyInfoList.Length > 2)
                            {
                                // ** todo 这一句在某种情况下会报错 ArgumentException: GUILayout: Mismatched LayoutGroup.ignore
                                GUILayout.BeginVertical("box");
                                {
                                    foreach(var innerPropertyInfo in innerPropertyInfoList)
                                    {
                                        if (innerPropertyInfo.Name == "Parser" ||
                                            innerPropertyInfo.Name == "Descriptor")
                                        {
                                            continue;
                                        }
                                        DrawPropertyName(innerPropertyInfo, descriptor);
                                        DrawProperty(innerPropertyInfo, innerNode, descriptor);
                                        noPropertyInfo = false;
                                    }
                                }
                                GUILayout.EndVertical();
                                GUILayout.Space(5f);
                            }

                            if (innerPropertyInfoList.Length <= 2)
                            {
                                noPropertyInfo = false;
                            }
                        }
                        break;
                }
            }
            
            if (noPropertyInfo && data.NodeType != BevNodeType.None)
            {
                Debug.LogWarning("请检查是否忘记把message注册到CodeNode的oneof prop字段结构中？？");
            }
        }

        void DrawPropertyName(PropertyInfo propertyInfo, MessageDescriptor descriptor)
        {
            var name = Char.ToLower(propertyInfo.Name[0]) + propertyInfo.Name.Substring(1);
            if (m_displayType == BevTreeDisplayType.FIELD_NAME || m_displayType == BevTreeDisplayType.FIELD_NAME_AND_EXPLANATION)
            {
                GUILayout.Label(name);
            }
            if (m_displayType == BevTreeDisplayType.FIELD_NAME_AND_EXPLANATION || m_displayType == BevTreeDisplayType.EXPLANATION)
            {
                var fullName = $"{descriptor.Name}.{name}";
                if (BevTreeWindow.instance.explanations.TryGetValue(fullName, out var exp))
                {
                    GUILayout.Label($"<color=cyan>{exp}</color>", m_GUIStyle);
                }
                else
                {
                    //Debug.Log($"节点[{fullName}]没有对应的proto 帮助？？推荐写注释// help=[]来帮助理解和记录修改");
                }
            }
        }
        
        void DrawProperty(PropertyInfo propertyInfo, object obj, MessageDescriptor descriptor)
        {
            var name = Char.ToLower(propertyInfo.Name[0]) + propertyInfo.Name.Substring(1);
            var fieldDescriptor = descriptor.FindFieldByName(name);
            if (fieldDescriptor == null)
            {
                Debug.LogError($"Not found field -> {name} <- in descriptor {descriptor.Name}");
                return;
            }
            // ** 从这里开始记录值是否发生变更
            GUI.changed = false;
            var value = propertyInfo.GetValue(obj);
            if (fieldDescriptor.IsRepeated)
            {
                switch (fieldDescriptor.FieldType)
                {
                    case FieldType.String:
                        // ** todo 这里只处理了string 其他也参照这种方式处理 可以提取一个公用函数??
                        List<string> result = Utility.GetRepeatedFields<string>(value);
                        for(int i = 0; i < result.Count; ++i)
                        {
                            object v = result[i];
                            DrawType(fieldDescriptor.FieldType, ref v);
                            if ((string)v != result[i])
                            {
                                Utility.RemoveAtRepeatedField(value, i);
                                Utility.Insert2RepeatedField(value, v, i);
                            }
                        }

                        DrawRepeatedModificators(value, "");
                        break;
                    default:
                        Debug.LogError($"未处理的Draw Repeated内置类型： {fieldDescriptor.FieldType}");
                        break;
                }
            }
            else
            {
                DrawType(fieldDescriptor.FieldType, ref value, propertyInfo);
                propertyInfo.SetValue(obj, value);
            }
            if (GUI.changed)
            {
                BevTreeWindow.instance.bevTreeView.changed = true;
                DisplayNameRefresh();
            }
            EditorGUILayout.Separator();
        }
        void DrawType(FieldType t, ref object value, PropertyInfo propertyInfo = null)
        {
            switch(t)
            {
                case FieldType.SFixed64:
                case FieldType.Fixed64:
                    float v = Utility.FixedToFloat((long)value);
                    v = EditorGUILayout.FloatField(v);
                    value = Utility.FloatToFixed(v);
                    break;
                case FieldType.Enum:
                    if (propertyInfo == null)
                    {
                        throw new InvalidOperationException("FieldType.Enum no propertyInfo??");
                    }
                    var enumNames = Enum.GetNames(propertyInfo.PropertyType);
                    var enumName = Enum.GetName(propertyInfo.PropertyType, value);
                    var index = Array.IndexOf(enumNames, enumName);
                    if (index == -1)
                    {
                        GUI.changed = true;
                        index = 0;
                    }
                    int newIndex = EditorGUILayout.Popup(index, enumNames);
                    var newEnumName = enumNames[newIndex];
                    value = (int)Enum.Parse(propertyInfo.PropertyType, newEnumName);
                    break;
                case FieldType.String:
                    value = EditorGUILayout.TextField((string)value);
                    break;
                case FieldType.Bool:
                    value = EditorGUILayout.Toggle((bool)value);
                    break;
                case FieldType.Int32:
                    value = EditorGUILayout.IntField((int)value);
                    break;
                case FieldType.Int64:
                    value = EditorGUILayout.LongField((long)value);
                    break;
                case FieldType.Float:
                    value = EditorGUILayout.FloatField((float) value);
                    break;
                case FieldType.Double:
                    value = EditorGUILayout.DoubleField((double) value);
                    break;
                default:
                    Debug.LogError($"未处理的DrawProperty类型： {t}");
                    break;
            }
        }

        void DrawRepeatedModificators(object obj, object defaultVal)
        {
            GUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("+"))
                {
                    Utility.Insert2RepeatedField(obj,defaultVal);
                }
            
                if (GUILayout.Button("-"))
                {
                    Utility.RemoveAtRepeatedField(obj);
                }
                        
            }
            GUILayout.EndHorizontal();
        }
    }
}
