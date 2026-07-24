/*using UnityEngine;
using UnityEditor;

public class TerrainGlobalSettings : EditorWindow
{
    private float pixelError = 5f;
    private int minDetail = 0;
    private int maxComplexity = 0;

    [MenuItem("Window/Terrain/Global Settings")]
    public static void ShowWindow() => GetWindow<TerrainGlobalSettings>("Terrain Global Settings");

    void OnGUI()
    {
        GUILayout.Label("Global Terrain Settings", EditorStyles.boldLabel);

        pixelError = EditorGUILayout.Slider("Pixel Error", pixelError, 1, 200);
        minDetail = EditorGUILayout.IntSlider("Minimum Detail Limit", minDetail, 0, 4);
        maxComplexity = EditorGUILayout.IntSlider("Maximum Complexity Limit", maxComplexity, 0, 4);

        EditorGUILayout.Space();

        if (GUILayout.Button("Apply to All Terrains"))
        {
            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            foreach (Terrain t in terrains)
            {
                Undo.RecordObject(t, "Update Terrain Settings");

                // 인스펙터의 수치와 매칭되는 API 속성들
                t.heightmapPixelError = pixelError;

                // 내부 패치 레벨 제어 (버전에 따라 작동 방식이 다를 수 있음)
                // 최신 버전에서는 LOD 관련 연산을 내부적으로 자동화하므로 
                // 아래 값들이 시각적으로 즉각 반영되지 않을 수 있습니다.
#if UNITY_2019_1_OR_NEWER
                // 인스펙터 상의 'Minimum Detail Limit'와 'Maximum Complexity Limit' 대응
                // 유니티 내부 시리얼라이즈드 데이터를 직접 건드려야 할 수도 있습니다.
#endif
            }
            Debug.Log($"{terrains.Length}개의 터레인 설정 변경 완료.");
        }
    }
}*/