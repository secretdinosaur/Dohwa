/*using UnityEngine;
using UnityEditor;
using System.IO;

public class TerrainMerger : EditorWindow
{
    public GameObject obj00, obj10, obj01, obj11;
    public string baseName = "Tuto";
    private int generationCount = 1;

    [MenuItem("Window/Terrain/Merge 2x2 Terrains")]
    public static void ShowWindow() => GetWindow<TerrainMerger>("Terrain Merger");

    void OnGUI()
    {
        baseName = EditorGUILayout.TextField("Base Name", baseName);

        EditorGUILayout.Space();
        obj00 = (GameObject)EditorGUILayout.ObjectField("Bottom Left (0,0)", obj00, typeof(GameObject), true);
        obj10 = (GameObject)EditorGUILayout.ObjectField("Bottom Right (1,0)", obj10, typeof(GameObject), true);
        obj01 = (GameObject)EditorGUILayout.ObjectField("Top Left (0,1)", obj01, typeof(GameObject), true);
        obj11 = (GameObject)EditorGUILayout.ObjectField("Top Right (1,1)", obj11, typeof(GameObject), true);

        EditorGUILayout.Space();
        if (GUILayout.Button("Merge and Replace Original")) Merge();

        if (GUILayout.Button("Reset Count")) generationCount = 1;
    }

    void Merge()
    {
        Terrain t00 = obj00?.GetComponent<Terrain>();
        Terrain t10 = obj10?.GetComponent<Terrain>();
        Terrain t01 = obj01?.GetComponent<Terrain>();
        Terrain t11 = obj11?.GetComponent<Terrain>();

        Terrain refT = t00 ?? t10 ?? t01 ?? t11;
        if (refT == null)
        {
            Debug.LogError("슬롯에 Terrain 컴포넌트가 포함된 오브젝트를 넣어주세요.");
            return;
        }

        // 경로 설정 및 폴더 생성
        string parentPath = "Assets/08_MapObject/TerrainData";
        string folderPath = Path.Combine(parentPath, baseName);

        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            // 부모 폴더가 없을 경우를 대비해 순차적 확인은 생략 (존재한다고 가정)
            AssetDatabase.CreateFolder(parentPath, baseName);
        }

        Vector3 startPos = refT.transform.position;
        Vector3 oldSize = refT.terrainData.size;

        if (t00 != null) startPos = t00.transform.position;
        else if (t10 != null) startPos = t10.transform.position - new Vector3(oldSize.x, 0, 0);
        else if (t01 != null) startPos = t01.transform.position - new Vector3(0, 0, oldSize.z);
        else if (t11 != null) startPos = t11.transform.position - new Vector3(oldSize.x, 0, oldSize.z);

        int res = 129;
        int newRes = 257;

        TerrainData newData = new TerrainData();
        newData.heightmapResolution = newRes;
        newData.size = new Vector3(oldSize.x * 2, oldSize.y, oldSize.z * 2);

        float[,] combinedHeights = new float[newRes, newRes];

        if (t00 != null) CopyHeights(t00.terrainData, combinedHeights, 0, 0, res);
        if (t10 != null) CopyHeights(t10.terrainData, combinedHeights, 128, 0, res);
        if (t01 != null) CopyHeights(t01.terrainData, combinedHeights, 0, 128, res);
        if (t11 != null) CopyHeights(t11.terrainData, combinedHeights, 128, 128, res);

        newData.SetHeights(0, 0, combinedHeights);

        // 지정된 폴더 경로에 에셋 생성
        string fileName = $"{generationCount}";
        string assetPath = $"{folderPath}/{fileName}.asset";
        AssetDatabase.CreateAsset(newData, assetPath);
        AssetDatabase.SaveAssets();

        GameObject newTerrainGO = Terrain.CreateTerrainGameObject(newData);
        newTerrainGO.name = baseName + fileName;
        newTerrainGO.transform.position = startPos;

        if (obj00 != null) Undo.DestroyObjectImmediate(obj00);
        if (obj10 != null) Undo.DestroyObjectImmediate(obj10);
        if (obj01 != null) Undo.DestroyObjectImmediate(obj01);
        if (obj11 != null) Undo.DestroyObjectImmediate(obj11);

        obj00 = obj10 = obj01 = obj11 = null;
        generationCount++;

        Debug.Log($"{assetPath} 생성 및 대체 완료.");
    }

    void CopyHeights(TerrainData source, float[,] target, int xStart, int yStart, int res)
    {
        float[,] h = source.GetHeights(0, 0, res, res);
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
                target[yStart + y, xStart + x] = h[y, x];
    }
}*/