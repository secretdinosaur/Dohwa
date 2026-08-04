using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Skill_Area))]
public class SkillAreaEditor : Editor
{
    private SerializedProperty areaPrefab;
    private SerializedProperty offset;
    private SerializedProperty defaultDuration;
    private SerializedProperty defaultTickInterval;

    private SerializedProperty damageMultiplier;
    private SerializedProperty hitSound;
    private SerializedProperty playerHitEffect;
    private SerializedProperty effectDirection;

    private SerializedProperty guardData;
    private SerializedProperty stunData;

    private SerializedProperty isEnemySkill;

    private void OnEnable()
    {
        areaPrefab = serializedObject.FindProperty("areaPrefab");
        offset = serializedObject.FindProperty("offset");
        defaultDuration = serializedObject.FindProperty("defaultDuration");
        defaultTickInterval = serializedObject.FindProperty("defaultTickInterval");

        damageMultiplier = serializedObject.FindProperty("damageMultiplier");
        hitSound = serializedObject.FindProperty("hitSound");
        playerHitEffect = serializedObject.FindProperty("playerHitEffect");
        effectDirection = serializedObject.FindProperty("effectDirection");
        guardData = serializedObject.FindProperty("guardData");
        stunData = serializedObject.FindProperty("stunData");

        isEnemySkill = serializedObject.FindProperty("isEnemySkill");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(serializedObject, "m_Script",
            "areaPrefab", "offset", "defaultDuration", "defaultTickInterval",
            "damageMultiplier", "hitSound", "playerHitEffect", "effectDirection",
            "guardData", "stunData");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Area Settings", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(areaPrefab);
        EditorGUILayout.PropertyField(offset);
        EditorGUI.indentLevel--;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Tuning Settings", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(defaultDuration);
        EditorGUILayout.PropertyField(defaultTickInterval);
        EditorGUI.indentLevel--;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Hit Settings", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(damageMultiplier);
        EditorGUILayout.PropertyField(hitSound);
        EditorGUILayout.PropertyField(effectDirection);

        if (isEnemySkill != null && isEnemySkill.boolValue)
        {
            EditorGUILayout.PropertyField(playerHitEffect);
        }

        EditorGUILayout.Space();

        if (guardData != null)
        {
            EditorGUILayout.PropertyField(guardData, new GUIContent("Guard Data"), false);
            if (guardData.isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(guardData.FindPropertyRelative("guardable"));
                EditorGUILayout.PropertyField(guardData.FindPropertyRelative("parryable"));
                EditorGUI.indentLevel--;
            }
        }

        EditorGUILayout.Space();

        if (stunData != null)
        {
            EditorGUILayout.PropertyField(stunData, new GUIContent("Stun Data"), false);
            if (stunData.isExpanded)
            {
                EditorGUI.indentLevel++;
                SerializedProperty stunTypeProp = stunData.FindPropertyRelative("stunType");
                if (stunTypeProp != null)
                {
                    EditorGUILayout.PropertyField(stunTypeProp);
                    EditorGUILayout.PropertyField(stunData.FindPropertyRelative("offset"));

                    if (stunTypeProp.enumValueIndex >= 0 && stunTypeProp.enumValueIndex < stunTypeProp.enumNames.Length)
                    {
                        if (stunTypeProp.enumNames[stunTypeProp.enumValueIndex] == "Grabbed")
                        {
                            EditorGUILayout.PropertyField(stunData.FindPropertyRelative("isRealtimeFollow"));
                        }
                    }
                    EditorGUILayout.PropertyField(stunData.FindPropertyRelative("stunDuration"));
                }
                EditorGUI.indentLevel--;
            }
        }
        EditorGUI.indentLevel--;

        serializedObject.ApplyModifiedProperties();
    }
}