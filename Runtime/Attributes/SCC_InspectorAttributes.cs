using System;
using UnityEngine;

namespace SuperiorCharacterController.Attributes
{
    /// <summary>
    /// Starts a new inspector tab. Every field declared after this one belongs to that tab, until the
    /// next TabAttribute. Rendered by AttributeInspectorGUI (Editor folder) as a toolbar. The optional
    /// icon is a built-in Unity Editor icon name (EditorGUIUtility.IconContent) shown instead of the label.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class TabAttribute : PropertyAttribute
    {
        public readonly string TabName;
        public readonly string Icon;

        public TabAttribute(string tabName, string icon = null)
        {
            TabName = tabName;
            Icon = icon;
        }
    }

    /// <summary>
    /// Greys out this field, and every field after it, while the named bool field/property is false.
    /// The range closes at the next EndIfAttribute, or is replaced by a new EnableIfAttribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class EnableIfAttribute : PropertyAttribute
    {
        public readonly string ConditionMember;

        public EnableIfAttribute(string conditionMember) => ConditionMember = conditionMember;
    }

    /// <summary>
    /// Closes the conditional range opened by the last EnableIfAttribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class EndIfAttribute : PropertyAttribute
    {
    }

    /// <summary>
    /// Displays a private, non-serialized field as a read-only debug row in the inspector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class ShowInInspectorAttribute : Attribute
    {
    }

    /// <summary>
    /// Draws a Vector2 field as a double-handle min/max slider clamped to [Min, Max].
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class MinMaxRangeSliderAttribute : PropertyAttribute
    {
        public readonly float Min;
        public readonly float Max;

        public MinMaxRangeSliderAttribute(float min, float max)
        {
            Min = min;
            Max = max;
        }
    }
}
