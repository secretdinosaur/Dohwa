using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(menuName = "Item/Armor")]
public class Armor : ScriptableObject
{
    [Header("Localization")]
    public LocalizedString Localize_armorName;
    public LocalizedString Localize_ItemDescription;
    public LocalizedString Localize_EffectDescription;

    [SerializeField] public string id;
    public string Id => id;

    public string armorName => Localize_armorName.GetLocalizedString();
    public string ItemDescription => Localize_ItemDescription.GetLocalizedString();
    public string EffectDescription => Localize_EffectDescription.GetLocalizedString();
    public Sprite icon;
    public bool isDeletable;
    [Header("Stats")]
    public float defense = 10f;
    public float hp = 10f;
    [Header("Equip Passive")]
    public PassiveSkillData equipPassive;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(id))
        {
            id = System.Guid.NewGuid().ToString();
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}