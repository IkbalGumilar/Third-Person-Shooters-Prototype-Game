using UnityEngine;

[CreateAssetMenu(fileName = "PlayerMeleeData", menuName = "Scriptable Objects/Player Melee Data")]
public class PlayerMeleeData : ScriptableObject
{
    [Header("Identity")]
    public string meleeName = "Player Melee";
    public WeaponHoldType holdType = WeaponHoldType.OneHand;

    [Header("Animation")]
    public string animationLayerName;
    public string[] attackStateNames =
    {
        "Melee1Hand 1",
        "Melee1Hand 2",
        "Melee1Hand 3",
        "Melee1Hand 4",
        "Melee1Hand 5",
        "Melee1Hand 6",
        "Melee1Hand 7"
    };
    public float animationDuration = 0.65f;
    public float cooldown = 0.75f;
    public float crossFade = 0.06f;

    [Header("Damage")]
    public float damage = 4f;
    public float range = 1.25f;
    public float hitRadius = 0.25f;
    public float damageDelay = 0.18f;

    [Header("Critical")]
    [Range(0f, 100f)] public float criticalChance = 0f;
    [Range(0f, 500f)] public float criticalDamagePercent = 50f;

    [Header("Knockback")]
    [Range(0f, 100f)] public float knockbackChance = 0f;
    public float knockbackPower = 0f;
    public float maxKnockbackDistance = 1f;
    public float knockbackDuration = 0.12f;

    [Header("Auto Aim")]
    [Range(1f, 180f)] public float frontAngle = 80f;
    public float autoAimRange = 5f;
    public float autoAimMaxStep = 4f;
    public float autoAimStopDistance = 1f;
    public float autoAimSpeed = 8f;

    [Header("Audio")]
    [HideInInspector]
    public AudioClip attackSound;
    [HideInInspector]
    public AudioClip hitSound;
    public AudioClip[] attackSounds;
    public AudioClip[] hitSounds;

    [Header("Effects")]
    public GameObject hitEffect;
    public GameObject criticalHitEffect;
}
