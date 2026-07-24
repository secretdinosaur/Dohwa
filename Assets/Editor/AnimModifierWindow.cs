#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class AnimKeyRemoverWindow : EditorWindow
{
    private string targetWord = "sword"; // 지울 오브젝트 이름 포함 단어

    [MenuItem("Tools/Anim Key Remover")]
    public static void ShowWindow()
    {
        GetWindow<AnimKeyRemoverWindow>("Anim Key Remover");
    }

    private void OnGUI()
    {
        GUILayout.Label("선택한 클립에서 특정 이름이 포함된 노드의 키프레임 삭제", EditorStyles.boldLabel);

        targetWord = EditorGUILayout.TextField("키를 지울 대상 단어:", targetWord);

        GUILayout.Space(10);

        if (GUILayout.Button("선택한 클립들에서 해당 키 모두 지우기"))
        {
            RemoveKeysFromClips();
        }
    }

    private void RemoveKeysFromClips()
    {
        AnimationClip[] clips = Selection.GetFiltered<AnimationClip>(SelectionMode.Assets);
        int modifiedClipCount = 0;
        int totalRemovedKeys = 0;

        foreach (AnimationClip clip in clips)
        {
            bool isModified = false;

            // 1. Float 기반 커브 (Position, Rotation, Scale 등 대부분의 Transform 데이터) 가져오기
            EditorCurveBinding[] floatBindings = AnimationUtility.GetCurveBindings(clip);
            List<EditorCurveBinding> bindingsToRemove = new List<EditorCurveBinding>();

            foreach (EditorCurveBinding bind in floatBindings)
            {
                // 경로(path)에 타겟 단어가 포함되어 있다면 삭제 대상 리스트에 추가
                // 대소문자 구분 없이 지우고 싶다면 .ToLower() 등을 활용하세요.
                if (bind.path.Contains(targetWord))
                {
                    bindingsToRemove.Add(bind);
                }
            }

            // 2. 수집된 대상 커브들을 null로 덮어씌워 완전히 삭제
            foreach (EditorCurveBinding bind in bindingsToRemove)
            {
                AnimationUtility.SetEditorCurve(clip, bind, null);
                isModified = true;
                totalRemovedKeys++;
            }

            // (선택 사항) 만약 Object Reference(스프라이트 변경, 머티리얼 변경 등) 커브도 지워야 한다면 활성화
            EditorCurveBinding[] objBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            List<EditorCurveBinding> objBindingsToRemove = new List<EditorCurveBinding>();
            foreach (EditorCurveBinding bind in objBindings)
            {
                if (bind.path.Contains(targetWord))
                    objBindingsToRemove.Add(bind);
            }
            foreach (EditorCurveBinding bind in objBindingsToRemove)
            {
                AnimationUtility.SetObjectReferenceCurve(clip, bind, null);
                isModified = true;
                totalRemovedKeys++;
            }

            // 변경 사항이 있다면 클립 저장
            if (isModified)
            {
                EditorUtility.SetDirty(clip);
                modifiedClipCount++;
            }
        }

        // 유니티 데이터베이스 강제 동기화 저장
        AssetDatabase.SaveAssets();

        Debug.Log($"작업 완료: 총 {modifiedClipCount}개의 클립에서 {totalRemovedKeys}개의 커브(키프레임 묶음)가 삭제되었습니다.");
    }
}
#endif