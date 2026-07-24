/*using UnityEngine;
using UnityEditor;

public class TerrainOffset : EditorWindow
{
    public float offset = 0.1f;

    [MenuItem("Tools/Offset All Terrains")]
    public static void ShowWindow() => GetWindow<TerrainOffset>();

    void OnGUI()
    {
        offset = EditorGUILayout.FloatField("Offset (0~1)", offset);

        if (GUILayout.Button("Apply to All Terrains"))
        {
            // 1. 씬 내의 모든 활성 터레인 가져오기
            Terrain[] allTerrains = Terrain.activeTerrains;

            if (allTerrains.Length == 0)
            {
                Debug.LogWarning("씬에 활성화된 터레인이 없습니다.");
                return;
            }

            // 2. Undo 그룹 시작 (여러 터레인의 변경사항을 한 번의 Ctrl+Z로 되돌리기 위함)
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            foreach (Terrain terrain in allTerrains)
            {
                TerrainData td = terrain.terrainData;
                if (td == null) continue;

                // 3. 각 터레인 데이터의 현재 상태를 Undo 시스템에 등록
                Undo.RegisterCompleteObjectUndo(td, "Offset All Terrains Height");

                int res = td.heightmapResolution;
                float[,] heights = td.GetHeights(0, 0, res, res);

                for (int y = 0; y < res; y++)
                {
                    for (int x = 0; x < res; x++)
                    {
                        // 기존 높이에 오프셋을 더하고 0~1 범위로 제한
                        heights[y, x] = Mathf.Clamp01(heights[y, x] + offset);
                    }
                }

                // 4. 변경된 높이 적용
                td.SetHeights(0, 0, heights);
            }

            // 5. Undo 그룹 이름 지정 및 마무리
            Undo.SetCurrentGroupName("Offset All Terrains");
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log($"{allTerrains.Length}개의 터레인에 오프셋이 적용되었습니다. (Ctrl+Z 가능)");
        }
    }
}*/