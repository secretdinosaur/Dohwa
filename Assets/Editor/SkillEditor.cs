using UnityEditor;

[CustomEditor(typeof(SkillBase), true)]
public class SkillBaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty iterator = serializedObject.GetIterator();

        // 첫 번째 프로퍼티(m_Script)로 진입 후 순회 시작
        if (iterator.NextVisible(true))
        {
            do
            {
                // 조건부 렌더링: UpgradedSkill
                if (iterator.name == "UpgradedSkill")
                {
                    SerializedProperty isUpgradedProp = serializedObject.FindProperty("isUpgraded");
                    if (isUpgradedProp != null && isUpgradedProp.boolValue)
                    {
                        EditorGUILayout.PropertyField(iterator, true);
                    }
                }
                else
                {
                    // 최상단 스크립트 이름 필드는 수정 불가능하도록 비활성화 (안전장치)
                    if (iterator.name == "m_Script")
                    {
                        using (new EditorGUI.DisabledScope(true))
                        {
                            EditorGUILayout.PropertyField(iterator, true);
                        }
                    }
                    else
                    {
                        // 나머지 모든 필드 정상 출력
                        EditorGUILayout.PropertyField(iterator, true);
                    }
                }
            }
            while (iterator.NextVisible(false));
            // NextVisible(false)를 통해 PropertyField가 내부 자식들(배열 등)을 알아서 그리도록 위임하고, 다음 루트 속성으로 이동합니다.
        }

        serializedObject.ApplyModifiedProperties();
    }
}