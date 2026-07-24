using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using static InventoryManager;

public class DataManager : MonoBehaviour
{
    public static DataManager Instance;

    private bool isResetting = false;

    [Header("Default Equipment")]
    public Weapon defaultWeapon;

    private Dictionary<string, SkillBookBase> skillBookDict = new Dictionary<string, SkillBookBase>();
    private Dictionary<string, Weapon> weaponDict = new Dictionary<string, Weapon>();
    private Dictionary<string, Armor> armorDict = new Dictionary<string, Armor>();
    private Dictionary<string, Consumable> consumableDict = new Dictionary<string, Consumable>();
    private Dictionary<string, NonConsumable> nonConsumableDict = new Dictionary<string, NonConsumable>();

    public SkillBookBase GetSkillBook(string id) { if (skillBookDict.TryGetValue(id, out SkillBookBase skillBook)) { return skillBook; } else { return null; } }
    public Weapon GetWeapon(string id) { if (weaponDict.TryGetValue(id, out Weapon weapon)) { return weapon; } else { return null; } }
    public Armor GetArmor(string id) { if (armorDict.TryGetValue(id, out Armor armor)) { return armor; } else { return null; } }
    public Consumable GetConsumable(string id) { if (consumableDict.TryGetValue(id, out Consumable consumable)) { return consumable; } else { return null; } }
    public NonConsumable GetNonConsumable(string id) { if (nonConsumableDict.TryGetValue(id, out NonConsumable nonConsumable)) { return nonConsumable; } else { return null; } }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            CacheAllResources();
        }
    }

    private void CacheAllResources()
    {
        foreach (var b in Resources.LoadAll<SkillBookBase>("").Where(x => x != null)) skillBookDict[b.Id] = b;
        foreach (var w in Resources.LoadAll<Weapon>("").Where(x => x != null)) weaponDict[w.Id] = w;
        foreach (var a in Resources.LoadAll<Armor>("").Where(x => x != null)) armorDict[a.Id] = a;
        foreach (var f in Resources.LoadAll<Consumable>("").Where(x => x != null)) consumableDict[f.Id] = f;
        foreach (var imp in Resources.LoadAll<NonConsumable>("").Where(x => x != null)) nonConsumableDict[imp.Id] = imp;
    }

    private void Start()
    {
        StartCoroutine(InitializeData());
    }

    public void ResetData()
    {
        isResetting = true;
        BackupSettingsData();
        PlayerPrefs.DeleteAll();
        DeleteAllFiles();
        RestoreSettingsData();
        PlayerPrefs.Save();
        if(PeachTree.Instance != null)
            PeachTree.Instance.SetInitialStage(0);
        ResetScriptableObjects();
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void DeleteAllFiles()
    {
        string[] fileNames = {
        "Stats.data",
        "SkillBook.data",
        "Settings.data",
        "Inventory.data"
        };

        foreach (string fileName in fileNames)
        {
            string path = Path.Combine(Application.persistentDataPath, fileName);

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    Debug.Log($"파일 삭제 완료: {fileName}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"{fileName} 파일 삭제 중 오류 발생: {e.Message}");
            }
        }
    }

    public bool IsTutorialCompleted(TutorialManager.TutorialType type)
    {
        string key = $"{type}Tutorial";
        return PlayerPrefs.GetInt(key, 0) == 1;
    }

    public void CompleteTutorial(TutorialManager.TutorialType type)
    {
        string key = $"{type}Tutorial";
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();
    }

    private float backup_SFX_Volume;
    private float backup_BGM_Volume;
    private float backup_Master_Volume;
    private float backup_Sensitivity;
    private int backup_LocaleIndex;
    private int backup_ResolutionIndex;
    private int backup_ScreenModeIndex;

    private void BackupSettingsData()
    {
        string path = Path.Combine(Application.persistentDataPath, "Settings.data");

        if (!File.Exists(path))
        {
            backup_SFX_Volume = 0.5f;
            backup_BGM_Volume = 0.5f;
            backup_Master_Volume = 0.5f;
            backup_Sensitivity = 0.5f;
            backup_LocaleIndex = 1;
            backup_ResolutionIndex = 0;
            backup_ScreenModeIndex = 2;
            return;
        }

        // 파일이 존재하면 읽기 진행
        string loadText = File.ReadAllText(path);
        string[] data = loadText.Split('/');

        // 데이터 개수 검증 (데이터가 깨졌을 때를 대비한 안전장치)
        if (data.Length < 7)
        {
            Debug.LogError("Settings.data 파일이 손상되었습니다. 백업을 취소합니다.");
            return;
        }

        // 파일에서 읽은 데이터 파싱
        float sfx = float.Parse(data[0]);
        float bgm = float.Parse(data[1]);
        float master = float.Parse(data[2]);
        float sens = float.Parse(data[3]);
        int locale = int.Parse(data[4]);
        int res = int.Parse(data[5]);
        int screen = int.Parse(data[6]);

        // 파일 값으로 백업 변수 채우기
        backup_SFX_Volume = sfx;
        backup_BGM_Volume = bgm;
        backup_Master_Volume = master;
        backup_Sensitivity = sens;
        backup_LocaleIndex = locale;
        backup_ResolutionIndex = res;
        backup_ScreenModeIndex = screen;
    }

    private void RestoreSettingsData()
    {
        string path = Path.Combine(Application.persistentDataPath, "Settings.data");
        StringBuilder sb = new StringBuilder();

        sb.Append(backup_SFX_Volume).Append("/")
          .Append(backup_BGM_Volume).Append("/")
          .Append(backup_Master_Volume).Append("/")
          .Append(backup_Sensitivity).Append("/")
          .Append(backup_LocaleIndex).Append("/")
          .Append(backup_ResolutionIndex).Append("/")
          .Append(backup_ScreenModeIndex);
    }

    void ResetScriptableObjects()
    {
        foreach (var book in skillBookDict.Values)
        {
            if (book.skillTree != null)
            {
                for (int i = 0; i < book.skillTree.Length; i++)
                    book.skillTree[i].unlocked = false;
            }
        }
    }
    public bool IsLoading { get; private set; } = false;
    IEnumerator InitializeData()
    {
        IsLoading = true; // 로딩 시작

        yield return null;
        LoadSettingsData();

        if (SceneManager.GetActiveScene().name == "Main")
        {
            IsLoading = false;
            yield break;
        }

        LoadStatData();

        LoadInventoryData();

        CheckAndGiveStartingSkillBook();

        LoadSkillBookData();

        IsLoading = false; // 로딩 완전 종료

        StartCoroutine(SaveRoutine());
    }

    private void CheckAndGiveStartingSkillBook()
    {
        // 이미 보유 중인지 확인 (DataManager 캐시 사용)
        string startBookId = "e3d10f5f-9ac4-4074-8a34-5f22747421dc";
        if (skillBookDict.TryGetValue(startBookId, out SkillBookBase startBook))
        {
            // 이미 획득한 적이 있는지 세이브 체크 (획득한 적이 없으면 지급)
            if (PlayerPrefs.GetInt($"Obtained_SkillBook_{startBookId}", 0) == 0)
            {
                // 인벤토리 매니저를 통해 지급
                if (InventoryManager.Instance != null)
                {
                    startBook.UnlockNode(0);
                    startBook.UnlockNode(1);
                    startBook.UnlockNode(2);
                    InventoryManager.Instance.AddItem(startBook);
                    PlayerPrefs.SetInt($"Obtained_SkillBook_{startBookId}", 1);
                    PlayerPrefs.Save();
                }
            }
        }
    }
    IEnumerator SaveRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(600f);
            AllSave();
        }
    }

    public void AllSave()
    {
        SaveSettingsData();
        Scene currentScene = SceneManager.GetActiveScene();
        string sceneName = currentScene.name;
        if (sceneName == "Main") return;
        SaveStatData();
        SaveInventoryData();
        SaveSkillBookData();
    }

    public void SaveStatData()
    {
        

        string path = Path.Combine(Application.persistentDataPath, "Stats.data");

        StringBuilder sb = new StringBuilder();

        if (DungeonManager.Instance != null)
        {
            string unlockedStr = string.Join(",", DungeonManager.Instance.unlockedCheckpoints);
            sb.Append(unlockedStr);
            sb.Append("/");
            sb.Append(DungeonManager.Instance.currentRespawnIndex);
            sb.Append("/");
        }

        sb.Append(StatManager.Instance.StatPoint);
        sb.Append("/");
        sb.Append(StatManager.Instance.earnedStatPoint);
        sb.Append("/");
        sb.Append(GradeManager.Instance.CurrentGradeIndex);
        sb.Append("/");
        sb.Append(GradeManager.Instance.CurrentExp);
        sb.AppendLine();

        try
        {
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"데이터 저장 완료: {path}");
        }
        catch (Exception e)
        {
            Debug.LogError($"데이터 저장 실패: {e.Message}");
        }
    }

    public void LoadStatData()
    {
        string path = Path.Combine(Application.persistentDataPath, "Stats.data");

        if (!File.Exists(path)) return;

        string loadText = File.ReadAllText(path);
        string[] data = loadText.Split('/');

        if (DungeonManager.Instance != null)
        {
            DungeonManager.Instance.unlockedCheckpoints.Clear();
            string unlockedStr = data[0];

            if (!string.IsNullOrEmpty(unlockedStr))
            {
                string[] strArr = unlockedStr.Split(',');
                foreach (string s in strArr)
                {
                    DungeonManager.Instance.unlockedCheckpoints.Add(int.Parse(s));
                }
            }

            DungeonManager.Instance.currentRespawnIndex = int.Parse(data[1]);

            if (!DungeonManager.Instance.unlockedCheckpoints.Contains(0))
            {
                DungeonManager.Instance.unlockedCheckpoints.Add(0);
            }

            if (DungeonManager.Instance.currentRespawnIndex == -1)
            {
                DungeonManager.Instance.currentRespawnIndex = 0;
            }
        }

        StatManager.Instance.StatPoint = int.Parse(data[2]);
        StatManager.Instance.earnedStatPoint = int.Parse(data[3]);
        GradeManager.Instance.CurrentGradeIndex = int.Parse(data[4]);
        GradeManager.Instance.CurrentExp = float.Parse(data[5]);

        GradeManager.Instance.InitializeGradeAbility();
    }

    private void OnApplicationQuit()
    {
        if (!isResetting) AllSave();
    }

    void SaveInventoryData()
    {
        List<InventorySlot> anotherSlots = new List<InventorySlot>
        {
            InventoryManager.Instance.EquippedWeaponSlot,
            InventoryManager.Instance.EquippedArmorSlot
        };

        List<SaveItemData> AllItems = CreateSaveItemData(InventoryManager.Instance.WeaponSlots.ToArray())
            .Concat(CreateSaveItemData(InventoryManager.Instance.ArmorSlots.ToArray()))
            .Concat(CreateSaveItemData(InventoryManager.Instance.SkillBookSlots.ToArray()))
            .Concat(CreateSaveItemData(InventoryManager.Instance.ItemSlots.ToArray()))
            .Concat(CreateSaveItemData(anotherSlots.ToArray()))
            .Concat(CreateSaveItemData(UIManager.Instance.GameQuickSlots))
            .ToList();

        string path = Path.Combine(Application.persistentDataPath, "Inventory.data");

        StringBuilder sb = new StringBuilder();

        foreach (var item in AllItems)
        {
            sb.Append(item.type);
            sb.Append("/");
            sb.Append(item.id);
            sb.Append("/");
            sb.Append(item.assetName);
            sb.Append("/");
            sb.Append(item.count);
            sb.Append("/");
            sb.Append(item.isEquippedSlot);
            sb.Append("/");
            sb.Append(item.isQuickSlot);
            sb.AppendLine();
        }

        try
        {
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"데이터 저장 완료: {path}");
        }
        catch (Exception e)
        {
            Debug.LogError($"데이터 저장 실패: {e.Message}");
        }
    }

    List<SaveItemData> CreateSaveItemData(InventorySlot[] Slots)
    {
        List<SaveItemData> saveItemDatas = new List<SaveItemData>();
        foreach (InventorySlot slot in Slots)
        {
            InventoryItem item = slot.item;
            if (item == null) continue;

            int type = (int)item.itemType;
            string id = "-1";
            string assetName = "null";

            switch (item.itemType)
            {
                case InventoryType.Weapon:
                    if (item.weapon == null) continue;
                    id = item.weapon.Id;
                    assetName = item.weapon.name;
                    break;
                case InventoryType.Armor:
                    if (item.armor == null) continue;
                    id = item.armor.Id;
                    assetName = item.armor.name;
                    break;
                case InventoryType.SkillBook:
                    if (item.skillBook == null) continue;
                    id = item.skillBook.Id;
                    assetName = item.skillBook.name;
                    break;
                case InventoryType.Item:
                    if (item.consumable == null && item.nonConsumable == null) continue;
                    if(item.consumable != null)
                    {
                        id = item.consumable.Id;
                        assetName = item.consumable.name;
                    }
                    else if(item.nonConsumable != null)
                    {
                        id = item.nonConsumable.Id;
                        assetName = item.nonConsumable.name;
                    }
                    break;
            }

            SaveItemData data = new SaveItemData(
                type,
                id,
                assetName,
                item.Count,
                item.isEquippedSlot ? 1 : 0,
                item.isQuickSlot ? 1 : 0
            );
            saveItemDatas.Add(data);
        }
        return saveItemDatas;
    }

    void LoadInventoryData()
    {
        if (UIManager.Instance == null || UIManager.Instance.Inventory == null || InventoryManager.Instance == null)
            return;

        string path = Path.Combine(Application.persistentDataPath, "Inventory.data");
        if (!File.Exists(path))
        {
            if (defaultWeapon != null && InventoryManager.Instance.EquippedWeaponSlot != null && InventoryManager.Instance.EquippedWeaponSlot.item)
            {
                InventoryManager.Instance.EquippedWeaponSlot.item.SetItem(defaultWeapon);
                InventoryManager.Instance.EquipWeapon(defaultWeapon);
            }
            return;
        }

        // 슬롯 초기화
        List<InventorySlot> WeaponSlots = InventoryManager.Instance.WeaponSlots;
        List<InventorySlot> ArmorSlots = InventoryManager.Instance.ArmorSlots;
        List<InventorySlot> SkillBookSlots = InventoryManager.Instance.SkillBookSlots;
        List<InventorySlot> ItemSlots = InventoryManager.Instance.ItemSlots;

        InventorySlot EquippedWeaponSlot = InventoryManager.Instance.EquippedWeaponSlot;
        InventorySlot EquippedArmorSlot = InventoryManager.Instance.EquippedArmorSlot;
        InventorySlot[] QuickSlots = UIManager.Instance.GameQuickSlots;

        List<InventorySlot> AllSlots = WeaponSlots
            .Concat(ArmorSlots)
            .Concat(SkillBookSlots)
            .Concat(ItemSlots)
            .ToList();

        foreach (var s in AllSlots)
        {
            if (s != null && s.item != null) s.item.Clear();
        }
        if (EquippedWeaponSlot != null && EquippedWeaponSlot.item != null) EquippedWeaponSlot.item.Clear();
        if (EquippedArmorSlot != null && EquippedArmorSlot.item != null) EquippedArmorSlot.item.Clear();
        foreach (var qs in QuickSlots)
        {
            if (qs != null && qs.item != null) qs.item.Clear();
        }

        string[] lines = File.ReadAllLines(path);
        int quickSlotIndex = 0;

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] data = line.Split('/');
            if (data.Length < 6) continue;

            int type = int.Parse(data[0]);
            string idValue = data[1];
            string assetName = data[2];
            int itemCount = int.Parse(data[3]);
            int isEquippedSlot = int.Parse(data[4]);
            int isQuickSlot = int.Parse(data[5]);

            // 저장 시 "-1" 또는 "null" 문자열도 비어있는 값으로 처리되도록 검사 강화
            bool idEmpty = string.IsNullOrEmpty(idValue) || idValue == "-1";
            bool nameEmpty = string.IsNullOrEmpty(assetName) || assetName == "null";
            if ((idEmpty && nameEmpty) || itemCount <= 0) continue;

            switch (type)
            {
                case 0: // Weapon
                    var weapon = FindWeaponByName(idValue) ?? weaponDict.Values.FirstOrDefault(w => w.name == assetName);
                    if (weapon != null)
                    {
                        if (isEquippedSlot == 1 && EquippedWeaponSlot != null)
                        {
                            EquippedWeaponSlot.item.SetItem(weapon);
                            var rt = EquippedWeaponSlot.item.GetComponent<RectTransform>();
                            if (rt) rt.anchoredPosition = Vector2.zero;
                            InventoryManager.Instance.EquipWeapon(weapon);
                        }
                        else
                        {
                            InventoryManager.Instance.AddItem(weapon);
                        }
                    }
                    break;

                case 1: // Armor
                    var armor = FindArmorByName(idValue) ?? armorDict.Values.FirstOrDefault(a => a.name == assetName);
                    if (armor != null)
                    {
                        if (isEquippedSlot == 1 && EquippedArmorSlot != null)
                        {
                            EquippedArmorSlot.item.SetItem(armor);
                            var rt = EquippedArmorSlot.item.GetComponent<RectTransform>();
                            if (rt) rt.anchoredPosition = Vector2.zero;
                            InventoryManager.Instance.EquipArmor(armor);
                        }
                        else
                        {
                            InventoryManager.Instance.AddItem(armor);
                        }
                    }
                    break;

                case 2: // SkillBook
                    var skillBook = FindSkillBookByName(idValue) ?? skillBookDict.Values.FirstOrDefault(b => b.name == assetName);
                    if (skillBook != null)
                    {
                        InventoryManager.Instance.AddItem(skillBook);
                    }
                    break;

                case 3: // Item (Consumable / NonConsumable)
                    // 먼저 Consumable로 시도하고, 없으면 NonConsumable로 시도 — 둘 다 처리하는 오류 방지
                    var consumable = FindConsumableByName(idValue) ?? consumableDict.Values.FirstOrDefault(f => f.name == assetName);
                    if (consumable != null)
                    {
                        if (isQuickSlot == 1 && quickSlotIndex < QuickSlots.Length)
                        {
                            QuickSlots[quickSlotIndex].item.SetItem(consumable);
                            QuickSlots[quickSlotIndex].item.Count = itemCount;
                            QuickSlots[quickSlotIndex].GetComponent<QuickSlot>().SetSlot();
                            quickSlotIndex++;
                        }
                        else
                        {
                            for (int i = 0; i < itemCount; i++)
                            {
                                InventoryManager.Instance.AddItem(consumable);
                            }
                        }
                        break;
                    }

                    var nonConsumable = FindNonConsumableByName(idValue) ?? nonConsumableDict.Values.FirstOrDefault(imp => imp.name == assetName);
                    if (nonConsumable != null)
                    {
                        if (isQuickSlot == 1 && quickSlotIndex < QuickSlots.Length)
                        {
                            QuickSlots[quickSlotIndex].item.SetItem(nonConsumable);
                            QuickSlots[quickSlotIndex].item.Count = itemCount;
                            QuickSlots[quickSlotIndex].GetComponent<QuickSlot>().SetSlot();
                            quickSlotIndex++;
                        }
                        for (int i = 0; i < itemCount; i++)
                        {
                            InventoryManager.Instance.AddItem(nonConsumable);
                        }
                    }
                    else
                    {
                        // 어떤 타입도 아니었지만 QuickSlot 자리로 저장된 경우 인덱스 맞추기 위해 증가
                        if (isQuickSlot == 1 && quickSlotIndex < QuickSlots.Length)
                            quickSlotIndex++;
                    }
                    break;
            }
        }

        if (EquippedWeaponSlot != null && EquippedWeaponSlot.item.weapon == null && defaultWeapon != null)
        {
            EquippedWeaponSlot.item.SetItem(defaultWeapon);
            InventoryManager.Instance.EquipWeapon(defaultWeapon);
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.SyncGameQuickSlots();
        }
    }

    SkillBookBase FindSkillBookByName(string bookName) => skillBookDict.TryGetValue(bookName, out var item) ? item : null;
    Weapon FindWeaponByName(string weaponName) => weaponDict.TryGetValue(weaponName, out var item) ? item : null;
    Armor FindArmorByName(string armorName) => armorDict.TryGetValue(armorName, out var item) ? item : null;
    Consumable FindConsumableByName(string consumableName) => consumableDict.TryGetValue(consumableName, out var item) ? item : null;
    NonConsumable FindNonConsumableByName(string nonConsumableName) => nonConsumableDict.TryGetValue(nonConsumableName, out var item) ? item : null;

    void SaveSkillBookData()
    {
        string path = Path.Combine(Application.persistentDataPath, "SkillBook.data");
        StringBuilder sb = new StringBuilder();

        foreach (var book in skillBookDict.Values)
        {
            if (book.skillTree == null) continue;

            sb.Append(book.Id);
            sb.Append("/");

            for (int i = 0; i < book.skillTree.Length; i++)
            {
                sb.Append(book.skillTree[i].unlocked ? "1" : "0");
                if (i < book.skillTree.Length - 1)
                {
                    sb.Append(",");
                }
            }

            sb.AppendLine();
        }

        try
        {
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"데이터 저장 완료: {path}");
        }
        catch (Exception e)
        {
            Debug.LogError($"데이터 저장 실패: {e.Message}");
        }
    }

    void LoadSkillBookData()
    {
        string path = Path.Combine(Application.persistentDataPath, "SkillBook.data");

        if (!File.Exists(path)) return;

        string[] lines = File.ReadAllLines(path);

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] data = line.Split('/');
            if (data.Length < 2) continue;

            string bookId = data[0];
            string[] nodeStatus = data[1].Split(',');

            if (skillBookDict.TryGetValue(bookId, out var book) && book.skillTree != null)
            {
                for (int i = 0; i < book.skillTree.Length; i++)
                {
                    var node = book.skillTree[i];
                    bool isUnlocked = false;

                    if (i < nodeStatus.Length)
                    {
                        isUnlocked = (nodeStatus[i] == "1");
                    }

                    if (node.isStartingNode)
                    {
                        isUnlocked = true;
                    }

                    node.unlocked = isUnlocked;

                    if (node.unlocked)
                    {
                        ApplyNodeEffects(node);
                    }
                }
            }
        }
    }

    private void ApplyNodeEffects(SkillBookBase.SkillTreeNode node)
    {
        switch (node.nodeType)
        {
            case SkillBookBase.NodeType.ActiveSkill:
                if (node.activeSkill != null && Player.Instance != null && !Player.Instance.PlayerAttack.acquiredSkills.Contains(node.activeSkill))
                    Player.Instance.PlayerAttack.acquiredSkills.Add(node.activeSkill);
                break;
            case SkillBookBase.NodeType.PassiveSkill:
                if (node.passiveSkill != null && node.passiveSkill.skillLogic != null) node.passiveSkill.skillLogic.Apply();
                break;
            case SkillBookBase.NodeType.StatSkill:
                if (node.statModifiers != null)
                    foreach (var mod in node.statModifiers) StatManager.Instance.ApplySingleStat(mod.statType, mod.statValue);
                break;
        }
    }

    private bool IsPassiveReady()
    {
        return StatManager.Instance != null && Player.Instance != null && Player.Instance.PlayerAttack != null;
    }

    public void SaveSettingsData()
    {
        string path = Path.Combine(Application.persistentDataPath, "Settings.data");
        StringBuilder sb = new StringBuilder();

        float sfx = SoundManager.Instance != null ? SoundManager.Instance.sfxVolume : 0.5f;
        float bgm = SoundManager.Instance != null ? SoundManager.Instance.bgmVolume : 0.5f;
        float master = SoundManager.Instance != null ? SoundManager.Instance.masterVolume : 0.5f;

        float sens = (SettingManager.Instance != null && SettingManager.Instance.sensitivitySlider != null)
            ? SettingManager.Instance.sensitivitySlider.value
            : 0.5f;

        int localeIndex = 1;
        if (SettingManager.Instance != null)
        {
            localeIndex = SettingManager.Instance.currentLocaleIndex;
        }
        else
        {
            var locales = LocalizationSettings.AvailableLocales.Locales;
            var sel = LocalizationSettings.SelectedLocale;
            if (sel != null)
                localeIndex = locales.IndexOf(sel);
        }
        if (localeIndex < 0) localeIndex = 1;

        int res = 0;
        int screen = 2;
        if (SettingManager.Instance != null)
        {
            if (SettingManager.Instance.resoltionDropdown != null)
                res = SettingManager.Instance.resoltionDropdown.value;

            if (SettingManager.Instance.screenDropdown != null)
                screen = SettingManager.Instance.screenDropdown.value;
        }

        sb.Append(sfx).Append("/")
          .Append(bgm).Append("/")
          .Append(master).Append("/")
          .Append(sens).Append("/")
          .Append(localeIndex).Append("/")
          .Append(res).Append("/")
          .Append(screen);

        try
        {
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"데이터 저장 완료: {path}");
        }
        catch (Exception e)
        {
            Debug.LogError($"데이터 저장 실패: {e.Message}");
        }
    }

    public void LoadSettingsData()
    {
        string path = Path.Combine(Application.persistentDataPath, "Settings.data");

        // 1. 세이브 파일이 없을 경우 (최초 실행 시) 기본값 강제 적용
        if (!File.Exists(path))
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.SetSFXVolume(0.5f);
                SoundManager.Instance.SetBGMVolume(0.5f);
                SoundManager.Instance.SetMasterVolume(0.5f);
            }
            if (SettingManager.Instance != null)
            {
                SettingManager.Instance.SyncUIs(0.5f, 0.5f, 0.5f, 0.5f, 1, 0, 2);
            }
            return; // 기본값 적용 후 종료
        }

        // 2. 세이브 파일이 있을 경우 기존 데이터 불러오기
        string loadText = File.ReadAllText(path);
        string[] data = loadText.Split('/');

        if (data.Length < 7) return;

        float sfx = float.Parse(data[0]);
        float bgm = float.Parse(data[1]);
        float master = float.Parse(data[2]);
        float sens = float.Parse(data[3]);
        int locale = int.Parse(data[4]);
        int res = int.Parse(data[5]);
        int screen = int.Parse(data[6]);

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.SetSFXVolume(sfx);
            SoundManager.Instance.SetBGMVolume(bgm);
            SoundManager.Instance.SetMasterVolume(master);
        }

        if (SettingManager.Instance != null)
        {
            SettingManager.Instance.SyncUIs(sfx, bgm, master, sens, locale, res, screen);
        }
    }

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    private void OnLocaleChanged(Locale newLocale)
    {
        Debug.Log($"[Localization] 언어 변경됨: {newLocale.LocaleName}");
        Debug.Log($"[Localization] 호출 스택:\n{System.Environment.StackTrace}");
    }

    public bool IsQuestAccepted(string eventKey)
    {
        return PlayerPrefs.GetInt(eventKey, 0) == 1;
    }
    public void SetQuestAccepted(string eventKey)
    {
        PlayerPrefs.SetInt(eventKey, 1);
        PlayerPrefs.Save();
    }


    public bool IsEventCleared(string eventKey)
    {
        return PlayerPrefs.GetInt(eventKey, 0) == 1;
    }

    public void SetEventCleared(string eventKey)
    {
        PlayerPrefs.SetInt(eventKey, 1);
        PlayerPrefs.Save();
    }

    private HashSet<string> obtainedOneTimeItems = new HashSet<string>();

    public bool IsItemObtained(int itemIndex)
    {
        return PlayerPrefs.GetInt("OneTimeItem_" + itemIndex, 0) == 1;
    }

    public void MarkItemAsObtained(int itemIndex)
    {
        PlayerPrefs.SetInt("OneTimeItem_" + itemIndex, 1);
        PlayerPrefs.Save();
    }
    public bool IsCutscenePlayed(string cutsceneId)
    {
        return PlayerPrefs.GetInt($"CutscenePlayed_{cutsceneId}", 0) == 1;
    }

    public void MarkCutsceneAsPlayed(string cutsceneId)
    {
        PlayerPrefs.SetInt($"CutscenePlayed_{cutsceneId}", 1);
        PlayerPrefs.Save();
    }
}
class SaveItemData
{
    public int type;
    public string id;
    public string assetName;
    public int count;
    public int isEquippedSlot;
    public int isQuickSlot;

    public SaveItemData(int type, string id, string assetName, int count, int isEquippedSlot, int isQuickSlot)
    {
        this.type = type;
        this.id = id;
        this.assetName = assetName;
        this.count = count;
        this.isEquippedSlot = isEquippedSlot;
        this.isQuickSlot = isQuickSlot;
    }
}