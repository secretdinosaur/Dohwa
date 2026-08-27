using UnityEngine;
using UnityEngine.Localization;
using TMPro;

public class LocalizedTMPFont : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI targetText;

    [SerializeField]
    private LocalizedTmpFont localizedFont;

    private void OnEnable()
    {
        localizedFont.AssetChanged += OnFontChanged;
    }

    private void OnDisable()
    {
        localizedFont.AssetChanged -= OnFontChanged;
    }

    private void OnFontChanged(TMP_FontAsset font)
    {
        targetText.font = font;
    }
}