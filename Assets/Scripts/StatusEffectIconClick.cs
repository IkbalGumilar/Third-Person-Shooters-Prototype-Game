using UnityEngine;
using UnityEngine.EventSystems;

public class StatusEffectIconClick : MonoBehaviour, IPointerClickHandler
{
    private StatusEffectHUD hud;
    private ActiveStatusEffect activeEffect;

    public void Initialize(StatusEffectHUD owner, ActiveStatusEffect effect)
    {
        hud = owner;
        activeEffect = effect;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (hud != null && activeEffect != null)
        {
            hud.ShowEffectInfo(activeEffect);
        }
    }
}
