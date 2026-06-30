using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MobileControlsUI : MonoBehaviour
{
    [SerializeField] private GameObject mobileRoot;
    [SerializeField] private bool autoShowOnMobile = true;
    [SerializeField] private bool hideOnNonMobile = true;

    private PlayerWeaponEquip weaponEquip;
    private InventoryGridUI inventory;
    private InGameOptionsMenu optionsMenu;
    private PlayerMovement playerMovement;

    private Transform pickupButton;
    private Transform emergencyUseButton;
    private Transform runButton;
    private Transform crouchButton;
    private Graphic runButtonGraphic;
    private Color runButtonNormalColor = Color.white;
    private Color runButtonOffColor = new Color(0.45f, 0.45f, 0.45f, 0.85f);
    private RectTransform crouchButtonRect;
    private Quaternion crouchDefaultRotation;
    private Image emergencyUseIcon;
    private Sprite lastEmergencyIcon;
    private readonly Image[] hotkeyIcons = new Image[4];
    private readonly Sprite[] hotkeyDefaultIcons = new Sprite[4];
    private readonly Sprite[] lastHotkeyIcons = new Sprite[4];
    private bool runToggleActive;
    private bool buttonsBound;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindAnyObjectByType<MobileControlsUI>() != null)
        {
            return;
        }

        GameObject root = FindSceneObjectByName("Mobile UI");
        if (root == null)
        {
            return;
        }

        GameObject runner = new GameObject("Mobile Controls Runtime");
        MobileControlsUI controls = runner.AddComponent<MobileControlsUI>();
        controls.mobileRoot = root;
    }

    private void Awake()
    {
        if (mobileRoot == null)
        {
            mobileRoot = FindSceneObjectByName("Mobile UI");
        }

        BindSceneReferences();
        BindButtons();
        ApplyInitialVisibility();
    }

    private void Update()
    {
        EnsureMobileRoot();
        EnsureButtonsBound();
        MobileInputBridge.SetMobileUIActive(mobileRoot != null && mobileRoot.activeInHierarchy);
        BindSceneReferencesIfMissing();
        UpdateConditionalButtons();
        UpdateToggleVisuals();
        UpdateHotkeyIcons();
    }

    private void OnDisable()
    {
        runToggleActive = false;
        MobileInputBridge.Clear();
    }

    private void BindSceneReferences()
    {
        weaponEquip = FindAnyObjectByType<PlayerWeaponEquip>();
        inventory = FindAnyObjectByType<InventoryGridUI>();
        optionsMenu = FindAnyObjectByType<InGameOptionsMenu>();
        playerMovement = FindAnyObjectByType<PlayerMovement>();
    }

    private void BindSceneReferencesIfMissing()
    {
        if (weaponEquip == null) weaponEquip = FindAnyObjectByType<PlayerWeaponEquip>();
        if (inventory == null) inventory = FindAnyObjectByType<InventoryGridUI>();
        if (optionsMenu == null) optionsMenu = FindAnyObjectByType<InGameOptionsMenu>();
        if (playerMovement == null) playerMovement = FindAnyObjectByType<PlayerMovement>();
    }

    private void ApplyInitialVisibility()
    {
        if (mobileRoot == null)
        {
            return;
        }

        if (Application.isMobilePlatform && autoShowOnMobile)
        {
            mobileRoot.SetActive(true);
        }
        else if (!Application.isEditor && hideOnNonMobile)
        {
            mobileRoot.SetActive(false);
        }
    }

    private void BindButtons()
    {
        if (mobileRoot == null)
        {
            return;
        }

        buttonsBound = true;

        BindJoystick("Analog Backround");
        runButton = BindTap("Run UI", ToggleRun);
        BindHold("Aim UI", MobileInputBridge.SetAimHeld);
        BindHold("Frie UI", MobileInputBridge.SetShootHeld, "Fire UI");
        BindHold("Block UI", MobileInputBridge.SetBlockHeld);

        BindTap("Jump UI", MobileInputBridge.QueueJump);
        BindTap("roll UI", MobileInputBridge.QueueRoll, "Roll UI");
        crouchButton = BindTap("Crouch UI", QueueCrouch);
        BindTap("Crawl UI", MobileInputBridge.QueueCrawl);
        BindTap("Reload UI", MobileInputBridge.QueueReload);
        BindTap("Melee UI", MobileInputBridge.QueueMelee);
        pickupButton = BindTap("PicUp UI", MobileInputBridge.QueuePickup, "Pickup UI");
        emergencyUseButton = BindTap("Emergen Use item UI", UseBestHealingItem, "Emergency Use item UI", "Emergency Use Item UI");
        BindTap("Inventory UI", ToggleInventory);
        BindTap("Cencel Inventory UI", CloseInventory, "Cancel Inventory UI", "Inventory Cancel UI", "Close Inventory UI", "Inventory Close UI", "Cencel Inventory", "Cancel Inventory");
        BindTap("Settings UI", OpenSettings);
        BindTap("Cencel Scope UI", CancelAim, "Cancel Scope UI");

        for (int i = 1; i <= 4; i++)
        {
            int hotkeyNumber = i;
            Transform hotkeyButton = BindTap($"Hotkey {i}", () => EquipHotkey(hotkeyNumber));
            CacheHotkeyIcon(hotkeyButton, i - 1);
        }

        CacheToggleVisuals();
        UpdateConditionalButtons(true);
        UpdateToggleVisuals(true);
        UpdateHotkeyIcons(true);
    }

    private void EnsureMobileRoot()
    {
        if (mobileRoot != null)
        {
            return;
        }

        mobileRoot = FindSceneObjectByName("Mobile UI");
        if (mobileRoot != null)
        {
            ApplyInitialVisibility();
        }
    }

    private void EnsureButtonsBound()
    {
        if (buttonsBound || mobileRoot == null)
        {
            return;
        }

        BindButtons();
    }

    private void BindJoystick(string name)
    {
        Transform target = FindChild(name);
        if (target == null)
        {
            return;
        }

        MobileJoystick joystick = target.GetComponent<MobileJoystick>();
        if (joystick == null)
        {
            joystick = target.gameObject.AddComponent<MobileJoystick>();
        }

        EnsureRaycastTarget(target);
        joystick.Configure(MobileInputBridge.SetMove);
    }

    private Transform BindHold(string primaryName, Action<bool> action, params string[] fallbackNames)
    {
        Transform target = FindChild(primaryName, fallbackNames);
        if (target == null)
        {
            return null;
        }

        MobileHoldTarget holdTarget = target.GetComponent<MobileHoldTarget>();
        if (holdTarget == null)
        {
            holdTarget = target.gameObject.AddComponent<MobileHoldTarget>();
        }

        EnsureRaycastTarget(target);
        holdTarget.Configure(action);
        return target;
    }

    private Transform BindTap(string primaryName, Action action, params string[] fallbackNames)
    {
        Transform target = FindChild(primaryName, fallbackNames);
        if (target == null)
        {
            return null;
        }

        Button button = target.GetComponent<Button>();
        if (button == null && target.GetComponent<Graphic>() != null)
        {
            button = target.gameObject.AddComponent<Button>();
        }

        if (button != null)
        {
            EnsureRaycastTarget(target);
            button.onClick.AddListener(() => action?.Invoke());
            return target;
        }

        MobileTapTarget tapTarget = target.GetComponent<MobileTapTarget>();
        if (tapTarget == null)
        {
            tapTarget = target.gameObject.AddComponent<MobileTapTarget>();
        }

        EnsureRaycastTarget(target);
        tapTarget.Configure(action);
        return target;
    }

    private void ToggleRun()
    {
        runToggleActive = !runToggleActive;
        MobileInputBridge.SetRunHeld(runToggleActive);
        UpdateToggleVisuals(true);
    }

    private void QueueCrouch()
    {
        MobileInputBridge.QueueCrouch();
    }

    private void ToggleInventory()
    {
        inventory = inventory != null ? inventory : FindAnyObjectByType<InventoryGridUI>();
        inventory?.Toggle();
    }

    private void CloseInventory()
    {
        inventory = inventory != null ? inventory : FindAnyObjectByType<InventoryGridUI>();
        inventory?.SetOpen(false);
    }

    private void UseBestHealingItem()
    {
        inventory = inventory != null ? inventory : FindAnyObjectByType<InventoryGridUI>();
        inventory?.UseBestHealingItemFromUI();
    }

    private void OpenSettings()
    {
        optionsMenu = optionsMenu != null ? optionsMenu : FindAnyObjectByType<InGameOptionsMenu>();
        optionsMenu?.OpenOptionsFromUI();
    }

    private void EquipHotkey(int hotkeyNumber)
    {
        weaponEquip = weaponEquip != null ? weaponEquip : FindAnyObjectByType<PlayerWeaponEquip>();
        weaponEquip?.TryEquipHotkeyFromUI(hotkeyNumber);
    }

    private static void CancelAim()
    {
        MobileInputBridge.SetAimHeld(false);
    }

    private void CacheToggleVisuals()
    {
        if (runButton != null)
        {
            runButtonGraphic = FindPrimaryGraphic(runButton);
            if (runButtonGraphic != null)
            {
                runButtonNormalColor = runButtonGraphic.color;
                Color grayscale = Color.Lerp(runButtonNormalColor, Color.gray, 0.75f);
                runButtonOffColor = new Color(grayscale.r, grayscale.g, grayscale.b, Mathf.Min(runButtonNormalColor.a, 0.85f));
            }
        }

        if (crouchButton != null)
        {
            crouchButtonRect = crouchButton as RectTransform;
            if (crouchButtonRect != null)
            {
                crouchDefaultRotation = crouchButtonRect.localRotation;
            }
        }

        if (emergencyUseButton != null)
        {
            emergencyUseIcon = FindIconImage(emergencyUseButton);
            StretchIconToParent(emergencyUseIcon);
        }
    }

    private void UpdateConditionalButtons(bool force = false)
    {
        if (pickupButton != null)
        {
            bool showPickup = inventory != null && inventory.HasActivePickupPrompt;
            if (force || pickupButton.gameObject.activeSelf != showPickup)
            {
                pickupButton.gameObject.SetActive(showPickup);
            }
        }

        if (emergencyUseButton != null)
        {
            bool showEmergencyUse = inventory != null && inventory.ShouldShowEmergencyHealingUse();
            if (force || emergencyUseButton.gameObject.activeSelf != showEmergencyUse)
            {
                emergencyUseButton.gameObject.SetActive(showEmergencyUse);
            }

            if (showEmergencyUse)
            {
                UpdateEmergencyUseIcon();
            }
        }
    }

    private void UpdateEmergencyUseIcon()
    {
        if (emergencyUseIcon == null || inventory == null)
        {
            return;
        }

        Sprite icon = inventory.GetBestHealingItemIcon();
        if (lastEmergencyIcon == icon && emergencyUseIcon.enabled == (icon != null))
        {
            return;
        }

        lastEmergencyIcon = icon;
        emergencyUseIcon.sprite = icon;
        emergencyUseIcon.enabled = icon != null;
        emergencyUseIcon.preserveAspect = true;
    }

    private void UpdateToggleVisuals(bool force = false)
    {
        if (runButtonGraphic != null && (force || runButtonGraphic.color != (runToggleActive ? runButtonNormalColor : runButtonOffColor)))
        {
            runButtonGraphic.color = runToggleActive ? runButtonNormalColor : runButtonOffColor;
        }

        if (crouchButtonRect != null)
        {
            bool isCrouching = playerMovement != null && playerMovement.IsCrouching;
            Quaternion targetRotation = isCrouching
                ? crouchDefaultRotation * Quaternion.Euler(0f, 0f, 180f)
                : crouchDefaultRotation;

            if (force || Quaternion.Angle(crouchButtonRect.localRotation, targetRotation) > 0.01f)
            {
                crouchButtonRect.localRotation = targetRotation;
            }
        }
    }

    private void CacheHotkeyIcon(Transform hotkeyButton, int index)
    {
        if (hotkeyButton == null || index < 0 || index >= hotkeyIcons.Length)
        {
            return;
        }

        Image icon = FindIconImage(hotkeyButton);
        StretchIconToParent(icon);
        hotkeyIcons[index] = icon;
        hotkeyDefaultIcons[index] = icon != null ? icon.sprite : null;
        lastHotkeyIcons[index] = null;
    }

    private void UpdateHotkeyIcons(bool force = false)
    {
        if (weaponEquip == null)
        {
            return;
        }

        for (int i = 0; i < hotkeyIcons.Length; i++)
        {
            Image iconImage = hotkeyIcons[i];
            if (iconImage == null)
            {
                continue;
            }

            Weapon weapon = weaponEquip.GetHotkeyWeapon(i + 1);
            Sprite targetIcon = weapon != null && weapon.inventoryIcon != null
                ? weapon.inventoryIcon
                : hotkeyDefaultIcons[i];

            if (!force && lastHotkeyIcons[i] == targetIcon && iconImage.sprite == targetIcon)
            {
                continue;
            }

            lastHotkeyIcons[i] = targetIcon;
            iconImage.sprite = targetIcon;
            iconImage.enabled = targetIcon != null;
            iconImage.preserveAspect = true;
        }
    }

    private static Graphic FindPrimaryGraphic(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        Graphic graphic = root.GetComponent<Graphic>();
        if (graphic != null)
        {
            return graphic;
        }

        return root.GetComponentInChildren<Graphic>(true);
    }

    private static Image FindIconImage(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        Transform iconTransform = FindChildByName(root, "Icon");
        if (iconTransform != null && iconTransform.TryGetComponent(out Image iconImage))
        {
            return iconImage;
        }

        Image[] images = root.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null && images[i].transform != root)
            {
                return images[i];
            }
        }

        return root.GetComponent<Image>();
    }

    private static void EnsureRaycastTarget(Transform target)
    {
        if (target == null)
        {
            return;
        }

        Graphic graphic = target.GetComponent<Graphic>();
        if (graphic != null)
        {
            graphic.raycastTarget = true;
        }

        Selectable selectable = target.GetComponent<Selectable>();
        if (selectable != null && selectable.targetGraphic != null)
        {
            selectable.targetGraphic.raycastTarget = true;
        }
    }

    private static void StretchIconToParent(Image icon)
    {
        if (icon == null)
        {
            return;
        }

        RectTransform rect = icon.rectTransform;
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private Transform FindChild(string primaryName, params string[] fallbackNames)
    {
        if (mobileRoot == null)
        {
            return null;
        }

        Transform found = FindChildByName(mobileRoot.transform, primaryName);
        if (found != null)
        {
            return found;
        }

        for (int i = 0; i < fallbackNames.Length; i++)
        {
            found = FindChildByName(mobileRoot.transform, fallbackNames[i]);
            if (found != null)
            {
                return found;
            }
        }

        GameObject sceneObject = FindSceneObjectByName(primaryName);
        if (sceneObject != null)
        {
            return sceneObject.transform;
        }

        for (int i = 0; i < fallbackNames.Length; i++)
        {
            sceneObject = FindSceneObjectByName(fallbackNames[i]);
            if (sceneObject != null)
            {
                return sceneObject.transform;
            }
        }

        return null;
    }

    private static Transform FindChildByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        if (string.Equals(root.name.Trim(), targetName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildByName(root.GetChild(i), targetName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static GameObject FindSceneObjectByName(string objectName)
    {
        RectTransform[] transforms = Resources.FindObjectsOfTypeAll<RectTransform>();
        for (int i = 0; i < transforms.Length; i++)
        {
            RectTransform rect = transforms[i];
            if (rect == null || !rect.gameObject.scene.IsValid() || rect.gameObject.scene != SceneManager.GetActiveScene())
            {
                continue;
            }

            if (string.Equals(rect.name.Trim(), objectName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return rect.gameObject;
            }
        }

        return null;
    }
}

public sealed class MobileHoldTarget : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    private Action<bool> onHoldChanged;

    public void Configure(Action<bool> callback)
    {
        onHoldChanged = callback;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        onHoldChanged?.Invoke(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        onHoldChanged?.Invoke(false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (eventData.pointerPress == gameObject)
        {
            onHoldChanged?.Invoke(false);
        }
    }

    private void OnDisable()
    {
        onHoldChanged?.Invoke(false);
    }
}

public sealed class MobileTapTarget : MonoBehaviour, IPointerClickHandler
{
    private Action onTap;

    public void Configure(Action callback)
    {
        onTap = callback;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        onTap?.Invoke();
    }
}

public sealed class MobileJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private RectTransform handle;
    [SerializeField] private float handleRadius = 65f;

    private RectTransform rectTransform;
    private Action<Vector2> onValueChanged;

    public void Configure(Action<Vector2> callback)
    {
        onValueChanged = callback;
        rectTransform = transform as RectTransform;
        if (handle == null && transform.childCount > 0)
        {
            handle = transform.GetChild(0) as RectTransform;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (rectTransform == null)
        {
            rectTransform = transform as RectTransform;
        }

        if (rectTransform == null)
        {
            return;
        }

        Camera eventCamera = eventData.pressEventCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventCamera, out Vector2 localPoint))
        {
            return;
        }

        Vector2 value = Vector2.ClampMagnitude(localPoint / Mathf.Max(1f, handleRadius), 1f);
        if (handle != null)
        {
            handle.anchoredPosition = value * handleRadius;
        }

        onValueChanged?.Invoke(value);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (handle != null)
        {
            handle.anchoredPosition = Vector2.zero;
        }

        onValueChanged?.Invoke(Vector2.zero);
    }

    private void OnDisable()
    {
        if (handle != null)
        {
            handle.anchoredPosition = Vector2.zero;
        }

        onValueChanged?.Invoke(Vector2.zero);
    }
}
