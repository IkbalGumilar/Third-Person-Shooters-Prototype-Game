using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnemyWorldHealthBar : MonoBehaviour
{
    private const string HealthBarName = "Enemy Health Bar";
    private const string BackgroundName = "Background";
    private const string ShieldBarName = "Shield Bar";
    private const string RegenFillName = "Regen Fill";
    private const string FillName = "Fill";
    private const string StatusRootName = "Status Icons";

    public Enemy enemy;
    public EnemyStatusEffectController statusController;
    public Transform targetCamera;
    public Vector3 worldOffset = new Vector3(0f, 2.1f, 0f);
    public Vector2 size = new Vector2(1.2f, 0.12f);
    [Header("Physical Shield")]
    public Color shieldBarColor = Color.white;
    public Color shieldOverchargeColor = new Color(0.55f, 0.9f, 1f, 1f);
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.65f);
    public Color fullColor = new Color(0f, 0.35f, 1f, 1f);
    public Color healthyColor = new Color(0f, 0.85f, 0.2f, 1f);
    public Color warningColor = new Color(1f, 0.85f, 0f, 1f);
    public Color dangerColor = new Color(1f, 0f, 0f, 1f);
    public Color regenFillColor = new Color(1f, 1f, 1f, 0.35f);
    public float blinkSpeed = 8f;
    public float blinkMinAlpha = 0.25f;
    public bool hideWhenFull;
    [Header("Status Icons")]
    public bool showStatusIcons = true;
    public Vector2 statusIconSize = new Vector2(0.16f, 0.16f);
    public Vector2 statusIconSpacing = new Vector2(0.18f, 0f);
    public float statusIconYOffset = 0.18f;
    public float statusIconLeftPadding = 0.04f;
    public string statusStackName = "Stack";
    public float statusBlinkRemainingTime = 5f;
    public byte statusBlinkMinAlpha = 60;
    public byte statusBlinkMaxAlpha = 255;
    public float statusBlinkSpeed = 10f;

    private Canvas canvas;
    private RectTransform canvasRect;
    private RectTransform shieldBarRect;
    private Image shieldBarImage;
    private float shieldNormalizedValue;
    private RectTransform regenFillRect;
    private Image regenFillImage;
    private RectTransform fillRect;
    private Image fillImage;
    private RectTransform statusRoot;
    private EnemyStatusEffectController subscribedStatusController;
    private readonly List<StatusIconInstance> statusIcons = new List<StatusIconInstance>();

    class StatusIconInstance
    {
        public GameObject root;
        public Image icon;
        public ActiveStatusEffect effect;
        public Color baseColor;
    }

    void Awake()
    {
        enemy = enemy != null ? enemy : GetComponent<Enemy>();
        statusController = statusController != null ? statusController : GetComponent<EnemyStatusEffectController>();
        BuildIfNeeded();
        SubscribeStatusController();
    }

    void OnEnable()
    {
        SubscribeStatusController();
        RefreshStatusIcons();
    }

    void OnDisable()
    {
        UnsubscribeStatusController();
    }

    void LateUpdate()
    {
        if (statusController == null)
        {
            statusController = GetComponent<EnemyStatusEffectController>();
        }

        if (subscribedStatusController != statusController)
        {
            SubscribeStatusController();
            RefreshStatusIcons();
        }

        if (enemy != null)
        {
            SetHealth(enemy.CurrentHealth, enemy.MaxHealth, enemy.RegenHealth);
            SetPhysicalShield(enemy.CurrentPhysicalShield, enemy.MaxHealth);
        }

        UpdateStatusIconBlink();

        if (targetCamera == null && Camera.main != null)
        {
            targetCamera = Camera.main.transform;
        }

        if (canvasRect != null)
        {
            canvasRect.position = transform.position + worldOffset;
        }

        if (targetCamera != null && canvas != null)
        {
            canvas.transform.forward = targetCamera.forward;
        }
    }

    public void SetEnemy(Enemy newEnemy)
    {
        enemy = newEnemy;
        statusController = enemy != null ? enemy.GetComponent<EnemyStatusEffectController>() : null;
        BuildIfNeeded();
        SubscribeStatusController();
        RefreshStatusIcons();

        if (enemy != null)
        {
            SetHealth(enemy.CurrentHealth, enemy.MaxHealth, enemy.RegenHealth);
            SetPhysicalShield(enemy.CurrentPhysicalShield, enemy.MaxHealth);
        }
    }

    public void SetHealth(float currentHealth, float maxHealth)
    {
        SetHealth(currentHealth, maxHealth, currentHealth);
    }

    public void SetHealth(float currentHealth, float maxHealth, float regenHealth)
    {
        BuildIfNeeded();

        float value = maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
        float regenValue = maxHealth > 0f ? Mathf.Clamp01(regenHealth / maxHealth) : value;
        regenValue = Mathf.Max(regenValue, value);

        if (regenFillRect != null)
        {
            regenFillRect.localScale = new Vector3(regenValue, 1f, 1f);
        }

        if (regenFillImage != null)
        {
            regenFillImage.color = regenFillColor;
        }

        if (fillRect != null)
        {
            fillRect.localScale = new Vector3(value, 1f, 1f);
        }

        if (fillImage != null)
        {
            Color color = GetColor(value);
            if (value <= 0.3f)
            {
                color.a = Mathf.Lerp(blinkMinAlpha, 1f, Mathf.PingPong(Time.time * blinkSpeed, 1f));
            }

            fillImage.color = color;
        }

        if (canvas != null)
        {
            canvas.enabled = !hideWhenFull || value < 0.999f || HasVisibleStatusIcons() || enemy != null && enemy.HasPhysicalShield;
        }
    }

    public void SetPhysicalShield(float currentShield, float maxShield)
    {
        bool hasShield = currentShield > 0f;
        shieldNormalizedValue = maxShield > 0f ? Mathf.Clamp01(currentShield / maxShield) : 0f;
        BuildIfNeeded();
        if (shieldBarImage == null)
        {
            return;
        }

        shieldBarImage.gameObject.SetActive(hasShield);
        if (!hasShield)
        {
            return;
        }

        if (shieldBarRect != null)
        {
            shieldBarRect.anchorMax = new Vector2(shieldNormalizedValue, 1f);
        }
        bool isOvercharged = maxShield > 0f && currentShield >= maxShield * 1.01f;
        shieldBarImage.color = isOvercharged ? shieldOverchargeColor : shieldBarColor;
    }

    void BuildIfNeeded()
    {
        if (canvas != null && fillRect != null)
        {
            EnsureShieldBar();
            EnsureStatusRoot();
            return;
        }

        if (TryUseExistingHealthBar())
        {
            EnsureShieldBar();
            EnsureStatusRoot();
            return;
        }

        GameObject canvasObject = new GameObject(HealthBarName);
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 20;

        canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = size;
        canvasRect.localScale = Vector3.one;

        GameObject backgroundObject = new GameObject(BackgroundName);
        backgroundObject.transform.SetParent(canvasObject.transform, false);

        RectTransform backgroundRect = backgroundObject.AddComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        Image backgroundImage = backgroundObject.AddComponent<Image>();
        backgroundImage.color = backgroundColor;
        backgroundImage.raycastTarget = false;

        EnsureShieldBar();

        GameObject regenFillObject = new GameObject(RegenFillName);
        regenFillObject.transform.SetParent(backgroundObject.transform, false);

        regenFillRect = regenFillObject.AddComponent<RectTransform>();
        ConfigureFillRect(regenFillRect);

        regenFillImage = regenFillObject.AddComponent<Image>();
        regenFillImage.color = regenFillColor;
        regenFillImage.raycastTarget = false;

        GameObject fillObject = new GameObject(FillName);
        fillObject.transform.SetParent(backgroundObject.transform, false);

        fillRect = fillObject.AddComponent<RectTransform>();
        ConfigureFillRect(fillRect);

        fillImage = fillObject.AddComponent<Image>();
        fillImage.color = fullColor;
        fillImage.raycastTarget = false;

        EnsureStatusRoot();
    }

    bool TryUseExistingHealthBar()
    {
        Transform selectedBar = null;

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name != HealthBarName || child.GetComponent<Canvas>() == null)
            {
                continue;
            }

            if (selectedBar == null)
            {
                selectedBar = child;
            }
            else
            {
                Destroy(child.gameObject);
            }
        }

        if (selectedBar == null)
        {
            return false;
        }

        canvas = selectedBar.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 20;

        canvasRect = selectedBar.GetComponent<RectTransform>();
        canvasRect.sizeDelta = size;
        canvasRect.localScale = Vector3.one;

        Transform background = selectedBar.Find(BackgroundName);
        Transform regenFill = background != null ? background.Find(RegenFillName) : null;
        Transform fill = background != null ? background.Find(FillName) : null;

        regenFillRect = regenFill != null ? regenFill.GetComponent<RectTransform>() : null;
        regenFillImage = regenFill != null ? regenFill.GetComponent<Image>() : null;
        fillRect = fill != null ? fill.GetComponent<RectTransform>() : null;
        fillImage = fill != null ? fill.GetComponent<Image>() : null;
        EnsureShieldBar();
        EnsureStatusRoot();

        if (fillRect != null && fillImage != null)
        {
            return true;
        }

        Destroy(selectedBar.gameObject);
        canvas = null;
        canvasRect = null;
        regenFillRect = null;
        regenFillImage = null;
        fillRect = null;
        fillImage = null;

        return false;
    }

    void EnsureShieldBar()
    {
        if (canvasRect == null)
        {
            return;
        }

        Transform background = canvasRect.Find(BackgroundName);
        if (background == null)
        {
            return;
        }

        Transform existing = background.Find(ShieldBarName);
        if (existing == null)
        {
            // Migrate the bar created by an earlier version of this component.
            existing = canvasRect.Find(ShieldBarName);
            if (existing != null)
            {
                existing.SetParent(background, false);
            }
        }

        if (existing != null)
        {
            shieldBarImage = existing.GetComponent<Image>();
        }

        if (shieldBarImage == null)
        {
            GameObject shieldObject = new GameObject(ShieldBarName);
            shieldObject.transform.SetParent(background, false);
            shieldBarImage = shieldObject.AddComponent<Image>();
            shieldBarImage.raycastTarget = false;
        }

        RectTransform shieldRect = shieldBarImage.rectTransform;
        shieldBarRect = shieldRect;
        shieldRect.anchorMin = Vector2.zero;
        shieldRect.anchorMax = new Vector2(shieldNormalizedValue, 1f);
        shieldRect.pivot = new Vector2(0f, 0.5f);
        shieldRect.offsetMin = Vector2.zero;
        shieldRect.offsetMax = Vector2.zero;
        shieldRect.localScale = Vector3.one;
        shieldBarImage.transform.SetSiblingIndex(0);
        shieldBarImage.color = shieldBarColor;
        shieldBarImage.type = Image.Type.Simple;
        shieldBarImage.gameObject.SetActive(enemy != null && enemy.HasPhysicalShield);
    }

    void EnsureStatusRoot()
    {
        if (canvasRect == null)
        {
            return;
        }

        Transform existing = canvasRect.Find(StatusRootName);
        if (existing != null)
        {
            statusRoot = existing.GetComponent<RectTransform>();
        }

        if (statusRoot == null)
        {
            GameObject rootObject = new GameObject(StatusRootName);
            rootObject.transform.SetParent(canvasRect, false);
            statusRoot = rootObject.AddComponent<RectTransform>();
        }

        statusRoot.anchorMin = new Vector2(0f, 0.5f);
        statusRoot.anchorMax = new Vector2(0f, 0.5f);
        statusRoot.pivot = new Vector2(0f, 0.5f);
        statusRoot.anchoredPosition = new Vector2(statusIconLeftPadding, statusIconYOffset);
        statusRoot.sizeDelta = new Vector2(size.x, statusIconSize.y);
    }

    void SubscribeStatusController()
    {
        if (subscribedStatusController == statusController)
        {
            return;
        }

        UnsubscribeStatusController();
        subscribedStatusController = statusController;
        if (subscribedStatusController != null)
        {
            subscribedStatusController.EffectsChanged += RefreshStatusIcons;
        }
    }

    void UnsubscribeStatusController()
    {
        if (subscribedStatusController != null)
        {
            subscribedStatusController.EffectsChanged -= RefreshStatusIcons;
        }

        subscribedStatusController = null;
    }

    void RefreshStatusIcons()
    {
        ClearStatusIcons();
        if (!showStatusIcons || statusController == null)
        {
            return;
        }

        BuildIfNeeded();
        EnsureStatusRoot();
        if (statusRoot == null)
        {
            return;
        }

        int visibleCount = GetVisibleStatusIconCount();
        int visibleIndex = 0;
        for (int i = 0; i < statusController.activeEffects.Count; i++)
        {
            ActiveStatusEffect active = statusController.activeEffects[i];
            StatusEffectData data = active != null ? active.data : null;
            if (!CanShowStatusIcon(data))
            {
                continue;
            }

            GameObject iconObject = new GameObject(data.effectName);
            iconObject.transform.SetParent(statusRoot, false);

            RectTransform iconRect = iconObject.AddComponent<RectTransform>();
            iconRect.sizeDelta = statusIconSize;
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = GetStatusIconPosition(visibleIndex, visibleCount);

            Image icon = iconObject.AddComponent<Image>();
            icon.sprite = data.statusIcon;
            icon.color = data.iconTint;
            icon.raycastTarget = false;

            CreateStatusStackText(iconObject.transform, active.stacks);

            statusIcons.Add(new StatusIconInstance
            {
                root = iconObject,
                icon = icon,
                effect = active,
                baseColor = data.iconTint
            });
            visibleIndex++;
        }
    }

    void ClearStatusIcons()
    {
        for (int i = statusIcons.Count - 1; i >= 0; i--)
        {
            if (statusIcons[i] != null && statusIcons[i].root != null)
            {
                Destroy(statusIcons[i].root);
            }
        }

        statusIcons.Clear();
    }

    int GetVisibleStatusIconCount()
    {
        if (statusController == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < statusController.activeEffects.Count; i++)
        {
            ActiveStatusEffect active = statusController.activeEffects[i];
            if (CanShowStatusIcon(active != null ? active.data : null))
            {
                count++;
            }
        }

        return count;
    }

    bool CanShowStatusIcon(StatusEffectData data)
    {
        return data != null && data.showInStatusHud && data.statusIcon != null;
    }

    Vector2 GetStatusIconPosition(int visibleIndex, int visibleCount)
    {
        return new Vector2(visibleIndex * statusIconSpacing.x, visibleIndex * statusIconSpacing.y);
    }

    void CreateStatusStackText(Transform iconTransform, int stacks)
    {
        if (stacks <= 1)
        {
            return;
        }

        GameObject textObject = new GameObject(statusStackName);
        textObject.transform.SetParent(iconTransform, false);

        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(1f, 0f);
        textRect.anchorMax = new Vector2(1f, 0f);
        textRect.pivot = new Vector2(1f, 0f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = statusIconSize;

        TMP_Text stackText = textObject.AddComponent<TextMeshProUGUI>();
        stackText.text = stacks.ToString();
        stackText.fontSize = 0.08f;
        stackText.alignment = TextAlignmentOptions.BottomRight;
        stackText.color = Color.white;
        stackText.raycastTarget = false;
    }

    void UpdateStatusIconBlink()
    {
        if (statusIcons.Count == 0)
        {
            return;
        }

        float alphaPulse = Mathf.Lerp(
            statusBlinkMinAlpha / 255f,
            statusBlinkMaxAlpha / 255f,
            (Mathf.Sin(Time.time * statusBlinkSpeed) + 1f) * 0.5f
        );

        for (int i = statusIcons.Count - 1; i >= 0; i--)
        {
            StatusIconInstance instance = statusIcons[i];
            if (instance == null || instance.icon == null || instance.effect == null || instance.effect.data == null)
            {
                statusIcons.RemoveAt(i);
                continue;
            }

            Color color = instance.baseColor;
            if (!instance.effect.IsPermanent && instance.effect.remainingTime <= statusBlinkRemainingTime)
            {
                color.a *= alphaPulse;
            }

            instance.icon.color = color;
        }
    }

    bool HasVisibleStatusIcons()
    {
        return statusIcons.Count > 0;
    }

    void ConfigureFillRect(RectTransform rectTransform)
    {
        rectTransform.anchorMin = new Vector2(0f, 0f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(0f, 0.5f);
        rectTransform.offsetMin = new Vector2(0.04f, 0.025f);
        rectTransform.offsetMax = new Vector2(-0.04f, -0.025f);
    }

    Color GetColor(float normalized)
    {
        if (normalized >= 0.999f)
        {
            return fullColor;
        }

        if (normalized > 0.5f)
        {
            return healthyColor;
        }

        if (normalized > 0.3f)
        {
            return warningColor;
        }

        return dangerColor;
    }
}
