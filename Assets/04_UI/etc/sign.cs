using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Events;
public class sign : InteractableObj
{
    public override int Type => 1;
    [TextArea]
    public string text;
    public Coroutine show;
    public LocalizedString showText;

    [Header("Execution Function After Interact This")]
    public UnityEvent ExecutionFunctionSet = new UnityEvent();

    public override void Interact(bool ShowPanelImmediately = true, bool LookPlayer = true)
    {
        base.Interact();

        string finalizedText = TextTagParser.Parse(showText.GetLocalizedString());
        show = StartCoroutine(UIManager.Instance.ShowSign(finalizedText, ExecutionFunctionSet));
    }
}