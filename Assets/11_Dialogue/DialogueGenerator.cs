#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public class DialogueGenerator : EditorWindow
{
    private string tableName = "";

    [MenuItem("Tools/Dialogue/Dialogue Set Generator")]
    public static void ShowWindow()
    {
        GetWindow<DialogueGenerator>("Dialogue Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("Dialogue System Setting", EditorStyles.boldLabel);
        tableName = EditorGUILayout.TextField("Table Name", tableName);

        EditorGUILayout.Space();

        if (GUILayout.Button("Generate Dialogue Sets"))
        {
            if (string.IsNullOrEmpty(tableName))
            {
                Debug.LogError("Table Name을 입력해주세요.");
                return;
            }
            Generate();
        }
    }

    public void Generate()
    {
        var collection = LocalizationEditorSettings.GetStringTableCollection(tableName);

        if (collection == null)
        {
            Debug.LogError($"'{tableName}' 테이블을 찾을 수 없습니다.");
            return;
        }

        var groups = new Dictionary<string, List<(int index, SharedTableData.SharedTableEntry entry)>>();

        foreach (var entry in collection.SharedData.Entries)
        {
            string[] splitKey = entry.Key.Split('_');
            if (splitKey.Length < 3) continue;

            string groupName = $"{splitKey[0]}_{splitKey[1]}";

            if (int.TryParse(splitKey[2], out int order))
            {
                if (!groups.ContainsKey(groupName))
                    groups[groupName] = new List<(int, SharedTableData.SharedTableEntry)>();

                groups[groupName].Add((order, entry));
            }
        }

        // 1. 테이블 이름으로 폴더 경로 설정 (Resources/Dialogues/TableName)
        string folderPath = Path.Combine("Assets/Resources/Dialogues", tableName);

        // 2. 폴더가 없으면 생성
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
            AssetDatabase.Refresh();
        }

        foreach (var group in groups)
        {
            // 세 번째 인자로 tableName을 넘겨 TableReference 오류 해결
            CreateOrUpdateDialogueSet(group.Key, group.Value, tableName, folderPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"{tableName} 폴더 내에 DialogueSet 생성 완료");
    }

    private static void CreateOrUpdateDialogueSet(string fileName, List<(int index, SharedTableData.SharedTableEntry entry)> data, string tableReferenceName, string folderPath)
    {
        string path = Path.Combine(folderPath, $"{fileName}.asset");
        DialogueSet asset = AssetDatabase.LoadAssetAtPath<DialogueSet>(path);

        if (asset == null)
        {
            asset = CreateInstance<DialogueSet>();
            AssetDatabase.CreateAsset(asset, path);
        }

        var sortedEntries = data.OrderBy(d => d.index).ToList();
        asset.Script = new UnityEngine.Localization.LocalizedString[sortedEntries.Count];

        for (int i = 0; i < sortedEntries.Count; i++)
        {
            asset.Script[i] = new UnityEngine.Localization.LocalizedString
            {
                TableReference = tableReferenceName,
                TableEntryReference = sortedEntries[i].entry.Key // Key 문자열 기반 참조
            };
        }

        EditorUtility.SetDirty(asset);
    }
}
#endif