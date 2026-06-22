using UnityEngine;

public enum EnemyType
{
    Basic,
    Elite,
    Aerial,
    Tank,
    Assassin,
    Range,
    MiniBoss,
    Boss,
    Support
}

[System.Serializable]
public class EnemyStatusEffectChance
{
    public StatusEffectData effect;
    [Range(0f, 100f)] public float chance = 100f;
}

[CreateAssetMenu(fileName = "EnemyData", menuName = "Scriptable Objects/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Identity")]
    public string enemyName = "Basic Enemy";
    public EnemyType enemyType = EnemyType.Basic;

    [Header("Health")]
    public float maxHealth = 100f;
    public float penetrationResistance = 0f;
    [Range(0f, 100f)] public float criticalResistance = 0f;
    [Range(0f, 100f)] public float stunResistance = 0f;
    [Range(0f, 100f)] public float knockbackResistance = 0f;
    public bool canBeStunned = true;
    public bool canBeKnockedBack = true;

    [Header("Movement")]
    public float moveSpeed = 2f;

    [Header("AI")]
    public bool autoAddNavMeshAgent = true;
    public bool useRandomPatrolWhenNoPoints = true;
    public float randomPatrolRadius = 6f;
    public float detectionRange = 10f;
    public float loseTargetRange = 14f;
    public float shotAlertRangeMultiplier = 3f;
    public float shotAlertShareRadius = 5f;
    public float shotAlertMinDuration = 5f;
    public float shotAlertMaxDuration = 60f;
    public float idleDuration = 2f;
    public float patrolSpeed = 2f;
    [Range(0.1f, 1f)] public float walkSpeedMultiplier = 0.6f;
    public float chaseSpeed = 4f;
    public float stoppingDistance = 0.85f;
    public float waypointReachDistance = 0.6f;
    public bool faceMoveDirection = true;
    public float rotationSpeed = 8f;

    [Header("Support AI")]
    public bool useSupportBehavior;
    public float supportAllySearchRadius = 14f;
    public float supportBehindAllyDistance = 3f;
    public float supportFrontlineBuffer = 1.25f;
    public float supportFleeDistanceFromPlayer = 20f;
    public float supportRepositionOnDamageDuration = 3f;
    public float supportDestinationRefreshInterval = 0.35f;

    [Header("Attack")]
    public float attackDamage = 10f;
    public float attackRange = 0.9f;
    public float attackCooldown = 1f;
    public float attackWindup = 0.35f;
    public float attackHitRadius = 0.25f;
    [Range(0f, 100f)] public float attackHeavyChance = 0f;
    [Range(0f, 100f)] public float attackCriticalChance = 5f;
    [Range(0f, 500f)] public float attackCriticalDamagePercent = 50f;
    [Range(0f, 100f)] public float attackKnockbackChance = 8f;
    public float attackKnockbackPower = 30f;
    public float attackMaxKnockbackDistance = 0.7f;
    public float attackKnockbackDuration = 0.15f;

    [Header("Status Effects On Hit")]
    public EnemyStatusEffectChance[] hitStatusEffects;

    [Header("Legacy Status Effects")]
    [Range(0f, 100f)] public float attackStatusEffectChance = 0f;
    public StatusEffectData[] attackStatusEffects;

    [Header("Effects")]
    public GameObject deathEffect;
    public AudioClip hitSound;
    public AudioClip deathSound;

    [Header("Voice")]
    public AudioClip growlSound;
    public AudioClip angryGrowlSound;
    public float growlMinInterval = 5f;
    public float growlMaxInterval = 12f;
    public float angryGrowlMinInterval = 3f;
    public float angryGrowlMaxInterval = 8f;
    [Range(0f, 1f)] public float voiceVolume = 1f;
}
