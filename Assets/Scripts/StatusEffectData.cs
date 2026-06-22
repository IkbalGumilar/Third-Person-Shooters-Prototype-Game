using UnityEngine;

public enum StatusEffectKind
{
    Buff,
    Debuff
}

public enum StatusEffectTarget
{
    Any,
    Player,
    Enemy,
    Weapon,
    EnemyWeapon
}

public enum StatusEffectStackMode
{
    RefreshDuration,
    AddStack,
    Replace,
    IgnoreIfActive
}

public enum StatusEffectModifierMode
{
    Add,
    Multiply,
    Override
}

public enum StatusEffectStat
{
    None,

    // Player health and shield.
    CurrentHealth,
    MaxHealth,
    RegenHealth,
    RegenDamageLossPercent,
    HealthCatchUpSpeed,
    CurrentShield,
    MaxShield,
    HeavyShieldHealthPiercePercent,
    CriticalShieldHealthPiercePercent,
    KnockbackShieldHealthPiercePercent,
    KnockbackExtraShieldDamagePercent,

    // Player movement and stamina.
    MoveSpeed,
    RunSpeed,
    RotationSpeed,
    AimRotationSpeed,
    JumpForce,
    Gravity,
    CurrentStamina,
    MaxStamina,
    RunStaminaDrainPerSecond,
    JumpStaminaCost,
    StaminaRegenPerSecond,
    StaminaRegenDelay,
    ExhaustedRecoveryPercent,
    ExhaustedMoveSpeedMultiplier,
    WalkStepInterval,
    RunStepInterval,

    // Camera, aim, and scope.
    LookSensitivity,
    SensitivityMultiplier,
    MinVerticalAngle,
    MaxVerticalAngle,
    PlayerRotationSmoothTime,
    YawSmoothTime,
    PitchSmoothTime,
    AimRaycastMaxDistance,
    AimNoHitDistance,
    AimStickSmoothSpeed,
    AimScaleMultiplier,
    AimScaleSmoothSpeed,
    ScopedFov,
    ScopedSensitivityMultiplier,
    ScopedAimScaleMultiplier,

    // Player weapon stats.
    WeaponDamage,
    WeaponRange,
    WeaponFireRate,
    WeaponMagazineSize,
    WeaponReloadTime,
    WeaponAutoReloadDelay,
    WeaponFullDamageRange,
    WeaponZeroDamageRangePercent,
    WeaponPenetrationPower,
    WeaponCriticalChance,
    WeaponCriticalDamagePercent,
    WeaponStunChance,
    WeaponStunDuration,
    WeaponKnockbackChance,
    WeaponKnockbackPower,
    WeaponMaxKnockbackDistance,
    WeaponKnockbackDuration,
    WeaponPelletCount,
    WeaponSpreadAngle,
    WeaponRecoilX,
    WeaponRecoilY,
    WeaponRecoilSnappiness,
    WeaponRecoilReturnSpeed,
    WeaponMaxRecoilOffset,
    WeaponShootAnimationDuration,
    WeaponSwitchDuration,

    // Enemy health, resistance, movement, and AI.
    EnemyCurrentHealth,
    EnemyMaxHealth,
    EnemyRegenHealth,
    EnemyPenetrationResistance,
    EnemyCriticalResistance,
    EnemyStunResistance,
    EnemyKnockbackResistance,
    EnemyMoveSpeed,
    EnemyRandomPatrolRadius,
    EnemyDetectionRange,
    EnemyLoseTargetRange,
    EnemyShotAlertRangeMultiplier,
    EnemyShotAlertShareRadius,
    EnemyShotAlertMinDuration,
    EnemyShotAlertMaxDuration,
    EnemyIdleDuration,
    EnemyPatrolSpeed,
    EnemyChaseSpeed,
    EnemyStoppingDistance,
    EnemyWaypointReachDistance,
    EnemyRotationSpeed,
    EnemyAttackDamage,
    EnemyAttackRange,
    EnemyAttackCooldown,
    EnemyAttackWindup,
    EnemyAttackHitRadius,
    EnemyGrowlMinInterval,
    EnemyGrowlMaxInterval,
    EnemyAngryGrowlMinInterval,
    EnemyAngryGrowlMaxInterval,
    EnemyVoiceVolume,

    // Enemy melee weapon.
    EnemyMeleeDamage,
    EnemyMeleeAttackRange,
    EnemyMeleeAttackCooldown,
    EnemyMeleeDamageDelay,
    EnemyMeleeHitRadius,
    EnemyMeleeAttackCrossFade,

    // Enemy ranged weapon.
    EnemyRangedDamage,
    EnemyRangedAttackRange,
    EnemyRangedAttackCooldown,
    EnemyRangedDamageDelay,
    EnemyRangedMagazineSize,
    EnemyRangedReloadDuration,
    EnemyRangedPelletCount,
    EnemyRangedSpreadAngle,
    EnemyRangedAimLockDelayMin,
    EnemyRangedAimLockDelayMax,
    EnemyRangedAimTargetHeight,
    EnemyRangedStationaryFallbackMoveTolerance,
    EnemyRangedCrossbowMeleeRange,
    EnemyRangedCrossbowMeleeDamage,
    EnemyRangedCrossbowMeleeDelay,
    EnemyRangedCrossbowMeleeCooldown,
    EnemyRangedAnimationCrossFade,
    EnemyRegenDamageLossPercent,
    EnemyHealthCatchUpSpeed,
    DamageReductionPercent,
    EnemyDamageReductionPercent,
    RegenHealthPercent,
    EnemyRegenHealthPercent,
    HealthCatchUpSpeedPercent,
    EnemyHealthCatchUpSpeedPercent
}

[System.Serializable]
public class StatusEffectModifier
{
    public StatusEffectStat stat = StatusEffectStat.None;
    public StatusEffectModifierMode mode = StatusEffectModifierMode.Add;
    public float value;
    public bool applyEveryTick;
    public bool clampAtZero = true;
}

[CreateAssetMenu(fileName = "Status Effect", menuName = "Scriptable Objects/Status Effect")]
public class StatusEffectData : ScriptableObject
{
    [Header("Identity")]
    public string effectName = "Status Effect";
    public StatusEffectKind kind = StatusEffectKind.Buff;
    public StatusEffectTarget target = StatusEffectTarget.Any;
    [TextArea(2, 6)] public string description;

    [Header("Status Icon")]
    public Sprite statusIcon;
    public Color iconTint = Color.white;
    public bool showInStatusHud = true;

    [Header("Duration")]
    public bool permanent;
    public float duration = 5f;
    public float tickInterval = 1f;
    public bool removeOnDeath = true;
    public bool removeWhenPhysicalShieldDepleted;

    [Header("Health Damage Rules")]
    public bool healthDamageCanKill = true;
    public bool healthDamageTriggersDamagedEvent;
    public bool healthDamageRegenClampsToCurrentHealth = true;

    [Header("Stacking")]
    public StatusEffectStackMode stackMode = StatusEffectStackMode.RefreshDuration;
    [Tooltip("Use 0 or less for unlimited stacks.")]
    public int maxStacks = 1;
    public bool multiplyModifierByStacks = true;
    public bool HasUnlimitedStacks => maxStacks <= 0;

    [Header("State Flags")]
    public bool blocksMovement;
    public bool blocksJump;
    public bool blocksRun;
    public bool blocksShooting;
    public bool blocksReload;
    public bool blocksAiming;
    public bool hidesWeapon;

    [Header("Modifiers")]
    public StatusEffectModifier[] modifiers;

    [Header("Visual / Audio")]
    public GameObject startEffectPrefab;
    public GameObject tickEffectPrefab;
    public GameObject endEffectPrefab;
    public AudioClip startSound;
    public AudioClip tickSound;
    public AudioClip endSound;
}
