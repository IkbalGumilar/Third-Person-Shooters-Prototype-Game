using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ActiveStatusEffect
{
    public StatusEffectData data;
    public float remainingTime;
    public int stacks = 1;
    public float tickTimer;

    public bool IsPermanent => data != null && data.permanent;
}

public class PlayerStatusEffectController : MonoBehaviour
{
    public PlayerHealth playerHealth;
    public PlayerMovement playerMovement;
    public PlayerShoot playerShoot;
    public PlayerWeaponAnimator weaponAnimator;
    public CameraControler cameraControler;
    public PlayerScopeController scopeController;
    public StatusEffectData[] startEffects;
    public List<ActiveStatusEffect> activeEffects = new List<ActiveStatusEffect>();

    public event System.Action EffectsChanged;

    private bool cachedBaseStats;
    private float baseMaxHealth;
    private float baseRegenDamageLossPercent;
    private float baseHealthCatchUpSpeed;
    private bool baseUsePercentHealthCatchUpSpeed;
    private float baseHealthCatchUpSpeedPercent;
    private float baseHeavyShieldHealthPiercePercent;
    private float baseCriticalShieldHealthPiercePercent;
    private float baseKnockbackShieldHealthPiercePercent;
    private float baseKnockbackExtraShieldDamagePercent;
    private float baseMoveSpeed;
    private float baseRunSpeed;
    private float baseRotationSpeed;
    private float baseAimRotationSpeed;
    private float baseJumpForce;
    private float baseGravity;
    private float baseMaxStamina;
    private float baseRunStaminaDrainPerSecond;
    private float baseJumpStaminaCost;
    private float baseStaminaRegenPerSecond;
    private float baseStaminaRegenDelay;
    private float baseExhaustedRecoveryPercent;
    private float baseExhaustedMoveSpeedMultiplier;
    private float baseWalkStepInterval;
    private float baseRunStepInterval;
    private float baseLookSensitivity;
    private float baseSensitivityMultiplier;
    private float baseMinVerticalAngle;
    private float baseMaxVerticalAngle;
    private float basePlayerRotationSmoothTime;
    private float baseYawSmoothTime;
    private float basePitchSmoothTime;
    private float baseAimRaycastMaxDistance;
    private float baseAimNoHitDistance;
    private float baseAimStickSmoothSpeed;
    private float baseAimScaleMultiplier;
    private float baseAimScaleSmoothSpeed;
    private float baseScopeFovSmoothSpeed;
    private float baseScopedAimScaleMultiplier;

    void Awake()
    {
        AutoBind();
        CacheBaseStats();
    }

    void Start()
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
        AutoBind();
        CacheBaseStats();
        UpdateEffects();
        SyncShieldStackEffects();
        ApplyContinuousModifiers();
    }

    public void AddEffect(StatusEffectData effect)
    {
        if (effect == null)
        {
            return;
        }

        CacheBaseStats();

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
                active.remainingTime = effect.duration;
                active.tickTimer = effect.tickInterval;
            }
            else if (effect.stackMode == StatusEffectStackMode.AddStack)
            {
                if (effect.HasUnlimitedStacks || active.stacks < effect.maxStacks)
                {
                    active.stacks++;
                    ApplyImmediateModifiers(effect, 1);
                }

                active.remainingTime = effect.duration;
            }
            else
            {
                active.remainingTime = effect.duration;
                ApplyImmediateModifiers(effect, 1);
            }

            PlaySound(effect.startSound);
            SpawnEffect(effect.startEffectPrefab);
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

        ApplyImmediateModifiers(effect, 1);
        PlaySound(effect.startSound);
        SpawnEffect(effect.startEffectPrefab);
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
        if (effect != null)
        {
            PlaySound(effect.endSound);
            SpawnEffect(effect.endEffectPrefab);
        }

        EffectsChanged?.Invoke();
    }

    void UpdateEffects()
    {
        bool changed = false;

        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            ActiveStatusEffect active = activeEffects[i];
            if (active == null || active.data == null)
            {
                activeEffects.RemoveAt(i);
                changed = true;
                continue;
            }

            StatusEffectData effect = active.data;
            if (!effect.permanent)
            {
                active.remainingTime -= Time.deltaTime;
                if (active.remainingTime <= 0f)
                {
                    PlaySound(effect.endSound);
                    SpawnEffect(effect.endEffectPrefab);
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
                    int stackMultiplier = effect.multiplyModifierByStacks ? active.stacks : 1;
                    ApplyTickModifiers(effect, stackMultiplier);
                    PlaySound(effect.tickSound);
                    SpawnEffect(effect.tickEffectPrefab);
                    active.tickTimer = effect.tickInterval;
                }
            }
        }

        if (changed)
        {
            EffectsChanged?.Invoke();
        }
    }

    void SyncShieldStackEffects()
    {
        if (playerHealth == null)
        {
            return;
        }

        bool changed = false;
        int shieldStackCount = playerHealth.ShieldStackCount;

        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            ActiveStatusEffect active = activeEffects[i];
            if (active == null || active.data == null || !HasCurrentShieldModifier(active.data))
            {
                continue;
            }

            if (shieldStackCount <= 0)
            {
                PlaySound(active.data.endSound);
                SpawnEffect(active.data.endEffectPrefab);
                activeEffects.RemoveAt(i);
                changed = true;
                continue;
            }

            if (active.stacks != shieldStackCount)
            {
                active.stacks = shieldStackCount;
                changed = true;
            }
        }

        if (changed)
        {
            EffectsChanged?.Invoke();
        }
    }

    bool HasCurrentShieldModifier(StatusEffectData effect)
    {
        if (effect == null || effect.modifiers == null)
        {
            return false;
        }

        for (int i = 0; i < effect.modifiers.Length; i++)
        {
            StatusEffectModifier modifier = effect.modifiers[i];
            if (modifier != null && modifier.stat == StatusEffectStat.CurrentShield)
            {
                return true;
            }
        }

        return false;
    }

    void ApplyContinuousModifiers()
    {
        RestoreBaseStats();

        bool blockMovement = false;
        bool blockShooting = false;
        bool blockAiming = false;

        for (int i = 0; i < activeEffects.Count; i++)
        {
            ActiveStatusEffect active = activeEffects[i];
            if (active == null || active.data == null)
            {
                continue;
            }

            StatusEffectData effect = active.data;
            int stackMultiplier = effect.multiplyModifierByStacks ? active.stacks : 1;
            ApplyContinuousModifiers(effect, stackMultiplier);

            blockMovement |= effect.blocksMovement;
            blockShooting |= effect.blocksShooting || effect.blocksReload;
            blockAiming |= effect.blocksAiming;
        }

        if (playerMovement != null)
        {
            playerMovement.statusBlocksMovement = blockMovement;
            playerMovement.statusBlocksRun = HasBlockRun();
            playerMovement.statusBlocksJump = HasBlockJump();
        }

        if (playerShoot != null)
        {
            playerShoot.statusBlocksInput = blockShooting;
        }

        if (weaponAnimator != null)
        {
            weaponAnimator.statusBlocksAiming = blockAiming;
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

    bool HasBlockRun()
    {
        for (int i = 0; i < activeEffects.Count; i++)
        {
            if (activeEffects[i] != null && activeEffects[i].data != null && activeEffects[i].data.blocksRun)
            {
                return true;
            }
        }

        return false;
    }

    bool HasBlockJump()
    {
        for (int i = 0; i < activeEffects.Count; i++)
        {
            if (activeEffects[i] != null && activeEffects[i].data != null && activeEffects[i].data.blocksJump)
            {
                return true;
            }
        }

        return false;
    }

    void ApplyImmediateModifiers(StatusEffectData effect, int stackMultiplier)
    {
        if (effect == null || effect.modifiers == null)
        {
            return;
        }

        for (int i = 0; i < effect.modifiers.Length; i++)
        {
            StatusEffectModifier modifier = effect.modifiers[i];
            if (modifier == null || modifier.applyEveryTick)
            {
                continue;
            }

            ApplyImmediateModifier(effect, modifier, stackMultiplier);
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

            ApplyImmediateModifier(effect, modifier, stackMultiplier);
        }
    }

    void ApplyContinuousModifiers(StatusEffectData effect, int stackMultiplier)
    {
        if (effect == null || effect.modifiers == null)
        {
            return;
        }

        for (int i = 0; i < effect.modifiers.Length; i++)
        {
            StatusEffectModifier modifier = effect.modifiers[i];
            if (modifier == null || modifier.applyEveryTick || IsImmediateStat(modifier.stat))
            {
                continue;
            }

            ApplyContinuousModifier(modifier, stackMultiplier);
        }
    }

    void ApplyImmediateModifier(StatusEffectData effect, StatusEffectModifier modifier, int stackMultiplier)
    {
        float value = GetStackedValue(modifier, stackMultiplier);

        switch (modifier.stat)
        {
            case StatusEffectStat.CurrentHealth:
                if (playerHealth != null)
                {
                    if (modifier.mode == StatusEffectModifierMode.Add)
                    {
                        playerHealth.ApplyDirectHealthDelta(
                            value,
                            value >= 0f || effect == null || effect.healthDamageCanKill,
                            value < 0f && effect != null && effect.healthDamageTriggersDamagedEvent,
                            playerHealth.regenDamageLossPercent,
                            effect == null || effect.healthDamageRegenClampsToCurrentHealth
                        );
                    }
                    else
                    {
                        playerHealth.currentHealth = ApplyValue(playerHealth.currentHealth, value, modifier.mode, modifier.clampAtZero, playerHealth.maxHealth);
                    }
                }
                break;
            case StatusEffectStat.RegenHealth:
                if (playerHealth != null)
                {
                    playerHealth.regenHealth = ApplyValue(playerHealth.regenHealth, value, modifier.mode, modifier.clampAtZero, playerHealth.maxHealth);
                }
                break;
            case StatusEffectStat.RegenHealthPercent:
                if (playerHealth != null)
                {
                    float percentValue = playerHealth.maxHealth * value / 100f;
                    playerHealth.regenHealth = ApplyValue(playerHealth.regenHealth, percentValue, modifier.mode, modifier.clampAtZero, playerHealth.maxHealth);
                }
                break;
            case StatusEffectStat.CurrentShield:
                if (playerHealth != null)
                {
                    if (modifier.mode == StatusEffectModifierMode.Add)
                    {
                        playerHealth.AddShieldStack(value);
                    }
                    else
                    {
                        playerHealth.currentShield = ApplyValue(playerHealth.currentShield, value, modifier.mode, modifier.clampAtZero);
                    }
                }
                break;
            case StatusEffectStat.CurrentStamina:
                if (playerMovement != null)
                {
                    playerMovement.currentStamina = ApplyValue(playerMovement.currentStamina, value, modifier.mode, modifier.clampAtZero, playerMovement.maxStamina);
                }
                break;
        }
    }

    void ApplyContinuousModifier(StatusEffectModifier modifier, int stackMultiplier)
    {
        float value = GetStackedValue(modifier, stackMultiplier);

        switch (modifier.stat)
        {
            case StatusEffectStat.MaxHealth:
                if (playerHealth != null) playerHealth.maxHealth = ApplyValue(playerHealth.maxHealth, value, modifier.mode, modifier.clampAtZero);
                break;
            case StatusEffectStat.RegenDamageLossPercent:
                if (playerHealth != null) playerHealth.regenDamageLossPercent = ApplyValue(playerHealth.regenDamageLossPercent, value, modifier.mode, modifier.clampAtZero);
                break;
            case StatusEffectStat.HealthCatchUpSpeed:
                if (playerHealth != null) playerHealth.healthCatchUpSpeed = ApplyValue(playerHealth.healthCatchUpSpeed, value, modifier.mode, modifier.clampAtZero);
                break;
            case StatusEffectStat.HealthCatchUpSpeedPercent:
                if (playerHealth != null) playerHealth.healthCatchUpSpeedPercent = ApplyValue(playerHealth.healthCatchUpSpeedPercent, value, modifier.mode, modifier.clampAtZero);
                break;
            case StatusEffectStat.HeavyShieldHealthPiercePercent:
                if (playerHealth != null) playerHealth.heavyShieldHealthPiercePercent = ApplyValue(playerHealth.heavyShieldHealthPiercePercent, value, modifier.mode, modifier.clampAtZero);
                break;
            case StatusEffectStat.CriticalShieldHealthPiercePercent:
                if (playerHealth != null) playerHealth.criticalShieldHealthPiercePercent = ApplyValue(playerHealth.criticalShieldHealthPiercePercent, value, modifier.mode, modifier.clampAtZero);
                break;
            case StatusEffectStat.KnockbackShieldHealthPiercePercent:
                if (playerHealth != null) playerHealth.knockbackShieldHealthPiercePercent = ApplyValue(playerHealth.knockbackShieldHealthPiercePercent, value, modifier.mode, modifier.clampAtZero);
                break;
            case StatusEffectStat.KnockbackExtraShieldDamagePercent:
                if (playerHealth != null) playerHealth.knockbackExtraShieldDamagePercent = ApplyValue(playerHealth.knockbackExtraShieldDamagePercent, value, modifier.mode, modifier.clampAtZero);
                break;
            case StatusEffectStat.MoveSpeed:
                if (playerMovement != null) playerMovement.moveSpeed = ApplyValue(playerMovement.moveSpeed, value, modifier.mode, modifier.clampAtZero);
                break;
            case StatusEffectStat.RunSpeed:
                if (playerMovement != null) playerMovement.runSpeed = ApplyValue(playerMovement.runSpeed, value, modifier.mode, modifier.clampAtZero);
                break;
            case StatusEffectStat.RotationSpeed:
                if (playerMovement != null) playerMovement.rotationSpeed = ApplyValue(playerMovement.rotationSpeed, value, modifier.mode, modifier.clampAtZero);
                break;
            case StatusEffectStat.AimRotationSpeed:
                if (playerMovement != null) playerMovement.aimRotationSpeed = ApplyValue(playerMovement.aimRotationSpeed, value, modifier.mode, modifier.clampAtZero);
                break;
            case StatusEffectStat.JumpForce:
                if (playerMovement != null) playerMovement.jumpForce = ApplyValue(playerMovement.jumpForce, value, modifier.mode, modifier.clampAtZero);
                break;
            case StatusEffectStat.Gravity:
                if (playerMovement != null) playerMovement.gravity = ApplyValue(playerMovement.gravity, value, modifier.mode, false);
                break;
            case StatusEffectStat.MaxStamina:
                if (playerMovement != null) playerMovement.maxStamina = ApplyValue(playerMovement.maxStamina, value, modifier.mode, modifier.clampAtZero);
                break;
            case StatusEffectStat.RunStaminaDrainPerSecond:
                if (playerMovement != null) playerMovement.runStaminaDrainPerSecond = ApplyValue(playerMovement.runStaminaDrainPerSecond, value, modifier.mode, modifier.clampAtZero);
                break;
            case StatusEffectStat.JumpStaminaCost:
                if (playerMovement != null) playerMovement.jumpStaminaCost = ApplyValue(playerMovement.jumpStaminaCost, value, modifier.mode, modifier.clampAtZero);
                break;
            case StatusEffectStat.StaminaRegenPerSecond:
                if (playerMovement != null) playerMovement.staminaRegenPerSecond = ApplyValue(playerMovement.staminaRegenPerSecond, value, modifier.mode, modifier.clampAtZero);
                break;
            case StatusEffectStat.StaminaRegenDelay:
                if (playerMovement != null) playerMovement.staminaRegenDelay = ApplyValue(playerMovement.staminaRegenDelay, value, modifier.mode, modifier.clampAtZero);
                break;
            case StatusEffectStat.ExhaustedRecoveryPercent:
                if (playerMovement != null) playerMovement.exhaustedRecoveryPercent = ApplyValue(playerMovement.exhaustedRecoveryPercent, value, modifier.mode, modifier.clampAtZero);
                break;
            case StatusEffectStat.ExhaustedMoveSpeedMultiplier:
                if (playerMovement != null) playerMovement.exhaustedMoveSpeedMultiplier = ApplyValue(playerMovement.exhaustedMoveSpeedMultiplier, value, modifier.mode, modifier.clampAtZero);
                break;
            case StatusEffectStat.WalkStepInterval:
                if (playerMovement != null) playerMovement.walkStepInterval = ApplyValue(playerMovement.walkStepInterval, value, modifier.mode, modifier.clampAtZero);
                break;
            case StatusEffectStat.RunStepInterval:
                if (playerMovement != null) playerMovement.runStepInterval = ApplyValue(playerMovement.runStepInterval, value, modifier.mode, modifier.clampAtZero);
                break;
            case StatusEffectStat.LookSensitivity:
                if (cameraControler != null) cameraControler.lookSensitivity = ApplyValue(cameraControler.lookSensitivity, value, modifier.mode, modifier.clampAtZero);
                break;
            case StatusEffectStat.SensitivityMultiplier:
                if (cameraControler != null) cameraControler.sensitivityMultiplier = ApplyValue(cameraControler.sensitivityMultiplier, value, modifier.mode, modifier.clampAtZero);
                break;
            case StatusEffectStat.MinVerticalAngle:
                if (cameraControler != null) cameraControler.minVerticalAngle = ApplyValue(cameraControler.minVerticalAngle, value, modifier.mode, false);
                break;
            case StatusEffectStat.MaxVerticalAngle:
                if (cameraControler != null) cameraControler.maxVerticalAngle = ApplyValue(cameraControler.maxVerticalAngle, value, modifier.mode, false);
                break;
            case StatusEffectStat.PlayerRotationSmoothTime:
                if (cameraControler != null) cameraControler.playerRotationSmoothTime = ApplyValue(cameraControler.playerRotationSmoothTime, value, modifier.mode, modifier.clampAtZero);
                break;
            case StatusEffectStat.YawSmoothTime:
                if (cameraControler != null) cameraControler.yawSmoothTime = ApplyValue(cameraControler.yawSmoothTime, value, modifier.mode, modifier.clampAtZero);
                break;
            case StatusEffectStat.PitchSmoothTime:
                if (cameraControler != null) cameraControler.pitchSmoothTime = ApplyValue(cameraControler.pitchSmoothTime, value, modifier.mode, modifier.clampAtZero);
                break;
            case StatusEffectStat.AimRaycastMaxDistance:
                if (cameraControler != null) cameraControler.aimRaycastMaxDistance = ApplyValue(cameraControler.aimRaycastMaxDistance, value, modifier.mode, modifier.clampAtZero);
                break;
            case StatusEffectStat.AimNoHitDistance:
                if (cameraControler != null) cameraControler.aimNoHitDistance = ApplyValue(cameraControler.aimNoHitDistance, value, modifier.mode, modifier.clampAtZero);
                break;
            case StatusEffectStat.AimStickSmoothSpeed:
                if (cameraControler != null) cameraControler.aimStickSmoothSpeed = ApplyValue(cameraControler.aimStickSmoothSpeed, value, modifier.mode, modifier.clampAtZero);
                break;
            case StatusEffectStat.AimScaleMultiplier:
                if (cameraControler != null) cameraControler.aimScaleMultiplier = ApplyValue(cameraControler.aimScaleMultiplier, value, modifier.mode, modifier.clampAtZero);
                break;
            case StatusEffectStat.AimScaleSmoothSpeed:
                if (cameraControler != null) cameraControler.aimScaleSmoothSpeed = ApplyValue(cameraControler.aimScaleSmoothSpeed, value, modifier.mode, modifier.clampAtZero);
                break;
            case StatusEffectStat.ScopedAimScaleMultiplier:
                if (scopeController != null) scopeController.scopedAimScaleMultiplier = ApplyValue(scopeController.scopedAimScaleMultiplier, value, modifier.mode, modifier.clampAtZero);
                break;
        }
    }

    bool IsImmediateStat(StatusEffectStat stat)
    {
        return stat == StatusEffectStat.CurrentHealth
            || stat == StatusEffectStat.RegenHealth
            || stat == StatusEffectStat.RegenHealthPercent
            || stat == StatusEffectStat.CurrentShield
            || stat == StatusEffectStat.CurrentStamina;
    }

    float ApplyValue(float current, float value, StatusEffectModifierMode mode, bool clampAtZero, float max = -1f)
    {
        float result = current;
        switch (mode)
        {
            case StatusEffectModifierMode.Add:
                result = current + value;
                break;
            case StatusEffectModifierMode.Multiply:
                result = current * value;
                break;
            case StatusEffectModifierMode.Override:
                result = value;
                break;
        }

        if (clampAtZero)
        {
            result = Mathf.Max(0f, result);
        }

        if (max >= 0f)
        {
            result = Mathf.Min(max, result);
        }

        return result;
    }

    float GetStackedValue(StatusEffectModifier modifier, int stackMultiplier)
    {
        int stacks = Mathf.Max(1, stackMultiplier);
        if (modifier.mode == StatusEffectModifierMode.Multiply)
        {
            return Mathf.Pow(modifier.value, stacks);
        }

        if (modifier.mode == StatusEffectModifierMode.Override)
        {
            return modifier.value;
        }

        return modifier.value * stacks;
    }

    void CacheBaseStats()
    {
        if (cachedBaseStats)
        {
            return;
        }

        AutoBind();

        if (playerHealth != null)
        {
            baseMaxHealth = playerHealth.maxHealth;
            baseRegenDamageLossPercent = playerHealth.regenDamageLossPercent;
            baseHealthCatchUpSpeed = playerHealth.healthCatchUpSpeed;
            baseUsePercentHealthCatchUpSpeed = playerHealth.usePercentHealthCatchUpSpeed;
            baseHealthCatchUpSpeedPercent = playerHealth.healthCatchUpSpeedPercent;
            baseHeavyShieldHealthPiercePercent = playerHealth.heavyShieldHealthPiercePercent;
            baseCriticalShieldHealthPiercePercent = playerHealth.criticalShieldHealthPiercePercent;
            baseKnockbackShieldHealthPiercePercent = playerHealth.knockbackShieldHealthPiercePercent;
            baseKnockbackExtraShieldDamagePercent = playerHealth.knockbackExtraShieldDamagePercent;
        }

        if (playerMovement != null)
        {
            baseMoveSpeed = playerMovement.moveSpeed;
            baseRunSpeed = playerMovement.runSpeed;
            baseRotationSpeed = playerMovement.rotationSpeed;
            baseAimRotationSpeed = playerMovement.aimRotationSpeed;
            baseJumpForce = playerMovement.jumpForce;
            baseGravity = playerMovement.gravity;
            baseMaxStamina = playerMovement.maxStamina;
            baseRunStaminaDrainPerSecond = playerMovement.runStaminaDrainPerSecond;
            baseJumpStaminaCost = playerMovement.jumpStaminaCost;
            baseStaminaRegenPerSecond = playerMovement.staminaRegenPerSecond;
            baseStaminaRegenDelay = playerMovement.staminaRegenDelay;
            baseExhaustedRecoveryPercent = playerMovement.exhaustedRecoveryPercent;
            baseExhaustedMoveSpeedMultiplier = playerMovement.exhaustedMoveSpeedMultiplier;
            baseWalkStepInterval = playerMovement.walkStepInterval;
            baseRunStepInterval = playerMovement.runStepInterval;
        }

        if (cameraControler != null)
        {
            baseLookSensitivity = cameraControler.lookSensitivity;
            baseSensitivityMultiplier = cameraControler.sensitivityMultiplier;
            baseMinVerticalAngle = cameraControler.minVerticalAngle;
            baseMaxVerticalAngle = cameraControler.maxVerticalAngle;
            basePlayerRotationSmoothTime = cameraControler.playerRotationSmoothTime;
            baseYawSmoothTime = cameraControler.yawSmoothTime;
            basePitchSmoothTime = cameraControler.pitchSmoothTime;
            baseAimRaycastMaxDistance = cameraControler.aimRaycastMaxDistance;
            baseAimNoHitDistance = cameraControler.aimNoHitDistance;
            baseAimStickSmoothSpeed = cameraControler.aimStickSmoothSpeed;
            baseAimScaleMultiplier = cameraControler.aimScaleMultiplier;
            baseAimScaleSmoothSpeed = cameraControler.aimScaleSmoothSpeed;
        }

        if (scopeController != null)
        {
            baseScopeFovSmoothSpeed = scopeController.fovSmoothSpeed;
            baseScopedAimScaleMultiplier = scopeController.scopedAimScaleMultiplier;
        }

        cachedBaseStats = true;
    }

    void RestoreBaseStats()
    {
        if (playerHealth != null)
        {
            playerHealth.maxHealth = baseMaxHealth;
            playerHealth.regenDamageLossPercent = baseRegenDamageLossPercent;
            playerHealth.healthCatchUpSpeed = baseHealthCatchUpSpeed;
            playerHealth.usePercentHealthCatchUpSpeed = baseUsePercentHealthCatchUpSpeed;
            playerHealth.healthCatchUpSpeedPercent = baseHealthCatchUpSpeedPercent;
            playerHealth.heavyShieldHealthPiercePercent = baseHeavyShieldHealthPiercePercent;
            playerHealth.criticalShieldHealthPiercePercent = baseCriticalShieldHealthPiercePercent;
            playerHealth.knockbackShieldHealthPiercePercent = baseKnockbackShieldHealthPiercePercent;
            playerHealth.knockbackExtraShieldDamagePercent = baseKnockbackExtraShieldDamagePercent;
        }

        if (playerMovement != null)
        {
            playerMovement.moveSpeed = baseMoveSpeed;
            playerMovement.runSpeed = baseRunSpeed;
            playerMovement.rotationSpeed = baseRotationSpeed;
            playerMovement.aimRotationSpeed = baseAimRotationSpeed;
            playerMovement.jumpForce = baseJumpForce;
            playerMovement.gravity = baseGravity;
            playerMovement.maxStamina = baseMaxStamina;
            playerMovement.runStaminaDrainPerSecond = baseRunStaminaDrainPerSecond;
            playerMovement.jumpStaminaCost = baseJumpStaminaCost;
            playerMovement.staminaRegenPerSecond = baseStaminaRegenPerSecond;
            playerMovement.staminaRegenDelay = baseStaminaRegenDelay;
            playerMovement.exhaustedRecoveryPercent = baseExhaustedRecoveryPercent;
            playerMovement.exhaustedMoveSpeedMultiplier = baseExhaustedMoveSpeedMultiplier;
            playerMovement.walkStepInterval = baseWalkStepInterval;
            playerMovement.runStepInterval = baseRunStepInterval;
        }

        if (cameraControler != null)
        {
            cameraControler.lookSensitivity = baseLookSensitivity;
            cameraControler.sensitivityMultiplier = baseSensitivityMultiplier;
            cameraControler.minVerticalAngle = baseMinVerticalAngle;
            cameraControler.maxVerticalAngle = baseMaxVerticalAngle;
            cameraControler.playerRotationSmoothTime = basePlayerRotationSmoothTime;
            cameraControler.yawSmoothTime = baseYawSmoothTime;
            cameraControler.pitchSmoothTime = basePitchSmoothTime;
            cameraControler.aimRaycastMaxDistance = baseAimRaycastMaxDistance;
            cameraControler.aimNoHitDistance = baseAimNoHitDistance;
            cameraControler.aimStickSmoothSpeed = baseAimStickSmoothSpeed;
            cameraControler.aimScaleMultiplier = baseAimScaleMultiplier;
            cameraControler.aimScaleSmoothSpeed = baseAimScaleSmoothSpeed;
        }

        if (scopeController != null)
        {
            scopeController.fovSmoothSpeed = baseScopeFovSmoothSpeed;
            scopeController.scopedAimScaleMultiplier = baseScopedAimScaleMultiplier;
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

    void AutoBind()
    {
        playerHealth = playerHealth != null ? playerHealth : GetComponent<PlayerHealth>();
        playerMovement = playerMovement != null ? playerMovement : GetComponent<PlayerMovement>();
        playerShoot = playerShoot != null ? playerShoot : GetComponent<PlayerShoot>();
        weaponAnimator = weaponAnimator != null ? weaponAnimator : GetComponent<PlayerWeaponAnimator>();
        cameraControler = cameraControler != null ? cameraControler : GetComponent<CameraControler>();
        scopeController = scopeController != null ? scopeController : GetComponent<PlayerScopeController>();
    }

    void SpawnEffect(GameObject prefab)
    {
        if (prefab == null)
        {
            return;
        }

        Instantiate(prefab, transform.position, Quaternion.identity, transform);
    }

    void PlaySound(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        AudioSource.PlayClipAtPoint(clip, transform.position);
    }
}
