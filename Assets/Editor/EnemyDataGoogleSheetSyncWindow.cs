using UnityEngine;
using UnityEditor;
using System;
using System.Text;
using System.Collections.Generic;
using System.Net.Http;
using System.Linq;

public class EnemyDataGoogleSheetSyncWindow : EditorWindow
{
    private string webAppUrl = "https://script.google.com/macros/s/AKfycbwpAPZnLefRZumgDLaqmApBtrgeqww6nMjvgVb-FIXSg2D8ZZKzune8IlkohV090bLT/exec";
    private bool isWorking = false;

    [Serializable]
    private class GASResponse { public List<SheetData> sheets; }
    [Serializable]
    private class SheetData { public string sheetName; public List<string> rows; }

    [MenuItem("Tools/Enemy Data Google Sheet Sync")]
    public static void ShowWindow()
    {
        var window = GetWindow<EnemyDataGoogleSheetSyncWindow>("Enemy Sync");
        window.minSize = new Vector2(450, 300);
        window.Show();
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("👾 Enemy Data <-> Google Sheet 동기화", EditorStyles.boldLabel);
        GUILayout.Space(10);

        webAppUrl = EditorGUILayout.TextField("Web App URL", webAppUrl);
        GUILayout.Space(10);

        EditorGUILayout.HelpBox(
            "[⬆️ Push (내보내기)]\n모든 EnemyCore 프리팹을 찾아 체력/스킬 정보를 시트에 기록합니다. (시트가 없으면 자동 생성)\n\n" +
            "[⬇️ Pull (가져오기)]\n시트에 적힌 Health 수치와 Skill의 MP Cost, 개별 Damage 목록을 유니티로 가져와 덮어씌웁니다.",
            MessageType.Info);

        GUILayout.Space(20);

        EditorGUI.BeginDisabledGroup(isWorking || string.IsNullOrEmpty(webAppUrl));

        if (GUILayout.Button("⬆️ Push (유니티 -> 구글 시트)", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("Push 확인", "모든 적 데이터를 구글 시트로 전송합니다.\n진행하시겠습니까?", "예", "아니오"))
                PushData();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("⬇️ Pull (구글 시트 -> 유니티)", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("Pull 확인", "구글 시트의 기획 수치를 유니티 프리팹/스킬에 덮어씌웁니다.\n진행하시겠습니까?", "예", "아니오"))
                PullData();
        }

        EditorGUI.EndDisabledGroup();

        if (isWorking)
        {
            GUILayout.Space(15);
            EditorGUILayout.HelpBox("네트워크 통신 중입니다. 잠시만 기다려주세요...", MessageType.Warning);
        }
    }

    #region Push (Export) Logic
    private async void PushData()
    {
        isWorking = true;
        Repaint();
        Debug.Log("구글 시트로 적 데이터를 전송하는 중...");

        try
        {
            Dictionary<string, List<List<string>>> allData = new Dictionary<string, List<List<string>>>();
            string[] guids = AssetDatabase.FindAssets("t:Prefab");

            foreach (string guid in guids)
            {
                try
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) continue;

                    if (prefab.name.Contains("Dummy")) continue;

                    EnemyCore core = prefab.GetComponentInChildren<EnemyCore>(true);
                    if (core == null) continue;

                    List<List<string>> sheetData = new List<List<string>>();

                    // 1. Health 데이터 추출
                    Component healthComp = prefab.GetComponentsInChildren<Component>(true).FirstOrDefault(c => c.GetType().Name.Contains("Health"));
                    float maxHp = 0, maxMp = 0, mpRec = 0;

                    if (healthComp != null)
                    {
                        SerializedObject soHealth = new SerializedObject(healthComp);
                        maxHp = GetFloat(soHealth, "maxHP", "MaxHp");
                        maxMp = GetFloat(soHealth, "maxMP", "MaxMp");
                        mpRec = GetFloat(soHealth, "mpRecoveryPerSecond", "MpRecoveryPerSecond");
                    }

                    sheetData.Add(new List<string> { "Stat", "Value", "" });
                    sheetData.Add(new List<string> { "MaxHP", maxHp.ToString("F1"), "" });
                    sheetData.Add(new List<string> { "MaxMP", maxMp.ToString("F1"), "" });
                    sheetData.Add(new List<string> { "MPRecovery", mpRec.ToString("F1"), "" });
                    sheetData.Add(new List<string> { "", "", "" });

                    // 2. Skill 데이터 추출
                    sheetData.Add(new List<string> { "Skill Name", "Damage", "MP Cost" });

                    Component skillSelector = prefab.GetComponentInChildren<SkillSelector>(true);
                    if (skillSelector != null)
                    {
                        SerializedObject soSelector = new SerializedObject(skillSelector);
                        SerializedProperty profiles = soSelector.FindProperty("SkillProfiles") ?? soSelector.FindProperty("skillProfiles");

                        if (profiles != null && profiles.isArray)
                        {
                            for (int i = 0; i < profiles.arraySize; i++)
                            {
                                SerializedProperty skillProp = profiles.GetArrayElementAtIndex(i).FindPropertyRelative("Skill") ?? profiles.GetArrayElementAtIndex(i).FindPropertyRelative("skill");
                                if (skillProp != null && skillProp.objectReferenceValue != null)
                                {
                                    SkillBase skill = skillProp.objectReferenceValue as SkillBase;
                                    if (skill == null) continue;

                                    string sName = GetSafeSkillName(skill);

                                    // 💡 개별 데미지 나열 문자열 생성 (예: "1.00 / 1.50")
                                    string dmgStr = ExtractAllDamageMultipliers(skill);

                                    sheetData.Add(new List<string> { sName, dmgStr, skill.MP_cost.ToString("F1") });
                                }
                            }
                        }
                    }

                    allData[prefab.name] = sheetData;
                }
                catch (Exception prefabEx)
                {
                    Debug.LogWarning($"프리팹 추출 중 에러 발생 (무시하고 다음 진행): {prefabEx.Message}");
                }
            }

            if (allData.Count == 0)
            {
                Debug.LogWarning("추출된 적 프리팹 데이터가 없습니다.");
                return;
            }

            string jsonData = ConvertToJson(allData);

            using (HttpClient client = new HttpClient())
            {
                StringContent content = new StringContent(jsonData, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(webAppUrl, content);

                string responseStr = await response.Content.ReadAsStringAsync();

                if (responseStr.Contains("Error"))
                {
                    Debug.LogError($"❌ 구글 서버 내부 에러 발생: {responseStr}");
                }
                else if (response.IsSuccessStatusCode)
                {
                    Debug.Log("✅ 구글 시트 적 데이터 Push(내보내기) 완벽하게 성공!");
                }
                else
                {
                    Debug.LogError($"❌ HTTP 통신 에러: {response.StatusCode}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"에러 발생: {e.Message}");
        }
        finally
        {
            isWorking = false;
            Repaint();
        }
    }
    #endregion

    #region Pull (Import) Logic
    private async void PullData()
    {
        isWorking = true;
        Repaint();
        Debug.Log("구글 시트에서 적 데이터를 불러오는 중...");

        using (HttpClient client = new HttpClient())
        {
            try
            {
                HttpResponseMessage response = await client.GetAsync(webAppUrl);
                string responseStr = await response.Content.ReadAsStringAsync();

                if (responseStr.Contains("Error"))
                {
                    Debug.LogError($"❌ 구글 서버 데이터 가져오기 실패: {responseStr}");
                    return;
                }

                if (!response.IsSuccessStatusCode)
                {
                    Debug.LogError($"데이터 통신 실패: {response.StatusCode}");
                    return;
                }

                GASResponse data = JsonUtility.FromJson<GASResponse>(responseStr);

                if (data != null && data.sheets != null)
                {
                    ApplyDataToEnemies(data);
                    Debug.Log("✅ 구글 시트 적 데이터(Pull) 유니티 적용 완료!");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"에러 발생: {e.Message}");
            }
            finally
            {
                isWorking = false;
                Repaint();
            }
        }
    }

    private void ApplyDataToEnemies(GASResponse data)
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        Dictionary<string, GameObject> prefabDict = new Dictionary<string, GameObject>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab != null && prefab.GetComponentInChildren<EnemyCore>(true) != null)
            {
                if (prefab.name.Contains("Dummy")) continue;
                prefabDict[prefab.name] = prefab;
            }
        }

        foreach (var sheet in data.sheets)
        {
            if (!prefabDict.TryGetValue(sheet.sheetName, out GameObject prefab)) continue;

            Component healthComp = prefab.GetComponentsInChildren<Component>(true).FirstOrDefault(c => c.GetType().Name.Contains("Health"));
            SerializedObject soHealth = healthComp != null ? new SerializedObject(healthComp) : null;

            Component skillSelector = prefab.GetComponentInChildren<SkillSelector>(true);
            SerializedObject soSelector = skillSelector != null ? new SerializedObject(skillSelector) : null;
            SerializedProperty profiles = soSelector?.FindProperty("SkillProfiles") ?? soSelector?.FindProperty("skillProfiles");

            bool isParsingSkills = false;

            foreach (string rowStr in sheet.rows)
            {
                string[] cols = rowStr.Split("|||");
                if (cols.Length < 2) continue;

                string key = cols[0].Trim();
                string valStr = cols[1].Trim();

                if (key == "Skill Name")
                {
                    isParsingSkills = true;
                    continue;
                }

                if (!isParsingSkills && soHealth != null)
                {
                    if (float.TryParse(valStr, out float val))
                    {
                        if (key == "MaxHP") SetFloat(soHealth, val, "maxHP", "MaxHp");
                        else if (key == "MaxMP") SetFloat(soHealth, val, "maxMP", "MaxMp");
                        else if (key == "MPRecovery") SetFloat(soHealth, val, "mpRecoveryPerSecond", "MpRecoveryPerSecond");
                    }
                }
                else if (isParsingSkills && profiles != null)
                {
                    string targetSkillName = key;
                    if (cols.Length < 3) continue;

                    string dmgRawStr = cols[1].Trim();
                    if (float.TryParse(cols[2], out float mpCost))
                    {
                        for (int i = 0; i < profiles.arraySize; i++)
                        {
                            SerializedProperty skillProp = profiles.GetArrayElementAtIndex(i).FindPropertyRelative("Skill") ?? profiles.GetArrayElementAtIndex(i).FindPropertyRelative("skill");
                            if (skillProp != null && skillProp.objectReferenceValue != null)
                            {
                                SkillBase skill = skillProp.objectReferenceValue as SkillBase;
                                if (skill == null) continue;

                                string sName = GetSafeSkillName(skill);

                                if (sName == targetSkillName)
                                {
                                    skill.MP_cost = mpCost;
                                    ApplyAllDamageMultipliers(skill, dmgRawStr);
                                    EditorUtility.SetDirty(skill);
                                }
                            }
                        }
                    }
                }
            }

            if (soHealth != null) soHealth.ApplyModifiedProperties();
            EditorUtility.SetDirty(prefab);
        }

        AssetDatabase.SaveAssets();
    }
    #endregion

    #region Helper Methods
    private string GetSafeSkillName(SkillBase skill)
    {
        if (skill == null) return "Unknown";
        try
        {
            if (skill.Localize_skillName != null && !skill.Localize_skillName.IsEmpty)
            {
                string locName = skill.skillName;
                if (!string.IsNullOrEmpty(locName)) return locName;
            }
        }
        catch { }
        return skill.name;
    }

    private float GetFloat(SerializedObject so, params string[] propNames)
    {
        foreach (string name in propNames)
        {
            var prop = so.FindProperty(name);
            if (prop != null) return prop.floatValue;
        }
        return 0f;
    }

    private void SetFloat(SerializedObject so, float value, params string[] propNames)
    {
        foreach (string name in propNames)
        {
            var prop = so.FindProperty(name);
            if (prop != null)
            {
                prop.floatValue = value;
                return;
            }
        }
    }

    // 💡 모든 히트(개별 데미지)를 슬래시로 묶어서 추출하되, 사운드가 없고 데미지가 0인 것은 제외
    private string ExtractAllDamageMultipliers(SkillBase skill)
    {
        if (skill == null) return "0.00";
        List<string> dmgList = new List<string>();

        if (skill is Skill_Melee melee && melee.hitDataList != null)
        {
            foreach (var hit in melee.hitDataList)
            {
                // 범위 체크용 더미 데이터 무시
                if (hit.hitSound == null && hit.damageMultiplier == 0f) continue;
                dmgList.Add(hit.damageMultiplier.ToString("F2"));
            }
        }
        else
        {
            // Range 등 다른 스킬들도 안전하게 지원
            SerializedObject so = new SerializedObject(skill);
            SerializedProperty hitList = so.FindProperty("hitDataList");
            if (hitList != null && hitList.isArray)
            {
                for (int i = 0; i < hitList.arraySize; i++)
                {
                    SerializedProperty hitProp = hitList.GetArrayElementAtIndex(i);
                    SerializedProperty dmgProp = hitProp.FindPropertyRelative("damageMultiplier");
                    SerializedProperty sndProp = hitProp.FindPropertyRelative("hitSound");

                    if (dmgProp != null)
                    {
                        bool hasSound = (sndProp != null && sndProp.objectReferenceValue != null);
                        // 사운드도 없고 데미지도 0이면 범위체크용이므로 제외
                        if (!hasSound && dmgProp.floatValue == 0f) continue;

                        dmgList.Add(dmgProp.floatValue.ToString("F2"));
                    }
                }
            }
            else if (skill.damageMultipliers != null)
            {
                for (int i = 0; i < skill.damageMultipliers.Length; i++)
                {
                    AudioClip snd = (skill.HitSound != null && skill.HitSound.Length > i) ? skill.HitSound[i] : null;
                    if (snd == null && skill.damageMultipliers[i] == 0f) continue;

                    dmgList.Add(skill.damageMultipliers[i].ToString("F2"));
                }
            }
        }

        return dmgList.Count > 0 ? string.Join(" / ", dmgList) : "0.00";
    }

    // 💡 슬래시로 쪼개진 데미지 문자열을 다시 개별 히트들에 적용 (더미 데이터 건너뜀)
    private void ApplyAllDamageMultipliers(SkillBase skill, string rawStr)
    {
        if (skill == null) return;
        string[] parts = rawStr.Split('/');
        List<float> parsedValues = new List<float>();

        foreach (string p in parts)
        {
            if (float.TryParse(p.Trim(), out float val))
                parsedValues.Add(val);
        }

        if (parsedValues.Count == 0) return;

        int valueIndex = 0;

        if (skill is Skill_Melee melee && melee.hitDataList != null)
        {
            for (int i = 0; i < melee.hitDataList.Count; i++)
            {
                // 더미 데이터에는 값을 할당하지 않고 건너뜀
                if (melee.hitDataList[i].hitSound == null && melee.hitDataList[i].damageMultiplier == 0f) continue;

                if (valueIndex < parsedValues.Count)
                {
                    melee.hitDataList[i].damageMultiplier = parsedValues[valueIndex];
                    valueIndex++;
                }
            }
        }
        else
        {
            SerializedObject so = new SerializedObject(skill);
            SerializedProperty hitList = so.FindProperty("hitDataList");
            if (hitList != null && hitList.isArray)
            {
                for (int i = 0; i < hitList.arraySize; i++)
                {
                    SerializedProperty hitProp = hitList.GetArrayElementAtIndex(i);
                    SerializedProperty dmgProp = hitProp.FindPropertyRelative("damageMultiplier");
                    SerializedProperty sndProp = hitProp.FindPropertyRelative("hitSound");

                    if (dmgProp != null)
                    {
                        bool hasSound = (sndProp != null && sndProp.objectReferenceValue != null);
                        if (!hasSound && dmgProp.floatValue == 0f) continue;

                        if (valueIndex < parsedValues.Count)
                        {
                            dmgProp.floatValue = parsedValues[valueIndex];
                            valueIndex++;
                        }
                    }
                }
                so.ApplyModifiedProperties();
            }
            else if (skill.damageMultipliers != null)
            {
                for (int i = 0; i < skill.damageMultipliers.Length; i++)
                {
                    AudioClip snd = (skill.HitSound != null && skill.HitSound.Length > i) ? skill.HitSound[i] : null;
                    if (snd == null && skill.damageMultipliers[i] == 0f) continue;

                    if (valueIndex < parsedValues.Count)
                    {
                        skill.damageMultipliers[i] = parsedValues[valueIndex];
                        valueIndex++;
                    }
                }
            }
        }
    }

    private string ConvertToJson(Dictionary<string, List<List<string>>> dataDict)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("{");
        int dictCount = 0;
        foreach (var kvp in dataDict)
        {
            sb.Append($"\"{kvp.Key}\": [");
            for (int i = 0; i < kvp.Value.Count; i++)
            {
                sb.Append("[");
                for (int j = 0; j < kvp.Value[i].Count; j++)
                {
                    string val = kvp.Value[i][j] != null ? kvp.Value[i][j]
                        .Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "").Replace("\t", "\\t") : "";
                    sb.Append($"\"{val}\"");
                    if (j < kvp.Value[i].Count - 1) sb.Append(",");
                }
                sb.Append("]");
                if (i < kvp.Value.Count - 1) sb.Append(",");
            }
            sb.Append("]");
            if (dictCount < dataDict.Count - 1) sb.Append(",");
            dictCount++;
        }
        sb.Append("}");
        return sb.ToString();
    }
    #endregion
}