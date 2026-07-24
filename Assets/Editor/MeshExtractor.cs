using UnityEngine;
using UnityEditor;

public class MeshExtractor
{
    [MenuItem("GameObject/메시 에셋으로 추출하기", false, 0)]
    public static void ExtractMesh()
    {
        GameObject selectedObj = Selection.activeGameObject;
        if (selectedObj == null) return;

        MeshFilter mf = selectedObj.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogWarning("선택한 오브젝트에 메쉬 필터나 연결된 메쉬가 없습니다.");
            return;
        }

        Mesh sourceMesh = mf.sharedMesh;

        string existingPath = AssetDatabase.GetAssetPath(sourceMesh);
        if (!string.IsNullOrEmpty(existingPath) && !existingPath.EndsWith(".unity"))
        {
            Debug.Log("이 메시는 이미 외부 에셋으로 존재합니다: " + existingPath);
            return;
        }

        string defaultName = string.IsNullOrEmpty(sourceMesh.name) ? "NewMesh" : sourceMesh.name;
        string savePath = EditorUtility.SaveFilePanelInProject("메시 저장", defaultName, "asset", "메시를 저장할 경로를 지정하세요.");

        if (string.IsNullOrEmpty(savePath)) return;

        Mesh newMesh = Object.Instantiate(sourceMesh);
        newMesh.name = defaultName;

        AssetDatabase.CreateAsset(newMesh, savePath);
        AssetDatabase.SaveAssets();

        mf.sharedMesh = newMesh;

        EditorGUIUtility.PingObject(newMesh);
        Debug.Log($"메시가 성공적으로 추출되고 재연결되었습니다: {savePath}");
    }
}