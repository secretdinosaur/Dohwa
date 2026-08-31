using UnityEngine;
using UnityEditor;
using System;
using System.Text;
using System.Collections.Generic;
using System.Net.Http;
using System.Linq;
using UnityEditor.Localization;

public class DataSheetWindow : EditorWindow
{
    private string webAppUrl = "https://script.google.com/macros/s/AKfycbylQmkk4__fy-oOHmULz3lUy-s2iZVK-bJh_u78IfmDzV6zxrESfdQ6Xk6wz9Whr81ZJw/exec";
    private bool isWorking = false;

    [Serializable]
    private class GASResponse { public List<SheetData> sheets; }
    [Serializable]
    private class SheetData { public string sheetName; public List<string> rows; }

    private class ParsedDamage
    {
        public float baseDamage;
        public bool hasArea;
        public int areaTicks;
        public float areaDamage;
    }

    [MenuItem("Tools/Data Sheet Sync (Skill & Enemy)")]
    public static void ShowWindow()
    {
        var window = GetWindow<DataSheetWindow>("Data Sync");
        window.minSize = new Vector2(450, 450);
        window.Show();
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("데이터 <-> Google Sheet 통합 동기화", EditorStyles.boldLabel);
        GUILayout.Space(10);

        webAppUrl = EditorGUILayout.TextField("통합 Web App URL", webAppUrl);
        GUILayout.Space(10);

        EditorGUI.BeginDisabledGroup(isWorking || string.IsNullOrEmpty(webAppUrl));

        EditorGUILayout.HelpBox(
            "[스킬 데이터 동기화]\nActive, Passive, Stat 데이터를 통합 시트와 동기화합니다.",
            MessageType.Info);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Push (유니티 -> 스킬 시트)", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("스킬 Push 확인", "유니티의 스킬 데이터를 구글 시트로 전송합니다.\n진행하시겠습니까?", "예", "아니오"))
                PushSkillData();
        }
        if (GUILayout.Button("Pull (스킬 시트 -> 유니티)", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("스킬 Pull 확인", "구글 시트의 스킬 데이터를 유니티로 가져옵니다.\n진행하시겠습니까?", "예", "아니오"))
                PullSkillData();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(20);

        EditorGUILayout.HelpBox(
            "[적 데이터 동기화]\n각 EnemyCore 프리팹의 체력/데미지 데이터를 통합 시트와 동기화합니다.",
            MessageType.Info);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Push (유니티 -> 적 시트)", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("적 데이터 Push 확인", "모든 적 데이터를 구글 시트로 전송합니다.\n진행하시겠습니까?", "예", "아니오"))
                PushEnemyData();
        }
        if (GUILayout.Button("Pull (적 시트 -> 유니티)", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("적 데이터 Pull 확인", "구글 시트의 적 데이터를 유니티로 가져옵니다.\n진행하시겠습니까?", "예", "아니오"))
                PullEnemyData();
        }
        GUILayout.EndHorizontal();

        EditorGUI.EndDisabledGroup();

        if (isWorking)
        {
            GUILayout.Space(15);
            EditorGUILayout.HelpBox("네트워크 통신 중입니다. 잠시만 기다려주세요...", MessageType.Warning);
        }
    }

    #region ========== [ 공통 데이터 전처리 헬퍼 ] ==========
    private string Clean(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\uFEFF", "").Trim();
    }

    private float ParseFloatSafe(string val)
    {
        if (string.IsNullOrEmpty(val)) return 0f;
        val = val.Replace(",", ".");
        float.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float result);
        return result;
    }
    #endregion

    #region ========== [ SkillBook 동기화 로직 ] ==========
    private async void PushSkillData()
    {
        isWorking = true;
        Repaint();
        Debug.Log("[Skill] 구글 시트로 스킬 데이터를 전송하는 중...");

        try
        {
            List<List<string>> activeData = new List<List<string>>();
            List<List<string>> passiveData = new List<List<string>>();
            List<List<string>> statData = new List<List<string>>();

            activeData.Add(new List<string> { "Skill Book", "Skill Name", "Cost", "Damage", "MP Cost", "Damage Sum", "Damage/Cost", "Damage/MP" });
            passiveData.Add(new List<string> { "Skill Book", "Passive Name", "Cost", "Linked Skill" });

            string[] bookGuids = AssetDatabase.FindAssets("t:SkillBookBase");
            HashSet<SkillBase> processedSkills = new HashSet<SkillBase>();
            Dictionary<SkillBase, int> globalSkillCosts = new Dictionary<SkillBase, int>();
            HashSet<string> uniqueStatTypes = new HashSet<string>();

            for (int i = 0; i < bookGuids.Length; i++)
            {
                try
                {
                    if (i % 5 == 0) EditorUtility.DisplayProgressBar("Skill Data Push", $"스킬북 기초 데이터 분석 중... {i}/{bookGuids.Length}", (float)i / bookGuids.Length);

                    string path = AssetDatabase.GUIDToAssetPath(bookGuids[i]);
                    SkillBookBase skillBook = AssetDatabase.LoadAssetAtPath<SkillBookBase>(path);
                    if (skillBook == null || skillBook.skillTree == null) continue;

                    foreach (var node in skillBook.skillTree)
                    {
                        if (node.nodeType == SkillBookBase.NodeType.ActiveSkill && node.activeSkill != null)
                        {
                            globalSkillCosts.TryAdd(node.activeSkill, node.cost);
                        }
                        else if (node.nodeType == SkillBookBase.NodeType.StatSkill && node.statModifiers != null)
                        {
                            foreach (var stat in node.statModifiers) uniqueStatTypes.Add(stat.statType.ToString());
                        }
                    }
                }
                catch { }
            }

            List<string> statNames = uniqueStatTypes.ToList();
            List<string> preferredOrder = new List<string> { "ExternalArt", "InnerArt", "HpRecovery", "MpRecovery", "VitalStrikeDamage", "VitalStrikeRate" };

            statNames.Sort((a, b) =>
            {
                int idxA = preferredOrder.IndexOf(a);
                int idxB = preferredOrder.IndexOf(b);
                if (idxA != -1 && idxB != -1) return idxA.CompareTo(idxB);
                if (idxA != -1) return -1;
                if (idxB != -1) return 1;
                return a.CompareTo(b);
            });

            List<string> statHeader = new List<string> { "Skill Book", "Stat Name", "Cost" };
            foreach (string sName in statNames) statHeader.Add(sName);
            foreach (string sName in statNames) statHeader.Add($"{sName}/Cost");
            statData.Add(statHeader);

            for (int i = 0; i < bookGuids.Length; i++)
            {
                try
                {
                    if (i % 5 == 0) EditorUtility.DisplayProgressBar("Skill Data Push", $"스킬북 세부 데이터 추출 중... {i}/{bookGuids.Length}", (float)i / bookGuids.Length);

                    string path = AssetDatabase.GUIDToAssetPath(bookGuids[i]);
                    SkillBookBase skillBook = AssetDatabase.LoadAssetAtPath<SkillBookBase>(path);
                    if (skillBook == null || skillBook.skillTree == null) continue;

                    string bookDisplayName = GetSafeSkillBookName(skillBook);

                    for (int j = 0; j < skillBook.skillTree.Length; j++)
                    {
                        var node = skillBook.skillTree[j];
                        int cost = node.cost;

                        switch (node.nodeType)
                        {
                            case SkillBookBase.NodeType.ActiveSkill:
                                if (node.activeSkill != null)
                                {
                                    if (node.activeSkill.isEnemySkill) break;

                                    processedSkills.Add(node.activeSkill);
                                    GetDamageData(node.activeSkill, out string dmgInfo, out float dmgSum);

                                    string mpStr = node.activeSkill.MP_cost > 0 ? node.activeSkill.MP_cost.ToString("F1") : "";
                                    string dmgSumStr = dmgSum > 0 ? dmgSum.ToString("F2") : "";
                                    string dpcStr = (cost > 0 && dmgSum > 0) ? (dmgSum / cost).ToString("F2") : "";
                                    string dpmStr = (node.activeSkill.MP_cost > 0 && dmgSum > 0) ? (dmgSum / node.activeSkill.MP_cost).ToString("F2") : "";

                                    string skillDisplayName = GetSafeSkillName(node.activeSkill);
                                    activeData.Add(new List<string> { bookDisplayName, skillDisplayName, cost.ToString(), dmgInfo, mpStr, dmgSumStr, dpcStr, dpmStr });
                                }
                                break;

                            case SkillBookBase.NodeType.PassiveSkill:
                                if (node.passiveSkill != null)
                                {
                                    SkillBase linkedSkill = null;
                                    int upgradeCost = cost;

                                    if (node.passiveSkill.skillLogic is UpgradeSkillPassive upgradeLogic)
                                    {
                                        linkedSkill = upgradeLogic.explicitUpgradedSkill;
                                    }

                                    if (linkedSkill == null)
                                    {
                                        string targetName = node.passiveSkill.name.Replace("PassiveSkillUpgrade_", "") + "_Upgraded";
                                        string[] guidsFound = AssetDatabase.FindAssets($"t:SkillBase {targetName}");
                                        if (guidsFound.Length > 0)
                                            linkedSkill = AssetDatabase.LoadAssetAtPath<SkillBase>(AssetDatabase.GUIDToAssetPath(guidsFound[0]));
                                    }

                                    string linkedName = linkedSkill != null ? GetSafeSkillName(linkedSkill) : "";
                                    string passiveDisplayName = GetSafePassiveName(node.passiveSkill);

                                    passiveData.Add(new List<string> { bookDisplayName, passiveDisplayName, upgradeCost.ToString(), linkedName });

                                    if (linkedSkill != null)
                                    {
                                        processedSkills.Add(linkedSkill);
                                        GetDamageData(linkedSkill, out string lDmgInfo, out float lDmgSum);

                                        string lMpStr = linkedSkill.MP_cost > 0 ? linkedSkill.MP_cost.ToString("F1") : "";
                                        string lDmgSumStr = lDmgSum > 0 ? lDmgSum.ToString("F2") : "";
                                        string lDpcStr = (upgradeCost > 0 && lDmgSum > 0) ? (lDmgSum / upgradeCost).ToString("F2") : "";
                                        string lDpmStr = (linkedSkill.MP_cost > 0 && lDmgSum > 0) ? (lDmgSum / linkedSkill.MP_cost).ToString("F2") : "";

                                        activeData.Add(new List<string> { bookDisplayName, linkedName, upgradeCost.ToString(), lDmgInfo, lMpStr, lDmgSumStr, lDpcStr, lDpmStr });
                                    }
                                }
                                break;

                            case SkillBookBase.NodeType.StatSkill:
                                string statNodeName = node.GetNodeName()?.Trim() ?? "Unknown";
                                List<string> rowBase = new List<string> { bookDisplayName, statNodeName, cost > 0 ? cost.ToString() : "" };
                                List<string> statValues = new List<string>();
                                List<string> ratioValues = new List<string>();

                                Dictionary<string, float> currentStats = new Dictionary<string, float>();
                                if (node.statModifiers != null)
                                {
                                    foreach (var stat in node.statModifiers)
                                    {
                                        string sName = stat.statType.ToString();
                                        if (!currentStats.ContainsKey(sName)) currentStats[sName] = 0f;
                                        currentStats[sName] += stat.statValue;
                                    }
                                }

                                foreach (string sName in statNames)
                                {
                                    if (currentStats.TryGetValue(sName, out float val) && val != 0f)
                                    {
                                        statValues.Add(val.ToString(System.Globalization.CultureInfo.InvariantCulture));
                                        ratioValues.Add(cost > 0 ? (val / cost).ToString("F2", System.Globalization.CultureInfo.InvariantCulture) : "");
                                    }
                                    else
                                    {
                                        statValues.Add("");
                                        ratioValues.Add("");
                                    }
                                }

                                List<string> finalRow = new List<string>();
                                finalRow.AddRange(rowBase);
                                finalRow.AddRange(statValues);
                                finalRow.AddRange(ratioValues);
                                statData.Add(finalRow);
                                break;
                        }
                    }
                }
                catch { }
            }

            string[] allSkillGuids = AssetDatabase.FindAssets("t:SkillBase");
            for (int i = 0; i < allSkillGuids.Length; i++)
            {
                try
                {
                    if (i % 10 == 0) EditorUtility.DisplayProgressBar("Skill Data Push", $"연결되지 않은 스킬 확인 중... {i}/{allSkillGuids.Length}", (float)i / allSkillGuids.Length);

                    string path = AssetDatabase.GUIDToAssetPath(allSkillGuids[i]);
                    SkillBase skill = AssetDatabase.LoadAssetAtPath<SkillBase>(path);

                    if (skill == null || skill.isEnemySkill || processedSkills.Contains(skill)) continue;

                    GetDamageData(skill, out string dmgInfo, out float dmgSum);

                    string mpStr = skill.MP_cost > 0 ? skill.MP_cost.ToString("F1") : "";
                    string dmgSumStr = dmgSum > 0 ? dmgSum.ToString("F2") : "";
                    string dpmStr = (skill.MP_cost > 0 && dmgSum > 0) ? (dmgSum / skill.MP_cost).ToString("F2") : "";

                    string unlinkedDisplayName = GetSafeSkillName(skill);
                    activeData.Add(new List<string> { "Unlinked Skills", unlinkedDisplayName, "", dmgInfo, mpStr, dmgSumStr, "", dpmStr });
                }
                catch { }
            }

            EditorUtility.ClearProgressBar();

            Dictionary<string, List<List<string>>> allData = new Dictionary<string, List<List<string>>>
            {
                { "Active", activeData },
                { "Passive", passiveData },
                { "Stat", statData }
            };

            string jsonData = ConvertToJson(allData);

            using (HttpClient client = new HttpClient())
            {
                StringContent content = new StringContent(jsonData, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(webAppUrl, content);

                if (response.IsSuccessStatusCode) Debug.Log("<color=cyan>[성공]</color> 구글 시트 Skill 데이터 Push 완료!");
                else Debug.LogError($"[Skill] Push 실패: {response.StatusCode}");
            }
        }
        catch (Exception e) { Debug.LogError($"[Skill] 통신 및 전체 에러 발생: {e.Message}"); }
        finally
        {
            EditorUtility.ClearProgressBar();
            isWorking = false;
            Repaint();
        }
    }

    private async void PullSkillData()
    {
        isWorking = true;
        Repaint();
        Debug.Log("\n<color=yellow>========== [Skill Pull 시작] ==========</color>");

        using (HttpClient client = new HttpClient())
        {
            try
            {
                HttpResponseMessage response = await client.GetAsync(webAppUrl);
                string jsonResponse = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Debug.LogError($"[Skill] Pull 실패. 상태 코드: {response.StatusCode}");
                    return;
                }

                if (jsonResponse.Contains("Error") || !jsonResponse.Trim().StartsWith("{"))
                {
                    Debug.LogError($"[Skill Pull 에러] 구글 시트에서 유효한 JSON을 반환하지 않았습니다. URL을 확인하세요.\n반환값: {jsonResponse}");
                    return;
                }

                GASResponse data = JsonUtility.FromJson<GASResponse>(jsonResponse);

                if (data == null || data.sheets == null || data.sheets.Count == 0)
                {
                    Debug.LogError("[Skill] 시트 데이터가 비어 있거나 JSON 파싱에 실패했습니다.");
                    return;
                }

                ApplySkillDataToAssets(data);
            }
            catch (Exception e) { Debug.LogError($"[Skill] Pull 중 예외: {e.Message}\n{e.StackTrace}"); }
            finally
            {
                EditorUtility.ClearProgressBar();
                isWorking = false;
                Repaint();
                Debug.Log("<color=green>========== [Skill Pull 종료] ==========</color>\n");
            }
        }
    }

    private void ApplySkillDataToAssets(GASResponse data)
    {
        int updateCount = 0;
        int identicalCount = 0;
        int notFoundCount = 0;

        string[] bookGuids = AssetDatabase.FindAssets("t:SkillBookBase");
        Dictionary<string, SkillBookBase> bookDict = new Dictionary<string, SkillBookBase>();

        for (int i = 0; i < bookGuids.Length; i++)
        {
            try
            {
                if (i % 5 == 0) EditorUtility.DisplayProgressBar("Skill Data Pull", $"스킬북 로드 중... {i}/{bookGuids.Length}", (float)i / bookGuids.Length);
                string path = AssetDatabase.GUIDToAssetPath(bookGuids[i]);
                SkillBookBase skillBook = AssetDatabase.LoadAssetAtPath<SkillBookBase>(path);
                if (skillBook != null) bookDict[Clean(GetSafeSkillBookName(skillBook))] = skillBook;
            }
            catch { continue; }
        }

        string[] skillGuids = AssetDatabase.FindAssets("t:SkillBase");
        Dictionary<string, SkillBase> skillDict = new Dictionary<string, SkillBase>();

        for (int i = 0; i < skillGuids.Length; i++)
        {
            try
            {
                if (i % 10 == 0) EditorUtility.DisplayProgressBar("Skill Data Pull", $"스킬 로드 중... {i}/{skillGuids.Length}", (float)i / skillGuids.Length);
                string path = AssetDatabase.GUIDToAssetPath(skillGuids[i]);
                SkillBase skill = AssetDatabase.LoadAssetAtPath<SkillBase>(path);
                if (skill == null) continue;

                string skillName = Clean(GetSafeSkillName(skill));
                if (!skillDict.ContainsKey(skillName)) skillDict[skillName] = skill;
            }
            catch { continue; }
        }
        EditorUtility.ClearProgressBar();

        var activeSheet = data.sheets.FirstOrDefault(s => Clean(s.sheetName).Equals("Active", StringComparison.OrdinalIgnoreCase));
        if (activeSheet != null && activeSheet.rows != null && activeSheet.rows.Count > 1)
        {
            string[] headers = activeSheet.rows[0].Split(new string[] { "|||" }, StringSplitOptions.None).Select(Clean).ToArray();
            int bookIdx = Array.IndexOf(headers, "Skill Book");
            int skillIdx = Array.IndexOf(headers, "Skill Name");
            int costIdx = Array.IndexOf(headers, "Cost");
            int dmgIdx = Array.IndexOf(headers, "Damage");
            int mpCostIdx = Array.IndexOf(headers, "MP Cost");

            if (bookIdx != -1 && skillIdx != -1)
            {
                for (int i = 1; i < activeSheet.rows.Count; i++)
                {
                    try
                    {
                        string[] row = activeSheet.rows[i].Split(new string[] { "|||" }, StringSplitOptions.None);
                        if (row.Length <= skillIdx) continue;

                        string bookName = Clean(row[bookIdx]);
                        string skillName = Clean(row[skillIdx]);
                        if (string.IsNullOrEmpty(skillName) || skillName == "Skill Name") continue;

                        bool isDirtyRow = false;

                        // 스킬북 코스트 적용
                        if (costIdx != -1 && row.Length > costIdx)
                        {
                            if (int.TryParse(Clean(row[costIdx]), out int cost))
                            {
                                if (bookDict.TryGetValue(bookName, out SkillBookBase book))
                                {
                                    foreach (var node in book.skillTree)
                                    {
                                        if (node.nodeType == SkillBookBase.NodeType.ActiveSkill && node.activeSkill != null && Clean(GetSafeSkillName(node.activeSkill)) == skillName)
                                        {
                                            if (node.cost != cost)
                                            {
                                                Debug.Log($"<color=cyan>[Active 코스트]</color> [{bookName}] -> [{skillName}] {node.cost} => {cost}");
                                                node.cost = cost;
                                                EditorUtility.SetDirty(book);
                                                isDirtyRow = true;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        // 스킬 데미지 및 MP 적용
                        if (skillDict.TryGetValue(skillName, out SkillBase skillData))
                        {
                            bool isSkillChanged = false;

                            if (mpCostIdx != -1 && row.Length > mpCostIdx)
                            {
                                float mp = ParseFloatSafe(row[mpCostIdx]);
                                if (Mathf.Abs(skillData.MP_cost - mp) > 0.001f)
                                {
                                    Debug.Log($"<color=cyan>[Active MP]</color> [{skillName}] MP Cost: {skillData.MP_cost} => {mp}");
                                    skillData.MP_cost = mp;
                                    isSkillChanged = true;
                                }
                            }

                            if (dmgIdx != -1 && row.Length > dmgIdx && !string.IsNullOrEmpty(Clean(row[dmgIdx])))
                            {
                                if (ApplyAllDamageMultipliers(skillData, Clean(row[dmgIdx])))
                                    isSkillChanged = true;
                            }

                            if (isSkillChanged)
                            {
                                EditorUtility.SetDirty(skillData);
                                isDirtyRow = true;
                            }
                        }
                        else
                        {
                            notFoundCount++;
                            Debug.LogWarning($"[Active] '{skillName}' 스킬을 유니티 에셋에서 찾을 수 없습니다.");
                            continue;
                        }

                        if (isDirtyRow) updateCount++;
                        else identicalCount++;
                    }
                    catch { continue; }
                }
            }
        }

        var statSheet = data.sheets.FirstOrDefault(s => Clean(s.sheetName).Equals("Stat", StringComparison.OrdinalIgnoreCase) || Clean(s.sheetName).Equals("Stats", StringComparison.OrdinalIgnoreCase));
        if (statSheet != null && statSheet.rows != null && statSheet.rows.Count > 1)
        {
            string[] sHeaders = statSheet.rows[0].Split(new string[] { "|||" }, StringSplitOptions.None).Select(Clean).ToArray();
            int sBookIdx = Array.IndexOf(sHeaders, "Skill Book");
            int sNameIdx = Array.IndexOf(sHeaders, "Stat Name");
            int sCostIdx = Array.IndexOf(sHeaders, "Cost");

            if (sBookIdx != -1 && sNameIdx != -1)
            {
                for (int i = 1; i < statSheet.rows.Count; i++)
                {
                    try
                    {
                        string[] row = statSheet.rows[i].Split(new string[] { "|||" }, StringSplitOptions.None);
                        if (row.Length <= sNameIdx) continue;

                        string bookName = Clean(row[sBookIdx]);
                        string statNodeName = Clean(row[sNameIdx]);
                        if (string.IsNullOrEmpty(statNodeName) || statNodeName == "Stat Name") continue;

                        bool isDirtyRow = false;

                        if (bookDict.TryGetValue(bookName, out SkillBookBase book))
                        {
                            for (int n = 0; n < book.skillTree.Length; n++)
                            {
                                var node = book.skillTree[n];
                                if (node.nodeType == SkillBookBase.NodeType.StatSkill && Clean(node.GetNodeName()) == statNodeName)
                                {
                                    if (sCostIdx != -1 && row.Length > sCostIdx)
                                    {
                                        if (int.TryParse(Clean(row[sCostIdx]), out int cost) && node.cost != cost)
                                        {
                                            Debug.Log($"<color=cyan>[Stat 코스트]</color> [{bookName}] -> [{statNodeName}] {node.cost} => {cost}");
                                            node.cost = cost;
                                            isDirtyRow = true;
                                        }
                                    }

                                    List<SkillBookBase.StatModifier> currentModifiers = node.statModifiers != null ? node.statModifiers.ToList() : new List<SkillBookBase.StatModifier>();
                                    bool statListChanged = false;

                                    for (int col = 0; col < sHeaders.Length; col++)
                                    {
                                        string headerName = sHeaders[col];
                                        if (headerName == "Skill Book" || headerName == "Stat Name" || headerName == "Cost" || headerName.EndsWith("/Cost")) continue;

                                        if (row.Length > col)
                                        {
                                            float val = ParseFloatSafe(row[col]);

                                            if (val != 0f)
                                            {
                                                if (Enum.TryParse(headerName, out StatManager.StatType parsedStatType))
                                                {
                                                    int existingIndex = currentModifiers.FindIndex(m => m.statType == parsedStatType);
                                                    if (existingIndex >= 0)
                                                    {
                                                        if (Mathf.Abs(currentModifiers[existingIndex].statValue - val) > 0.001f)
                                                        {
                                                            Debug.Log($"<color=cyan>[Stat 갱신]</color> [{statNodeName}] {headerName}: {currentModifiers[existingIndex].statValue} => {val}");
                                                            var temp = currentModifiers[existingIndex];
                                                            temp.statValue = val;
                                                            currentModifiers[existingIndex] = temp;
                                                            isDirtyRow = true;
                                                            statListChanged = true;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        Debug.Log($"<color=cyan>[Stat 추가]</color> [{statNodeName}] {headerName}: 0 => {val}");
                                                        currentModifiers.Add(new SkillBookBase.StatModifier { statType = parsedStatType, statValue = val });
                                                        isDirtyRow = true;
                                                        statListChanged = true;
                                                    }
                                                }
                                            }
                                            else if (string.IsNullOrEmpty(Clean(row[col])) || val == 0f)
                                            {
                                                if (Enum.TryParse(headerName, out StatManager.StatType parsedStatType))
                                                {
                                                    int existingIndex = currentModifiers.FindIndex(m => m.statType == parsedStatType);
                                                    if (existingIndex >= 0)
                                                    {
                                                        Debug.Log($"<color=cyan>[Stat 삭제]</color> [{statNodeName}] {headerName}");
                                                        currentModifiers.RemoveAt(existingIndex);
                                                        isDirtyRow = true;
                                                        statListChanged = true;
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    if (statListChanged) node.statModifiers = currentModifiers.ToArray();
                                    if (isDirtyRow) EditorUtility.SetDirty(book);
                                }
                            }

                            if (isDirtyRow) updateCount++;
                            else identicalCount++;
                        }
                    }
                    catch { continue; }
                }
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"<color=magenta>[스킬 결과 요약]</color> 갱신성공: {updateCount}개, 값동일무시: {identicalCount}개, 찾을수없음: {notFoundCount}개");
    }
    #endregion

    #region ========== [ Enemy 동기화 로직 ] ==========
    private async void PushEnemyData()
    {
        isWorking = true;
        Repaint();
        Debug.Log("[Enemy] 구글 시트로 적 데이터를 전송하는 중...");

        try
        {
            Dictionary<string, List<List<string>>> allData = new Dictionary<string, List<List<string>>>();
            string[] guids = AssetDatabase.FindAssets("t:Prefab");

            for (int i = 0; i < guids.Length; i++)
            {
                try
                {
                    string guid = guids[i];
                    string path = AssetDatabase.GUIDToAssetPath(guid);

                    if (i % 10 == 0)
                    {
                        EditorUtility.DisplayProgressBar("Enemy Data Push", $"프리팹 분석 중... {i}/{guids.Length}", (float)i / guids.Length);
                    }

                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null || prefab.name.Contains("Dummy")) continue;

                    EnemyCore core = prefab.GetComponentInChildren<EnemyCore>(true);
                    if (core == null) continue;

                    List<List<string>> sheetData = new List<List<string>>();

                    Component healthComp = prefab.GetComponentsInChildren<Component>(true).FirstOrDefault(c => c != null && c.GetType().Name.Contains("Health"));
                    float maxHp = 0, maxMp = 0, mpRec = 0;

                    if (healthComp != null)
                    {
                        SerializedObject soHealth = new SerializedObject(healthComp);
                        maxHp = GetFloat(soHealth, "maxHP", "MaxHp");
                        maxMp = GetFloat(soHealth, "maxMP", "MaxMp");
                        mpRec = GetFloat(soHealth, "MpRecoveryPerSecond");
                    }

                    sheetData.Add(new List<string> { "Stat", "Value", "" });
                    sheetData.Add(new List<string> { "MaxHP", maxHp.ToString("F1"), "" });
                    sheetData.Add(new List<string> { "MaxMP", maxMp.ToString("F1"), "" });
                    sheetData.Add(new List<string> { "MPRecovery", mpRec.ToString("F1"), "" });
                    // 💡 StatPoint 확률과 획득량 추가
                    sheetData.Add(new List<string> { "StatPointChance", core.statpointGainChance.ToString("F2", System.Globalization.CultureInfo.InvariantCulture), "" });
                    sheetData.Add(new List<string> { "StatPointAmount", core.statpointGainAmount.ToString(), "" });
                    sheetData.Add(new List<string> { "", "", "" });

                    sheetData.Add(new List<string> { "Skill Name", "Damage", "MP Cost" });

                    Component skillSelector = prefab.GetComponentInChildren<SkillSelector>(true);
                    if (skillSelector != null)
                    {
                        SerializedObject soSelector = new SerializedObject(skillSelector);
                        SerializedProperty profiles = soSelector.FindProperty("SkillProfiles") ?? soSelector.FindProperty("skillProfiles");

                        if (profiles != null && profiles.isArray)
                        {
                            for (int j = 0; j < profiles.arraySize; j++)
                            {
                                try
                                {
                                    SerializedProperty skillProp = profiles.GetArrayElementAtIndex(j).FindPropertyRelative("Skill") ?? profiles.GetArrayElementAtIndex(j).FindPropertyRelative("skill");
                                    if (skillProp != null && skillProp.objectReferenceValue != null)
                                    {
                                        SkillBase skill = skillProp.objectReferenceValue as SkillBase;
                                        if (skill == null) continue;

                                        string sName = skill.name;
                                        string dmgStr = ExtractAllDamageMultipliers(skill);
                                        string mpStr = skill.MP_cost.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

                                        sheetData.Add(new List<string> { sName, dmgStr, mpStr });
                                    }
                                }
                                catch { continue; }
                            }
                        }
                    }

                    allData[prefab.name] = sheetData;
                }
                catch { continue; }
            }

            EditorUtility.ClearProgressBar();

            if (allData.Count == 0)
            {
                Debug.LogWarning("[Enemy] 추출된 적 프리팹 데이터가 없습니다.");
                return;
            }

            string jsonData = ConvertToJson(allData);

            using (HttpClient client = new HttpClient())
            {
                StringContent content = new StringContent(jsonData, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(webAppUrl, content);
                string responseStr = await response.Content.ReadAsStringAsync();

                if (responseStr.Contains("Error")) Debug.LogError($"[Enemy] 구글 서버 에러: {responseStr}");
                else if (response.IsSuccessStatusCode) Debug.Log("<color=cyan>[성공]</color> 구글 시트 Enemy 데이터 Push 완료!");
                else Debug.LogError($"[Enemy] HTTP 통신 에러: {response.StatusCode}");
            }
        }
        catch (Exception e) { Debug.LogError($"[Enemy] 통신 및 전체 에러 발생: {e.Message}"); }
        finally
        {
            EditorUtility.ClearProgressBar();
            isWorking = false;
            Repaint();
        }
    }

    private async void PullEnemyData()
    {
        isWorking = true;
        Repaint();
        Debug.Log("\n<color=yellow>========== [Enemy Pull 시작] ==========</color>");

        using (HttpClient client = new HttpClient())
        {
            try
            {
                HttpResponseMessage response = await client.GetAsync(webAppUrl);
                string responseStr = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Debug.LogError($"[Enemy] 데이터 통신 실패: {response.StatusCode}");
                    return;
                }

                if (responseStr.Contains("Error") || !responseStr.Trim().StartsWith("{"))
                {
                    Debug.LogError($"[Enemy Pull 에러] 구글 시트에서 유효한 JSON을 반환하지 않았습니다.\n반환값: {responseStr}");
                    return;
                }

                GASResponse data = JsonUtility.FromJson<GASResponse>(responseStr);

                if (data != null && data.sheets != null && data.sheets.Count > 0)
                {
                    ApplyDataToEnemies(data);
                }
                else
                {
                    Debug.LogError("[Enemy Pull] 시트 데이터가 비어 있거나 JSON 파싱에 실패했습니다.");
                }
            }
            catch (Exception e) { Debug.LogError($"[Enemy] 예외 발생: {e.Message}\n{e.StackTrace}"); }
            finally
            {
                isWorking = false;
                Repaint();
                Debug.Log("<color=green>========== [Enemy Pull 종료] ==========</color>\n");
            }
        }
    }

    private void ApplyDataToEnemies(GASResponse data)
    {
        int updateCount = 0;
        int identicalCount = 0;
        int notFoundCount = 0;

        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        Dictionary<string, GameObject> prefabDict = new Dictionary<string, GameObject>();

        for (int i = 0; i < guids.Length; i++)
        {
            try
            {
                if (i % 10 == 0) EditorUtility.DisplayProgressBar("Enemy Data Pull", $"프리팹 로드 중... {i}/{guids.Length}", (float)i / guids.Length);

                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab != null && prefab.GetComponentInChildren<EnemyCore>(true) != null && !prefab.name.Contains("Dummy"))
                {
                    prefabDict[Clean(prefab.name)] = prefab;
                }
            }
            catch { continue; }
        }
        EditorUtility.ClearProgressBar();

        foreach (var sheet in data.sheets)
        {
            try
            {
                string cleanSheetName = Clean(sheet.sheetName);
                if (cleanSheetName == "Active" || cleanSheetName == "Passive" || cleanSheetName == "Stat" || cleanSheetName == "Stats") continue;

                if (!prefabDict.TryGetValue(cleanSheetName, out GameObject prefab))
                {
                    notFoundCount++;
                    Debug.LogWarning($"[Enemy Pull] 시트 이름 '{cleanSheetName}'에 해당하는 프리팹을 유니티에서 찾을 수 없습니다.");
                    continue;
                }

                Component healthComp = prefab.GetComponentsInChildren<Component>(true).FirstOrDefault(c => c != null && c.GetType().Name.Contains("Health"));
                SerializedObject soHealth = healthComp != null ? new SerializedObject(healthComp) : null;

                // 💡 EnemyCore 프로퍼티 수정을 위해 SerializedObject 추가
                EnemyCore coreComp = prefab.GetComponentInChildren<EnemyCore>(true);
                SerializedObject soCore = coreComp != null ? new SerializedObject(coreComp) : null;

                Component skillSelector = prefab.GetComponentInChildren<SkillSelector>(true);
                SerializedObject soSelector = skillSelector != null ? new SerializedObject(skillSelector) : null;
                SerializedProperty profiles = soSelector?.FindProperty("SkillProfiles") ?? soSelector?.FindProperty("skillProfiles");

                bool isParsingSkills = false;
                bool isPrefabDirty = false;

                foreach (string rowStr in sheet.rows)
                {
                    try
                    {
                        string[] cols = rowStr.Split(new string[] { "|||" }, StringSplitOptions.None);
                        if (cols.Length < 2) continue;

                        string key = Clean(cols[0]);
                        string valStr = Clean(cols[1]);

                        if (key == "Skill Name")
                        {
                            isParsingSkills = true;
                            continue;
                        }

                        if (!isParsingSkills)
                        {
                            float val = ParseFloatSafe(valStr);

                            if (soHealth != null)
                            {
                                if (key == "MaxHP")
                                {
                                    if (Mathf.Abs(GetFloat(soHealth, "maxHP", "MaxHp") - val) > 0.001f)
                                    {
                                        SetFloat(soHealth, val, "maxHP", "MaxHp");
                                        Debug.Log($"<color=cyan>[적 스탯 적용]</color> [{prefab.name}] MaxHP => {val}");
                                        isPrefabDirty = true;
                                    }
                                }
                                else if (key == "MaxMP")
                                {
                                    if (Mathf.Abs(GetFloat(soHealth, "maxMP", "MaxMp") - val) > 0.001f)
                                    {
                                        SetFloat(soHealth, val, "maxMP", "MaxMp");
                                        Debug.Log($"<color=cyan>[적 스탯 적용]</color> [{prefab.name}] MaxMP => {val}");
                                        isPrefabDirty = true;
                                    }
                                }
                                else if (key == "MPRecovery")
                                {
                                    if (Mathf.Abs(GetFloat(soHealth, "MpRecoveryPerSecond") - val) > 0.001f)
                                    {
                                        SetFloat(soHealth, val, "MpRecoveryPerSecond");
                                        Debug.Log($"<color=cyan>[적 스탯 적용]</color> [{prefab.name}] MPRecovery => {val}");
                                        isPrefabDirty = true;
                                    }
                                }
                            }

                            // 💡 StatPoint 동기화 로직 추가
                            if (soCore != null)
                            {
                                if (key == "StatPointChance")
                                {
                                    SerializedProperty prop = soCore.FindProperty("statpointGainChance");
                                    if (prop != null && Mathf.Abs(prop.floatValue - val) > 0.001f)
                                    {
                                        prop.floatValue = val;
                                        Debug.Log($"<color=cyan>[적 스탯 적용]</color> [{prefab.name}] StatPointChance => {val}");
                                        isPrefabDirty = true;
                                    }
                                }
                                else if (key == "StatPointAmount")
                                {
                                    int intVal = Mathf.RoundToInt(val);
                                    SerializedProperty prop = soCore.FindProperty("statpointGainAmount");
                                    if (prop != null && prop.intValue != intVal)
                                    {
                                        prop.intValue = intVal;
                                        Debug.Log($"<color=cyan>[적 스탯 적용]</color> [{prefab.name}] StatPointAmount => {intVal}");
                                        isPrefabDirty = true;
                                    }
                                }
                            }
                        }
                        else if (isParsingSkills && profiles != null)
                        {
                            string targetSkillName = key;
                            string dmgRawStr = cols.Length > 1 ? Clean(cols[1]) : "";
                            string mpCostStr = cols.Length > 2 ? Clean(cols[2]) : "";

                            for (int i = 0; i < profiles.arraySize; i++)
                            {
                                SerializedProperty skillProp = profiles.GetArrayElementAtIndex(i).FindPropertyRelative("Skill") ?? profiles.GetArrayElementAtIndex(i).FindPropertyRelative("skill");
                                if (skillProp != null && skillProp.objectReferenceValue != null)
                                {
                                    SkillBase skill = skillProp.objectReferenceValue as SkillBase;
                                    if (skill == null) continue;

                                    string sName = Clean(skill.name);

                                    if (sName == targetSkillName)
                                    {
                                        bool isSkillDirty = false;

                                        if (!string.IsNullOrEmpty(mpCostStr))
                                        {
                                            float mpCost = ParseFloatSafe(mpCostStr);
                                            if (Mathf.Abs(skill.MP_cost - mpCost) > 0.001f)
                                            {
                                                Debug.Log($"<color=cyan>[적 스킬 적용]</color> [{prefab.name}] 스킬 [{sName}] MP Cost: {skill.MP_cost} => {mpCost}");
                                                skill.MP_cost = mpCost;
                                                isSkillDirty = true;
                                            }
                                        }

                                        if (!string.IsNullOrEmpty(dmgRawStr))
                                        {
                                            if (ApplyAllDamageMultipliers(skill, dmgRawStr))
                                            {
                                                isSkillDirty = true;
                                            }
                                        }

                                        if (isSkillDirty)
                                        {
                                            EditorUtility.SetDirty(skill);
                                            isPrefabDirty = true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { continue; }
                }

                if (isPrefabDirty)
                {
                    if (soHealth != null) soHealth.ApplyModifiedProperties();
                    if (soCore != null) soCore.ApplyModifiedProperties(); // 💡 EnemyCore 프로퍼티 변경사항 적용
                    EditorUtility.SetDirty(prefab);
                    updateCount++;
                }
                else
                {
                    identicalCount++;
                }
            }
            catch { continue; }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"<color=magenta>[적 결과 요약]</color> 갱신성공: {updateCount}개, 값동일무시: {identicalCount}개, 찾을수없음: {notFoundCount}개");
    }
    #endregion

    #region ========== [ 공통 헬퍼 메서드 ] ==========
    // 💡 철저한 속성 감지를 위해 파스칼/카멜/언더바/m_ 규칙 전부 적용
    private SerializedProperty GetProp(SerializedProperty parent, string name)
    {
        var p = parent.FindPropertyRelative(name);
        if (p == null) p = parent.FindPropertyRelative(char.ToUpper(name[0]) + name.Substring(1));
        if (p == null) p = parent.FindPropertyRelative("_" + name);
        if (p == null) p = parent.FindPropertyRelative("m_" + char.ToUpper(name[0]) + name.Substring(1));
        return p;
    }

    private string GetSafeSkillName(SkillBase skill)
    {
        if (skill == null) return "Unknown";
        string fallbackName = string.IsNullOrEmpty(skill.skillName) ? skill.name : skill.skillName;

        try
        {
            if (skill.Localize_skillName != null && !skill.Localize_skillName.IsEmpty)
            {
                if (skill.Localize_skillName.TableReference.ReferenceType == UnityEngine.Localization.Tables.TableReference.Type.Guid)
                {
                    string guidStr = skill.Localize_skillName.TableReference.TableCollectionNameGuid.ToString("N");
                    if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guidStr))) return fallbackName;
                }

                var collection = LocalizationEditorSettings.GetStringTableCollection(skill.Localize_skillName.TableReference);
                if (collection != null)
                {
                    var table = collection.GetTable("ko") as UnityEngine.Localization.Tables.StringTable;
                    if (table != null)
                    {
                        var entry = table.GetEntry(skill.Localize_skillName.TableEntryReference.KeyId);
                        if (entry != null && !string.IsNullOrEmpty(entry.LocalizedValue)) return entry.LocalizedValue;
                    }
                }
            }
        }
        catch { }

        return fallbackName;
    }

    private string GetSafePassiveName(PassiveSkillData passive)
    {
        if (passive == null) return "Unknown";
        try
        {
            if (passive.Localize_passiveName != null && !passive.Localize_passiveName.IsEmpty)
            {
                if (passive.Localize_passiveName.TableReference.ReferenceType == UnityEngine.Localization.Tables.TableReference.Type.Guid)
                {
                    string guidStr = passive.Localize_passiveName.TableReference.TableCollectionNameGuid.ToString("N");
                    if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guidStr))) return passive.name;
                }

                var collection = LocalizationEditorSettings.GetStringTableCollection(passive.Localize_passiveName.TableReference);
                if (collection != null)
                {
                    var table = collection.GetTable("ko") as UnityEngine.Localization.Tables.StringTable;
                    if (table != null)
                    {
                        var entry = table.GetEntry(passive.Localize_passiveName.TableEntryReference.KeyId);
                        if (entry != null && !string.IsNullOrEmpty(entry.LocalizedValue)) return entry.LocalizedValue;
                    }
                }
            }
        }
        catch { }
        return passive.name;
    }

    private string GetSafeSkillBookName(SkillBookBase book)
    {
        if (book == null) return "Unknown";
        try
        {
            if (book.Localize_skillBookName != null && !book.Localize_skillBookName.IsEmpty)
            {
                if (book.Localize_skillBookName.TableReference.ReferenceType == UnityEngine.Localization.Tables.TableReference.Type.Guid)
                {
                    string guidStr = book.Localize_skillBookName.TableReference.TableCollectionNameGuid.ToString("N");
                    if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guidStr))) return book.name;
                }

                var collection = LocalizationEditorSettings.GetStringTableCollection(book.Localize_skillBookName.TableReference);
                if (collection != null)
                {
                    var table = collection.GetTable("ko") as UnityEngine.Localization.Tables.StringTable;
                    if (table != null)
                    {
                        var entry = table.GetEntry(book.Localize_skillBookName.TableEntryReference.KeyId);
                        if (entry != null && !string.IsNullOrEmpty(entry.LocalizedValue)) return entry.LocalizedValue;
                    }
                }
            }
        }
        catch { }
        return book.name;
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

    private void GetDamageData(SkillBase skill, out string damageInfo, out float damageSum)
    {
        damageInfo = "";
        damageSum = 0f;
        List<string> infoList = new List<string>();

        if (skill is Skill_Melee melee && melee.hitDataList != null)
        {
            foreach (var hit in melee.hitDataList)
            {
                float hitTotal = hit.damageMultiplier;
                string hitStr = hit.damageMultiplier.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

                if (hit.useAreaSpawn && hit.areaTickInterval > 0f)
                {
                    int ticks = Mathf.FloorToInt(hit.areaDuration / hit.areaTickInterval);
                    hitTotal += ticks * hit.areaDamageMultiplier;
                    hitStr += $"+A({ticks}x{hit.areaDamageMultiplier.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)})";
                }
                if (hit.useDebuffEffect && hit.debuffType != DebuffType.None) hitStr += $"+{hit.debuffType}({hit.debuffChance * 100:0}%)";
                if (hit.knockbackForce.magnitude > 0f) hitStr += hit.knockdownType != KnockdownType.None ? $"+{hit.knockdownType}({hit.knockdownDuration.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}s)" : "+KB";

                damageSum += hitTotal;
                infoList.Add(hitStr);
            }
        }
        else if (skill is Skill_Range range && range.hitDataList != null)
        {
            foreach (var hit in range.hitDataList)
            {
                damageSum += hit.damageMultiplier;
                infoList.Add(hit.damageMultiplier.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
            }
        }
        else if (skill.damageMultipliers != null)
        {
            foreach (float dmg in skill.damageMultipliers)
            {
                damageSum += dmg;
                infoList.Add(dmg.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        if (infoList.Count > 0) damageInfo = string.Join(" / ", infoList);
    }

    private string ExtractAllDamageMultipliers(SkillBase skill)
    {
        if (skill == null) return "0.00";
        List<string> dmgList = new List<string>();
        SerializedObject so = new SerializedObject(skill);
        SerializedProperty hitList = so.FindProperty("hitDataList");

        if (hitList != null && hitList.isArray)
        {
            for (int i = 0; i < hitList.arraySize; i++)
            {
                SerializedProperty hitProp = hitList.GetArrayElementAtIndex(i);
                SerializedProperty dmgProp = GetProp(hitProp, "damageMultiplier");

                if (dmgProp != null)
                {
                    string hitStr = dmgProp.floatValue.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

                    SerializedProperty useAreaProp = GetProp(hitProp, "useAreaSpawn");
                    if (useAreaProp != null && useAreaProp.boolValue)
                    {
                        SerializedProperty areaDmgProp = GetProp(hitProp, "areaDamageMultiplier");
                        SerializedProperty durationProp = GetProp(hitProp, "areaDuration");
                        SerializedProperty intervalProp = GetProp(hitProp, "areaTickInterval");

                        if (intervalProp != null && intervalProp.floatValue > 0f)
                        {
                            int ticks = Mathf.FloorToInt(durationProp.floatValue / intervalProp.floatValue);
                            if (areaDmgProp != null)
                            {
                                hitStr += $"+A({ticks}x{areaDmgProp.floatValue.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)})";
                            }
                        }
                    }
                    dmgList.Add(hitStr);
                }
            }
        }
        else if (skill.damageMultipliers != null)
        {
            for (int i = 0; i < skill.damageMultipliers.Length; i++)
            {
                dmgList.Add(skill.damageMultipliers[i].ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        return dmgList.Count > 0 ? string.Join(" / ", dmgList) : "0.00";
    }

    // 💡 0 데미지 스킵 로직을 완전히 제거하여 무조건 1:1로 매칭되도록 수정한 메서드
    private bool ApplyAllDamageMultipliers(SkillBase skill, string rawStr)
    {
        if (skill == null || string.IsNullOrEmpty(rawStr)) return false;

        string[] parts = rawStr.Split('/');
        List<ParsedDamage> parsedValues = new List<ParsedDamage>();

        foreach (string p in parts)
        {
            ParsedDamage pd = new ParsedDamage();
            string[] plusParts = p.Split('+');
            pd.baseDamage = ParseFloatSafe(plusParts[0]);

            for (int i = 1; i < plusParts.Length; i++)
            {
                string ex = Clean(plusParts[i]);
                if (ex.StartsWith("A("))
                {
                    string inside = ex.Substring(2).TrimEnd(')');
                    string[] tx = inside.Split(new char[] { 'x', 'X' });
                    if (tx.Length == 2)
                    {
                        if (int.TryParse(Clean(tx[0]), out int ticks)) pd.areaTicks = ticks;
                        pd.areaDamage = ParseFloatSafe(tx[1]);
                        pd.hasArea = true;
                    }
                }
            }
            parsedValues.Add(pd);
        }

        if (parsedValues.Count == 0) return false;

        bool isDirty = false;
        int valueIndex = 0;
        SerializedObject so = new SerializedObject(skill);
        SerializedProperty hitList = so.FindProperty("hitDataList");

        if (hitList != null && hitList.isArray)
        {
            for (int i = 0; i < hitList.arraySize; i++)
            {
                SerializedProperty hitProp = hitList.GetArrayElementAtIndex(i);
                SerializedProperty dmgProp = GetProp(hitProp, "damageMultiplier");

                if (dmgProp != null)
                {
                    if (valueIndex < parsedValues.Count)
                    {
                        ParsedDamage pd = parsedValues[valueIndex];

                        if (Mathf.Abs(dmgProp.floatValue - pd.baseDamage) > 0.001f)
                        {
                            Debug.Log($"<color=cyan>[기본 데미지 적용]</color> [{skill.name}] Hit {i}: {dmgProp.floatValue} => {pd.baseDamage}");
                            dmgProp.floatValue = pd.baseDamage;
                            isDirty = true;
                        }

                        if (pd.hasArea)
                        {
                            SerializedProperty useAreaProp = GetProp(hitProp, "useAreaSpawn");
                            if (useAreaProp != null)
                            {
                                if (!useAreaProp.boolValue)
                                {
                                    useAreaProp.boolValue = true;
                                    isDirty = true;
                                }

                                SerializedProperty areaDmgProp = GetProp(hitProp, "areaDamageMultiplier");
                                SerializedProperty durationProp = GetProp(hitProp, "areaDuration");
                                SerializedProperty intervalProp = GetProp(hitProp, "areaTickInterval");

                                if (areaDmgProp != null && durationProp != null && intervalProp != null)
                                {
                                    if (Mathf.Abs(areaDmgProp.floatValue - pd.areaDamage) > 0.001f)
                                    {
                                        Debug.Log($"<color=cyan>[장판 데미지 적용]</color> [{skill.name}] Hit {i} 장판: {areaDmgProp.floatValue} => {pd.areaDamage}");
                                        areaDmgProp.floatValue = pd.areaDamage;
                                        isDirty = true;
                                    }

                                    if (pd.areaTicks > 0 && durationProp.floatValue > 0f)
                                    {
                                        float newInterval = durationProp.floatValue / pd.areaTicks;
                                        if (Mathf.Abs(intervalProp.floatValue - newInterval) > 0.001f)
                                        {
                                            Debug.Log($"<color=cyan>[장판 틱 조절]</color> [{skill.name}] Hit {i} 틱간격: {intervalProp.floatValue:F3} => {newInterval:F3} (틱수: {pd.areaTicks})");
                                            intervalProp.floatValue = newInterval;
                                            isDirty = true;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Debug.LogWarning($"[{skill.name}] Hit {i}에 'useAreaSpawn' 프로퍼티를 찾을 수 없습니다.");
                            }
                        }

                        valueIndex++;
                    }
                }
            }
            if (isDirty) so.ApplyModifiedProperties();
        }
        else if (skill.damageMultipliers != null)
        {
            for (int i = 0; i < skill.damageMultipliers.Length; i++)
            {
                if (valueIndex < parsedValues.Count)
                {
                    if (Mathf.Abs(skill.damageMultipliers[i] - parsedValues[valueIndex].baseDamage) > 0.001f)
                    {
                        Debug.Log($"<color=cyan>[데미지 적용]</color> [{skill.name}] Hit {i}: {skill.damageMultipliers[i]} => {parsedValues[valueIndex].baseDamage}");
                        skill.damageMultipliers[i] = parsedValues[valueIndex].baseDamage;
                        isDirty = true;
                    }
                    valueIndex++;
                }
            }
        }

        return isDirty;
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
    #endregion
}