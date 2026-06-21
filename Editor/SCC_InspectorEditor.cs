using System;
using System.Collections.Generic;
using System.Reflection;
using SuperiorCharacterController.Attributes;
using UnityEditor;
using UnityEngine;

namespace SuperiorCharacterController.Editor
{
    [CustomPropertyDrawer(typeof(MinMaxRangeSliderAttribute))]
    public class MinMaxRangeSliderDrawer : PropertyDrawer
    {
        private const float FieldWidth = 45f;
        private const float Spacing = 4f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Vector2)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var range = (MinMaxRangeSliderAttribute)attribute;
            Vector2 value = property.vector2Value;

            Rect labelRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, position.height);
            Rect minRect = new Rect(labelRect.xMax, position.y, FieldWidth, position.height);
            Rect maxRect = new Rect(position.xMax - FieldWidth, position.y, FieldWidth, position.height);
            Rect sliderRect = new Rect(minRect.xMax + Spacing, position.y, maxRect.x - minRect.xMax - Spacing * 2f, position.height);

            EditorGUI.LabelField(labelRect, label);

            EditorGUI.BeginChangeCheck();
            value.x = EditorGUI.FloatField(minRect, value.x);
            EditorGUI.MinMaxSlider(sliderRect, ref value.x, ref value.y, range.Min, range.Max);
            value.y = EditorGUI.FloatField(maxRect, value.y);
            if (EditorGUI.EndChangeCheck())
            {
                value.x = Mathf.Clamp(value.x, range.Min, value.y);
                value.y = Mathf.Clamp(value.y, value.x, range.Max);
                property.vector2Value = value;
            }
        }
    }

    /// <summary>
    /// Reflection-driven inspector renderer for TabAttribute, EnableIfAttribute/EndIfAttribute and
    /// ShowInInspectorAttribute. Build one per Editor (it caches the field layout) and call Draw from
    /// OnInspectorGUI instead of DrawDefaultInspector.
    ///
    /// Field order is read via Type.GetFields, which Unity's own runtimes (Mono/IL2CPP) return in
    /// declaration order in practice — the same assumption every attribute-driven inspector (Odin,
    /// NaughtyAttributes, VInspector) relies on, since .NET doesn't guarantee it by spec.
    /// </summary>
    public class AttributeInspectorGUI
    {
        private readonly struct Entry
        {
            public readonly FieldInfo Field;
            public readonly string Tab;

            public Entry(FieldInfo field, string tab)
            {
                Field = field;
                Tab = tab;
            }
        }

        private readonly List<Entry> entries = new List<Entry>();
        private readonly string[] tabNames;
        private readonly GUIContent[] tabContents;
        private int selectedTab;

        public AttributeInspectorGUI(Type targetType)
        {
            var fields = targetType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            var tabs = new List<string>();
            var icons = new List<string>();
            string currentTab = null;

            foreach (var field in fields)
            {
                if (field.GetCustomAttribute<HideInInspector>() != null) continue;

                var tab = field.GetCustomAttribute<TabAttribute>();
                if (tab != null)
                {
                    currentTab = tab.TabName;
                    tabs.Add(currentTab);
                    icons.Add(tab.Icon);
                }

                bool serializable = field.IsPublic || field.GetCustomAttribute<SerializeField>() != null;
                bool showInInspector = field.GetCustomAttribute<ShowInInspectorAttribute>() != null;
                if (!serializable && !showInInspector) continue;

                entries.Add(new Entry(field, currentTab));
            }

            if (tabs.Count == 0)
            {
                tabs.Add(string.Empty);
                icons.Add(null);
            }

            tabNames = tabs.ToArray();
            tabContents = new GUIContent[tabNames.Length];
            for (int i = 0; i < tabNames.Length; i++)
                tabContents[i] = BuildTabContent(tabNames[i], icons[i]);
        }

        private static GUIContent BuildTabContent(string tabName, string icon)
        {
            if (string.IsNullOrEmpty(icon)) return new GUIContent(tabName);

            string iconName = EditorGUIUtility.isProSkin ? "d_" + icon : icon;
            GUIContent content = EditorGUIUtility.IconContent(iconName);
            return content.image != null ? new GUIContent(content.image, tabName) : new GUIContent(tabName);
        }

        public void Draw(SerializedObject serializedObject, object target)
        {
            if (tabNames.Length > 1)
            {
                EditorGUILayout.Space(8f);
                DrawTabBar();
            }

            string activeTab = tabNames[Mathf.Clamp(selectedTab, 0, tabNames.Length - 1)];
            string activeCondition = null;

            EditorGUILayout.Space(6f);

            foreach (var entry in entries)
            {
                if (entry.Tab != activeTab) continue;

                // EndIf always closes the previous range first, even on a field that opens its own.
                if (entry.Field.GetCustomAttribute<EndIfAttribute>() != null)
                    activeCondition = null;

                var enableIf = entry.Field.GetCustomAttribute<EnableIfAttribute>();
                if (enableIf != null)
                    activeCondition = enableIf.ConditionMember;

                bool enabled = activeCondition == null || ReadCondition(target, activeCondition);
                bool hasOwnDecorator = entry.Field.GetCustomAttribute<HeaderAttribute>() != null || entry.Field.GetCustomAttribute<SpaceAttribute>() != null;

                if (enabled || !hasOwnDecorator)
                {
                    // Common case: nothing to protect from the grey-out, or nothing to grey — PropertyField
                    // handles decorators, custom drawers, enums, object refs etc. in one supported call.
                    using (new EditorGUI.DisabledScope(!enabled))
                        DrawField(serializedObject, entry.Field, target);
                }
                else
                {
                    // Disabled field that owns a Header/Space: PropertyField would grey the decorator along
                    // with the control, so draw the decorator first at full brightness, then the value
                    // through a small per-type renderer that never goes through the decorator pipeline.
                    DrawDecorators(entry.Field);
                    using (new EditorGUI.DisabledScope(true))
                        DrawFieldValue(serializedObject, entry.Field, target);
                }
            }
        }

        // ButtonLeft/Mid/Right are Unity's built-in skin styles for a flush, joined-edge button row
        // (same family used for segmented controls elsewhere in the Editor) — taller and bolder than
        // EditorStyles.toolbarButton, with the theme's native selected/highlight color for the active tab.
        private void DrawTabBar()
        {
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < tabContents.Length; i++)
            {
                GUIStyle style = tabContents.Length == 1 ? GUI.skin.button
                    : i == 0 ? GUI.skin.GetStyle("ButtonLeft")
                    : i == tabContents.Length - 1 ? GUI.skin.GetStyle("ButtonRight")
                    : GUI.skin.GetStyle("ButtonMid");

                // MinWidth keeps it from clipping the text/icon; ExpandWidth lets the row's leftover
                // space split across tabs so the bar always spans the full inspector width.
                float minWidth = style.CalcSize(tabContents[i]).x + 10f;
                bool isSelected = GUILayout.Toggle(selectedTab == i, tabContents[i], style, GUILayout.MinWidth(minWidth), GUILayout.ExpandWidth(true), GUILayout.Height(22f));
                if (isSelected) selectedTab = i;
            }
            EditorGUILayout.EndHorizontal();
        }

        private static bool ReadCondition(object target, string memberName)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var field = target.GetType().GetField(memberName, flags);
            if (field != null) return Convert.ToBoolean(field.GetValue(target));

            var property = target.GetType().GetProperty(memberName, flags);
            return property != null && Convert.ToBoolean(property.GetValue(target));
        }

        private static void DrawField(SerializedObject serializedObject, FieldInfo field, object target)
        {
            var property = serializedObject.FindProperty(field.Name);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, true);
                return;
            }

            // ShowInInspector fields skip PropertyField, which is what normally draws Header/Space
            // decorators — so a non-serialized field carrying those attributes needs them drawn by hand.
            DrawDecorators(field);
            DrawReflectedValue(field, target);
        }

        // Value-only counterpart of DrawField: never calls PropertyField, so it can be used right after
        // a manually-drawn (always bright) decorator without Unity re-drawing that same decorator dimmed.
        private static void DrawFieldValue(SerializedObject serializedObject, FieldInfo field, object target)
        {
            var property = serializedObject.FindProperty(field.Name);
            if (property != null)
            {
                DrawPropertyValue(property, ObjectNames.NicifyVariableName(field.Name));
                return;
            }

            DrawReflectedValue(field, target);
        }

        private static void DrawPropertyValue(SerializedProperty property, string label)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean: EditorGUILayout.Toggle(label, property.boolValue); break;
                case SerializedPropertyType.Integer: EditorGUILayout.IntField(label, property.intValue); break;
                case SerializedPropertyType.Float: EditorGUILayout.FloatField(label, property.floatValue); break;
                case SerializedPropertyType.Vector2: EditorGUILayout.Vector2Field(label, property.vector2Value); break;
                case SerializedPropertyType.Vector3: EditorGUILayout.Vector3Field(label, property.vector3Value); break;
                case SerializedPropertyType.String: EditorGUILayout.TextField(label, property.stringValue); break;
                case SerializedPropertyType.LayerMask: EditorGUILayout.LayerField(label, property.intValue); break;
                case SerializedPropertyType.Enum: EditorGUILayout.Popup(label, property.enumValueIndex, property.enumDisplayNames); break;
                case SerializedPropertyType.ObjectReference: EditorGUILayout.ObjectField(label, property.objectReferenceValue, typeof(UnityEngine.Object), true); break;
                default: EditorGUILayout.PropertyField(property, new GUIContent(label), true); break;
            }
        }

        // ShowInInspector on a non-serialized field: read-only reflected value, no live edits.
        // Drawn with the same control as a real field of that type, so it doesn't stand out as text.
        private static void DrawReflectedValue(FieldInfo field, object target)
        {
            string label = ObjectNames.NicifyVariableName(field.Name);
            object value = field.GetValue(target);

            using (new EditorGUI.DisabledScope(true))
            {
                switch (value)
                {
                    case bool b: EditorGUILayout.Toggle(label, b); break;
                    case int i: EditorGUILayout.IntField(label, i); break;
                    case float f: EditorGUILayout.FloatField(label, f); break;
                    case Vector2 v2: EditorGUILayout.Vector2Field(label, v2); break;
                    case Vector3 v3: EditorGUILayout.Vector3Field(label, v3); break;
                    case string s: EditorGUILayout.TextField(label, s); break;
                    default: EditorGUILayout.LabelField(label, value?.ToString() ?? "null"); break;
                }
            }
        }

        // Mirrors Unity's built-in HeaderDecoratorDrawer/SpaceDecoratorDrawer spacing so a ShowInInspector
        // field lines up with the SerializeField ones around it.
        private static void DrawDecorators(FieldInfo field)
        {
            var space = field.GetCustomAttribute<SpaceAttribute>();
            if (space != null) GUILayout.Space(space.height);

            var header = field.GetCustomAttribute<HeaderAttribute>();
            if (header != null)
            {
                GUILayout.Space(8f);
                EditorGUILayout.LabelField(header.header, EditorStyles.boldLabel);
            }
        }
    }

    [CustomEditor(typeof(SCC_Controller))]
    public class SCC_ControllerEditor : UnityEditor.Editor
    {
        private AttributeInspectorGUI gui;

        private void OnEnable() => gui = new AttributeInspectorGUI(target.GetType());

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            gui.Draw(serializedObject, target);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
