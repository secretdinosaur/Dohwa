using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryItem : MonoBehaviour
{
    [Header("Item Settings")]
    public InventoryManager.InventoryType itemType;

    [HideInInspector] public Transform parentAfterDrag;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Canvas canvas;

    public SkillBookBase skillBook;
    public Weapon weapon;
    public Armor armor;
    public Consumable consumable;
    public NonConsumable nonConsumable;
    public Image icon;

    public bool isSettedItem;
    public bool isEquippedSlot = false;
    public bool isQuickSlot = false;


    public TextMeshProUGUI countText;

    private int count = 1;
    public int Count
    {
        get => count;
        set
        {
            count = value;
            if(countText != null)
            {
                if (count > 1)
                {
                    countText.text = count.ToString();
                }
                else if (count == 1)
                {
                    countText.text = "";
                }
                else if (count <= 0)
                {
                    Clear();
                }
            }
        }
    }
    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        canvas = GetComponentInParent<Canvas>();
    }

    public void SetItem(SkillBookBase skillBook)
    {
        Clear();

        Count = 1;
        itemType = InventoryManager.InventoryType.SkillBook;
        this.skillBook = skillBook;
        isSettedItem = true;
        icon.sprite = skillBook.icon;

        Color color = icon.color;
        color.a = 1;
        icon.color = color;
    }

    public void SetItem(Consumable consumable)
    {
        Clear();

        Count = 1;
        itemType = InventoryManager.InventoryType.Item;
        this.consumable = consumable;
        isSettedItem = true;
        icon.sprite = consumable.icon;

        Color color = icon.color;
        color.a = 1;
        icon.color = color;
    }

    public void SetItem(NonConsumable nonConsumable)
    {
        Clear();

        Count = 1;
        itemType = InventoryManager.InventoryType.Item;
        this.nonConsumable = nonConsumable;
        isSettedItem = true;
        icon.sprite = nonConsumable.icon;

        Color color = icon.color;
        color.a = 1;
        icon.color = color;
    }

    public void SetItem(Weapon weapon)
    {
        Clear();

        Count = 1;
        itemType = InventoryManager.InventoryType.Weapon;
        this.weapon = weapon;
        isSettedItem = true;
        icon.sprite = weapon.icon;

        Color color = icon.color;
        color.a = 1;
        icon.color = color;
    }

    public void SetItem(Armor armor)
    {
        Clear();

        Count = 1;
        itemType = InventoryManager.InventoryType.Armor;
        this.armor = armor;
        isSettedItem = true;
        icon.sprite = armor.icon;

        Color color = icon.color;
        color.a = 1;
        icon.color = color;
    }

    public void Clear()
    {
        countText.text = "";
        skillBook = null;
        weapon = null;
        armor = null;
        consumable = null;
        nonConsumable = null;

        icon.sprite = null;

        isSettedItem = false;

        Color c = icon.color;
        c.a = 0;
        icon.color = c;
    }
}