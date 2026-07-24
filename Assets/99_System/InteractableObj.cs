using UnityEngine;
using UnityEngine.Events;

public abstract class InteractableObj : MonoBehaviour
{
    public abstract int Type { get; }
    public GameObject Interactor { get; set; }
    public bool isInteractable = true;
    public virtual void Interact(bool ShowPanelImmediately = true, bool LookPlayer = true)
    {
        UIManager.Instance.InteractPanel.SetActive(false);
    }
}
