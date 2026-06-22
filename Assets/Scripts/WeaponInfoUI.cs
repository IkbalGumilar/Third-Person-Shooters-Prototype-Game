using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WeaponInfoUI : MonoBehaviour
{
    [Header("Source")]
    public PlayerWeaponEquip weaponEquip;
    public Weapon weaponOverride;

    [Header("Text")]
    public TMP_Text nameText;
    public TMP_Text descriptionText;
    public TMP_Text statsText;
    public Image previewIconImage;

    [Header("Separate Stat Text")]
    public TMP_Text damageText;
    public TMP_Text fireRateText;
    public TMP_Text rangeText;
    public TMP_Text magazineSizeText;
    public TMP_Text typeMachineText;

    [Header("Info Groups")]
    public GameObject weaponInfoGroup;
    public GameObject healingInfoGroup;
    public GameObject ammoInfoGroup;

    [Header("Healing / Status Text")]
    public TMP_Text recoveryText;
    public TMP_Text durationText;
    public TMP_Text effectText;
    public TMP_Text healingTypeText;
    public TMP_Text healingStackText;

    [Header("Ammo Text")]
    public TMP_Text ammoAmountText;
    public TMP_Text ammoTypeText;
    public TMP_Text caliberText;
    public TMP_Text ammoStackText;

    Weapon shownWeapon;
    Weapon previewWeapon;
    InventoryItemData shownItem;
    InventoryItemData previewItem;
    int previewItemAmount = 1;
    int shownItemAmount = 1;
    bool hasPreviewWeapon;
    bool hasPreviewItem;

    void Awake()
    {
        FindMissingReferences();
    }

    void OnEnable()
    {
        Refresh(true);
    }

    void Update()
    {
        Refresh(false);
    }

    public void ShowWeapon(Weapon weapon)
    {
        previewWeapon = weapon;
        previewItem = null;
        hasPreviewWeapon = true;
        hasPreviewItem = false;
        shownWeapon = weapon;
        shownItem = null;
        ApplyWeapon(weapon);
    }

    public void ShowItem(InventoryItemData item)
    {
        ShowItem(item, 1);
    }

    public void ShowItem(InventoryItemData item, int amount)
    {
        previewItem = item;
        previewItemAmount = Mathf.Max(0, amount);
        previewWeapon = null;
        hasPreviewItem = true;
        hasPreviewWeapon = false;
        shownItem = item;
        shownItemAmount = previewItemAmount;
        shownWeapon = null;
        ApplyItem(item, shownItemAmount);
    }

    public void ClearPreviewWeapon()
    {
        previewWeapon = null;
        previewItem = null;
        previewItemAmount = 1;
        shownItemAmount = 1;
        hasPreviewWeapon = false;
        hasPreviewItem = false;
        Refresh(true);
    }

    void Refresh(bool force)
    {
        if (hasPreviewItem)
        {
            if (force || previewItem != shownItem || previewItemAmount != shownItemAmount)
            {
                shownItem = previewItem;
                shownItemAmount = previewItemAmount;
                shownWeapon = null;
                ApplyItem(previewItem, shownItemAmount);
            }

            return;
        }

        Weapon weapon = hasPreviewWeapon
            ? previewWeapon
            : weaponOverride != null
            ? weaponOverride
            : weaponEquip != null ? weaponEquip.CurrentWeapon : null;

        if (!force && weapon == shownWeapon)
        {
            return;
        }

        shownWeapon = weapon;
        ApplyWeapon(weapon);
    }

    void ApplyWeapon(Weapon weapon)
    {
        SetActiveInfoGroup(weapon != null ? InventoryItemType.Weapon : (InventoryItemType?)null);

        if (weapon == null)
        {
            SetText(nameText, "");
            SetText(descriptionText, "");
            SetText(statsText, "");
            SetText(damageText, "0");
            SetText(fireRateText, "0");
            SetText(rangeText, "0");
            SetText(magazineSizeText, "0");
            SetText(typeMachineText, "");
            SetPreviewIcon(null);
            return;
        }

        string weaponName = string.IsNullOrWhiteSpace(weapon.displayName)
            ? weapon.weaponName
            : weapon.displayName;

        SetText(nameText, weaponName);
        SetText(descriptionText, weapon.description);
        SetText(statsText, BuildStatsText(weapon));
        SetText(damageText, FormatNumber(weapon.damage));
        SetText(fireRateText, FormatNumber(weapon.fireRate));
        SetText(rangeText, FormatNumber(weapon.range));
        SetText(magazineSizeText, weapon.magazineSize.ToString());
        SetText(typeMachineText, weapon.Auto ? "Otomatis" : "Manual");
        SetPreviewIcon(weapon.inventoryIcon);
    }

    void ApplyItem(InventoryItemData item, int amount)
    {
        if (item == null)
        {
            ApplyWeapon(null);
            return;
        }

        if (item.weapon != null)
        {
            ApplyWeapon(item.weapon);
            return;
        }

        SetActiveInfoGroup(item.itemType);
        SetText(nameText, item.DisplayName);
        SetText(descriptionText, item.description);
        SetText(statsText, BuildItemStatsText(item, amount));
        SetPreviewIcon(item.DisplayIcon);

        if (item.itemType == InventoryItemType.Weapon)
        {
            SetText(damageText, "0");
            SetText(fireRateText, "0");
            SetText(rangeText, "0");
            SetText(magazineSizeText, "0");
            SetText(typeMachineText, "");
        }
        else if (item.itemType == InventoryItemType.Ammo)
        {
            ApplyAmmoItem(item, amount);
        }
        else
        {
            ApplyHealingItem(item, amount);
        }
    }

    void ApplyHealingItem(InventoryItemData item, int amount)
    {
        SetText(recoveryText, item.healAmount > 0f ? FormatNumber(item.healAmount) : "0");
        SetText(durationText, item.useDuration > 0f ? FormatNumber(item.useDuration) + "s" : "Instant");
        SetText(effectText, BuildEffectList(item));
        SetText(healingTypeText, item.itemType.ToString());
        SetText(healingStackText, Mathf.Max(0, amount).ToString());
    }

    void ApplyAmmoItem(InventoryItemData item, int amount)
    {
        SetText(ammoAmountText, item.ammoAmount.ToString());
        SetText(ammoTypeText, item.ammoType);
        SetText(caliberText, string.IsNullOrWhiteSpace(item.caliber) ? item.ammoType : item.caliber);
        SetText(ammoStackText, Mathf.Max(0, amount).ToString());
    }

    string BuildStatsText(Weapon weapon)
    {
        return
            $"Damage: {FormatNumber(weapon.damage)}\n" +
            $"Range: {FormatNumber(weapon.range)}\n" +
            $"Fire Rate: {FormatNumber(weapon.fireRate)}\n" +
            $"Magazine: {weapon.magazineSize}\n" +
            $"Reload: {FormatNumber(weapon.reloadTime)}s\n" +
            $"Critical: {FormatNumber(weapon.criticalChance)}%\n" +
            $"Stun: {FormatNumber(weapon.stunChance)}%\n" +
            $"Knockback: {FormatNumber(weapon.knockbackChance)}%";
    }

    string FormatNumber(float value)
    {
        return value % 1f == 0f ? value.ToString("0") : value.ToString("0.##");
    }

    string BuildItemStatsText(InventoryItemData item, int amount)
    {
        string text = $"Type: {item.itemType}\nStack: {Mathf.Max(0, amount)}";

        if (item.healAmount > 0f)
        {
            text += $"\nHeal: {FormatNumber(item.healAmount)}";
        }

        if (item.ammoAmount > 0 && item.itemType == InventoryItemType.Ammo)
        {
            text += $"\nAmmo: {item.ammoAmount}\nAmmo Type: {item.ammoType}";
        }

        if (item.statusEffects != null && item.statusEffects.Length > 0)
        {
            text += $"\nEffects: {item.statusEffects.Length}";
        }

        return text;
    }

    string BuildEffectList(InventoryItemData item)
    {
        if (item.statusEffects == null || item.statusEffects.Length == 0)
        {
            return "-";
        }

        string text = "";
        for (int i = 0; i < item.statusEffects.Length; i++)
        {
            StatusEffectData effect = item.statusEffects[i];
            if (effect == null)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(text))
            {
                text += ", ";
            }

            text += !string.IsNullOrWhiteSpace(effect.effectName) ? effect.effectName : effect.name;
        }

        return string.IsNullOrEmpty(text) ? "-" : text;
    }

    void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    void SetPreviewIcon(Sprite icon)
    {
        if (previewIconImage == null)
        {
            return;
        }

        previewIconImage.sprite = icon;
        previewIconImage.enabled = icon != null;
        previewIconImage.preserveAspect = true;
    }

    void SetActiveInfoGroup(InventoryItemType? itemType)
    {
        bool showWeapon = itemType == InventoryItemType.Weapon;
        bool showAmmo = itemType == InventoryItemType.Ammo;
        bool showHealing = itemType == InventoryItemType.HealingItem || itemType == InventoryItemType.StatusEffectItem;

        if (weaponInfoGroup != null) weaponInfoGroup.SetActive(showWeapon);
        if (ammoInfoGroup != null) ammoInfoGroup.SetActive(showAmmo);
        if (healingInfoGroup != null) healingInfoGroup.SetActive(showHealing);
    }

    void FindMissingReferences()
    {
        if (weaponEquip == null)
        {
            weaponEquip = FindAnyObjectByType<PlayerWeaponEquip>();
        }

        if (descriptionText == null)
        {
            descriptionText = FindTextByName("InformationText");
        }

        if (previewIconImage == null)
        {
            GameObject iconObject = GameObject.Find("Icon");
            previewIconImage = iconObject != null ? iconObject.GetComponent<Image>() : null;
        }

        if (damageText == null)
        {
            damageText = FindTextByName("Damage Text");
        }

        if (fireRateText == null)
        {
            fireRateText = FindTextByName("Fire rate Text");
        }

        if (rangeText == null)
        {
            rangeText = FindTextByName("Range Text");
        }

        if (magazineSizeText == null)
        {
            magazineSizeText = FindTextByName("MagazineSize Text");
        }

        if (typeMachineText == null)
        {
            typeMachineText = FindTextByName("TypeMachine Text");
        }

        if (weaponInfoGroup == null)
        {
            weaponInfoGroup = FindChildByName(transform, "WeaponInfoGroub")?.gameObject;
        }

        if (healingInfoGroup == null)
        {
            healingInfoGroup = FindChildByName(transform, "HealingInfoGroub")?.gameObject;
        }

        if (ammoInfoGroup == null)
        {
            ammoInfoGroup = FindChildByName(transform, "AmmoInfoGroub")?.gameObject;
        }

        BindGroupTexts();
    }

    void BindGroupTexts()
    {
        Transform weaponGroup = weaponInfoGroup != null ? weaponInfoGroup.transform : null;
        if (weaponGroup != null)
        {
            if (damageText == null) damageText = FindTextInChildren(weaponGroup, "Damage Text");
            if (fireRateText == null) fireRateText = FindTextInChildren(weaponGroup, "Fire rate Text");
            if (rangeText == null) rangeText = FindTextInChildren(weaponGroup, "Range Text");
            if (magazineSizeText == null) magazineSizeText = FindTextInChildren(weaponGroup, "MagazineSize Text");
            if (typeMachineText == null) typeMachineText = FindTextInChildren(weaponGroup, "TypeMachine Text");
        }

        Transform healingGroup = healingInfoGroup != null ? healingInfoGroup.transform : null;
        if (healingGroup != null)
        {
            if (recoveryText == null) recoveryText = FindTextInChildren(healingGroup, "Recovery Text");
            if (durationText == null) durationText = FindTextInChildren(healingGroup, "Duration Text");
            if (effectText == null) effectText = FindTextInChildren(healingGroup, "Effect Text");
            if (healingTypeText == null) healingTypeText = FindTextInChildren(healingGroup, "Type Text");
            if (healingStackText == null) healingStackText = FindTextInChildren(healingGroup, "StackText");
        }

        Transform ammoGroup = ammoInfoGroup != null ? ammoInfoGroup.transform : null;
        if (ammoGroup != null)
        {
            if (ammoAmountText == null) ammoAmountText = FindTextInChildren(ammoGroup, "Ammo Text");
            if (ammoTypeText == null) ammoTypeText = FindTextInChildren(ammoGroup, "Ammp Type Text");
            if (caliberText == null) caliberText = FindTextInChildren(ammoGroup, "Caliber Text");
            if (ammoStackText == null) ammoStackText = FindTextInChildren(ammoGroup, "Stack Text");
        }
    }

    TMP_Text FindTextByName(string objectName)
    {
        GameObject target = GameObject.Find(objectName);
        return target != null ? target.GetComponent<TMP_Text>() : null;
    }

    TMP_Text FindTextInChildren(Transform root, string objectName)
    {
        Transform child = FindChildByName(root, objectName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    Transform FindChildByName(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            Transform found = FindChildByName(child, childName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
