using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class SceneMeshInspector : EditorWindow
{
    private Vector2 scrollPosition;
    private List<InspectionResult> problematicObjects = new List<InspectionResult>();

    private class InspectionResult
    {
        public GameObject gameObject;
        public string reason;
    }

    [MenuItem("Tools/Scene Mesh Inspector")]
    public static void ShowWindow()
    {
        GetWindow<SceneMeshInspector>("Scene Mesh Inspector");
    }

    private void OnGUI()
    {
        if (GUILayout.Button("현재 씬 분석하기", GUILayout.Height(30)))
        {
            AnalyzeScene();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"비정상적으로 씬에 묶인 대상: {problematicObjects.Count}개", EditorStyles.boldLabel);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        foreach (var result in problematicObjects)
        {
            if (result.gameObject == null) continue;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.ObjectField(result.gameObject, typeof(GameObject), true);
            EditorGUILayout.LabelField(result.reason, GUILayout.Width(240));

            if (GUILayout.Button("선택", GUILayout.Width(50)))
            {
                Selection.activeGameObject = result.gameObject;
                EditorGUIUtility.PingObject(result.gameObject);
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    private void AnalyzeScene()
    {
        problematicObjects.Clear();

        MeshFilter[] meshFilters = FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude);
        foreach (MeshFilter mf in meshFilters)
        {
            CheckAndAdd(mf.gameObject, mf.sharedMesh);
        }

        SkinnedMeshRenderer[] smrs = FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Exclude);
        foreach (SkinnedMeshRenderer smr in smrs)
        {
            CheckAndAdd(smr.gameObject, smr.sharedMesh);
        }
    }

    private void CheckAndAdd(GameObject obj, Mesh mesh)
    {
        if (problematicObjects.Exists(r => r.gameObject == obj)) return;

        string reason = GetProblemReason(obj, mesh);
        if (!string.IsNullOrEmpty(reason))
        {
            problematicObjects.Add(new InspectionResult { gameObject = obj, reason = reason });
        }
    }

    private string GetProblemReason(GameObject obj, Mesh mesh)
    {
        if (mesh != null)
        {
            string assetPath = AssetDatabase.GetAssetPath(mesh);

            if (string.IsNullOrEmpty(assetPath))
            {
                return "메시 에셋 경로 없음 (메모리 메시)";
            }
            if (assetPath.EndsWith(".unity"))
            {
                return "메시가 씬 파일(.unity)에 내장됨";
            }
            if (assetPath.EndsWith(".prefab"))
            {
                return "메시가 프리팹에 내장됨 (FBX 미연결)";
            }
        }

        if (!PrefabUtility.IsPartOfPrefabInstance(obj))
        {
            return "프리팹 인스턴스가 아님";
        }

        return null;
    }
}