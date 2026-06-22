using UnityEngine;

public enum EnemyMeleeWeaponCategory
{
    Sword,
    Dagger,
    Mace,
    SmallAxe,
    GreatSword,
    Spear
}

[CreateAssetMenu(fileName = "EnemyMeleeWeapon", menuName = "Scriptable Objects/Enemy Melee Weapon")]
public class EnemyMeleeWeapon : ScriptableObject
{
    [Header("Identity")]
    public string weaponName = "Enemy Melee Weapon";
    public EnemyMeleeWeaponCategory category = EnemyMeleeWeaponCategory.Sword;
    public WeaponHoldType holdType = WeaponHoldType.OneHand;
    public GameObject weaponPrefab;

    [Header("Stats")]
    public float damage = 10f;
    public float attackRange = 1.05f;
    public float attackCooldown = 1f;
    public float damageDelay = 0.35f;
    public float hitRadius = 0.3f;
    [Range(0f, 100f)] public float heavyChance = 0f;
    [Header("Critical")]
    [Range(0f, 100f)] public float criticalChance = 10f;
    [Range(0f, 500f)] public float criticalDamagePercent = 50f;

    [Header("Knockback")]
    [Range(0f, 100f)] public float knockbackChance = 10f;
    public float knockbackPower = 40f;
    public float maxKnockbackDistance = 1f;
    public float knockbackDuration = 0.12f;

    [Header("Status Effects")]
    [Range(0f, 100f)] public float statusEffectChance = 0f;
    public StatusEffectData[] statusEffects;

    [Header("Animation")]
    public string[] attackStateNames;
    public float attackCrossFade = 0.08f;

    [Header("Hold Offset")]
    public Vector3 holdPosition;
    public Vector3 holdRotation;
    public Vector3 holdScale = Vector3.one;

    [Header("Effects")]
    public AudioClip attackSound;
    public AudioClip hitSound;
}
