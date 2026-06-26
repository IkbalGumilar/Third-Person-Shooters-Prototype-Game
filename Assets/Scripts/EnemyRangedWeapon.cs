using UnityEngine;

public enum EnemyRangedWeaponKind
{
    Handgun,
    DualPistol,
    Crossbow,
    Shotgun
}

[CreateAssetMenu(fileName = "EnemyRangedWeapon", menuName = "Scriptable Objects/Enemy Ranged Weapon")]
public class EnemyRangedWeapon : ScriptableObject
{
    [Header("Identity")]
    public string weaponName = "Enemy Ranged Weapon";
    public EnemyRangedWeaponKind weaponKind = EnemyRangedWeaponKind.Handgun;
    public GameObject weaponPrefab;

    [Header("Stats")]
    public float damage = 8f;
    public float attackRange = 12f;
    public float attackCooldown = 0.8f;
    public float damageDelay = 0.2f;
    public int magazineSize = 6;
    public float reloadDuration = 1.2f;
    public int pelletCount = 1;
    public float spreadAngle = 1f;
    public LayerMask hitMask = ~0;
    public bool requireLineOfSight = true;
    [Range(0f, 100f)] public float statusEffectChance = 0f;
    public StatusEffectData[] statusEffects;

    [Header("Knockback")]
    [Range(0f, 100f)] public float knockbackChance = 0f;
    public float knockbackPower = 0f;
    public float maxKnockbackDistance = 0f;
    public float knockbackDuration = 0.15f;

    [Header("Aim")]
    public Vector2 aimLockDelayRange = new Vector2(0.1f, 0.5f);
    public float aimTargetHeight = 1f;
    public bool allowDirectHitFallback = false;
    public bool allowStationaryTargetFallback = true;
    public float stationaryFallbackMoveTolerance = 0.15f;

    [Header("Rules")]
    public bool eliteAndMiniBossUseDualPistol = true;
    public bool miniBossAndBossOnly = false;
    public bool bossCanUseCrossbowMelee = true;
    public float crossbowMeleeRange = 1.8f;
    public float crossbowMeleeDamage = 12f;
    public float crossbowMeleeDelay = 0.25f;
    public float crossbowMeleeCooldown = 1f;
    public float rangedMeleeKnockbackDistance = 0f;
    public float rangedMeleeKnockbackDuration = 0.25f;
    [Range(0f, 100f)] public float crossbowMeleeStatusEffectChance = 0f;
    public StatusEffectData[] crossbowMeleeStatusEffects;

    [HideInInspector]
    public string rangeLayerName = "2Hand-Shooting";
    [HideInInspector]
    public string[] handgunShootStateNames = { "Pistol-Attack-L1", "Pistol-Attack-R1", "Pistol-Attack-L2" };
    [HideInInspector]
    public string handgunReloadStateName = "Pistol-Reload-L1";
    [HideInInspector]
    public string[] dualPistolShootStateNames = { "Pistol-Attack-Dual1", "Pistol-Attack-Dual2", "Pistol-Attack-Dual3" };
    [HideInInspector]
    public string[] dualPistolReloadStateNames = { "Pistol-Reload-L1", "Pistol-Reload-R1" };
    [HideInInspector]
    public string[] crossbowShootStateNames = { "2Hand-Crossbow-Attack1", "2Hand-Crossbow-Attack2", "2Hand-Crossbow-Attack3", "2Hand-Crossbow-Attack4", "2Hand-Crossbow-Attack5", "2Hand-Crossbow-Attack6" };
    [HideInInspector]
    public string crossbowReloadStateName = "2Hand-Crossbow-Reload";
    [HideInInspector]
    public string[] crossbowMeleeStateNames = { "2Hand-Crossbow-Attack-Kick-L1", "2Hand-Crossbow-Attack-Kick-R1", "2Hand-Crossbow-Attack-Kick-L2", "2Hand-Crossbow-Attack-Kick-R2" };
    [HideInInspector]
    public string[] shotgunMeleeStateNames = { "Shooting-Attack-Kick-L1", "Shooting-Attack-Kick-R1", "Shooting-Attack-Kick-L2", "Shooting-Attack-Kick-R2" };
    [HideInInspector]
    public string[] shotgunShootStateNames = { "Shooting-Fire-Rifle1", "Shooting-Fire-Rifle2", "Shooting-Attack1" };
    [HideInInspector]
    public string shotgunReloadStateName = "Shooting-Reload-Start";
    [HideInInspector]
    public float animationCrossFade = 0.08f;

    [Header("Hold Offset")]
    public Vector3 holdPosition;
    public Vector3 holdRotation;
    public Vector3 holdScale = Vector3.one;

    [Header("Audio")]
    public AudioClip shootSound;
    public AudioClip reloadSound;
    public AudioClip hitSound;
}
