using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomPropertyDrawer(typeof(MeleeHitData))]
public class MeleeHitDataDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

        if (property.isExpanded)
        {
            int baseIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = baseIndent + 1;
            float y = position.y + EditorGUIUtility.singleLineHeight + 2;

            bool isEnemySkill = property.serializedObject.FindProperty("isEnemySkill").boolValue;

            DrawField(ref y, position, property, "centerOffset");
            DrawField(ref y, position, property, "boxSize");
            DrawField(ref y, position, property, "boxRotation");
            DrawField(ref y, position, property, "damageMultiplier");
            DrawField(ref y, position, property, "knockbackForce");

            DrawField(ref y, position, property, "knockdownType");

            // 💡 넉다운 타입이 None이 아닐 때만 지속 시간 입력창 표시
            if (property.FindPropertyRelative("knockdownType").enumValueIndex > 0)
            {
                EditorGUI.indentLevel = baseIndent + 2;
                DrawField(ref y, position, property, "knockdownDuration");
                EditorGUI.indentLevel = baseIndent + 1;
            }

            DrawField(ref y, position, property, "hitSound");
            DrawField(ref y, position, property, "effectDirection");

            if (isEnemySkill)
            {
                DrawField(ref y, position, property, "playerHitEffect");
            }

            EditorGUI.indentLevel = baseIndent + 1;
            DrawField(ref y, position, property, "useAreaSpawn");
            if (property.FindPropertyRelative("useAreaSpawn").boolValue)
            {
                EditorGUI.indentLevel = baseIndent + 2;
                DrawField(ref y, position, property, "areaPrefab");
                DrawField(ref y, position, property, "areaOffset");
                DrawField(ref y, position, property, "areaDuration");
                DrawField(ref y, position, property, "areaTickInterval");
                DrawField(ref y, position, property, "areaDamageMultiplier");
            }

            EditorGUI.indentLevel = baseIndent + 1;
            DrawField(ref y, position, property, "useGuardSettings");
            if (property.FindPropertyRelative("useGuardSettings").boolValue)
            {
                EditorGUI.indentLevel = baseIndent + 2;
                SerializedProperty guardData = property.FindPropertyRelative("guardData");
                DrawField(ref y, position, guardData, "guardable");
                DrawField(ref y, position, guardData, "parryable");
            }

            EditorGUI.indentLevel = baseIndent + 1;
            DrawField(ref y, position, property, "useStunSettings");
            if (property.FindPropertyRelative("useStunSettings").boolValue)
            {
                EditorGUI.indentLevel = baseIndent + 2;
                SerializedProperty stunData = property.FindPropertyRelative("stunData");
                SerializedProperty stunTypeProp = stunData.FindPropertyRelative("stunType");

                DrawField(ref y, position, stunData, "stunType");
                DrawField(ref y, position, stunData, "offset");

                if (stunTypeProp.enumNames[stunTypeProp.enumValueIndex] == "Grabbed")
                {
                    DrawField(ref y, position, stunData, "isRealtimeFollow");
                }

                DrawField(ref y, position, stunData, "stunDuration");
            }

            EditorGUI.indentLevel = baseIndent;
        }
        EditorGUI.EndProperty();
    }

    private void DrawField(ref float y, Rect position, SerializedProperty parent, string name)
    {
        SerializedProperty prop = parent.FindPropertyRelative(name);
        float height = EditorGUI.GetPropertyHeight(prop, true);
        Rect rect = new Rect(position.x, y, position.width, height);
        EditorGUI.PropertyField(rect, prop, true);
        y += height + 2;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded) return EditorGUIUtility.singleLineHeight;

        bool isEnemySkill = property.serializedObject.FindProperty("isEnemySkill").boolValue;

        float h = EditorGUIUtility.singleLineHeight + 4;
        h += GetHeight(property, "centerOffset") + GetHeight(property, "boxSize") + GetHeight(property, "boxRotation");
        h += GetHeight(property, "damageMultiplier") + GetHeight(property, "knockbackForce") + GetHeight(property, "knockdownType");

        // 💡 넉다운 시간 높이 동적 추가
        if (property.FindPropertyRelative("knockdownType").enumValueIndex > 0)
        {
            h += GetHeight(property, "knockdownDuration");
        }

        h += GetHeight(property, "hitSound");
        h += GetHeight(property, "effectDirection");

        if (isEnemySkill) h += GetHeight(property, "playerHitEffect");

        h += GetHeight(property, "useAreaSpawn");
        if (property.FindPropertyRelative("useAreaSpawn").boolValue)
        {
            h += GetHeight(property, "areaPrefab") + GetHeight(property, "areaOffset") + GetHeight(property, "areaDuration") + GetHeight(property, "areaTickInterval") + GetHeight(property, "areaDamageMultiplier");
        }

        h += GetHeight(property, "useGuardSettings");
        if (property.FindPropertyRelative("useGuardSettings").boolValue)
        {
            SerializedProperty guardData = property.FindPropertyRelative("guardData");
            if (guardData != null)
            {
                h += GetHeight(guardData, "guardable") + GetHeight(guardData, "parryable");
            }
        }

        h += GetHeight(property, "useStunSettings");
        if (property.FindPropertyRelative("useStunSettings").boolValue)
        {
            SerializedProperty stunData = property.FindPropertyRelative("stunData");
            if (stunData != null)
            {
                SerializedProperty stunTypeProp = stunData.FindPropertyRelative("stunType");

                h += GetHeight(stunData, "stunType") + GetHeight(stunData, "offset");

                if (stunTypeProp != null && stunTypeProp.enumValueIndex >= 0 && stunTypeProp.enumNames.Length > stunTypeProp.enumValueIndex)
                {
                    if (stunTypeProp.enumNames[stunTypeProp.enumValueIndex] == "Grabbed")
                    {
                        h += GetHeight(stunData, "isRealtimeFollow");
                    }
                }
                h += GetHeight(stunData, "stunDuration");
            }
        }

        return h;
    }

    private float GetHeight(SerializedProperty parent, string name) => EditorGUI.GetPropertyHeight(parent.FindPropertyRelative(name), true) + 2;
}