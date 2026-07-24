using UnityEngine;
using UnityEditor;
using System.Text;
using System.Collections.Generic;
using System.Net.Http;
using System.Linq;

public class SkillBookSheetUpdater
{
    private static readonly string WebAppUrl = "https://script.google.com/macros/s/AKfycbzutE9LdQbPxBXD-b-3f-bWrZWyl7NKxSLrMgk4UiILQOl6LLWG3Cw45xFstAC2hOicyQ/exec";

    [MenuItem("Tools/Update SkillBook to Google Sheet")]
    public static async void UpdateSheet()
    {
        List<List<string>> activeData = new List<List<string>>();
        List<List<string>> passiveData = new List<List<string>>();
        List<List<string>> statData = new List<List<string>>();

        // 💡 Active, Passive 시트에서 Node Index 열 제거
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
        statNames.Sort();

        // 💡 Stat 시트 헤더 구성 (Stat 순번용 'Stat Index' 추가)
        List<string> statHeader = new List<string> { "Skill Book", "Stat Index", "Cost" };
        foreach (string sName in statNames)
        {
            statHeader.Add(sName);
        }
        foreach (string sName in statNames)
        {
            statHeader.Add($"{sName}/Cost");
        }
        statData.Add(statHeader);

        foreach (string guid in bookGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            SkillBookBase skillBook = AssetDatabase.LoadAssetAtPath<SkillBookBase>(path);
            if (skillBook.skillTree == null) continue;

            // 💡 Book ID 대신 한글 스킬북 이름 사용
            string bookDisplayName = !string.IsNullOrEmpty(skillBook.skillBookName) ? skillBook.skillBookName : skillBook.name;

            int statNodeCounter = 0; // 해당 북의 스탯 노드 순번 카운터

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

                            // 💡 Node Index 제외된 행 추가
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
                                {
                                    upgradeCost += baseCost;
                                }
                            }

                            if (linkedSkill == null)
                            {
                                string targetName = node.passiveSkill.name.Replace("PassiveSkillUpgrade_", "") + "_Upgraded";
                                string[] guidsFound = AssetDatabase.FindAssets($"t:SkillBase {targetName}");
                                if (guidsFound.Length > 0)
                                {
                                    linkedSkill = AssetDatabase.LoadAssetAtPath<SkillBase>(AssetDatabase.GUIDToAssetPath(guidsFound[0]));
                                }
                            }

                            string linkedName = linkedSkill != null ? (!string.IsNullOrEmpty(linkedSkill.skillName) ? linkedSkill.skillName : linkedSkill.name) : "";
                            string passiveDisplayName = !string.IsNullOrEmpty(node.passiveSkill.passiveName) ? node.passiveSkill.passiveName : node.passiveSkill.name;

                            // 💡 Node Index 제외된 패시브 행 추가
                            passiveData.Add(new List<string> { bookDisplayName, passiveDisplayName, cost.ToString(), linkedName });

                            if (linkedSkill != null)
                            {
                                processedSkills.Add(linkedSkill);
                                GetDamageData(linkedSkill, out string lDmgInfo, out float lDmgSum);

                                string lMpStr = linkedSkill.MP_cost > 0 ? linkedSkill.MP_cost.ToString("F1") : "";
                                string lDmgSumStr = lDmgSum > 0 ? lDmgSum.ToString("F2") : "";
                                string lDpcStr = (upgradeCost > 0 && lDmgSum > 0) ? (lDmgSum / upgradeCost).ToString("F2") : "";
                                string lDpmStr = (linkedSkill.MP_cost > 0 && lDmgSum > 0) ? (lDmgSum / linkedSkill.MP_cost).ToString("F2") : "";

                                // 💡 업그레이드 스킬 행에서도 Node Index 제외
                                activeData.Add(new List<string> { bookDisplayName, linkedName, upgradeCost.ToString(), lDmgInfo, lMpStr, lDmgSumStr, lDpcStr, lDpmStr });
                            }
                        }
                        break;

                    case SkillBookBase.NodeType.StatSkill:
                        if (node.statModifiers != null && node.statModifiers.Length > 0)
                        {
                            statNodeCounter++; // 해당 북에서의 스탯 노드 순번 증가

                            Dictionary<string, float> currentStats = new Dictionary<string, float>();
                            foreach (var stat in node.statModifiers)
                            {
                                string sName = stat.statType.ToString();
                                if (!currentStats.ContainsKey(sName)) currentStats[sName] = 0f;
                                currentStats[sName] += stat.statValue;
                            }

                            // 💡 Stat Index에 해당 북의 몇 번째 스탯 노드인지 번호 부여
                            List<string> rowBase = new List<string> { bookDisplayName, statNodeCounter.ToString(), cost > 0 ? cost.ToString() : "" };
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
            HttpResponseMessage response = await client.PostAsync(WebAppUrl, content);

            if (response.IsSuccessStatusCode)
            {
                Debug.Log("Google Sheet Update Complete");
            }
            else
            {
                Debug.LogError($"Failed to update sheet: {response.StatusCode}");
            }
        }
    }

    private static string ConvertToJson(Dictionary<string, List<List<string>>> dataDict)
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
                    string val = kvp.Value[i][j] != null ? kvp.Value[i][j].Replace("\"", "\\\"") : "";
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

    private static void GetDamageData(SkillBase skill, out string damageInfo, out float damageSum)
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
}