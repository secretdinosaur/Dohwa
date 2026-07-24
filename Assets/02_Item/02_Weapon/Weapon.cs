using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(menuName = "Item/Weapon")]
public class Weapon : ScriptableObject
{
    [Header("Localization")]
    public LocalizedString Localize_weaponName;
    public LocalizedString Localize_ItemDescription;
    public LocalizedString Localize_EffectDescription;

    [SerializeField] public string id;
    public string Id => id;

    public string weaponName => Localize_weaponName.GetLocalizedString();
    public string ItemDescription => Localize_ItemDescription.GetLocalizedString();
    public string EffectDescription => Localize_EffectDescription.GetLocalizedString();

    public Sprite icon;
    public Mesh weaponMesh;
    public bool isDeletable;
    [Header("Stats")]
    public float attack = 10f;
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