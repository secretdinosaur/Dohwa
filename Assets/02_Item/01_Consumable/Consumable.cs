using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(menuName = "Item/Consumable")]
public class Consumable : ScriptableObject
{
    [Header("Localization")]
    public LocalizedString Localize_ConsumableName;
    public LocalizedString Localize_ItemDescription;
    public LocalizedString Localize_EffectDescription;

    [SerializeField] public string id;
    public string Id => id;

    public string ConsumableName => Localize_ConsumableName.GetLocalizedString();
    public string ItemDescription => Localize_ItemDescription.GetLocalizedString();
    public string EffectDescription => Localize_EffectDescription.GetLocalizedString();
    public Sprite icon;
    public bool isDeletable;

    public virtual void Use() { }

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