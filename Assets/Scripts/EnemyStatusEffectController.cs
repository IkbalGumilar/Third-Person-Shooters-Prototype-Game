using System.Collections.Generic;
using UnityEngine;

public class EnemyStatusEffectController : MonoBehaviour
{
    public Enemy enemy;
    public StatusEffectData[] startEffects;
    public bool clearEffectsOnEnable = true;
    public bool applyStartEffectsOnEnable = true;
    public List<ActiveStatusEffect> activeEffects = new List<ActiveStatusEffect>();

    public event System.Action EffectsChanged;

    void Awake()
    {
        enemy = enemy != null ? enemy : GetComponent<Enemy>();
    }

    void OnEnable()
    {
        enemy = enemy != null ? enemy : GetComponent<Enemy>();

        if (clearEffectsOnEnable)
        {
            activeEffects.Clear();
        }

        if (applyStartEffectsOnEnable)
        {
            ApplyStartEffects();
        }

        EffectsChanged?.Invoke();
    }

    void Start()
    {
        if (!applyStartEffectsOnEnable)
        {
            ApplyStartEffects();
        }
    }

    void ApplyStartEffects()
    {
        if (startEffects == null)
        {
            return;
        }

        for (int i = 0; i < startEffects.Length; i++)
        {
            AddEffect(startEffects[i]);
        }
    }

    void Update()
    {
        UpdateEffects();
    }

    public void AddEffect(StatusEffectData effect)
    {
        if (effect == null || !CanApplyToEnemy(effect))
        {
            return;
        }

        ActiveStatusEffect active = FindActive(effect);
        if (active != null)
        {
            if (effect.stackMode == StatusEffectStackMode.IgnoreIfActive)
            {
                return;
            }

            if (effect.stackMode == StatusEffectStackMode.Replace)
            {
                active.stacks = 1;
            }
            else if (effect.stackMode == StatusEffectStackMode.AddStack && (effect.HasUnlimitedStacks || active.stacks < effect.maxStacks))
            {
                active.stacks++;
            }

            active.remainingTime = effect.duration;
            active.tickTimer = effect.tickInterval;
            EffectsChanged?.Invoke();
            return;
        }

        activeEffects.Add(new ActiveStatusEffect
        {
            data = effect,
            remainingTime = effect.duration,
            stacks = 1,
            tickTimer = effect.tickInterval
        });

        EffectsChanged?.Invoke();
    }

    public void RemoveEffect(StatusEffectData effect)
    {
        ActiveStatusEffect active = FindActive(effect);
        if (active == null)
        {
            return;
        }

        activeEffects.Remove(active);
        EffectsChanged?.Invoke();
    }

    public void RemovePhysicalShieldDepletedEffects()
    {
        bool changed = false;
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            ActiveStatusEffect active = activeEffects[i];
            if (active != null && active.data != null && active.data.removeWhenPhysicalShieldDepleted)
            {
                activeEffects.RemoveAt(i);
                changed = true;
            }
        }

        if (changed)
        {
            EffectsChanged?.Invoke();
        }
    }

    public float ModifyStat(StatusEffectStat stat, float baseValue)
    {
        float value = baseValue;
        for (int i = 0; i < activeEffects.Count; i++)
        {
            ActiveStatusEffect active = activeEffects[i];
            StatusEffectData effect = active != null ? active.data : null;
            if (effect == null || effect.modifiers == null)
            {
                continue;
            }

            int stackMultiplier = effect.multiplyModifierByStacks ? active.stacks : 1;
            for (int m = 0; m < effect.modifiers.Length; m++)
            {
                StatusEffectModifier modifier = effect.modifiers[m];
                if (modifier == null || modifier.applyEveryTick || modifier.stat != stat)
                {
                    continue;
                }

                value = ApplyValue(value, GetStackedValue(modifier, stackMultiplier), modifier.mode, modifier.clampAtZero);
            }
        }

        return value;
    }

    public int ModifyIntStat(StatusEffectStat stat, int baseValue)
    {
        return Mathf.Max(1, Mathf.RoundToInt(ModifyStat(stat, baseValue)));
    }

    void UpdateEffects()
    {
        bool changed = false;
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            ActiveStatusEffect active = activeEffects[i];
            StatusEffectData effect = active != null ? active.data : null;
            if (effect == null)
            {
                activeEffects.RemoveAt(i);
                changed = true;
                continue;
            }

            if (!effect.permanent)
            {
                active.remainingTime -= Time.deltaTime;
                if (active.remainingTime <= 0f)
                {
                    activeEffects.RemoveAt(i);
                    changed = true;
                    continue;
                }
            }

            if (effect.tickInterval > 0f)
            {
                active.tickTimer -= Time.deltaTime;
                if (active.tickTimer <= 0f)
                {
                    ApplyTickModifiers(effect, effect.multiplyModifierByStacks ? active.stacks : 1);
                    active.tickTimer = effect.tickInterval;
                }
            }
        }

        if (changed)
        {
            EffectsChanged?.Invoke();
        }
    }

    void ApplyTickModifiers(StatusEffectData effect, int stackMultiplier)
    {
        if (effect == null || effect.modifiers == null)
        {
            return;
        }

        for (int i = 0; i < effect.modifiers.Length; i++)
        {
            StatusEffectModifier modifier = effect.modifiers[i];
            if (modifier == null || !modifier.applyEveryTick)
            {
                continue;
            }

            ApplyTickModifier(modifier, stackMultiplier);
        }
    }

    void ApplyTickModifier(StatusEffectModifier modifier, int stackMultiplier)
    {
        float value = GetStackedValue(modifier, stackMultiplier);
        switch (modifier.stat)
        {
            case StatusEffectStat.EnemyCurrentHealth:
                if (enemy != null)
                {
                    if (value < 0f)
                    {
                        enemy.TakeDamage(Mathf.Abs(value));
                    }
                    else if (value > 0f)
                    {
                        enemy.Heal(value);
                    }
                }
                break;
            case StatusEffectStat.EnemyRegenHealthPercent:
                if (enemy != null && value > 0f)
                {
                    enemy.Heal(enemy.MaxHealth * value / 100f);
                }
                break;
        }
    }

    ActiveStatusEffect FindActive(StatusEffectData effect)
    {
        for (int i = 0; i < activeEffects.Count; i++)
        {
            if (activeEffects[i] != null && activeEffects[i].data == effect)
            {
                return activeEffects[i];
            }
        }

        return null;
    }

    bool CanApplyToEnemy(StatusEffectData effect)
    {
        return effect.target == StatusEffectTarget.Any
            || effect.target == StatusEffectTarget.Enemy
            || effect.target == StatusEffectTarget.EnemyWeapon;
    }

    float ApplyValue(float current, float value, StatusEffectModifierMode mode, bool clampAtZero)
    {
        switch (mode)
        {
            case StatusEffectModifierMode.Add:
                current += value;
                break;
            case StatusEffectModifierMode.Multiply:
                current *= value;
                break;
            case StatusEffectModifierMode.Override:
                current = value;
                break;
        }

        return clampAtZero ? Mathf.Max(0f, current) : current;
    }

    float GetStackedValue(StatusEffectModifier modifier, int stackMultiplier)
    {
        if (modifier.mode == StatusEffectModifierMode.Multiply)
        {
            return Mathf.Pow(modifier.value, Mathf.Max(1, stackMultiplier));
        }

        if (modifier.mode == StatusEffectModifierMode.Override)
        {
            return modifier.value;
        }

        return modifier.value * Mathf.Max(1, stackMultiplier);
    }
}
