using UnityEngine;
using UnityEngine.UI;

public class HudValueBar : MonoBehaviour
{
    public Slider slider;
    public Image fillImage;
    public RectTransform fillRect;
    public Slider targetSlider;
    public Image targetFillImage;
    public RectTransform targetFillRect;
    public bool useTargetFill = true;
    public Color targetFillColor = new Color(1f, 1f, 1f, 0.35f);
    public Color fullColor = new Color(0f, 0.35f, 1f, 1f);
    public Color healthyColor = new Color(0f, 0.85f, 0.2f, 1f);
    public Color warningColor = new Color(1f, 0.85f, 0f, 1f);
    public Color dangerColor = new Color(1f, 0f, 0f, 1f);
    public float blinkSpeed = 8f;
    public float blinkMinAlpha = 0.25f;
    public bool repairTargetRectWhenCollapsed = true;

    void Awake()
    {
        AutoBind();
    }

    public void SetValue(float current, float max)
    {
        SetValue(current, max, current);
    }

    public void SetValue(float current, float max, float target)
    {
        AutoBind();

        float normalized = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        float targetNormalized = max > 0f ? Mathf.Clamp01(target / max) : normalized;
        targetNormalized = Mathf.Max(targetNormalized, normalized);

        if (useTargetFill)
        {
            Slider effectiveTargetSlider = targetSlider != slider ? targetSlider : null;
            SetBarValue(effectiveTargetSlider, targetFillRect, targetNormalized);
        }

        SetBarValue(slider, fillRect, normalized);

        if (useTargetFill && targetFillImage != null)
        {
            targetFillImage.enabled = true;
            targetFillImage.color = targetFillColor;
        }

        if (fillImage != null)
        {
            Color color = GetColor(normalized);
            if (normalized <= 0.3f)
            {
                float blink = Mathf.Lerp(blinkMinAlpha, 1f, Mathf.PingPong(Time.time * blinkSpeed, 1f));
                color.a = blink;
            }

            fillImage.color = color;
        }
    }

    void SetBarValue(Slider valueSlider, RectTransform valueRect, float normalized)
    {
        if (valueSlider != null)
        {
            valueSlider.minValue = 0f;
            valueSlider.maxValue = 1f;
            if (valueRect != null)
            {
                valueSlider.fillRect = valueRect;
            }

            RectTransform sliderFillRect = valueSlider.fillRect;
            if (sliderFillRect != null)
            {
                sliderFillRect.anchorMin = Vector2.zero;
                sliderFillRect.anchorMax = new Vector2(normalized, 1f);
                sliderFillRect.offsetMin = Vector2.zero;
                sliderFillRect.offsetMax = Vector2.zero;
                sliderFillRect.localScale = Vector3.one;
                sliderFillRect.pivot = new Vector2(0f, sliderFillRect.pivot.y);
            }

            valueSlider.value = normalized;
            return;
        }

        if (valueRect != null)
        {
            valueRect.anchorMin = new Vector2(0f, valueRect.anchorMin.y);
            valueRect.anchorMax = new Vector2(1f, valueRect.anchorMax.y);
            valueRect.pivot = new Vector2(0f, valueRect.pivot.y);
            Vector3 scale = valueRect.localScale;
            scale.x = normalized;
            valueRect.localScale = scale;
        }
    }

    public Color GetColor(float normalized)
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

    void AutoBind()
    {
        slider = slider != null ? slider : GetComponent<Slider>();

        if (slider != null)
        {
            fillRect = fillRect != null ? fillRect : slider.fillRect;
            if (fillImage == null && fillRect != null)
            {
                fillImage = fillRect.GetComponent<Image>();
            }
        }

        if (!useTargetFill)
        {
            targetSlider = null;
            targetFillImage = null;
            targetFillRect = null;
        }

        if (useTargetFill && targetSlider == null)
        {
            Transform targetSliderTransform = transform.Find("Regen Slider");
            if (targetSliderTransform == null)
            {
                targetSliderTransform = transform.Find("Regen");
            }

            targetSlider = targetSliderTransform != null ? targetSliderTransform.GetComponent<Slider>() : null;
        }

        if (useTargetFill && targetSlider != null)
        {
            if (targetSlider.fillRect != null)
            {
                targetFillRect = targetSlider.fillRect;
                Image sliderFillImage = targetFillRect.GetComponent<Image>();
                if (sliderFillImage != null)
                {
                    targetFillImage = sliderFillImage;
                }
            }

            Image targetSliderImage = targetSlider.GetComponent<Image>();
            if (targetSliderImage != null && targetSliderImage != targetFillImage)
            {
                targetSliderImage.enabled = false;
                targetSliderImage.raycastTarget = false;
            }

            if (targetFillImage == null && targetFillRect != null)
            {
                targetFillImage = targetFillRect.GetComponent<Image>();
            }

            RepairTargetRectIfNeeded();
        }

        if (useTargetFill && (targetFillRect == null || targetFillImage == null))
        {
            Transform targetFill = transform.Find("Regen Fill");
            if (targetFill == null)
            {
                targetFill = transform.Find("Regen");
            }

            if (targetFill == null)
            {
                targetFill = transform.Find("Target Fill");
            }

            if (targetFill != null)
            {
                targetFillRect = targetFillRect != null ? targetFillRect : targetFill.GetComponent<RectTransform>();
                targetFillImage = targetFillImage != null ? targetFillImage : targetFill.GetComponent<Image>();
            }
        }

        if (slider != null || (fillImage != null && fillRect != null))
        {
            return;
        }

        Transform fill = transform.Find("Fill");
        if (fill == null)
        {
            return;
        }

        fillRect = fillRect != null ? fillRect : fill.GetComponent<RectTransform>();
        fillImage = fillImage != null ? fillImage : fill.GetComponent<Image>();

        if (fillRect != null)
        {
            fillRect.pivot = new Vector2(0f, 0.5f);
        }
    }

    void RepairTargetRectIfNeeded()
    {
        if (!repairTargetRectWhenCollapsed || targetSlider == null)
        {
            return;
        }

        RectTransform targetSliderRect = targetSlider.GetComponent<RectTransform>();
        if (targetSliderRect == null)
        {
            return;
        }

        RectTransform parentRect = targetSliderRect.parent as RectTransform;
        if (parentRect == null)
        {
            return;
        }

        if (targetSliderRect.rect.width > 1f && targetSliderRect.rect.height > 1f)
        {
            return;
        }

        targetSliderRect.anchorMin = Vector2.zero;
        targetSliderRect.anchorMax = Vector2.one;
        targetSliderRect.offsetMin = Vector2.zero;
        targetSliderRect.offsetMax = Vector2.zero;
        targetSliderRect.pivot = new Vector2(0.5f, 0.5f);
        targetSliderRect.localScale = Vector3.one;
    }
}
