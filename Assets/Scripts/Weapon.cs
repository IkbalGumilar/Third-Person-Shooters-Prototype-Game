using UnityEngine;

public enum WeaponHoldType
{
    OneHand,
    TwoHand
}

public enum WeaponReloadType
{
    Standard,
    ShotgunPerShell,
    StandardSequence
}

[CreateAssetMenu(fileName = "Weapon", menuName = "Scriptable Objects/Weapon")]
public class Weapon : ScriptableObject
{
    [Header("Identity")]
    public string weaponName = "Default Weapon";
    public WeaponHoldType holdType = WeaponHoldType.TwoHand;
    public GameObject weaponPrefab;

    [Header("Inventory Info")]
    public Sprite inventoryIcon;
    public string displayName;
    [TextArea(3, 8)] public string description;
    [Min(1)] public int inventoryWidth = 1;
    [Min(1)] public int inventoryHeight = 1;
    public bool canRotateInInventory = true;

    [Header("Stats")]
    public float damage = 10f;
    public float range = 100f;
    public float fireRate = 5f;
    public bool Auto;
    public int magazineSize = 30;
    public bool useChamberedRound = false;
    public string ammoType = "Default";
    public string caliber = "";
    public float reloadTime = 1.5f;
    public bool allowManualReload = true;
    public bool autoReloadOnEmpty = false;
    public float autoReloadDelay = 0.5f;

    [Header("Damage Falloff")]
    public float fullDamageRange = 20f;
    public float zeroDamageRangePercent = 200f;

    [Header("Penetration")]
    public float penetrationPower = 0f;

    [Header("Critical")]
    [Range(0f, 100f)] public float criticalChance = 0f;
    [Range(0f, 500f)] public float criticalDamagePercent = 100f;

    [Header("Stun")]
    [Range(0f, 100f)] public float stunChance = 0f;
    public float stunDuration = 0.5f;

    [Header("Knockback")]
    [Range(0f, 100f)] public float knockbackChance = 0f;
    public float knockbackPower = 0f;
    public float maxKnockbackDistance = 2f;
    public float knockbackDuration = 0.15f;

    [Header("Spread")]
    public bool useSpread = false;
    public int pelletCount = 1;
    public float spreadAngle = 0f;

    [Header("Scope")]
    public bool hasScope = false;
    public bool useCinemachineScopeCamera = false;
    public float scopedFov = 20f;
    public float scopedSensitivityMultiplier = 0.35f;
    public GameObject scopeOverlayPrefab;
    public bool hideWeaponWhenScoped = false;

    [Header("Recoil")]
    public Vector2 recoilPerShot = new Vector2(0.02f, 0.08f);
    public float recoilSnappiness = 18f;
    public float recoilReturnSpeed = 10f;
    public float maxRecoilOffset = 0.35f;

    [Header("Body Recoil")]
    public bool useBodyRecoil = true;
    public Vector2 bodyRecoilPerShot = new Vector2(0.6f, 1.4f);
    public float maxBodyRecoilAngle = 8f;
    public float bodyRecoilSnappiness = 18f;
    public float bodyRecoilReturnSpeed = 10f;
    [Range(0f, 1f)] public float bodyRecoilImmediateResponse = 0.35f;
    [Range(0f, 1f)] public float oneHandUpperBodyRecoilScale = 0f;
    [Range(0f, 1f)] public float twoHandUpperBodyRecoilScale = 1f;
    public float oneHandShoulderRecoilScale = 1.15f;
    public float oneHandUpperArmRecoilScale = 0.75f;
    public float oneHandElbowRecoilScale = 1.8f;
    [Range(0f, 1f)] public float bodyRecoilSpineWeight = 0.18f;
    [Range(0f, 1f)] public float bodyRecoilChestWeight = 0.32f;
    [Range(0f, 1f)] public float bodyRecoilUpperChestWeight = 0.42f;
    [Range(0f, 1f)] public float bodyRecoilRightClavicleWeight = 0.75f;
    [Range(0f, 1f)] public float bodyRecoilLeftClavicleWeight = 0.45f;
    [Range(0f, 1f)] public float bodyRecoilRightUpperArmWeight = 0.5f;
    [Range(0f, 1f)] public float bodyRecoilRightLowerArmWeight = 0.35f;
    [Range(0f, 1f)] public float bodyRecoilLeftUpperArmWeight = 0.25f;
    [Range(0f, 1f)] public float bodyRecoilLeftLowerArmWeight = 0.15f;

    [Header("Hold Offset")]
    public Vector3 holdPosition;
    public Vector3 holdRotation;
    public Vector3 holdScale = Vector3.one;
    public Vector3 aimHoldPosition;
    public Vector3 aimHoldRotation;

    [Header("Aim IK")]
    public bool useAimIK = true;
    [Range(0f, 1f)] public float aimIKWeight = 1f;
    [Range(0f, 1f)] public float aimIKBodyWeight = 0.28f;
    [Range(0f, 1f)] public float aimIKHeadWeight = 0.65f;
    [Range(0f, 1f)] public float aimIKRightHandWeight = 1f;
    public Vector3 aimIKRightHandRotationOffset;
    [Range(0f, 1f)] public float aimIKLeftHandWeight = 0.75f;

    [Header("Animation")]
    public string animationLayerName = "";
    public string shootLayerName = "";
    public string reloadLayerName = "";
    public string switchLayerName = "";
    public string aimStateName = "Shooting-Aiming-CM";
    public string switchInStateName = "Shooting-Unsheath-Hips-Relax";
    public float switchDuration = 0.35f;
    public string shootStateName = "Shooting-Aiming-Fire-CM";
    public float shootAnimationDuration = 0.12f;
    public WeaponReloadType reloadType = WeaponReloadType.Standard;
    public string reloadStateName = "";
    public string shotgunReloadStartStateName = "Shotgun start";
    public string shotgunReloadInsertStateName = "Shotgun insert";
    public string shotgunReloadEndStateName = "Shotgun end";
    public float reloadStartDuration = 0f;
    public float reloadInsertDuration = 0f;
    public float reloadEndDuration = 0f;

    [Header("Effects")]
    public GameObject bulletShell;
    public GameObject muzzleFlash;
    public GameObject impactEffect;
    public GameObject enemyHitEffect;
    public GameObject criticalHitEffect;
    public AudioClip fireSound;
    public AudioClip reloadSound;
    public AudioClip emptyAmmoSound;
}
