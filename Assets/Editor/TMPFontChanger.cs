#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using System.IO;

public class TMPFontChanger : EditorWindow
{
    private TMP_FontAsset targetFont;

    [MenuItem("Tools/TMP Font Changer")]
    public static void ShowWindow()
    {
        GetWindow<TMPFontChanger>("TMP Font Changer");
    }

    private void OnGUI()
    {
        GUILayout.Label("TMP Font Changer (Scene & Prefabs)", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        targetFont = (TMP_FontAsset)EditorGUILayout.ObjectField("Target Font", targetFont, typeof(TMP_FontAsset), false);

        EditorGUILayout.Space();
        GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1));
        EditorGUILayout.Space();

        if (GUILayout.Button("Change All Fonts in Current Scene", GUILayout.Height(30)))
        {
            if (!ValidateFont()) return;
            ChangeFontsInActiveScene();
        }

        EditorGUILayout.Space();

        GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
        if (GUILayout.Button("Change All Fonts in ALL PREFABS", GUILayout.Height(40)))
        {
            if (!ValidateFont()) return;

            if (EditorUtility.DisplayDialog("Warning", "프로젝트 내의 모든 프리팹 파일의 폰트를 변경합니다. 이 작업은 되돌릴 수 없습니다(Undo 불가). 진행하시겠습니까?", "Yes", "No"))
            {
                ChangeFontsInAllPrefabs();
            }
        }
        GUI.backgroundColor = Color.white;
    }

    private bool ValidateFont()
    {
        if (targetFont == null)
        {
            EditorUtility.DisplayDialog("Error", "Please assign a Target Font first!", "OK");
            return false;
        }
        return true;
    }

    private void ChangeFontsInActiveScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        GameObject[] rootObjects = activeScene.GetRootGameObjects();
        int totalCount = 0;

        foreach (GameObject root in rootObjects)
        {
            totalCount += ModifyTMPComponents(root);
        }

        if (totalCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorUtility.DisplayDialog("Success", $"씬 내의 {totalCount}개 TextMeshPro 폰트가 변경되었습니다.", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Notice", "변경할 컴포넌트가 없습니다.", "OK");
        }
    }

    private void ChangeFontsInAllPrefabs()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        int totalChangedCount = 0;
        int prefabCount = 0;

        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);

                EditorUtility.DisplayProgressBar("Changing Prefab Fonts", $"{Path.GetFileName(assetPath)} 처리 중...", (float)i / guids.Length);

                // 프리팹 콘텐츠 로드
                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);

                if (prefabRoot != null)
                {
                    int changedInThisPrefab = ModifyTMPComponents(prefabRoot);

                    if (changedInThisPrefab > 0)
                    {
                        // [수정된 부분] SavePrefabAsset 대신 SaveAsPrefabAsset을 사용하여 경로에 안전하게 덮어쓰기 고정
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
                        totalChangedCount += changedInThisPrefab;
                        prefabCount++;
                    }

                    // 프리팹 콘텐츠 언로드
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Success", $"{prefabCount}개의 프리팹에서 총 {totalChangedCount}개의 TextMeshPro 폰트가 성공적으로 변경되었습니다!", "OK");
    }

    private int ModifyTMPComponents(GameObject root)
    {
        int count = 0;

        TextMeshPro[] tmps = root.GetComponentsInChildren<TextMeshPro>(true);
        foreach (var tmp in tmps)
        {
            if (tmp.font != targetFont)
            {
                tmp.font = targetFont;
                EditorUtility.SetDirty(tmp);
                count++;
            }
        }

        TextMeshProUGUI[] tmpUIs = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var tmpUI in tmpUIs)
        {
            if (tmpUI.font != targetFont)
            {
                tmpUI.font = targetFont;
                EditorUtility.SetDirty(tmpUI);
                count++;
            }
        }

        return count;
    }
}
#endif