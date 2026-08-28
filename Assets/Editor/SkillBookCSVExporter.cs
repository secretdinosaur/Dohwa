using UnityEngine;
using UnityEditor;
using System;
using System.Text;
using System.Collections.Generic;
using System.Net.Http;
using System.Linq;

public class SkillBookGoogleSheetSyncWindow : EditorWindow
{
    private string webAppUrl = "https://script.google.com/macros/s/AKfycbxVgGFYfBN5LksLaAr43lqMPlF6OoXUNfFG4Ofm475jPd5dbXk_bojXVqIHveMLlntYXQ/exec";
    private bool isWorking = false;

    [Serializable]
    private class GoogleSheetData
    {
        public List<string> Active;
        public List<string> Passive;
        public List<string> Stat;
    }

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
        GUILayout.Label("📝 SkillBook <-> Google Sheet 동기화", EditorStyles.boldLabel);
        GUILayout.Space(10);

        webAppUrl = EditorGUILayout.TextField("Web App URL", webAppUrl);
        GUILayout.Space(10);

        EditorGUILayout.HelpBox(
            "[⬆️ Push (내보내기)]\n현재 유니티에 있는 스킬 데이터를 구글 시트로 전송하여 덮어씌웁니다.\n\n" +
            "[⬇️ Pull (가져오기)]\n구글 시트의 수정된 기획 수치(Cost, MP Cost, 스탯량)를 유니티로 가져옵니다.",
            MessageType.Info);

        GUILayout.Space(20);

        // 작업 중이거나 URL이 비어있으면 버튼 비활성화
        EditorGUI.BeginDisabledGroup(isWorking || string.IsNullOrEmpty(webAppUrl));

        if (GUILayout.Button("⬆️ Push (유니티 -> 구글 시트)", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("Push 확인", "현재 유니티의 데이터를 구글 시트로 덮어씌웁니다.\n진행하시겠습니까?", "예", "아니오"))
            {
                PushData();
            }
        }

        GUILayout.Space(10);

        if (GUILayout.Button("⬇️ Pull (구글 시트 -> 유니티)", GUILayout.Height(40)))
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

            // 💡 요청하신 스탯 열(Column) 우선 정렬 로직
            List<string> preferredOrder = new List<string>
            {
                "ExternalArt", "InnerArt", "HpRecovery", "MpRecovery", "VitalStrikeDamage", "VitalStrikeRate"
            };

            statNames.Sort((a, b) =>
            {
                int idxA = preferredOrder.IndexOf(a);
                int idxB = preferredOrder.IndexOf(b);

                if (idxA != -1 && idxB != -1) return idxA.CompareTo(idxB); // 둘 다 우선순위 목록에 있으면 순서대로
                if (idxA != -1) return -1; // a만 우선순위 목록에 있으면 a를 앞으로
                if (idxB != -1) return 1;  // b만 우선순위 목록에 있으면 b를 앞으로
                return a.CompareTo(b);     // 둘 다 없으면 남은 것들끼리 알파벳순 정렬
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

                                string skillDisplayName = !string.IsNullOrEmpty(node.activeSkill.skillName) ? node.activeSkill.skillName : node.activeSkill.name;
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

                                string linkedName = linkedSkill != null ? (!string.IsNullOrEmpty(linkedSkill.skillName) ? linkedSkill.skillName : linkedSkill.name) : "";
                                string passiveDisplayName = !string.IsNullOrEmpty(node.passiveSkill.passiveName) ? node.passiveSkill.passiveName : node.passiveSkill.name;

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

                string unlinkedDisplayName = !string.IsNullOrEmpty(skill.skillName) ? skill.skillName : skill.name;
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
                    Debug.Log("✅ 구글 시트 Push(내보내기) 완료!");
                else
                    Debug.LogError($"❌ 구글 시트 갱신 실패: {response.StatusCode}");
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
        Debug.Log("구글 시트에서 데이터를 불러오는 중...");

        using (HttpClient client = new HttpClient())
        {
            try
            {
                HttpResponseMessage response = await client.GetAsync(webAppUrl);
                if (!response.IsSuccessStatusCode)
                {
                    Debug.LogError($"데이터 불러오기 실패: {response.StatusCode}");
                    return;
                }

                string jsonResponse = await response.Content.ReadAsStringAsync();
                GoogleSheetData data = JsonUtility.FromJson<GoogleSheetData>(jsonResponse);

                if (data != null)
                {
                    ApplyDataToAssets(data);
                    Debug.Log("✅ 구글 시트 기획 데이터(Pull) 유니티 적용 완료!");
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

    private void ApplyDataToAssets(GoogleSheetData data)
    {
        string[] bookGuids = AssetDatabase.FindAssets("t:SkillBookBase");
        Dictionary<string, SkillBookBase> bookDict = new Dictionary<string, SkillBookBase>();

        foreach (string guid in bookGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            SkillBookBase skillBook = AssetDatabase.LoadAssetAtPath<SkillBookBase>(path);
            string bookName = !string.IsNullOrEmpty(skillBook.skillBookName) ? skillBook.skillBookName : skillBook.name;
            bookDict[bookName] = skillBook;
        }

        // 1. Active 적용
        if (data.Active != null && data.Active.Count > 1)
        {
            string[] headers = data.Active[0].Split("|||");
            int bookIdx = Array.IndexOf(headers, "Skill Book");
            int skillIdx = Array.IndexOf(headers, "Skill Name");
            int costIdx = Array.IndexOf(headers, "Cost");
            int mpCostIdx = Array.IndexOf(headers, "MP Cost");

            for (int i = 1; i < data.Active.Count; i++)
            {
                string[] row = data.Active[i].Split("|||");
                if (row.Length <= mpCostIdx) continue;

                string bookName = row[bookIdx];
                string skillName = row[skillIdx];

                if (bookDict.TryGetValue(bookName, out SkillBookBase book))
                {
                    foreach (var node in book.skillTree)
                    {
                        if (node.nodeType == SkillBookBase.NodeType.ActiveSkill && node.activeSkill != null)
                        {
                            string sName = !string.IsNullOrEmpty(node.activeSkill.skillName) ? node.activeSkill.skillName : node.activeSkill.name;
                            if (sName == skillName)
                            {
                                if (int.TryParse(row[costIdx], out int cost)) node.cost = cost;
                                if (float.TryParse(row[mpCostIdx], out float mp)) node.activeSkill.MP_cost = mp;

                                EditorUtility.SetDirty(node.activeSkill);
                                EditorUtility.SetDirty(book);
                            }
                        }
                    }
                }
            }
        }

        // 2. Passive 적용
        if (data.Passive != null && data.Passive.Count > 1)
        {
            string[] headers = data.Passive[0].Split("|||");
            int bookIdx = Array.IndexOf(headers, "Skill Book");
            int passiveIdx = Array.IndexOf(headers, "Passive Name");
            int costIdx = Array.IndexOf(headers, "Cost");

            for (int i = 1; i < data.Passive.Count; i++)
            {
                string[] row = data.Passive[i].Split("|||");
                if (row.Length <= costIdx) continue;

                string bookName = row[bookIdx];
                string passiveName = row[passiveIdx];

                if (bookDict.TryGetValue(bookName, out SkillBookBase book))
                {
                    foreach (var node in book.skillTree)
                    {
                        if (node.nodeType == SkillBookBase.NodeType.PassiveSkill && node.passiveSkill != null)
                        {
                            string pName = !string.IsNullOrEmpty(node.passiveSkill.passiveName) ? node.passiveSkill.passiveName : node.passiveSkill.name;
                            if (pName == passiveName)
                            {
                                if (int.TryParse(row[costIdx], out int cost)) node.cost = cost;
                                EditorUtility.SetDirty(book);
                            }
                        }
                    }
                }
            }
        }

        // 3. Stat 적용
        if (data.Stat != null && data.Stat.Count > 1)
        {
            string[] headers = data.Stat[0].Split("|||");
            int bookIdx = Array.IndexOf(headers, "Skill Book");
            int statNameIdx = Array.IndexOf(headers, "Stat Name");
            int costIdx = Array.IndexOf(headers, "Cost");

            for (int i = 1; i < data.Stat.Count; i++)
            {
                string[] row = data.Stat[i].Split("|||");
                if (row.Length <= costIdx) continue;

                string bookName = row[bookIdx];
                string statNodeName = row[statNameIdx];

                if (bookDict.TryGetValue(bookName, out SkillBookBase book))
                {
                    foreach (var node in book.skillTree)
                    {
                        if (node.nodeType == SkillBookBase.NodeType.StatSkill && node.GetNodeName() == statNodeName)
                        {
                            if (int.TryParse(row[costIdx], out int cost)) node.cost = cost;

                            List<SkillBookBase.StatModifier> newStats = new List<SkillBookBase.StatModifier>();

                            for (int h = costIdx + 1; h < headers.Length; h++)
                            {
                                string headerName = headers[h];
                                if (headerName.Contains("/Cost")) continue;

                                if (Enum.TryParse<StatManager.StatType>(headerName, out var statType))
                                {
                                    if (float.TryParse(row[h], out float val) && val != 0f)
                                    {
                                        newStats.Add(new SkillBookBase.StatModifier { statType = statType, statValue = val });
                                    }
                                }
                            }

                            node.statModifiers = newStats.ToArray();
                            EditorUtility.SetDirty(book);
                        }
                    }
                }
            }
        }

        AssetDatabase.SaveAssets();
    }
    #endregion

    #region Helper Methods
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
    #endregion
}