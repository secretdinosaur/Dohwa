using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(menuName = "Item/NonConsumable")]
public class NonConsumable : ScriptableObject
{
    [Header("Localization")]
    public LocalizedString Localize_NonConsumableName;
    public LocalizedString Localize_ItemDescription;
    public LocalizedString Localize_EffectDescription;

    [SerializeField] private string id;
    public string Id => id;

    public string NonConsumableName => Localize_NonConsumableName.GetLocalizedString();
    public string ItemDescription => Localize_ItemDescription.GetLocalizedString();
    public string EffectDescription => Localize_EffectDescription.GetLocalizedString();
    public Sprite icon;
    public bool isEatable = false;
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