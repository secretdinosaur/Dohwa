using UnityEngine;
using UnityEditor;
using System;
using System.Text;
using System.Collections.Generic;
using System.Net.Http;
using System.Linq;
using Unity.VisualScripting.FullSerializer;
using UnityEditor.PackageManager;
using UnityEditor.Localization;

public class SkillBookGoogleSheetSyncWindow : EditorWindow
{
    private string webAppUrl = "https://script.google.com/macros/s/AKfycbylQmkk4__fy-oOHmULz3lUy-s2iZVK-bJh_u78IfmDzV6zxrESfdQ6Xk6wz9Whr81ZJw/exec";
    private bool isWorking = false;

    [Serializable]
    private class GASResponse { public List<SheetData> sheets; }
    [Serializable]
    private class SheetData { public string sheetName; public List<string> rows; }

    [MenuItem("Tools/SkillBook Google Sheet Sync")]
    public static void ShowWindow()
    {
        var window = GetWindow<SkillBookGoogleSheetSyncWindow>("SkillBook Sync");
        window.minSize = new Vector2(450, 300);
        window.Show();
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("SkillBook <-> Google Sheet 동기화", EditorStyles.boldLabel);
        GUILayout.Space(10);

        webAppUrl = EditorGUILayout.TextField("Web App URL", webAppUrl);
        GUILayout.Space(10);

        EditorGUILayout.HelpBox(
            "[Push (내보내기)]\n현재 유니티에 있는 스킬 데이터를 구글 시트로 전송하여 덮어씌웁니다.\n\n" +
            "[Pull (가져오기)]\n구글 시트의 수정된 기획 수치(Cost, MP Cost, Damage, 스탯량)를 유니티로 가져옵니다.",
            MessageType.Info);

        GUILayout.Space(20);

        EditorGUI.BeginDisabledGroup(isWorking || string.IsNullOrEmpty(webAppUrl));

        if (GUILayout.Button("Push (유니티 -> 구글 시트)", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("Push 확인", "현재 유니티의 데이터를 구글 시트로 덮어씌웁니다.\n진행하시겠습니까?", "예", "아니오"))
            {
                PushData();
            }
        }

        GUILayout.Space(10);

        if (GUILayout.Button("Pull (구글 시트 -> 유니티)", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("Pull 확인", "구글 시트의 기획 데이터를 유니티로 가져옵니다.\n진행하시겠습니까?", "예", "아니오"))
            {
                PullData();
            }
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
        Debug.Log("구글 시트로 데이터를 전송하는 중...");

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

            foreach (string guid in bookGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                SkillBookBase skillBook = AssetDatabase.LoadAssetAtPath<SkillBookBase>(path);
                if (skillBook.skillTree == null) continue;

                foreach (var node in skillBook.skillTree)
                {
                    if (node.nodeType == SkillBookBase.NodeType.ActiveSkill && node.activeSkill != null)
                    {
                        globalSkillCosts.TryAdd(node.activeSkill, node.cost);
                    }
                    else if (node.nodeType == SkillBookBase.NodeType.StatSkill && node.statModifiers != null)
                    {
                        foreach (var stat in node.statModifiers)
                        {
                            uniqueStatTypes.Add(stat.statType.ToString());
                        }
                    }
                }
            }

            List<string> statNames = uniqueStatTypes.ToList();

            List<string> preferredOrder = new List<string>
            {
                "ExternalArt", "InnerArt", "HpRecovery", "MpRecovery", "VitalStrikeDamage", "VitalStrikeRate"
            };

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

            foreach (string guid in bookGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                SkillBookBase skillBook = AssetDatabase.LoadAssetAtPath<SkillBookBase>(path);
                if (skillBook.skillTree == null) continue;

                string bookDisplayName = !string.IsNullOrEmpty(skillBook.skillBookName) ? skillBook.skillBookName : skillBook.name;

                for (int i = 0; i < skillBook.skillTree.Length; i++)
                {
                    var node = skillBook.skillTree[i];
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
                                    if (upgradeLogic.baseSkill != null && globalSkillCosts.TryGetValue(upgradeLogic.baseSkill, out int baseCost))
                                        upgradeCost += baseCost;
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

                                passiveData.Add(new List<string> { bookDisplayName, passiveDisplayName, cost.ToString(), linkedName });

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
                            if (node.statModifiers != null && node.statModifiers.Length > 0)
                            {
                                Dictionary<string, float> currentStats = new Dictionary<string, float>();
                                foreach (var stat in node.statModifiers)
                                {
                                    string sName = stat.statType.ToString();
                                    if (!currentStats.ContainsKey(sName)) currentStats[sName] = 0f;
                                    currentStats[sName] += stat.statValue;
                                }

                                string statNodeName = node.GetNodeName();
                                List<string> rowBase = new List<string> { bookDisplayName, statNodeName, cost > 0 ? cost.ToString() : "" };
                                List<string> statValues = new List<string>();
                                List<string> ratioValues = new List<string>();

                                foreach (string sName in statNames)
                                {
                                    if (currentStats.TryGetValue(sName, out float val) && val != 0f)
                                    {
                                        statValues.Add(val.ToString());
                                        ratioValues.Add(cost > 0 ? (val / cost).ToString("F2") : "");
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
                            }
                            break;
                    }
                }
            }

            string[] allSkillGuids = AssetDatabase.FindAssets("t:SkillBase");
            foreach (string guid in allSkillGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                SkillBase skill = AssetDatabase.LoadAssetAtPath<SkillBase>(path);

                if (skill == null || skill.isEnemySkill || processedSkills.Contains(skill)) continue;

                GetDamageData(skill, out string dmgInfo, out float dmgSum);

                string mpStr = skill.MP_cost > 0 ? skill.MP_cost.ToString("F1") : "";
                string dmgSumStr = dmgSum > 0 ? dmgSum.ToString("F2") : "";
                string dpmStr = (skill.MP_cost > 0 && dmgSum > 0) ? (dmgSum / skill.MP_cost).ToString("F2") : "";

                string unlinkedDisplayName = GetSafeSkillName(skill);
                activeData.Add(new List<string> { "Unlinked Skills", unlinkedDisplayName, "", dmgInfo, mpStr, dmgSumStr, "", dpmStr });
            }

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

                if (response.IsSuccessStatusCode)
                    Debug.Log("[완료] 구글 시트 Push(내보내기) 완료");
                else
                    Debug.LogError($"[실패] 구글 시트 갱신 실패: {response.StatusCode}");
            }
        }
        catch (Exception e)
        {
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
        Debug.Log("\n<color=yellow>==================== [디버그 Pull 시작] ====================</color>");

        using (HttpClient client = new HttpClient())
        {
            try
            {
                HttpResponseMessage response = await client.GetAsync(webAppUrl);
                string jsonResponse = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Debug.LogError($"[오류] 데이터 불러오기 실패. 상태 코드: {response.StatusCode}");
                    return;
                }

                GASResponse data = JsonUtility.FromJson<GASResponse>(jsonResponse);

                if (data == null || data.sheets == null)
                {
                    Debug.LogError("[오류] JSON 파싱 실패. 데이터가 비어있거나 형식이 맞지 않습니다.");
                    return;
                }

                Debug.Log($"[디버그] 서버에서 받아온 총 시트(탭) 개수: {data.sheets.Count}");
                foreach (var s in data.sheets)
                {
                    Debug.Log($"   -> 탭 이름: [{s.sheetName}], 행(Row) 개수: {s.rows?.Count ?? 0}");
                }

                ApplyDataToAssets(data);
                Debug.Log("<color=green>[디버그 Pull 종료]</color>\n");
            }
            catch (Exception e)
            {
            }
            finally
            {
                isWorking = false;
                Repaint();
            }
        }
    }

    private void ApplyDataToAssets(GASResponse data)
    {
        Debug.Log("\n--- [1단계] 유니티 에셋 메모리 매핑 시작 ---");

        string[] bookGuids = AssetDatabase.FindAssets("t:SkillBookBase");
        Dictionary<string, SkillBookBase> bookDict = new Dictionary<string, SkillBookBase>();

        foreach (string guid in bookGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            SkillBookBase skillBook = AssetDatabase.LoadAssetAtPath<SkillBookBase>(path);
            string bookName = !string.IsNullOrEmpty(skillBook.skillBookName) ? skillBook.skillBookName.Trim() : skillBook.name.Trim();
            bookDict[bookName] = skillBook;
            Debug.Log($"   스킬북 에셋 발견: [{bookName}]");
        }

        string[] skillGuids = AssetDatabase.FindAssets("t:SkillBase");
        Dictionary<string, SkillBase> skillDict = new Dictionary<string, SkillBase>();

        foreach (string guid in skillGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            SkillBase skill = AssetDatabase.LoadAssetAtPath<SkillBase>(path);
            if (skill == null) continue;

            string skillName = GetSafeSkillName(skill).Trim();
            if (!skillDict.ContainsKey(skillName))
            {
                skillDict[skillName] = skill;
            }
        }
        Debug.Log($"   총 스킬 에셋 로드 개수: {skillDict.Count}개");

        var activeSheet = data.sheets.FirstOrDefault(s => string.Equals(s.sheetName?.Trim(), "Active", StringComparison.OrdinalIgnoreCase));
        List<string> activeRows = activeSheet?.rows;

        Debug.Log("\n--- [2단계] Active 시트 데이터 정밀 검사 및 적용 ---");
        if (activeRows == null)
        {
            Debug.LogError("[치명적 오류] 'Active' 탭을 찾지 못했습니다.");
        }
        else if (activeRows.Count <= 1)
        {
            Debug.LogWarning("[경고] 'Active' 탭에 데이터 행이 없습니다 (헤더만 존재).");
        }
        else
        {
            Debug.Log($"   Active 탭 총 행 개수: {activeRows.Count}개");
            string[] headers = activeRows[0].Split(new string[] { "|||" }, StringSplitOptions.None).Select(h => h.Trim()).ToArray();
            Debug.Log($"   Active 헤더 목록: {string.Join(" / ", headers)}");

            int bookIdx = Array.IndexOf(headers, "Skill Book");
            int skillIdx = Array.IndexOf(headers, "Skill Name");
            int costIdx = Array.IndexOf(headers, "Cost");
            int dmgIdx = Array.IndexOf(headers, "Damage");
            int mpCostIdx = Array.IndexOf(headers, "MP Cost");

            Debug.Log($"   인덱스 매칭 결과 -> BookIdx:{bookIdx}, SkillIdx:{skillIdx}, CostIdx:{costIdx}, DmgIdx:{dmgIdx}, MpIdx:{mpCostIdx}");

            if (bookIdx == -1 || skillIdx == -1)
            {
                Debug.LogError("[오류] 헤더에서 'Skill Book' 또는 'Skill Name'을 찾지 못했습니다.");
            }
            else
            {
                for (int i = 1; i < activeRows.Count; i++)
                {
                    string rowLine = activeRows[i];
                    string[] row = rowLine.Split(new string[] { "|||" }, StringSplitOptions.None);

                    Debug.Log($"   [행 {i}] 원본 데이터열 개수: {row.Length}개 (내용: {rowLine})");

                    if (row.Length <= skillIdx)
                    {
                        Debug.LogWarning($"     -> 스킵됨: 행의 데이터 개수가 너무 적습니다.");
                        continue;
                    }

                    string bookName = row[bookIdx].Trim();
                    string skillName = row[skillIdx].Trim();

                    Debug.Log($"     -> 파싱된 책이름: [{bookName}], 스킬이름: [{skillName}]");

                    if (costIdx != -1 && row.Length > costIdx)
                    {
                        string costStr = row[costIdx].Trim();
                        if (bookDict.TryGetValue(bookName, out SkillBookBase book))
                        {
                            bool matchedInBook = false;
                            foreach (var node in book.skillTree)
                            {
                                if (node.nodeType == SkillBookBase.NodeType.ActiveSkill && node.activeSkill != null)
                                {
                                    string sName = GetSafeSkillName(node.activeSkill).Trim();
                                    if (sName == skillName)
                                    {
                                        matchedInBook = true;
                                        if (int.TryParse(costStr, out int cost))
                                        {
                                            if (node.cost != cost)
                                            {
                                                Debug.Log($"       [Cost 변경 성공] 스킬북 [{bookName}] -> 스킬 [{skillName}] Cost: {node.cost} ==> {cost}");
                                                node.cost = cost;
                                                EditorUtility.SetDirty(book);
                                            }
                                            else
                                            {
                                                Debug.Log($"       [Cost 동일함] 변경 없음 ({cost})");
                                            }
                                        }
                                        else
                                        {
                                            Debug.LogWarning($"       [Cost 파싱 실패] 값: [{costStr}]");
                                        }
                                    }
                                }
                            }
                            if (!matchedInBook)
                            {
                                Debug.LogWarning($"       [주의] 스킬북 [{bookName}] 내부에서 스킬 [{skillName}] 노드를 찾지 못했습니다.");
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"       [주의] 메모리에서 스킬북 [{bookName}] 에셋을 찾지 못했습니다.");
                        }
                    }

                    if (skillDict.TryGetValue(skillName, out SkillBase skillData))
                    {
                        bool isSkillChanged = false;

                        if (mpCostIdx != -1 && row.Length > mpCostIdx)
                        {
                            string mpStr = row[mpCostIdx].Trim();
                            if (float.TryParse(mpStr, out float mp))
                            {
                                if (skillData.MP_cost != mp)
                                {
                                    Debug.Log($"       [MP 변경 성공] 스킬 에셋 [{skillName}] MP Cost: {skillData.MP_cost} ==> {mp}");
                                    skillData.MP_cost = mp;
                                    isSkillChanged = true;
                                }
                            }
                        }

                        if (dmgIdx != -1 && row.Length > dmgIdx)
                        {
                            string dmgStr = row[dmgIdx].Trim();
                            if (!string.IsNullOrEmpty(dmgStr))
                            {
                                Debug.Log($"       [데미지 적용 시도] 스킬 [{skillName}]에 전달할 문자열: [{dmgStr}]");
                                ApplyAllDamageMultipliers(skillData, dmgStr);
                                isSkillChanged = true;
                            }
                        }

                        if (isSkillChanged)
                        {
                            EditorUtility.SetDirty(skillData);
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"       [주의] 전체 스킬 에셋 중 [{skillName}] 이름과 일치하는 SkillBase 에셋이 없습니다.");
                    }
                }
            }
        }

        Debug.Log("\n--- [3단계] 변경 사항 저장 중 ---");
        AssetDatabase.SaveAssets();
        Debug.Log("--- [완료] 저장 완료 ---");
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
                var collection = LocalizationEditorSettings.GetStringTableCollection(skill.Localize_skillName.TableReference);
                if (collection != null)
                {
                    var table = collection.GetTable("ko") as UnityEngine.Localization.Tables.StringTable;
                    if (table != null)
                    {
                        var entry = table.GetEntry(skill.Localize_skillName.TableEntryReference.KeyId);
                        if (entry != null && !string.IsNullOrEmpty(entry.LocalizedValue))
                        {
                            return entry.LocalizedValue;
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[{skill.name}] 번역 테이블 로드 실패, 기본 이름 사용. 사유: {e.Message}");
        }

        return skill.name;
    }

    private string GetSafePassiveName(PassiveSkillData passive)
    {
        if (passive == null) return "Unknown";

        try
        {
            if (passive.Localize_passiveName != null && !passive.Localize_passiveName.IsEmpty)
            {
                var collection = LocalizationEditorSettings.GetStringTableCollection(passive.Localize_passiveName.TableReference);
                if (collection != null)
                {
                    var table = collection.GetTable("ko") as UnityEngine.Localization.Tables.StringTable;
                    if (table != null)
                    {
                        var entry = table.GetEntry(passive.Localize_passiveName.TableEntryReference.KeyId);
                        if (entry != null && !string.IsNullOrEmpty(entry.LocalizedValue))
                        {
                            return entry.LocalizedValue;
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[{passive.name}] 번역 테이블 로드 실패, 기본 이름 사용. 사유: {e.Message}");
        }

        return passive.name;
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
                        .Replace("\\", "\\\\")
                        .Replace("\"", "\\\"")
                        .Replace("\n", "\\n")
                        .Replace("\r", "")
                        .Replace("\t", "\\t")
                        : "";

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
                string hitStr = hit.damageMultiplier.ToString("F2");

                if (hit.useAreaSpawn && hit.areaTickInterval > 0f)
                {
                    int ticks = Mathf.FloorToInt(hit.areaDuration / hit.areaTickInterval);
                    float areaDamage = ticks * hit.areaDamageMultiplier;

                    hitTotal += areaDamage;
                    hitStr += $"+A({ticks}x{hit.areaDamageMultiplier:F2})";
                }

                if (hit.useDebuffEffect && hit.debuffType != DebuffType.None)
                {
                    hitStr += $"+{hit.debuffType}({hit.debuffChance * 100:0}%)";
                }

                if (hit.knockbackForce.magnitude > 0f)
                {
                    if (hit.knockdownType != KnockdownType.None)
                        hitStr += $"+{hit.knockdownType}({hit.knockdownDuration:F1}s)";
                    else
                        hitStr += "+KB";
                }

                damageSum += hitTotal;
                infoList.Add(hitStr);
            }
        }
        else if (skill is Skill_Range range && range.hitDataList != null)
        {
            foreach (var hit in range.hitDataList)
            {
                damageSum += hit.damageMultiplier;
                infoList.Add(hit.damageMultiplier.ToString("F2"));
            }
        }
        else if (skill.damageMultipliers != null)
        {
            foreach (float dmg in skill.damageMultipliers)
            {
                damageSum += dmg;
                infoList.Add(dmg.ToString("F2"));
            }
        }

        if (infoList.Count > 0)
        {
            damageInfo = string.Join(" / ", infoList);
        }
    }

    private void ApplyAllDamageMultipliers(SkillBase skill, string rawStr)
    {
        if (skill == null || string.IsNullOrEmpty(rawStr)) return;

        string[] parts = rawStr.Split('/');
        List<float> parsedValues = new List<float>();

        foreach (string p in parts)
        {
            string cleanStr = p.Split('+')[0].Trim();

            if (float.TryParse(cleanStr, out float val))
            {
                parsedValues.Add(val);
            }
        }

        if (parsedValues.Count == 0) return;

        int valueIndex = 0;
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
                    if (!hasSound && Mathf.Abs(dmgProp.floatValue) <= 0.001f) continue;

                    if (valueIndex < parsedValues.Count)
                    {
                        float oldDmg = dmgProp.floatValue;
                        dmgProp.floatValue = parsedValues[valueIndex];
                        Debug.Log($"       => [데미지 갱신 완료] 스킬: [{skill.name}] Hit {i}: {oldDmg} -> {dmgProp.floatValue}");
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
                if (snd == null && Mathf.Abs(skill.damageMultipliers[i]) <= 0.001f) continue;

                if (valueIndex < parsedValues.Count)
                {
                    float oldDmg = skill.damageMultipliers[i];
                    skill.damageMultipliers[i] = parsedValues[valueIndex];
                    Debug.Log($"       => [데미지 갱신 완료] 스킬: [{skill.name}] Hit {i}: {oldDmg} -> {skill.damageMultipliers[i]}");
                    valueIndex++;
                }
            }
        }
    }
    #endregion
}