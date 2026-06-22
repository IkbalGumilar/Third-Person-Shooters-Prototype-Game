using UnityEngine;

public enum InventoryItemType
{
    Weapon,
    Ammo,
    HealingItem,
    StatusEffectItem
}

[CreateAssetMenu(fileName = "Inventory Item", menuName = "Scriptable Objects/Inventory Item")]
public class InventoryItemData : ScriptableObject
{
    [Header("Identity")]
    public string itemName = "Inventory Item";
    public InventoryItemType itemType;
    public Sprite icon;
    public GameObject itemPrefab;
    public int maxStack = 99;
    [Min(1)] public int inventoryWidth = 1;
    [Min(1)] public int inventoryHeight = 1;
    public bool canRotateInInventory = true;
    [TextArea(2, 6)] public string description;

    [Header("Weapon")]
    public Weapon weapon;

    [Header("Ammo")]
    public string ammoType = "Default";
    public string caliber = "";
    public int ammoAmount = 1;

    [Header("Healing")]
    public float healAmount = 25f;
    public float useDuration = 0f;

    [Header("Status Effects")]
    public StatusEffectData[] statusEffects;
    public bool consumeOnUse = true;

    public string DisplayName => !string.IsNullOrWhiteSpace(itemName) ? itemName : name;
    public Sprite DisplayIcon => icon != null ? icon : weapon != null ? weapon.inventoryIcon : null;
}
