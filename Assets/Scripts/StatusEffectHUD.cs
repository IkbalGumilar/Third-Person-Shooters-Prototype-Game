using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StatusEffectHUD : MonoBehaviour
{
    class StatusIconInstance
    {
        public GameObject root;
        public Image icon;
        public ActiveStatusEffect effect;
        public Color baseColor;
    }

    public PlayerStatusEffectController statusController;
    public RectTransform statusRoot;
    public Image iconTemplate;
    public TMP_Text effectText;
    public Vector2 firstIconPosition = Vector2.zero;
    public Vector2 iconSpacing = new Vector2(60f, 0f);
    public bool useTemplatePosition = true;
    public bool hideTemplate = true;
    public string stackTextName = "Stack";
    public string effectTextName = "Effect Text";
    public string infiniteDurationText = "\u221e";
    public float blinkRemainingTime = 5f;
    public byte blinkMinAlpha = 60;
    public byte blinkMaxAlpha = 255;
    public float blinkSpeed = 10f;

    private readonly List<GameObject> spawnedIcons = new List<GameObject>();
    private readonly List<StatusIconInstance> statusIcons = new List<StatusIconInstance>();
    private PlayerStatusEffectController subscribedController;
    private ActiveStatusEffect selectedEffect;

    void Awake()
    {
        AutoBind();
        Subscribe();
        Refresh();
    }

    void OnEnable()
    {
        AutoBind();
        Subscribe();
        Refresh();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    void Update()
    {
        AutoBind();
        if (subscribedController != statusController)
        {
            Subscribe();
            Refresh();
        }

        UpdateExpiringIconBlink();
        if (Cursor.lockState == CursorLockMode.Locked && !Cursor.visible)
        {
            ClearEffectInfo();
            return;
        }

        RefreshSelectedEffectText();
    }

    public void ShowEffectInfo(ActiveStatusEffect active)
    {
        selectedEffect = active;
        RefreshSelectedEffectText();
    }

    public void ClearEffectInfo()
    {
        selectedEffect = null;
        if (effectText != null)
        {
            effectText.text = "";
        }
    }

    public void Refresh()
    {
        ClearSpawnedIcons();

        if (statusController == null || statusRoot == null)
        {
            return;
        }

        EnsureTemplate();
        if (iconTemplate == null)
        {
            return;
        }

        if (hideTemplate)
        {
            HideTemplateIcons();
        }

        Vector2 startPosition = useTemplatePosition
            ? iconTemplate.rectTransform.anchoredPosition
            : firstIconPosition;

        int visibleIndex = 0;
        for (int i = 0; i < statusController.activeEffects.Count; i++)
        {
            ActiveStatusEffect active = statusController.activeEffects[i];
            StatusEffectData data = active != null ? active.data : null;
            if (data == null || !data.showInStatusHud || data.statusIcon == null)
            {
                continue;
            }

            Image icon = Instantiate(iconTemplate, statusRoot);
            icon.gameObject.name = data.effectName;
            icon.gameObject.SetActive(true);
            icon.enabled = true;
            icon.sprite = data.statusIcon;
            icon.color = data.iconTint;
            icon.raycastTarget = true;

            RectTransform iconRect = icon.rectTransform;
            iconRect.anchoredPosition = startPosition + iconSpacing * visibleIndex;
            iconRect.localScale = Vector3.one;

            DisableEffectTextClone(icon.transform);
            UpdateStackText(icon.transform, active.stacks);
            StatusEffectIconClick click = icon.GetComponent<StatusEffectIconClick>();
            if (click == null)
            {
                click = icon.gameObject.AddComponent<StatusEffectIconClick>();
            }

            click.Initialize(this, active);
            spawnedIcons.Add(icon.gameObject);
            statusIcons.Add(new StatusIconInstance
            {
                root = icon.gameObject,
                icon = icon,
                effect = active,
                baseColor = data.iconTint
            });
            visibleIndex++;
        }

        RefreshSelectedEffectText();
    }

    void UpdateExpiringIconBlink()
    {
        if (statusIcons.Count == 0)
        {
            return;
        }

        float alphaPulse = Mathf.Lerp(
            blinkMinAlpha / 255f,
            blinkMaxAlpha / 255f,
            (Mathf.Sin(Time.time * blinkSpeed) + 1f) * 0.5f
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
            if (!instance.effect.IsPermanent && instance.effect.remainingTime <= blinkRemainingTime)
            {
                color.a *= alphaPulse;
            }

            instance.icon.color = color;
        }
    }

    void UpdateStackText(Transform iconTransform, int stacks)
    {
        TMP_Text stackText = FindStackText(iconTransform);
        if (stackText == null)
        {
            return;
        }

        bool showStack = stacks > 1;
        stackText.gameObject.SetActive(showStack);
        if (showStack)
        {
            stackText.text = stacks.ToString();
        }
    }

    TMP_Text FindStackText(Transform iconTransform)
    {
        Transform stack = iconTransform.Find(stackTextName);
        if (stack != null)
        {
            return stack.GetComponent<TMP_Text>();
        }

        return null;
    }

    void DisableEffectTextClone(Transform iconTransform)
    {
        Transform clonedText = iconTransform.Find(effectTextName);
        if (clonedText == null)
        {
            return;
        }

        TMP_Text text = clonedText.GetComponent<TMP_Text>();
        if (text != null && text != effectText)
        {
            text.gameObject.SetActive(false);
        }
    }

    void AutoBind()
    {
        if (statusController == null)
        {
            statusController = FindAnyObjectByType<PlayerStatusEffectController>();
        }

        if (statusRoot == null)
        {
            PlayerHUD playerHud = FindAnyObjectByType<PlayerHUD>();
            if (playerHud != null)
            {
                Transform status = playerHud.transform.Find("Health/Status");
                statusRoot = status != null ? status.GetComponent<RectTransform>() : null;
            }
        }

        if (effectText == null)
        {
            effectText = FindEffectText();
        }

        EnsureTemplate();
    }

    TMP_Text FindEffectText()
    {
        if (statusRoot != null)
        {
            Transform localText = statusRoot.Find(effectTextName);
            if (localText != null)
            {
                TMP_Text text = localText.GetComponent<TMP_Text>();
                if (text != null)
                {
                    return text;
                }
            }
        }

        GameObject textObject = GameObject.Find(effectTextName);
        return textObject != null ? textObject.GetComponent<TMP_Text>() : null;
    }

    void EnsureTemplate()
    {
        if (statusRoot == null || iconTemplate != null)
        {
            return;
        }

        for (int i = 0; i < statusRoot.childCount; i++)
        {
            Image childImage = statusRoot.GetChild(i).GetComponent<Image>();
            if (childImage != null)
            {
                iconTemplate = childImage;
                return;
            }
        }

        GameObject templateObject = new GameObject("Status Icon Template", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        templateObject.transform.SetParent(statusRoot, false);
        RectTransform templateRect = templateObject.GetComponent<RectTransform>();
        templateRect.sizeDelta = new Vector2(50f, 50f);
        templateRect.anchoredPosition = firstIconPosition;
        iconTemplate = templateObject.GetComponent<Image>();
        iconTemplate.raycastTarget = false;
    }

    void HideTemplateIcons()
    {
        if (statusRoot == null)
        {
            return;
        }

        for (int i = 0; i < statusRoot.childCount; i++)
        {
            Transform child = statusRoot.GetChild(i);
            Image childImage = child != null ? child.GetComponent<Image>() : null;
            if (childImage != null)
            {
                childImage.enabled = false;
                childImage.raycastTarget = false;
            }
        }
    }

    void Subscribe()
    {
        Unsubscribe();
        subscribedController = statusController;
        if (subscribedController != null)
        {
            subscribedController.EffectsChanged += Refresh;
        }
    }

    void Unsubscribe()
    {
        if (subscribedController != null)
        {
            subscribedController.EffectsChanged -= Refresh;
            subscribedController = null;
        }
    }

    void ClearSpawnedIcons()
    {
        for (int i = spawnedIcons.Count - 1; i >= 0; i--)
        {
            if (spawnedIcons[i] != null)
            {
                Destroy(spawnedIcons[i]);
            }
        }

        spawnedIcons.Clear();
        statusIcons.Clear();
    }

    void RefreshSelectedEffectText()
    {
        if (effectText == null)
        {
            return;
        }

        if (selectedEffect == null
            || selectedEffect.data == null
            || statusController == null
            || !statusController.activeEffects.Contains(selectedEffect))
        {
            effectText.text = "";
            selectedEffect = null;
            return;
        }

        StatusEffectData data = selectedEffect.data;
        string duration = data.permanent ? infiniteDurationText : FormatDuration(selectedEffect.remainingTime);
        string stack = selectedEffect.stacks > 1 ? $" x{selectedEffect.stacks}" : "";
        effectText.text = $"{data.effectName}{stack}  {duration}";
    }

    string FormatDuration(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
        int minutes = totalSeconds / 60;
        int remainingSeconds = totalSeconds % 60;
        return $"{minutes}:{remainingSeconds:00}";
    }
}
