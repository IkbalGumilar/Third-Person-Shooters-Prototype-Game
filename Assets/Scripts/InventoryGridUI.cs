using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[System.Serializable]
public class InventoryEntry
{
    public InventoryItemData itemData;
    public Weapon weapon;
    public GameObject weaponObject;
    public int amount = 1;

    public Weapon ResolvedWeapon => weapon != null ? weapon : itemData != null ? itemData.weapon : null;
    public Sprite Icon => itemData != null ? itemData.DisplayIcon : ResolvedWeapon != null ? ResolvedWeapon.inventoryIcon : null;
    public string DisplayName => itemData != null ? itemData.DisplayName : ResolvedWeapon != null ? ResolvedWeapon.weaponName : "Item";
    public bool IsEmpty => itemData == null && ResolvedWeapon == null;
}

public class InventoryGridUI : MonoBehaviour
{
    const string GeneratedSlotPrefix = "Generated Slot ";

    public static int LastClosedByCancelFrame { get; private set; }
    public static bool IsAnyInventoryOpen { get; private set; }

    [Header("Source")]
    public PlayerWeaponEquip weaponEquip;
    public WeaponInfoUI weaponInfoUI;
    public PlayerHealth playerHealth;
    public PlayerStatusEffectController statusEffectController;
    public InventoryEntry[] inventory;
    public bool usePlayerWeaponSlotsAsFallback = true;
    public bool syncToPlayerWeaponEquip = true;

    [Header("Open / Close")]
    public GameObject inventoryRoot;
    public KeyCode toggleKey = KeyCode.I;
    public KeyCode closeKey = KeyCode.Escape;
    public KeyCode useSelectedKey = KeyCode.U;
    public bool startClosed = true;
    public bool closeWhenPlayerDamaged = true;

    [Header("Grid")]
    public RectTransform slotRoot;
    public RectTransform slotTemplate;
    public RectTransform itemRoot;
    public int gridWidth = 6;
    public int gridHeight = 4;
    public Vector2 slotSpacing = new Vector2(128f, -128f);
    public Vector2 itemSlotVisualSize = new Vector2(100f, 100f);
    public bool stretchItemIconsToGrid = true;
    public string slotKeyLabelName = "Key";
    public Color selectedColor = new Color(1f, 0.86f, 0.28f, 1f);
    public Color equippedColor = new Color(0.35f, 0.75f, 1f, 1f);
    public Color normalColor = Color.white;

    [Header("Throw Away")]
    public RectTransform throwAwayDropZone;
    public Transform worldDropOrigin;
    public GameObject pickupEffectPrefab;
    public float throwAwayDropForwardDistance = 1.4f;
    public float throwAwayDropHeightOffset = 0.35f;
    public float thrownPickupRadius = 2f;
    public string thrownPickupLayerName = "Pickup";
    public Color thrownAmmoColor = Color.white;
    public Color thrownHealingColor = new Color(0.15f, 1f, 0.25f, 1f);
    public Color thrownWeaponColor = new Color(0.25f, 0.55f, 1f, 1f);
    public Color thrownStatusEffectColor = new Color(0.75f, 0.35f, 1f, 1f);
    public Color thrownDefaultColor = new Color(1f, 0.9f, 0.25f, 1f);
    public float throwAwayHoverScale = 1.5f;
    public Color throwAwayDragColor = new Color(1f, 0.15f, 0.08f, 1f);

    [Header("Context Menu")]
    public Vector2 contextMenuSize = new Vector2(180f, 220f);
    public Vector2 contextMenuButtonSize = new Vector2(160f, 30f);
    public Color contextMenuColor = new Color(0.08f, 0.08f, 0.08f, 0.94f);
    public Color contextMenuButtonColor = new Color(0.16f, 0.16f, 0.16f, 1f);
    public Color contextMenuTextColor = Color.white;

    [Header("Notifications")]
    public TMP_Text notificationText;
    public string inventoryFullMessage = "Inventory Full";
    public string lowHealthHealingPromptMessage = "Use Healing Item: U";
    public string noHealingItemMessage = "No Healing Item";
    public bool createNotificationIfMissing = true;
    public float notificationDuration = 1.4f;
    public Color notificationColor = new Color(1f, 0.25f, 0.18f, 1f);
    [Range(0.01f, 1f)] public float lowHealthHealingThreshold = 0.1f;
    public float lowHealthPromptInterval = 2f;
    public TMP_Text pickupPromptText;
    public bool createPickupPromptIfMissing = true;
    public string pickupPromptFormat = "{0} - Ambil {1}";
    public Color pickupPromptColor = Color.white;

    [Header("Freeze Controls")]
    public bool freezeControlsWhenOpen = true;
    public PlayerMovement playerMovement;
    public PlayerShoot playerShoot;
    public PlayerWeaponAnimator weaponAnimator;
    public CameraControler cameraControler;
    public PlayerScopeController scopeController;
    public CursorController cursorController;

    readonly List<Image> itemImages = new List<Image>();
    readonly List<InventoryItemDragHandler> itemHandlers = new List<InventoryItemDragHandler>();
    RectTransform[] slots;
    Weapon highlightedWeapon;
    CanvasGroup inventoryCanvasGroup;
    bool controlsFrozen;
    bool previousMovementInput;
    bool previousShootEnabled;
    bool previousWeaponEquipInput;
    bool previousWeaponAimInput;
    bool previousCameraEnabled;
    bool previousScopeEnabled;
    bool previousCursorEnabled;
    bool previousCursorVisible;
    CursorLockMode previousCursorLockState;
    InventoryItemDragHandler draggingItem;
    Transform draggingOriginalParent;
    Vector2 draggingOriginalPosition;
    Vector3 throwAwayOriginalScale = Vector3.one;
    bool throwAwayScaleInitialized;
    bool pointerOverThrowAway;
    bool draggedItemThisFrame;
    RectTransform contextMenuRect;
    int contextMenuItemIndex = -1;
    int previewedItemIndex = -1;
    Coroutine notificationRoutine;
    WorldItemPickup activePickupPrompt;
    float nextLowHealthPromptTime;
    private KontrolPemain kontrolPemain;

    void Awake()
    {
        kontrolPemain = new KontrolPemain();
        FindMissingReferences();
        SyncInventoryToPlayerEquip();
        SetOpen(!startClosed);
        Refresh();
    }

    void OnEnable()
    {
        kontrolPemain?.Enable();
        SubscribePlayerHealth();
        Refresh();
    }

    void OnDisable()
    {
        kontrolPemain?.Disable();
        UnsubscribePlayerHealth();
        SetControlsFrozen(false);
        IsAnyInventoryOpen = false;
    }

    void OnDestroy()
    {
        kontrolPemain?.Dispose();
    }

    void Update()
    {
        if (playerMovement != null && playerMovement.IsGuardBroken)
        {
            if (IsOpen())
            {
                SetOpen(false);
            }

            return;
        }

        if (IsInventoryTogglePressedThisFrame())
        {
            Toggle();
        }

        bool isOpen = IsOpen();
        if (isOpen && closeKey != KeyCode.None && IsInventoryClosePressedThisFrame())
        {
            SetOpen(false);
            LastClosedByCancelFrame = Time.frameCount;
            return;
        }

        if (!isOpen)
        {
            TryPickupActiveWorldItem();
            HandleClosedInventoryHealingShortcut();
        }

        if (isOpen && useSelectedKey != KeyCode.None && IsInventoryUsePressedThisFrame())
        {
            UseSelectedInventoryItem();
            return;
        }

        if (isOpen && TryAssignSelectedWeaponWithNumberKey())
        {
            return;
        }

        if (contextMenuRect != null && IsInventoryPointerClickPressedThisFrame())
        {
            Camera eventCamera = GetEventCamera();
            if (!RectTransformUtility.RectangleContainsScreenPoint(contextMenuRect, GetPointerPosition(), eventCamera))
            {
                CloseContextMenu();
            }
        }

        if (weaponEquip != null && weaponEquip.CurrentWeapon != highlightedWeapon)
        {
            Refresh();
        }

        RefreshPreviewInfo();
    }

    bool IsInventoryTogglePressedThisFrame()
    {
        return kontrolPemain != null && kontrolPemain.Pemain.InventoryToggle.WasPressedThisFrame();
    }

    bool IsInventoryClosePressedThisFrame()
    {
        return kontrolPemain != null && kontrolPemain.UI.Cancel.WasPressedThisFrame();
    }

    bool IsInventoryUsePressedThisFrame()
    {
        return kontrolPemain != null && kontrolPemain.Pemain.InventoryUse.WasPressedThisFrame();
    }

    bool IsPickupPressedThisFrame()
    {
        return kontrolPemain != null && kontrolPemain.Pemain.Pickup.WasPressedThisFrame();
    }

    bool IsInventoryPointerClickPressedThisFrame()
    {
        return kontrolPemain != null
            && (kontrolPemain.UI.Click.WasPressedThisFrame()
                || kontrolPemain.UI.RightClick.WasPressedThisFrame());
    }

    Vector2 GetPointerPosition()
    {
        return kontrolPemain != null ? kontrolPemain.UI.Point.ReadValue<Vector2>() : Vector2.zero;
    }

    void TryPickupActiveWorldItem()
    {
        if (activePickupPrompt == null || !IsPickupPressedThisFrame())
        {
            return;
        }

        activePickupPrompt.TryPickupCurrentPlayer();
    }

    public void Toggle()
    {
        SetOpen(!IsOpen());
    }

    public void SetOpen(bool open)
    {
        if (inventoryRoot == null)
        {
            return;
        }

        if (inventoryRoot == gameObject)
        {
            inventoryCanvasGroup = inventoryCanvasGroup != null
                ? inventoryCanvasGroup
                : inventoryRoot.GetComponent<CanvasGroup>();

            if (inventoryCanvasGroup == null)
            {
                inventoryCanvasGroup = inventoryRoot.AddComponent<CanvasGroup>();
            }

            inventoryCanvasGroup.alpha = open ? 1f : 0f;
            inventoryCanvasGroup.interactable = open;
            inventoryCanvasGroup.blocksRaycasts = open;
        }
        else
        {
            inventoryRoot.SetActive(open);
        }

        if (open)
        {
            Refresh();
        }
        else
        {
            CloseContextMenu();
        }

        IsAnyInventoryOpen = open;
        SetControlsFrozen(open && freezeControlsWhenOpen);
    }

    bool IsOpen()
    {
        if (inventoryRoot == null)
        {
            return false;
        }

        if (inventoryRoot == gameObject)
        {
            inventoryCanvasGroup = inventoryCanvasGroup != null
                ? inventoryCanvasGroup
                : inventoryRoot.GetComponent<CanvasGroup>();

            return inventoryCanvasGroup == null || inventoryCanvasGroup.alpha > 0.01f;
        }

        return inventoryRoot.activeSelf;
    }

    public void Refresh()
    {
        FindMissingReferences();
        GenerateSlots();
        CacheSlots();
        ClearItems();

        InventoryEntry[] items = GetInventoryItems();
        if (items == null || slots == null || slots.Length == 0)
        {
            return;
        }

        highlightedWeapon = weaponEquip != null ? weaponEquip.CurrentWeapon : null;
        bool[] occupied = new bool[Mathf.Max(1, slots.Length)];

        for (int i = 0; i < items.Length; i++)
        {
            InventoryEntry entry = items[i];
            if (entry == null || entry.IsEmpty)
            {
                continue;
            }

            bool rotated = false;
            Vector2Int position;
            if (!TryFindFreePosition(i, entry, occupied, out position, out rotated))
            {
                continue;
            }

            MarkOccupied(occupied, position.x, position.y, GetWidth(entry, rotated), GetHeight(entry, rotated), true);
            CreateItemIcon(entry, i, position.x, position.y, rotated);
        }

        ClearSlotKeyLabels();
    }

    void CreateItemIcon(InventoryEntry entry, int itemIndex, int x, int y, bool rotated)
    {
        if (itemRoot == null)
        {
            return;
        }

        GameObject itemObject = new GameObject(entry.DisplayName + " Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(InventoryItemDragHandler));
        itemObject.transform.SetParent(itemRoot, false);

        RectTransform rectTransform = itemObject.GetComponent<RectTransform>();
        Image image = itemObject.GetComponent<Image>();
        InventoryItemDragHandler dragHandler = itemObject.GetComponent<InventoryItemDragHandler>();

        image.sprite = entry.Icon;
        image.type = Image.Type.Simple;
        image.preserveAspect = !stretchItemIconsToGrid;
        image.raycastTarget = true;
        image.color = GetInventoryItemColor(entry, itemIndex);
        itemImages.Add(image);
        itemHandlers.Add(dragHandler);

        ApplyItemRect(rectTransform, x, y, GetWidth(entry, rotated), GetHeight(entry, rotated));
        CreateItemKeyLabel(itemObject.transform, entry);

        dragHandler.Initialize(this, itemIndex, entry);
    }

    public void PreviewInventoryItem(int itemIndex)
    {
        InventoryEntry entry = GetInventoryEntry(itemIndex);
        if (entry == null || entry.IsEmpty)
        {
            ClearInventoryInfo();
            return;
        }

        previewedItemIndex = itemIndex;
        ShowInventoryInfo(entry);
        Refresh();
    }

    public void UseInventoryItem(int itemIndex)
    {
        InventoryEntry entry = GetInventoryEntry(itemIndex);
        if (entry == null || entry.IsEmpty)
        {
            ClearInventoryInfo();
            return;
        }

        previewedItemIndex = itemIndex;
        if (entry.ResolvedWeapon != null)
        {
            EquipInventoryWeapon(itemIndex, entry.ResolvedWeapon, entry.weaponObject);
            ShowInventoryInfo(entry);
        }
        else
        {
            UseConsumableItem(entry, itemIndex);
        }

        Refresh();
    }

    public void UseSelectedInventoryItem()
    {
        if (previewedItemIndex < 0)
        {
            return;
        }

        CloseContextMenu();
        UseInventoryItem(previewedItemIndex);
    }

    public void BeginItemDrag(InventoryItemDragHandler item, PointerEventData eventData)
    {
        if (item == null || eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        CloseContextMenu();
        draggingItem = item;
        RectTransform itemRect = item.GetComponent<RectTransform>();
        draggingOriginalParent = itemRect.parent;
        draggingOriginalPosition = itemRect.anchoredPosition;
        itemRect.SetAsLastSibling();
        CaptureThrowAwayOriginalScale();
        UpdateThrowAwayDragFeedback(eventData.position, eventData.pressEventCamera);

        CanvasGroup canvasGroup = item.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = item.gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.blocksRaycasts = false;
    }

    public void DragItem(InventoryItemDragHandler item, PointerEventData eventData)
    {
        if (item == null || item != draggingItem)
        {
            return;
        }

        RectTransform itemRect = item.GetComponent<RectTransform>();
        itemRect.anchoredPosition += eventData.delta / GetCanvasScaleFactor();
        draggedItemThisFrame = true;
        UpdateThrowAwayDragFeedback(eventData.position, eventData.pressEventCamera);
    }

    public void EndItemDrag(InventoryItemDragHandler item, PointerEventData eventData)
    {
        if (item == null || item != draggingItem)
        {
            return;
        }

        CanvasGroup canvasGroup = item.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
        }

        RectTransform itemRect = item.GetComponent<RectTransform>();
        if (IsPointerOverThrowAway(eventData.position, eventData.pressEventCamera))
        {
            ResetThrowAwayDragFeedback();
            ThrowAwayInventoryItem(item.ItemIndex);
            draggingItem = null;
            if (draggedItemThisFrame)
            {
                StartCoroutine(ClearDragClickSuppression());
            }
            return;
        }

        int targetIndex = GetSlotIndexAtScreenPosition(eventData.position, eventData.pressEventCamera);
        if (targetIndex >= 0)
        {
            MoveInventoryItem(item.ItemIndex, targetIndex, eventData.position, eventData.pressEventCamera, itemRect);
        }
        else
        {
            itemRect.SetParent(draggingOriginalParent, false);
            itemRect.anchoredPosition = draggingOriginalPosition;
        }

        ResetThrowAwayDragFeedback();
        draggingItem = null;
        if (draggedItemThisFrame)
        {
            StartCoroutine(ClearDragClickSuppression());
        }
    }

    IEnumerator ClearDragClickSuppression()
    {
        yield return null;
        draggedItemThisFrame = false;
    }

    public void HandleItemClick(int itemIndex, PointerEventData eventData)
    {
        if (draggedItemThisFrame)
        {
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            CloseContextMenu();
            PreviewInventoryItem(itemIndex);
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            OpenContextMenu(itemIndex, eventData.position, eventData.pressEventCamera);
        }
    }

    void OpenContextMenu(int itemIndex, Vector2 screenPosition, Camera eventCamera)
    {
        InventoryEntry entry = GetInventoryEntry(itemIndex);
        if (entry == null || entry.IsEmpty)
        {
            CloseContextMenu();
            return;
        }

        CloseContextMenu();
        contextMenuItemIndex = itemIndex;

        RectTransform parent = itemRoot != null ? itemRoot : slotRoot;
        if (parent == null)
        {
            return;
        }

        GameObject menuObject = new GameObject("Inventory Context Menu", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        menuObject.transform.SetParent(parent, false);
        contextMenuRect = menuObject.GetComponent<RectTransform>();
        Image background = menuObject.GetComponent<Image>();
        background.color = contextMenuColor;
        background.raycastTarget = true;

        contextMenuRect.anchorMin = new Vector2(0.5f, 0.5f);
        contextMenuRect.anchorMax = new Vector2(0.5f, 0.5f);
        contextMenuRect.pivot = new Vector2(0f, 1f);
        contextMenuRect.sizeDelta = contextMenuSize;
        contextMenuRect.SetAsLastSibling();

        Vector2 localPosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition, eventCamera, out localPosition);
        contextMenuRect.anchoredPosition = localPosition;

        VerticalLayoutGroup layout = menuObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        CreateContextButton("Gunakan", () =>
        {
            UseInventoryItem(contextMenuItemIndex);
            CloseContextMenu();
        });

        if (entry.ResolvedWeapon != null)
        {
            for (int i = 0; i < 4; i++)
            {
                int hotkeyIndex = i;
                CreateContextButton("Key " + (hotkeyIndex + 1), () =>
                {
                    AssignInventoryItemToHotkey(contextMenuItemIndex, hotkeyIndex);
                    CloseContextMenu();
                });
            }
        }

        CreateContextButton("Close", CloseContextMenu);
    }

    void CreateContextButton(string label, UnityEngine.Events.UnityAction action)
    {
        if (contextMenuRect == null)
        {
            return;
        }

        GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(contextMenuRect, false);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.sizeDelta = contextMenuButtonSize;

        Image image = buttonObject.GetComponent<Image>();
        image.color = contextMenuButtonColor;

        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(action);

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 0f);
        textRect.offsetMax = new Vector2(-8f, 0f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 18f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = contextMenuTextColor;
        text.raycastTarget = false;
    }

    void CloseContextMenu()
    {
        if (contextMenuRect != null)
        {
            Destroy(contextMenuRect.gameObject);
        }

        contextMenuRect = null;
        contextMenuItemIndex = -1;
    }

    void AssignInventoryItemToHotkey(int itemIndex, int hotkeyIndex)
    {
        if (weaponEquip == null || inventory == null || itemIndex < 0 || itemIndex >= inventory.Length)
        {
            return;
        }

        InventoryEntry entry = inventory[itemIndex];
        Weapon weapon = entry != null ? entry.ResolvedWeapon : null;
        if (weapon == null)
        {
            return;
        }

        weaponEquip.SetHotkeyWeapon(hotkeyIndex, weapon, entry.weaponObject);
        weaponInfoUI?.ShowWeapon(weapon);
        Refresh();
    }

    bool TryAssignSelectedWeaponWithNumberKey()
    {
        for (int i = 0; i < 4; i++)
        {
            if (!IsInventoryHotkeyPressedThisFrame(i))
            {
                continue;
            }

            InventoryEntry entry = GetInventoryEntry(previewedItemIndex);
            Weapon weapon = entry != null ? entry.ResolvedWeapon : null;
            if (weapon == null)
            {
                return true;
            }

            AssignInventoryItemToHotkey(previewedItemIndex, i);
            ShowNotification($"{weapon.weaponName} -> Key {i + 1}");
            return true;
        }

        return false;
    }

    bool IsInventoryHotkeyPressedThisFrame(int index)
    {
        if (kontrolPemain == null)
        {
            return false;
        }

        return index switch
        {
            0 => kontrolPemain.Pemain.Hotkey1.WasPressedThisFrame(),
            1 => kontrolPemain.Pemain.Hotkey2.WasPressedThisFrame(),
            2 => kontrolPemain.Pemain.Hotkey3.WasPressedThisFrame(),
            3 => kontrolPemain.Pemain.Hotkey4.WasPressedThisFrame(),
            _ => false
        };
    }

    void ClearSlotKeyLabels()
    {
        if (slots == null)
        {
            return;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            SetSlotKeyText(slots[i], "");
        }
    }

    void CreateItemKeyLabel(Transform itemTransform, InventoryEntry entry)
    {
        Weapon weapon = entry != null ? entry.ResolvedWeapon : null;
        if (itemTransform == null || weapon == null || weaponEquip == null)
        {
            return;
        }

        int keyNumber = weaponEquip.GetHotkeyNumberFor(weapon, entry.weaponObject);
        if (keyNumber <= 0)
        {
            return;
        }

        TMP_Text template = FindSlotKeyLabel(slotTemplate);
        TMP_Text keyLabel;
        GameObject labelObject;

        if (template != null)
        {
            labelObject = Instantiate(template.gameObject, itemTransform, false);
            keyLabel = labelObject.GetComponent<TMP_Text>();
        }
        else
        {
            labelObject = new GameObject(slotKeyLabelName, typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(itemTransform, false);
            keyLabel = labelObject.GetComponent<TMP_Text>();
            keyLabel.fontSize = 18f;
            keyLabel.alignment = TextAlignmentOptions.Center;
            keyLabel.color = Color.white;
        }

        labelObject.name = slotKeyLabelName;
        labelObject.SetActive(true);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        if (labelRect != null && template == null)
        {
            labelRect.anchorMin = new Vector2(1f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0f);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = new Vector2(-15f, 15f);
            labelRect.sizeDelta = new Vector2(20f, 20f);
        }

        if (keyLabel != null)
        {
            keyLabel.text = keyNumber.ToString();
            keyLabel.enabled = true;
            keyLabel.raycastTarget = false;
        }
    }

    void SetSlotKeyText(RectTransform slot, string text)
    {
        TMP_Text keyLabel = FindSlotKeyLabel(slot);
        if (keyLabel == null)
        {
            return;
        }

        keyLabel.text = text;
        keyLabel.enabled = !string.IsNullOrEmpty(text);
    }

    TMP_Text FindSlotKeyLabel(RectTransform slot)
    {
        if (slot == null)
        {
            return null;
        }

        Transform directChild = slot.Find(slotKeyLabelName);
        if (directChild != null)
        {
            TMP_Text directText = directChild.GetComponent<TMP_Text>();
            if (directText != null)
            {
                return directText;
            }
        }

        TMP_Text[] labels = slot.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] != null && labels[i].name == slotKeyLabelName)
            {
                return labels[i];
            }
        }

        return labels.Length > 0 ? labels[0] : null;
    }

    void MoveInventoryItem(int fromIndex, int toIndex)
    {
        MoveInventoryItem(fromIndex, toIndex, Vector2.zero, null, null);
    }

    void MoveInventoryItem(int fromIndex, int hoveredIndex, Vector2 screenPosition, Camera eventCamera, RectTransform itemRect)
    {
        if (inventory == null
            || fromIndex < 0
            || fromIndex >= inventory.Length
            || hoveredIndex < 0
            || hoveredIndex >= inventory.Length)
        {
            Refresh();
            return;
        }

        InventoryEntry movingEntry = inventory[fromIndex];
        if (movingEntry == null || movingEntry.IsEmpty)
        {
            Refresh();
            return;
        }

        int width = GetWidth(movingEntry, false);
        int height = GetHeight(movingEntry, false);
        Vector2Int cursorCell = GetCursorCellOffset(itemRect, screenPosition, eventCamera, width, height);
        int hoveredX = hoveredIndex % Mathf.Max(1, gridWidth);
        int hoveredY = hoveredIndex / Mathf.Max(1, gridWidth);
        int anchorX = hoveredX - cursorCell.x;
        int anchorY = hoveredY - cursorCell.y;
        int anchorIndex = ToIndex(anchorX, anchorY);

        if (!TryMoveInventoryItemToAnchor(fromIndex, anchorIndex, movingEntry, width, height))
        {
            Refresh();
            return;
        }

        previewedItemIndex = anchorIndex;
        SyncInventoryToPlayerEquip();
        Refresh();
        ShowInventoryInfo(movingEntry);
    }

    bool TryMoveInventoryItemToAnchor(int fromIndex, int anchorIndex, InventoryEntry movingEntry, int width, int height)
    {
        if (movingEntry == null
            || movingEntry.IsEmpty
            || !IsValidInventoryIndex(anchorIndex))
        {
            return false;
        }

        int anchorX = anchorIndex % Mathf.Max(1, gridWidth);
        int anchorY = anchorIndex / Mathf.Max(1, gridWidth);
        if (anchorX < 0 || anchorY < 0 || anchorX + width > gridWidth || anchorY + height > gridHeight)
        {
            return false;
        }

        bool[] occupied = BuildOccupiedMapIgnoring(fromIndex);
        if (!CanPlace(occupied, anchorX, anchorY, width, height))
        {
            return false;
        }

        if (fromIndex == anchorIndex)
        {
            return true;
        }

        inventory[fromIndex] = null;
        inventory[anchorIndex] = movingEntry;
        return true;
    }

    Vector2Int GetCursorCellOffset(RectTransform itemRect, Vector2 screenPosition, Camera eventCamera, int width, int height)
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        if (itemRect == null)
        {
            return Vector2Int.zero;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(itemRect, screenPosition, eventCamera, out Vector2 localPoint))
        {
            return Vector2Int.zero;
        }

        Rect rect = itemRect.rect;
        float normalizedX = Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);
        float normalizedY = Mathf.InverseLerp(rect.yMax, rect.yMin, localPoint.y);
        int cellX = Mathf.Clamp(Mathf.FloorToInt(normalizedX * width), 0, width - 1);
        int cellY = Mathf.Clamp(Mathf.FloorToInt(normalizedY * height), 0, height - 1);
        return new Vector2Int(cellX, cellY);
    }

    bool IsPointerOverThrowAway(Vector2 screenPosition, Camera eventCamera)
    {
        RectTransform dropZone = ResolveThrowAwayDropZone();
        return dropZone != null && RectTransformUtility.RectangleContainsScreenPoint(dropZone, screenPosition, eventCamera);
    }

    void CaptureThrowAwayOriginalScale()
    {
        RectTransform dropZone = ResolveThrowAwayDropZone();
        if (dropZone == null)
        {
            throwAwayScaleInitialized = false;
            throwAwayOriginalScale = Vector3.one;
            return;
        }

        throwAwayOriginalScale = dropZone.localScale;
        throwAwayScaleInitialized = true;
    }

    void UpdateThrowAwayDragFeedback(Vector2 screenPosition, Camera eventCamera)
    {
        bool isOverThrowAway = IsPointerOverThrowAway(screenPosition, eventCamera);
        if (pointerOverThrowAway == isOverThrowAway)
        {
            return;
        }

        pointerOverThrowAway = isOverThrowAway;
        RectTransform dropZone = ResolveThrowAwayDropZone();
        if (dropZone != null)
        {
            if (!throwAwayScaleInitialized)
            {
                CaptureThrowAwayOriginalScale();
            }

            dropZone.localScale = isOverThrowAway
                ? throwAwayOriginalScale * Mathf.Max(0.01f, throwAwayHoverScale)
                : throwAwayOriginalScale;
        }

        SetDraggingItemColor(isOverThrowAway);
    }

    void ResetThrowAwayDragFeedback()
    {
        RectTransform dropZone = ResolveThrowAwayDropZone();
        if (dropZone != null && throwAwayScaleInitialized)
        {
            dropZone.localScale = throwAwayOriginalScale;
        }

        pointerOverThrowAway = false;
        SetDraggingItemColor(false);
    }

    void SetDraggingItemColor(bool throwAwayPreview)
    {
        if (draggingItem == null)
        {
            return;
        }

        Image image = draggingItem.GetComponent<Image>();
        if (image == null)
        {
            return;
        }

        image.color = throwAwayPreview
            ? throwAwayDragColor
            : GetInventoryItemColor(draggingItem.Entry, draggingItem.ItemIndex);
    }

    RectTransform ResolveThrowAwayDropZone()
    {
        if (throwAwayDropZone != null)
        {
            return throwAwayDropZone;
        }

        Transform found = null;
        if (inventoryRoot != null)
        {
            found = FindChildByName(inventoryRoot.transform, "Throw Away");
        }

        if (found == null)
        {
            found = FindChildByName(transform, "Throw Away");
        }

        throwAwayDropZone = found as RectTransform;
        return throwAwayDropZone;
    }

    void ThrowAwayInventoryItem(int itemIndex)
    {
        InventoryEntry entry = GetInventoryEntry(itemIndex);
        if (entry == null || entry.IsEmpty)
        {
            Refresh();
            return;
        }

        if (!SpawnThrownPickup(entry))
        {
            Refresh();
            return;
        }

        Weapon thrownWeapon = entry.ResolvedWeapon;
        GameObject thrownWeaponObject = entry.weaponObject;
        if (weaponEquip != null && thrownWeapon != null)
        {
            weaponEquip.ClearHotkeyFor(thrownWeapon, thrownWeaponObject);
            if (weaponEquip.CurrentWeapon == thrownWeapon)
            {
                weaponEquip.UnequipWeapon();
            }
        }

        inventory[itemIndex] = null;
        if (previewedItemIndex == itemIndex)
        {
            previewedItemIndex = -1;
            ClearInventoryInfo();
        }

        CloseContextMenu();
        SyncInventoryToPlayerEquip();
        Refresh();
    }

    bool SpawnThrownPickup(InventoryEntry entry)
    {
        if (entry == null || entry.IsEmpty)
        {
            return false;
        }

        Vector3 position = GetWorldDropPosition();
        GameObject pickupObject = CreateThrownPickupObject(entry, position, Quaternion.identity);
        if (pickupObject == null)
        {
            return false;
        }

        WorldItemPickup pickup = pickupObject.GetComponent<WorldItemPickup>();
        if (pickup == null)
        {
            pickup = pickupObject.AddComponent<WorldItemPickup>();
        }

        pickup.itemData = entry.itemData;
        pickup.weapon = entry.itemData == null ? entry.ResolvedWeapon : null;
        pickup.weaponObject = entry.itemData == null ? entry.weaponObject : null;
        pickup.amount = Mathf.Max(1, entry.amount);
        pickup.autoPickup = false;
        pickup.requireKey = true;
        pickup.pickupKey = KeyCode.T;
        pickup.pickupRadius = Mathf.Max(0.1f, thrownPickupRadius);
        pickup.ensureTriggerCollider = true;
        pickup.useIgnoreRaycastLayer = true;
        pickup.nonBlockingLayerName = thrownPickupLayerName;
        AttachThrownPickupEffect(pickupObject);
        pickup.RefreshPickupSetup();
        return true;
    }

    GameObject CreateThrownPickupObject(InventoryEntry entry, Vector3 position, Quaternion rotation)
    {
        string itemName = entry.DisplayName;
        GameObject prefab = entry.itemData != null && entry.itemData.itemPrefab != null ? entry.itemData.itemPrefab : null;
        GameObject pickupObject = prefab != null ? Instantiate(prefab, position, rotation) : new GameObject($"{itemName} Pickup");
        pickupObject.name = $"{itemName} Pickup";
        pickupObject.transform.SetPositionAndRotation(position, rotation);

        SphereCollider trigger = pickupObject.GetComponent<SphereCollider>();
        if (trigger == null)
        {
            trigger = pickupObject.AddComponent<SphereCollider>();
        }

        trigger.isTrigger = true;
        trigger.radius = Mathf.Max(0.1f, thrownPickupRadius);

        Rigidbody body = pickupObject.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = pickupObject.AddComponent<Rigidbody>();
        }

        body.isKinematic = true;
        body.useGravity = false;

        if (prefab == null)
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "Visual";
            visual.transform.SetParent(pickupObject.transform, false);
            visual.transform.localScale = Vector3.one * 0.25f;
            ApplyThrownPickupVisualColor(visual, entry);
            Collider visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
            {
                Destroy(visualCollider);
            }
        }

        return pickupObject;
    }

    Vector3 GetWorldDropPosition()
    {
        Transform origin = worldDropOrigin != null
            ? worldDropOrigin
            : playerHealth != null
                ? playerHealth.transform
                : weaponEquip != null
                    ? weaponEquip.transform
                    : transform;

        Vector3 forward = origin.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f && Camera.main != null)
        {
            forward = Camera.main.transform.forward;
            forward.y = 0f;
        }

        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }

        return origin.position + forward.normalized * Mathf.Max(0f, throwAwayDropForwardDistance) + Vector3.up * throwAwayDropHeightOffset;
    }

    void AttachThrownPickupEffect(GameObject pickupObject)
    {
        if (pickupObject == null || pickupEffectPrefab == null)
        {
            return;
        }

        Transform existingEffect = pickupObject.transform.Find("Pickup Effect");
        GameObject effectObject = existingEffect != null
            ? existingEffect.gameObject
            : Instantiate(pickupEffectPrefab, pickupObject.transform);

        effectObject.name = "Pickup Effect";
        effectObject.transform.SetParent(pickupObject.transform, false);
        effectObject.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        effectObject.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        effectObject.transform.localScale = Vector3.one * 0.1f;

        ParticleSystem[] particles = effectObject.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            particles[i].Clear(true);
            particles[i].Play(true);
        }
    }

    void ApplyThrownPickupVisualColor(GameObject visual, InventoryEntry entry)
    {
        if (visual == null)
        {
            return;
        }

        Renderer renderer = visual.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        Material material = new Material(renderer.sharedMaterial);
        material.color = GetThrownPickupColor(entry);
        renderer.sharedMaterial = material;
    }

    Color GetThrownPickupColor(InventoryEntry entry)
    {
        if (entry == null)
        {
            return thrownDefaultColor;
        }

        if (entry.ResolvedWeapon != null)
        {
            return thrownWeaponColor;
        }

        InventoryItemData item = entry.itemData;
        if (item == null)
        {
            return thrownDefaultColor;
        }

        switch (item.itemType)
        {
            case InventoryItemType.Ammo:
                return thrownAmmoColor;
            case InventoryItemType.HealingItem:
                return thrownHealingColor;
            case InventoryItemType.StatusEffectItem:
                return thrownStatusEffectColor;
            default:
                return thrownDefaultColor;
        }
    }

    void ShowInventoryInfo(InventoryEntry entry)
    {
        if (weaponInfoUI == null)
        {
            return;
        }

        if (entry == null || entry.IsEmpty)
        {
            ClearInventoryInfo();
            return;
        }

        if (entry.itemData != null)
        {
            weaponInfoUI.ShowItem(entry.itemData, entry.amount);
            return;
        }

        weaponInfoUI.ShowWeapon(entry.ResolvedWeapon);
    }

    void RefreshPreviewInfo()
    {
        if (!IsOpen() || previewedItemIndex < 0)
        {
            return;
        }

        InventoryEntry entry = GetInventoryEntry(previewedItemIndex);
        if (entry == null || entry.IsEmpty)
        {
            ClearInventoryInfo();
            previewedItemIndex = -1;
            return;
        }

        ShowInventoryInfo(entry);
    }

    void ClearInventoryInfo()
    {
        weaponInfoUI?.ShowWeapon(null);
    }

    void EquipInventoryWeapon(int weaponSlotIndex, Weapon weapon, GameObject weaponObject)
    {
        if (weaponEquip == null || weapon == null)
        {
            return;
        }

        if (syncToPlayerWeaponEquip && weaponEquip.weaponSlots != null && weaponSlotIndex >= 0 && weaponSlotIndex < weaponEquip.weaponSlots.Length)
        {
            weaponEquip.EquipWeapon(weaponSlotIndex);
            return;
        }

        weaponEquip.EquipWeapon(weapon, weaponObject);
    }

    Color GetInventoryItemColor(InventoryEntry entry, int itemIndex)
    {
        if (entry == null || entry.IsEmpty)
        {
            return normalColor;
        }

        Weapon weapon = entry.ResolvedWeapon;
        bool isEquipped = weapon != null && weaponEquip != null && weaponEquip.CurrentWeapon == weapon;
        bool isSelected = itemIndex == previewedItemIndex;

        if (isEquipped && isSelected)
        {
            return Color.Lerp(equippedColor, selectedColor, 0.5f);
        }

        if (isEquipped)
        {
            return equippedColor;
        }

        return isSelected ? selectedColor : normalColor;
    }

    void UseConsumableItem(InventoryEntry entry, int itemIndex)
    {
        InventoryItemData item = entry != null ? entry.itemData : null;
        if (item == null)
        {
            return;
        }

        FindMissingReferences();

        bool used = false;
        if (item.healAmount > 0f && playerHealth != null)
        {
            playerHealth.Heal(item.healAmount);
            used = true;
        }

        if (item.statusEffects != null && statusEffectController != null)
        {
            for (int i = 0; i < item.statusEffects.Length; i++)
            {
                if (item.statusEffects[i] == null)
                {
                    continue;
                }

                statusEffectController.AddEffect(item.statusEffects[i]);
                used = true;
            }
        }

        if (used && item.consumeOnUse)
        {
            entry.amount = Mathf.Max(0, entry.amount - 1);
            if (entry.amount <= 0 && inventory != null && itemIndex >= 0 && itemIndex < inventory.Length)
            {
                inventory[itemIndex] = null;
                previewedItemIndex = -1;
                ClearInventoryInfo();
            }
        }
    }

    void HandleClosedInventoryHealingShortcut()
    {
        if (playerHealth == null || playerHealth.IsDead || playerHealth.MaxHealth <= 0f)
        {
            return;
        }

        bool isLowHealth = playerHealth.CurrentHealth / playerHealth.MaxHealth <= Mathf.Clamp01(lowHealthHealingThreshold);
        if (!isLowHealth)
        {
            return;
        }

        if (Time.unscaledTime >= nextLowHealthPromptTime)
        {
            ShowNotification(HasUsableHealingItem() ? lowHealthHealingPromptMessage : noHealingItemMessage);
            nextLowHealthPromptTime = Time.unscaledTime + Mathf.Max(0.1f, lowHealthPromptInterval);
        }

        if (useSelectedKey != KeyCode.None && IsInventoryUsePressedThisFrame())
        {
            if (!TryUseBestHealingItem())
            {
                ShowNotification(noHealingItemMessage);
                nextLowHealthPromptTime = Time.unscaledTime + Mathf.Max(0.1f, lowHealthPromptInterval);
            }
        }
    }

    bool HasUsableHealingItem()
    {
        return FindBestHealingItemIndex() >= 0;
    }

    bool TryUseBestHealingItem()
    {
        int itemIndex = FindBestHealingItemIndex();
        if (itemIndex < 0)
        {
            return false;
        }

        InventoryEntry entry = GetInventoryEntry(itemIndex);
        InventoryItemData item = entry != null ? entry.itemData : null;
        if (item == null)
        {
            return false;
        }

        UseConsumableItem(entry, itemIndex);
        ShowNotification($"Used {item.DisplayName}");
        nextLowHealthPromptTime = Time.unscaledTime + Mathf.Max(0.1f, lowHealthPromptInterval);
        SyncInventoryToPlayerEquip();
        Refresh();
        RefreshPreviewInfo();
        return true;
    }

    int FindBestHealingItemIndex()
    {
        if (inventory == null || playerHealth == null)
        {
            return -1;
        }

        float missingHealth = Mathf.Max(0f, playerHealth.MaxHealth - playerHealth.CurrentHealth);
        int fallbackIndex = -1;
        float fallbackHeal = float.NegativeInfinity;
        int bestIndex = -1;
        float bestOverheal = float.PositiveInfinity;

        for (int i = 0; i < inventory.Length; i++)
        {
            InventoryEntry entry = inventory[i];
            InventoryItemData item = entry != null ? entry.itemData : null;
            if (item == null || entry.amount <= 0 || item.itemType != InventoryItemType.HealingItem)
            {
                continue;
            }

            if (item.healAmount <= 0f)
            {
                if (fallbackIndex < 0)
                {
                    fallbackIndex = i;
                }
                continue;
            }

            if (item.healAmount >= missingHealth)
            {
                float overheal = item.healAmount - missingHealth;
                if (overheal < bestOverheal)
                {
                    bestOverheal = overheal;
                    bestIndex = i;
                }
            }
            else if (bestIndex < 0 && item.healAmount > fallbackHeal)
            {
                fallbackHeal = item.healAmount;
                fallbackIndex = i;
            }
        }

        return bestIndex >= 0 ? bestIndex : fallbackIndex;
    }

    public int GetAmmoCountForWeapon(Weapon weapon)
    {
        if (weapon == null || inventory == null)
        {
            return 0;
        }

        int total = 0;
        for (int i = 0; i < inventory.Length; i++)
        {
            InventoryEntry entry = inventory[i];
            if (!IsMatchingAmmoEntry(entry, weapon))
            {
                continue;
            }

            total += Mathf.Max(0, entry.amount);
        }

        return total;
    }

    public bool TryConsumeAmmoForWeapon(Weapon weapon, int requestedAmount, out int consumedAmount)
    {
        consumedAmount = 0;
        if (weapon == null || inventory == null || requestedAmount <= 0)
        {
            return false;
        }

        for (int i = 0; i < inventory.Length && consumedAmount < requestedAmount; i++)
        {
            InventoryEntry entry = inventory[i];
            if (!IsMatchingAmmoEntry(entry, weapon))
            {
                continue;
            }

            int available = Mathf.Max(0, entry.amount);
            int take = Mathf.Min(available, requestedAmount - consumedAmount);
            if (take <= 0)
            {
                continue;
            }

            entry.amount = available - take;
            consumedAmount += take;

            if (entry.amount <= 0)
            {
                inventory[i] = null;
                if (previewedItemIndex == i)
                {
                    previewedItemIndex = -1;
                    ClearInventoryInfo();
                }
            }
        }

        if (consumedAmount > 0)
        {
            SyncInventoryToPlayerEquip();
            Refresh();
        }

        return consumedAmount > 0;
    }

    public bool TryAddItem(InventoryItemData item, int amount)
    {
        return TryAddItem(item, amount, out _);
    }

    public bool TryAddPickup(WorldItemPickup pickup, out int remainingAmount)
    {
        remainingAmount = pickup != null ? Mathf.Max(0, pickup.amount) : 0;
        if (pickup == null || remainingAmount <= 0)
        {
            return false;
        }

        if (pickup.itemData != null)
        {
            return TryAddItem(pickup.itemData, pickup.amount, out remainingAmount);
        }

        if (pickup.weapon == null)
        {
            return false;
        }

        FindMissingReferences();
        EnsureInventoryArray();
        if (inventory == null || inventory.Length == 0)
        {
            return false;
        }

        InventoryEntry newEntry = new InventoryEntry
        {
            weapon = pickup.weapon,
            weaponObject = pickup.weaponObject,
            amount = 1
        };

        bool[] occupied = BuildOccupiedMap();
        Vector2Int position;
        bool rotated;
        if (!TryFindFreePosition(newEntry, occupied, out position, out rotated))
        {
            ShowInventoryFullNotification();
            return false;
        }

        int index = ToIndex(position.x, position.y);
        if (!IsValidInventoryIndex(index) || (inventory[index] != null && !inventory[index].IsEmpty))
        {
            ShowInventoryFullNotification();
            return false;
        }

        inventory[index] = newEntry;
        remainingAmount = 0;
        SyncInventoryToPlayerEquip();
        Refresh();
        RefreshPreviewInfo();
        return true;
    }

    public bool TryAddItem(InventoryItemData item, int amount, out int remainingAmount)
    {
        remainingAmount = Mathf.Max(0, amount);
        if (item == null || remainingAmount <= 0)
        {
            return false;
        }

        FindMissingReferences();
        EnsureInventoryArray();
        if (inventory == null || inventory.Length == 0)
        {
            return false;
        }

        gridHeight = Mathf.Max(1, Mathf.CeilToInt(inventory.Length / (float)Mathf.Max(1, gridWidth)));
        AddToExistingStacks(item, ref remainingAmount);
        AddToEmptySlots(item, ref remainingAmount);

        bool addedAny = remainingAmount < amount;
        if (addedAny)
        {
            SyncInventoryToPlayerEquip();
            Refresh();
            RefreshPreviewInfo();
        }

        if (remainingAmount > 0)
        {
            ShowInventoryFullNotification();
        }

        return addedAny;
    }

    public void ShowInventoryFullNotification()
    {
        ShowNotification(inventoryFullMessage);
    }

    public void ShowNotification(string message)
    {
        EnsureNotificationText();
        if (notificationText == null)
        {
            Debug.Log(message);
            return;
        }

        if (notificationRoutine != null)
        {
            StopCoroutine(notificationRoutine);
        }

        notificationRoutine = StartCoroutine(NotificationRoutine(message));
    }

    public void ShowPickupPrompt(WorldItemPickup pickup, string itemName, int amount, KeyCode key)
    {
        if (pickup == null)
        {
            return;
        }

        EnsurePickupPromptText();
        if (pickupPromptText == null)
        {
            return;
        }

        activePickupPrompt = pickup;
        string amountText = amount > 1 ? $" x{amount}" : string.Empty;
        pickupPromptText.text = string.Format(
            string.IsNullOrEmpty(pickupPromptFormat) ? "{0} - Ambil {1}" : pickupPromptFormat,
            key,
            $"{itemName}{amountText}"
        );
        pickupPromptText.color = pickupPromptColor;
        pickupPromptText.gameObject.SetActive(true);
    }

    public void HidePickupPrompt(WorldItemPickup pickup)
    {
        if (pickup != null && activePickupPrompt != null && activePickupPrompt != pickup)
        {
            return;
        }

        activePickupPrompt = null;
        EnsurePickupPromptText();
        if (pickupPromptText != null)
        {
            pickupPromptText.gameObject.SetActive(false);
        }
    }

    IEnumerator NotificationRoutine(string message)
    {
        notificationText.text = string.IsNullOrEmpty(message) ? inventoryFullMessage : message;
        notificationText.color = notificationColor;
        notificationText.gameObject.SetActive(true);

        float holdDuration = Mathf.Max(0f, notificationDuration);
        float elapsed = 0f;
        while (elapsed < holdDuration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        notificationText.gameObject.SetActive(false);
        notificationRoutine = null;
    }

    void AddToExistingStacks(InventoryItemData item, ref int remainingAmount)
    {
        if (!CanStackItem(item))
        {
            return;
        }

        int maxStack = GetMaxStack(item);
        for (int i = 0; i < inventory.Length && remainingAmount > 0; i++)
        {
            InventoryEntry entry = inventory[i];
            if (!IsMatchingStackEntry(entry, item))
            {
                continue;
            }

            int currentAmount = Mathf.Max(0, entry.amount);
            int freeSpace = maxStack - currentAmount;
            if (freeSpace <= 0)
            {
                continue;
            }

            int addAmount = Mathf.Min(freeSpace, remainingAmount);
            entry.amount = currentAmount + addAmount;
            remainingAmount -= addAmount;
        }
    }

    void AddToEmptySlots(InventoryItemData item, ref int remainingAmount)
    {
        int maxStack = CanStackItem(item) ? GetMaxStack(item) : 1;
        while (remainingAmount > 0)
        {
            bool[] occupied = BuildOccupiedMap();
            InventoryEntry newEntry = new InventoryEntry
            {
                itemData = item,
                amount = Mathf.Min(maxStack, remainingAmount)
            };

            Vector2Int position;
            bool rotated;
            if (!TryFindFreePosition(newEntry, occupied, out position, out rotated))
            {
                return;
            }

            int index = ToIndex(position.x, position.y);
            if (inventory == null || index < 0 || index >= inventory.Length)
            {
                return;
            }

            if (inventory[index] != null && !inventory[index].IsEmpty)
            {
                return;
            }

            inventory[index] = newEntry;
            remainingAmount -= newEntry.amount;
        }
    }

    bool[] BuildOccupiedMap()
    {
        int slotCount = Mathf.Max(1, inventory != null ? inventory.Length : GetSlotCount());
        bool[] occupied = new bool[slotCount];
        if (inventory == null)
        {
            return occupied;
        }

        for (int i = 0; i < inventory.Length; i++)
        {
            InventoryEntry entry = inventory[i];
            if (entry == null || entry.IsEmpty)
            {
                continue;
            }

            Vector2Int position;
            bool rotated;
            if (TryFindFreePosition(i, entry, occupied, out position, out rotated))
            {
                MarkOccupied(occupied, position.x, position.y, GetWidth(entry, rotated), GetHeight(entry, rotated), true);
            }
        }

        return occupied;
    }

    bool[] BuildOccupiedMapIgnoring(int ignoredIndex)
    {
        int slotCount = Mathf.Max(1, inventory != null ? inventory.Length : GetSlotCount());
        bool[] occupied = new bool[slotCount];
        if (inventory == null)
        {
            return occupied;
        }

        for (int i = 0; i < inventory.Length; i++)
        {
            if (i == ignoredIndex)
            {
                continue;
            }

            InventoryEntry entry = inventory[i];
            if (entry == null || entry.IsEmpty)
            {
                continue;
            }

            int x = i % Mathf.Max(1, gridWidth);
            int y = i / Mathf.Max(1, gridWidth);
            int width = GetWidth(entry, false);
            int height = GetHeight(entry, false);
            if (x + width <= gridWidth && y + height <= gridHeight)
            {
                MarkOccupied(occupied, x, y, width, height, true);
            }
        }

        return occupied;
    }

    void EnsureInventoryArray()
    {
        if (inventory != null && inventory.Length > 0)
        {
            return;
        }

        int slotCount = 0;
        if (slots != null && slots.Length > 0)
        {
            slotCount = slots.Length;
        }
        else
        {
            slotCount = Mathf.Max(1, gridWidth * gridHeight);
        }

        inventory = new InventoryEntry[slotCount];
    }

    bool CanStackItem(InventoryItemData item)
    {
        return item != null && item.maxStack != 1 && item.itemType != InventoryItemType.Weapon;
    }

    int GetMaxStack(InventoryItemData item)
    {
        return item != null && item.maxStack > 0 ? item.maxStack : int.MaxValue;
    }

    bool IsMatchingStackEntry(InventoryEntry entry, InventoryItemData item)
    {
        InventoryItemData existing = entry != null ? entry.itemData : null;
        if (existing == null || item == null || existing.itemType != item.itemType)
        {
            return false;
        }

        if (existing == item)
        {
            return true;
        }

        if (existing.itemType != InventoryItemType.Ammo)
        {
            return false;
        }

        string existingCaliber = NormalizeAmmoKey(existing.caliber);
        string itemCaliber = NormalizeAmmoKey(item.caliber);
        if (!string.IsNullOrEmpty(existingCaliber) && !string.IsNullOrEmpty(itemCaliber))
        {
            return existingCaliber == itemCaliber;
        }

        string existingAmmoType = NormalizeAmmoKey(existing.ammoType);
        string itemAmmoType = NormalizeAmmoKey(item.ammoType);
        return !string.IsNullOrEmpty(existingAmmoType) && existingAmmoType == itemAmmoType;
    }

    bool IsMatchingAmmoEntry(InventoryEntry entry, Weapon weapon)
    {
        InventoryItemData item = entry != null ? entry.itemData : null;
        if (item == null || item.itemType != InventoryItemType.Ammo || weapon == null)
        {
            return false;
        }

        string weaponCaliber = NormalizeAmmoKey(weapon.caliber);
        string itemCaliber = NormalizeAmmoKey(item.caliber);
        if (!string.IsNullOrEmpty(weaponCaliber) && !string.IsNullOrEmpty(itemCaliber))
        {
            return weaponCaliber == itemCaliber;
        }

        string weaponAmmoType = NormalizeAmmoKey(weapon.ammoType);
        string itemAmmoType = NormalizeAmmoKey(item.ammoType);
        return !string.IsNullOrEmpty(weaponAmmoType) && weaponAmmoType == itemAmmoType;
    }

    string NormalizeAmmoKey(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }

    void ApplyItemRect(RectTransform itemRect, int x, int y, int width, int height)
    {
        int firstIndex = ToIndex(x, y);
        if (!IsValidIndex(firstIndex))
        {
            return;
        }

        Vector2 min = slots[firstIndex].anchoredPosition;
        Vector2 max = min;

        for (int iy = 0; iy < height; iy++)
        {
            for (int ix = 0; ix < width; ix++)
            {
                int index = ToIndex(x + ix, y + iy);
                if (!IsValidIndex(index))
                {
                    continue;
                }

                Vector2 position = slots[index].anchoredPosition;
                min = Vector2.Min(min, position);
                max = Vector2.Max(max, position);
            }
        }

        itemRect.anchorMin = new Vector2(0.5f, 0.5f);
        itemRect.anchorMax = new Vector2(0.5f, 0.5f);
        itemRect.pivot = new Vector2(0.5f, 0.5f);
        itemRect.anchoredPosition = (min + max) * 0.5f;
        itemRect.sizeDelta = GetItemIconSize(width, height);
    }

    Vector2 GetItemIconSize(int width, int height)
    {
        float slotWidth = Mathf.Max(1f, itemSlotVisualSize.x);
        float slotHeight = Mathf.Max(1f, itemSlotVisualSize.y);
        float horizontalStep = Mathf.Abs(slotSpacing.x);
        float verticalStep = Mathf.Abs(slotSpacing.y);

        return new Vector2(
            slotWidth + Mathf.Max(0, width - 1) * horizontalStep,
            slotHeight + Mathf.Max(0, height - 1) * verticalStep
        );
    }

    bool TryFindFreePosition(InventoryEntry entry, bool[] occupied, out Vector2Int position, out bool rotated)
    {
        rotated = false;
        if (entry == null || entry.IsEmpty)
        {
            position = Vector2Int.zero;
            return false;
        }

        if (TryFindFreePosition(GetWidth(entry, false), GetHeight(entry, false), occupied, out position))
        {
            return true;
        }

        if (CanRotate(entry) && TryFindFreePosition(GetWidth(entry, true), GetHeight(entry, true), occupied, out position))
        {
            rotated = true;
            return true;
        }

        position = Vector2Int.zero;
        return false;
    }

    bool TryFindFreePosition(int width, int height, bool[] occupied, out Vector2Int position)
    {
        for (int y = 0; y <= gridHeight - height; y++)
        {
            for (int x = 0; x <= gridWidth - width; x++)
            {
                if (CanPlace(occupied, x, y, width, height))
                {
                    position = new Vector2Int(x, y);
                    return true;
                }
            }
        }

        position = Vector2Int.zero;
        return false;
    }

    bool CanPlace(bool[] occupied, int x, int y, int width, int height)
    {
        for (int iy = 0; iy < height; iy++)
        {
            for (int ix = 0; ix < width; ix++)
            {
                int index = ToIndex(x + ix, y + iy);
                if (!IsValidIndex(index) || occupied[index])
                {
                    return false;
                }
            }
        }

        return true;
    }

    void MarkOccupied(bool[] occupied, int x, int y, int width, int height, bool value)
    {
        for (int iy = 0; iy < height; iy++)
        {
            for (int ix = 0; ix < width; ix++)
            {
                int index = ToIndex(x + ix, y + iy);
                if (IsValidIndex(index))
                {
                    occupied[index] = value;
                }
            }
        }
    }

    int GetWidth(Weapon weapon, bool rotated)
    {
        return Mathf.Max(1, rotated ? weapon.inventoryHeight : weapon.inventoryWidth);
    }

    int GetHeight(Weapon weapon, bool rotated)
    {
        return Mathf.Max(1, rotated ? weapon.inventoryWidth : weapon.inventoryHeight);
    }

    int GetWidth(InventoryEntry entry, bool rotated)
    {
        if (entry == null)
        {
            return 1;
        }

        if (entry.itemData != null)
        {
            return Mathf.Max(1, rotated ? entry.itemData.inventoryHeight : entry.itemData.inventoryWidth);
        }

        Weapon weapon = entry.ResolvedWeapon;
        return weapon != null ? GetWidth(weapon, rotated) : 1;
    }

    int GetHeight(InventoryEntry entry, bool rotated)
    {
        if (entry == null)
        {
            return 1;
        }

        if (entry.itemData != null)
        {
            return Mathf.Max(1, rotated ? entry.itemData.inventoryWidth : entry.itemData.inventoryHeight);
        }

        Weapon weapon = entry.ResolvedWeapon;
        return weapon != null ? GetHeight(weapon, rotated) : 1;
    }

    bool CanRotate(InventoryEntry entry)
    {
        if (entry == null)
        {
            return false;
        }

        if (entry.itemData != null)
        {
            return entry.itemData.canRotateInInventory;
        }

        Weapon weapon = entry.ResolvedWeapon;
        return weapon != null && weapon.canRotateInInventory;
    }

    int ToIndex(int x, int y)
    {
        return y * gridWidth + x;
    }

    bool IsValidIndex(int index)
    {
        if (slots != null)
        {
            return index >= 0 && index < slots.Length;
        }

        return inventory != null && index >= 0 && index < inventory.Length;
    }

    bool IsValidInventoryIndex(int index)
    {
        return inventory != null && index >= 0 && index < inventory.Length;
    }

    void CacheSlots()
    {
        if (slotRoot == null || slotTemplate == null)
        {
            slots = null;
            return;
        }

        int count = GetSlotCount();
        slots = new RectTransform[count];
        if (count <= 0)
        {
            return;
        }

        slots[0] = slotTemplate;
        int slotIndex = 1;
        for (int i = 0; i < slotRoot.childCount && slotIndex < count; i++)
        {
            RectTransform slot = slotRoot.GetChild(i) as RectTransform;
            if (slot == null || slot == slotTemplate || !IsGeneratedSlot(slot))
            {
                continue;
            }

            slots[slotIndex] = slot;
            slotIndex++;
        }
    }

    void ClearItems()
    {
        itemImages.Clear();
        itemHandlers.Clear();

        if (itemRoot == null)
        {
            return;
        }

        for (int i = itemRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(itemRoot.GetChild(i).gameObject);
        }
    }

    InventoryEntry GetInventoryEntry(int index)
    {
        if (inventory == null || index < 0 || index >= inventory.Length)
        {
            return null;
        }

        return inventory[index];
    }

    int GetSlotIndexAtScreenPosition(Vector2 screenPosition, Camera eventCamera)
    {
        if (slots == null)
        {
            return -1;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            RectTransform slot = slots[i];
            if (slot != null && RectTransformUtility.RectangleContainsScreenPoint(slot, screenPosition, eventCamera))
            {
                return i;
            }
        }

        return -1;
    }

    float GetCanvasScaleFactor()
    {
        Canvas canvas = itemRoot != null ? itemRoot.GetComponentInParent<Canvas>() : GetComponentInParent<Canvas>();
        return canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
    }

    Camera GetEventCamera()
    {
        Canvas canvas = itemRoot != null ? itemRoot.GetComponentInParent<Canvas>() : GetComponentInParent<Canvas>();
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return canvas.worldCamera;
    }

    void FindMissingReferences()
    {
        if (weaponEquip == null)
        {
            weaponEquip = FindAnyObjectByType<PlayerWeaponEquip>();
        }

        if (weaponInfoUI == null)
        {
            weaponInfoUI = FindAnyObjectByType<WeaponInfoUI>();
        }

        if (statusEffectController == null)
        {
            statusEffectController = FindAnyObjectByType<PlayerStatusEffectController>();
        }

        if (playerHealth == null)
        {
            playerHealth = FindAnyObjectByType<PlayerHealth>();
            SubscribePlayerHealth();
        }

        if (playerMovement == null)
        {
            playerMovement = FindAnyObjectByType<PlayerMovement>();
        }

        if (playerShoot == null)
        {
            playerShoot = FindAnyObjectByType<PlayerShoot>();
        }

        if (cameraControler == null)
        {
            cameraControler = FindAnyObjectByType<CameraControler>();
        }

        if (weaponAnimator == null)
        {
            weaponAnimator = FindAnyObjectByType<PlayerWeaponAnimator>();
        }

        if (scopeController == null)
        {
            scopeController = FindAnyObjectByType<PlayerScopeController>();
        }

        if (cursorController == null)
        {
            cursorController = FindAnyObjectByType<CursorController>();
        }

        if (inventoryRoot == null)
        {
            if (slotRoot != null)
            {
                inventoryRoot = slotRoot.parent != null ? slotRoot.parent.gameObject : slotRoot.gameObject;
            }
            else if (name == "Panel")
            {
                inventoryRoot = gameObject;
            }
            else
            {
                Transform panel = FindChildByName(transform, "Panel");
                inventoryRoot = panel != null ? panel.gameObject : gameObject;
            }
        }

        if (slotRoot == null)
        {
            if (name == "Inventory")
            {
                slotRoot = transform as RectTransform;
            }
            else
            {
                Transform inventory = FindChildByName(transform, "Inventory");
                slotRoot = inventory as RectTransform;
            }
        }

        if (slotTemplate == null && slotRoot != null && slotRoot.childCount > 0)
        {
            slotTemplate = slotRoot.GetChild(0) as RectTransform;
        }

        if (itemRoot == null && slotRoot != null)
        {
            Transform existingItemRoot = slotRoot.parent != null ? slotRoot.parent.Find("ItemRoot") : null;
            itemRoot = existingItemRoot as RectTransform;
        }

        if (itemRoot != null && IsSlotTransform(itemRoot))
        {
            itemRoot = null;
        }

        if (itemRoot == null && slotRoot != null && slotRoot.parent != null)
        {
            GameObject itemRootObject = new GameObject("ItemRoot", typeof(RectTransform));
            itemRootObject.transform.SetParent(slotRoot.parent, false);
            itemRoot = itemRootObject.GetComponent<RectTransform>();
            itemRoot.anchorMin = slotRoot.anchorMin;
            itemRoot.anchorMax = slotRoot.anchorMax;
            itemRoot.pivot = slotRoot.pivot;
            itemRoot.anchoredPosition = slotRoot.anchoredPosition;
            itemRoot.sizeDelta = slotRoot.sizeDelta;
            itemRoot.SetSiblingIndex(slotRoot.GetSiblingIndex() + 1);
        }

        EnsureNotificationText();
        EnsurePickupPromptText();
    }

    void EnsureNotificationText()
    {
        if (notificationText != null || !createNotificationIfMissing)
        {
            return;
        }

        Transform existing = FindChildByName(transform, "Inventory Full Notification");
        if (existing == null && inventoryRoot != null)
        {
            existing = FindChildByName(inventoryRoot.transform, "Inventory Full Notification");
        }

        if (existing != null)
        {
            notificationText = existing.GetComponent<TMP_Text>();
            return;
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        Transform parent = canvas != null ? canvas.transform : transform;
        GameObject textObject = new GameObject("Inventory Full Notification", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -80f);
        rect.sizeDelta = new Vector2(520f, 60f);

        notificationText = textObject.GetComponent<TMP_Text>();
        notificationText.alignment = TextAlignmentOptions.Center;
        notificationText.fontSize = 32f;
        notificationText.raycastTarget = false;
        notificationText.color = notificationColor;
        notificationText.text = inventoryFullMessage;
        notificationText.gameObject.SetActive(false);
    }

    void EnsurePickupPromptText()
    {
        if (pickupPromptText != null || !createPickupPromptIfMissing)
        {
            return;
        }

        Transform existing = FindChildByName(transform, "Pickup Prompt");
        if (existing == null && inventoryRoot != null)
        {
            existing = FindChildByName(inventoryRoot.transform, "Pickup Prompt");
        }

        if (existing != null)
        {
            TMP_Text existingText = existing.GetComponent<TMP_Text>();
            if (existingText != null)
            {
                pickupPromptText = existingText;
                return;
            }
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        Transform parent = canvas != null ? canvas.transform : transform;
        GameObject textObject = new GameObject("Pickup Prompt", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -180f);
        rect.sizeDelta = new Vector2(640f, 60f);

        pickupPromptText = textObject.GetComponent<TMP_Text>();
        pickupPromptText.alignment = TextAlignmentOptions.Center;
        pickupPromptText.fontSize = 28f;
        pickupPromptText.raycastTarget = false;
        pickupPromptText.color = pickupPromptColor;
        pickupPromptText.gameObject.SetActive(false);
    }

    InventoryEntry[] GetInventoryItems()
    {
        if (inventory != null && inventory.Length > 0)
        {
            return inventory;
        }

        if (usePlayerWeaponSlotsAsFallback && weaponEquip != null)
        {
            return ConvertWeaponSlots(weaponEquip.weaponSlots);
        }

        return null;
    }

    int GetSlotCount()
    {
        InventoryEntry[] items = GetInventoryItems();
        if (items != null && items.Length > 0)
        {
            return items.Length;
        }

        return Mathf.Max(1, gridWidth * gridHeight);
    }

    void GenerateSlots()
    {
        if (slotRoot == null || slotTemplate == null)
        {
            return;
        }

        int slotCount = GetSlotCount();
        gridHeight = Mathf.Max(1, Mathf.CeilToInt(slotCount / (float)Mathf.Max(1, gridWidth)));

        Vector2 startPosition = slotTemplate.anchoredPosition;
        slotTemplate.gameObject.SetActive(slotCount > 0);
        ApplySlotTransform(slotTemplate, 0, startPosition);

        int generatedCount = CountGeneratedSlots();
        while (generatedCount < slotCount - 1)
        {
            RectTransform slot = Instantiate(slotTemplate, slotRoot);
            slot.name = GeneratedSlotPrefix + generatedCount;
            generatedCount++;
        }

        int slotIndex = 0;
        slotTemplate.name = "Index 0";
        ApplySlotTransform(slotTemplate, slotIndex, startPosition);
        slotIndex++;

        for (int i = 0; i < slotRoot.childCount; i++)
        {
            RectTransform slot = slotRoot.GetChild(i) as RectTransform;
            if (slot == null || slot == slotTemplate || !IsGeneratedSlot(slot))
            {
                continue;
            }

            bool active = slotIndex < slotCount;
            slot.gameObject.SetActive(active);
            if (active)
            {
                slot.name = GeneratedSlotPrefix + slotIndex;
                ApplySlotTransform(slot, slotIndex, startPosition);
            }

            slotIndex++;
        }
    }

    void ApplySlotTransform(RectTransform slot, int index, Vector2 startPosition)
    {
        int columns = Mathf.Max(1, gridWidth);
        int column = index % columns;
        int row = index / columns;
        slot.anchoredPosition = startPosition + new Vector2(column * slotSpacing.x, row * slotSpacing.y);
    }

    bool TryFindFreePosition(int preferredIndex, InventoryEntry entry, bool[] occupied, out Vector2Int position, out bool rotated)
    {
        rotated = false;
        if (entry == null || entry.IsEmpty)
        {
            position = Vector2Int.zero;
            return false;
        }

        int preferredX = preferredIndex % Mathf.Max(1, gridWidth);
        int preferredY = preferredIndex / Mathf.Max(1, gridWidth);
        if (CanPlace(occupied, preferredX, preferredY, GetWidth(entry, false), GetHeight(entry, false)))
        {
            position = new Vector2Int(preferredX, preferredY);
            return true;
        }

        if (CanRotate(entry) && CanPlace(occupied, preferredX, preferredY, GetWidth(entry, true), GetHeight(entry, true)))
        {
            position = new Vector2Int(preferredX, preferredY);
            rotated = true;
            return true;
        }

        return TryFindFreePosition(entry, occupied, out position, out rotated);
    }

    void SubscribePlayerHealth()
    {
        if (playerHealth != null)
        {
            playerHealth.Damaged -= HandlePlayerDamaged;
            playerHealth.Damaged += HandlePlayerDamaged;
        }
    }

    void UnsubscribePlayerHealth()
    {
        if (playerHealth != null)
        {
            playerHealth.Damaged -= HandlePlayerDamaged;
        }
    }

    void HandlePlayerDamaged()
    {
        if (closeWhenPlayerDamaged)
        {
            SetOpen(false);
        }
    }

    void SetControlsFrozen(bool frozen)
    {
        if (!freezeControlsWhenOpen)
        {
            frozen = false;
        }

        if (frozen == controlsFrozen)
        {
            return;
        }

        if (frozen)
        {
            previousMovementInput = playerMovement == null || playerMovement.allowInput;
            previousShootEnabled = playerShoot != null && playerShoot.enabled;
            previousWeaponEquipInput = weaponEquip == null || weaponEquip.allowInput;
            previousWeaponAimInput = weaponAnimator == null || weaponAnimator.allowAimInput;
            previousCameraEnabled = cameraControler != null && cameraControler.enabled;
            previousScopeEnabled = scopeController != null && scopeController.enabled;
            previousCursorEnabled = cursorController != null && cursorController.enabled;
            previousCursorVisible = Cursor.visible;
            previousCursorLockState = Cursor.lockState;

            if (playerMovement != null)
            {
                playerMovement.StopLocomotionForInputFreeze();
                playerMovement.allowInput = false;
            }
            if (playerShoot != null) playerShoot.enabled = false;
            if (weaponEquip != null) weaponEquip.allowInput = false;
            if (weaponAnimator != null) weaponAnimator.SetAimInputEnabled(false, false, true);
            if (cameraControler != null) cameraControler.enabled = false;
            if (scopeController != null) scopeController.enabled = false;
            if (cursorController != null) cursorController.enabled = false;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            if (playerMovement != null) playerMovement.allowInput = previousMovementInput;
            if (playerShoot != null) playerShoot.enabled = previousShootEnabled;
            if (weaponEquip != null) weaponEquip.allowInput = previousWeaponEquipInput;
            if (weaponAnimator != null) weaponAnimator.SetAimInputEnabled(previousWeaponAimInput, false);
            if (cameraControler != null) cameraControler.enabled = previousCameraEnabled;
            if (scopeController != null) scopeController.enabled = previousScopeEnabled;
            if (cursorController != null) cursorController.enabled = previousCursorEnabled;
            Cursor.visible = previousCursorVisible;
            Cursor.lockState = previousCursorLockState;
        }

        controlsFrozen = frozen;
    }

    void SyncInventoryToPlayerEquip()
    {
        if (!syncToPlayerWeaponEquip || weaponEquip == null || inventory == null || inventory.Length == 0)
        {
            return;
        }

        WeaponSlot[] slotsToEquip = new WeaponSlot[inventory.Length];
        for (int i = 0; i < inventory.Length; i++)
        {
            slotsToEquip[i] = new WeaponSlot();
            if (inventory[i] != null)
            {
                slotsToEquip[i].weapon = inventory[i].ResolvedWeapon;
                slotsToEquip[i].weaponObject = inventory[i].weaponObject;
            }
        }

        weaponEquip.weaponSlots = slotsToEquip;
    }

    InventoryEntry[] ConvertWeaponSlots(WeaponSlot[] weaponSlots)
    {
        if (weaponSlots == null)
        {
            return null;
        }

        InventoryEntry[] entries = new InventoryEntry[weaponSlots.Length];
        for (int i = 0; i < weaponSlots.Length; i++)
        {
            entries[i] = new InventoryEntry();
            if (weaponSlots[i] != null)
            {
                entries[i].weapon = weaponSlots[i].weapon;
                entries[i].weaponObject = weaponSlots[i].weaponObject;
            }
        }

        return entries;
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

    bool IsSlotTransform(RectTransform target)
    {
        if (target == null || slotRoot == null)
        {
            return false;
        }

        if (target == slotRoot)
        {
            return true;
        }

        for (int i = 0; i < slotRoot.childCount; i++)
        {
            if (slotRoot.GetChild(i) == target)
            {
                return true;
            }
        }

        return false;
    }

    bool IsGeneratedSlot(RectTransform slot)
    {
        return slot != null && slot.name.StartsWith(GeneratedSlotPrefix);
    }

    int CountGeneratedSlots()
    {
        if (slotRoot == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < slotRoot.childCount; i++)
        {
            RectTransform slot = slotRoot.GetChild(i) as RectTransform;
            if (slot != null && slot != slotTemplate && IsGeneratedSlot(slot))
            {
                count++;
            }
        }

        return count;
    }
}

public class InventoryItemDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    InventoryGridUI inventoryGrid;
    InventoryEntry entry;

    public int ItemIndex { get; private set; }
    public InventoryEntry Entry => entry;

    public void Initialize(InventoryGridUI grid, int itemIndex, InventoryEntry inventoryEntry)
    {
        inventoryGrid = grid;
        ItemIndex = itemIndex;
        entry = inventoryEntry;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        inventoryGrid?.HandleItemClick(ItemIndex, eventData);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        inventoryGrid?.BeginItemDrag(this, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        inventoryGrid?.DragItem(this, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        inventoryGrid?.EndItemDrag(this, eventData);
    }
}
