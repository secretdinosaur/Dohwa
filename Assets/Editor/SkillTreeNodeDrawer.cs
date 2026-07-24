#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SkillBookBase.SkillTreeNode))]
public class SkillTreeNodeDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
            return EditorGUIUtility.singleLineHeight;

        float height = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        SerializedProperty nodeType = property.FindPropertyRelative("nodeType");
        if (nodeType == null) return height;

        height += GetHeight(nodeType);
        int typeIndex = nodeType.enumValueIndex;

        if (typeIndex == (int)SkillBookBase.NodeType.ActiveSkill)
        {
            height += GetHeight(property.FindPropertyRelative("activeSkill"));
        }
        else if (typeIndex == (int)SkillBookBase.NodeType.PassiveSkill)
        {
            height += GetHeight(property.FindPropertyRelative("passiveSkill"));
        }
        else if (typeIndex == (int)SkillBookBase.NodeType.StatSkill)
        {
            height += GetHeight(property.FindPropertyRelative("localizeStatNodeName"));
            height += GetHeight(property.FindPropertyRelative("localizeStatNodeDesc"));
            height += GetHeight(property.FindPropertyRelative("statModifiers"));
        }

        SerializedProperty useCondition = property.FindPropertyRelative("useCondition");
        height += GetHeight(useCondition);

        if (useCondition != null && useCondition.boolValue)
        {
            height += GetHeight(property.FindPropertyRelative("autoUnlockOnCondition"));
            height += GetHeight(property.FindPropertyRelative("conditions"));
        }

        height += GetHeight(property.FindPropertyRelative("cost"));
        height += GetHeight(property.FindPropertyRelative("unlocked"));
        height += GetHeight(property.FindPropertyRelative("prerequisiteIndices"));
        height += GetHeight(property.FindPropertyRelative("isStartingNode"));

        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect rect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(rect, property.isExpanded, label, true);

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;

            SerializedProperty nodeType = property.FindPropertyRelative("nodeType");
            if (nodeType != null)
            {
                DrawProperty(ref rect, nodeType);
                int typeIndex = nodeType.enumValueIndex;

                if (typeIndex == (int)SkillBookBase.NodeType.ActiveSkill)
                {
                    DrawProperty(ref rect, property.FindPropertyRelative("activeSkill"));
                }
                else if (typeIndex == (int)SkillBookBase.NodeType.PassiveSkill)
                {
                    DrawProperty(ref rect, property.FindPropertyRelative("passiveSkill"));
                }
                else if (typeIndex == (int)SkillBookBase.NodeType.StatSkill)
                {
                    DrawProperty(ref rect, property.FindPropertyRelative("localizeStatNodeName"));
                    DrawProperty(ref rect, property.FindPropertyRelative("localizeStatNodeDesc"));
                    DrawProperty(ref rect, property.FindPropertyRelative("statModifiers"));
                }

                SerializedProperty useCondition = property.FindPropertyRelative("useCondition");
                DrawProperty(ref rect, useCondition);

                if (useCondition != null && useCondition.boolValue)
                {
                    DrawProperty(ref rect, property.FindPropertyRelative("autoUnlockOnCondition"));
                    DrawProperty(ref rect, property.FindPropertyRelative("conditions"));
                }

                DrawProperty(ref rect, property.FindPropertyRelative("cost"));
                DrawProperty(ref rect, property.FindPropertyRelative("unlocked"));
                DrawProperty(ref rect, property.FindPropertyRelative("prerequisiteIndices"));
                DrawProperty(ref rect, property.FindPropertyRelative("isStartingNode"));
            }

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    private float GetHeight(SerializedProperty prop)
    {
        if (prop == null) return 0f;
        return EditorGUI.GetPropertyHeight(prop, true) + EditorGUIUtility.standardVerticalSpacing;
    }

    private void DrawProperty(ref Rect rect, SerializedProperty prop)
    {
        if (prop == null) return;
        rect.y += rect.height + EditorGUIUtility.standardVerticalSpacing;
        rect.height = EditorGUI.GetPropertyHeight(prop, true);
        EditorGUI.PropertyField(rect, prop, true);
    }
}
#endif