using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Events;
using Unity.VisualScripting;
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

        
        if (showText == null || string.IsNullOrEmpty(showText.TableReference.TableCollectionName) ||
    string.IsNullOrEmpty(showText.TableEntryReference.Key))
        {
            ExecutionFunctionSet.Invoke();
        }
        else
        {
            string finalizedText = TextTagParser.Parse(showText.GetLocalizedString());
            show = StartCoroutine(UIManager.Instance.ShowSign(finalizedText, ExecutionFunctionSet));
        }
    }
}