using System.Collections;
using System.Collections.Generic;
using Lean.Pool;
using UnityEngine;

public class PlayerShoot : MonoBehaviour
{
    const int MaxRaycastHits = 64;

    public bool allowInput = true;
    public bool statusBlocksInput;
    public bool externalActionBlocksInput;
    public Weapon currentWeapon;
    public Transform firePoint;
    public Transform muzzleEffectPoint;
    public Transform shellEjectPoint;
    public Transform cameraTransform;
    public Transform aimLookTarget;
    public LayerMask hitMask;
    public PlayerWeaponAnimator weaponAnimator;
    public PlayerWeaponEquip weaponEquip;
    public PlayerRecoil recoil;
    public bool drawDebugLaser = true;
    public bool logHits;
    public float debugLaserDuration = 0.1f;
    public float impactSurfaceOffset = 0.01f;
    public float muzzleEffectLifetime = 0.4f;
    public float impactEffectLifetime = 2f;
    public float shellLifetime = 2f;
    public float fireSoundVolume = 0.35f;
    public float reloadSoundVolume = 0.7f;
    public float emptyAmmoSoundVolume = 0.8f;
    public InventoryGridUI ammoInventory;
    public bool useInventoryAmmo = true;
    public int initialReserveAmmo = 999;
    public bool subtractInitialMagazineFromReserve = true;

    private AudioSource audioSource;
    private int currentAmmo;
    private int currentChamberAmmo;
    private int reserveAmmo;
    private float nextFireTime;
    private float reloadShootBlockedUntil;
    private bool isReloading;
    private bool isWaitingAutoReload;
    private bool autoShootAnimationPlayed;
    private bool initializedAmmo;
    private bool canInterruptShotgunReload;
    private bool shotgunReloadInterrupted;
    private int reloadSequence;
    private readonly Dictionary<Weapon, int> magazineAmmoByWeapon = new Dictionary<Weapon, int>();
    private readonly Dictionary<Weapon, int> chamberAmmoByWeapon = new Dictionary<Weapon, int>();
    private readonly RaycastHit[] raycastHits = new RaycastHit[MaxRaycastHits];
    private readonly HashSet<Enemy> damagedEnemies = new HashSet<Enemy>();
    private readonly List<TrailRenderer> pooledTrails = new List<TrailRenderer>();
    private readonly List<ParticleSystem> pooledParticles = new List<ParticleSystem>();
    private KontrolPemain kontrolPemain;

    public int CurrentAmmo => currentAmmo + currentChamberAmmo;
    public int CurrentMagazineAmmo => currentAmmo;
    public int CurrentChamberAmmo => currentChamberAmmo;
    public int ReserveAmmo => GetAvailableReserveAmmo();
    public bool HasChamberedRound => currentWeapon != null && currentWeapon.useChamberedRound && currentChamberAmmo > 0;
    public bool IsReloading => isReloading;
    public bool IsAiming => weaponAnimator != null && weaponAnimator.IsAiming;

    void Awake()
    {
        kontrolPemain = new KontrolPemain();
    }

    void OnEnable()
    {
        kontrolPemain?.Pemain.Enable();
    }

    void OnDisable()
    {
        kontrolPemain?.Pemain.Disable();
    }

    void OnDestroy()
    {
        kontrolPemain?.Dispose();
    }

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 1f;

        weaponAnimator = weaponAnimator != null ? weaponAnimator : GetComponent<PlayerWeaponAnimator>();
        weaponEquip = weaponEquip != null ? weaponEquip : GetComponent<PlayerWeaponEquip>();
        recoil = recoil != null ? recoil : GetComponent<PlayerRecoil>();
        ammoInventory = ammoInventory != null ? ammoInventory : FindAnyObjectByType<InventoryGridUI>();
        if (recoil != null && aimLookTarget != null)
        {
            recoil.SetTarget(aimLookTarget);
        }

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        if (currentWeapon != null)
        {
            InitializeAmmo(currentWeapon);
            weaponAnimator?.SetWeapon(currentWeapon);
        }
    }

    void Update()
    {
        if (!allowInput
            || statusBlocksInput
            || externalActionBlocksInput
            || (weaponEquip != null && weaponEquip.IsSwitching))
        {
            return;
        }

        if (currentWeapon == null)
        {
            return;
        }

        if (!IsShootPressed())
        {
            autoShootAnimationPlayed = false;
        }

        if (isReloading)
        {
            TryEmergencyShotgunShot();
            return;
        }

        bool wantsToShoot = currentWeapon.Auto ? IsShootPressed() : IsShootPressedThisFrame();
        if (wantsToShoot)
        {
            TryShoot(false);
        }

        if (IsReloadPressedThisFrame())
        {
            TryManualReload();
        }
    }

    void TryShoot(bool ignoreAim)
    {
        if (!ignoreAim && Time.time < reloadShootBlockedUntil)
        {
            return;
        }

        if (!ignoreAim && !IsAiming)
        {
            return;
        }

        if (Time.time < nextFireTime)
        {
            return;
        }

        if (GetReadyAmmo() <= 0)
        {
            autoShootAnimationPlayed = false;
            TryReloadWhenEmpty();
            return;
        }

        float fireDelay = currentWeapon.fireRate > 0f ? 1f / currentWeapon.fireRate : 0f;
        nextFireTime = Time.time + fireDelay;
        ConsumeShotAmmo();
        SaveCurrentWeaponAmmo();

        bool shouldPlayShootAnimation = !currentWeapon.Auto || !autoShootAnimationPlayed;
        if (shouldPlayShootAnimation && weaponAnimator != null)
        {
            weaponAnimator.PlayShootState();
            autoShootAnimationPlayed = currentWeapon.Auto;
        }

        FireWeapon();

        if (currentWeapon.autoReloadOnEmpty && GetReadyAmmo() <= 0 && GetAvailableReserveAmmo() > 0 && !isWaitingAutoReload)
        {
            StartCoroutine(AutoReloadAfterDelay(currentWeapon.autoReloadDelay));
        }
    }

    void TryEmergencyShotgunShot()
    {
        if (!CanEmergencyShotgunShot()
            || Time.time < nextFireTime
            || !IsShootPressedThisFrame())
        {
            return;
        }

        shotgunReloadInterrupted = true;
        reloadSequence++;
        canInterruptShotgunReload = false;
        weaponAnimator?.StopReloadState();
        isReloading = false;
        autoShootAnimationPlayed = false;
        TryShoot(true);
    }

    bool IsShootPressed()
    {
        return MobileInputBridge.ShootHeld
            || kontrolPemain != null && kontrolPemain.Pemain.Tembak.IsPressed();
    }

    bool IsShootPressedThisFrame()
    {
        return MobileInputBridge.ConsumeShootPressedThisFrame()
            || kontrolPemain != null && kontrolPemain.Pemain.Tembak.WasPressedThisFrame();
    }

    bool IsReloadPressedThisFrame()
    {
        return MobileInputBridge.ConsumeReload()
            || kontrolPemain != null && kontrolPemain.Pemain.Reload.WasPressedThisFrame();
    }

    bool CanEmergencyShotgunShot()
    {
        return isReloading
            && canInterruptShotgunReload
            && currentWeapon != null
            && currentWeapon.reloadType == WeaponReloadType.ShotgunPerShell
            && currentWeapon.holdType == WeaponHoldType.TwoHand
            && GetReadyAmmo() > 0;
    }

    IEnumerator AutoReloadAfterDelay(float delay)
    {
        if (isReloading || isWaitingAutoReload)
        {
            yield break;
        }

        isWaitingAutoReload = true;
        yield return new WaitForSeconds(Mathf.Max(0f, delay));
        isWaitingAutoReload = false;

        if (!externalActionBlocksInput
            && (weaponEquip == null || !weaponEquip.IsSwitching)
            && currentWeapon != null
            && GetReadyAmmo() <= 0
            && GetAvailableReserveAmmo() > 0)
        {
            StartCoroutine(Reload());
        }
    }

    void TryManualReload()
    {
        if (currentWeapon == null || !currentWeapon.allowManualReload)
        {
            return;
        }

        StartCoroutine(Reload());
    }

    void TryReloadWhenEmpty()
    {
        if (currentWeapon == null || GetAvailableReserveAmmo() <= 0)
        {
            PlayEmptyAmmoSound(currentWeapon);
            return;
        }

        if (currentWeapon.autoReloadOnEmpty)
        {
            if (!isWaitingAutoReload)
            {
                StartCoroutine(AutoReloadAfterDelay(currentWeapon.autoReloadDelay));
            }

            return;
        }

        if (currentWeapon.allowManualReload)
        {
            StartCoroutine(Reload());
        }
    }

    public void Shoot()
    {
        // Some imported animation clips contain a "Shoot" event. Real firing is controlled by input via TryShoot.
    }

    void FireWeapon()
    {
        Vector3 origin = cameraTransform != null ? cameraTransform.position : transform.position;
        int pelletCount = currentWeapon.useSpread ? Mathf.Max(1, currentWeapon.pelletCount) : 1;
        float damagePerPellet = currentWeapon.damage / pelletCount;
        float spreadRotation = Random.Range(0f, Mathf.PI * 2f);

        for (int i = 0; i < pelletCount; i++)
        {
            Vector3 direction = GetShootDirection(i, pelletCount, spreadRotation);
            FireRay(origin, direction, damagePerPellet);
        }

        SpawnEffect(currentWeapon.muzzleFlash, muzzleEffectPoint != null ? muzzleEffectPoint : firePoint);
        SpawnShell();
        PlaySound(currentWeapon.fireSound, fireSoundVolume);
        ApplyRecoil();
    }

    void FireRay(Vector3 origin, Vector3 direction, float baseDamage)
    {
        float range = GetRaycastRange();
        Vector3 laserEnd = origin + direction * range;
        float remainingPenetration = currentWeapon.penetrationPower;
        bool stopped = false;
        damagedEnemies.Clear();
        int hitCount = Physics.RaycastNonAlloc(origin, direction, raycastHits, range, hitMask);
        SortRaycastHitsByDistance(raycastHits, hitCount);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = raycastHits[i];
            if (hit.collider == null)
            {
                continue;
            }

            if (IsPickupHit(hit.collider))
            {
                continue;
            }

            laserEnd = hit.point;
            Enemy enemy = hit.collider.GetComponentInParent<Enemy>();
            if (enemy == null)
            {
                SpawnImpactEffect(hit);
                stopped = true;
                break;
            }

            if (!damagedEnemies.Contains(enemy))
            {
                float damage = CalculateDamageByDistance(baseDamage, hit.distance);
                bool isCritical = false;
                if (damage > 0f)
                {
                    damage = ApplyCriticalDamage(enemy, damage, out isCritical);
                    TryApplyStun(enemy);
                    TryApplyKnockback(enemy, direction);
                    enemy.TakeDamage(damage);
                    if (logHits)
                    {
                        Debug.Log($"Hit {hit.collider.name} for {damage} damage");
                    }
                }

                SpawnEnemyHitEffect(hit, isCritical);
                damagedEnemies.Add(enemy);
            }

            float resistance = enemy.enemyData != null
                ? GetEnemyModifiedStat(enemy, StatusEffectStat.EnemyPenetrationResistance, enemy.enemyData.penetrationResistance)
                : 0f;
            if (remainingPenetration > resistance)
            {
                remainingPenetration -= resistance;
                continue;
            }

            stopped = true;
            break;
        }

        if (drawDebugLaser)
        {
            Debug.DrawLine(origin, laserEnd, stopped ? Color.red : Color.yellow, debugLaserDuration);
        }
    }

    void SortRaycastHitsByDistance(RaycastHit[] hits, int hitCount)
    {
        for (int i = 1; i < hitCount; i++)
        {
            RaycastHit current = hits[i];
            int j = i - 1;
            while (j >= 0 && hits[j].distance > current.distance)
            {
                hits[j + 1] = hits[j];
                j--;
            }

            hits[j + 1] = current;
        }
    }

    bool IsPickupHit(Collider collider)
    {
        return collider != null && collider.GetComponentInParent<WorldItemPickup>() != null;
    }

    Vector3 GetShootDirection(int pelletIndex, int pelletCount, float spreadRotation)
    {
        Transform originTransform = cameraTransform != null ? cameraTransform : transform;
        if (currentWeapon == null || !currentWeapon.useSpread || currentWeapon.spreadAngle <= 0f || pelletCount <= 1)
        {
            return originTransform.forward;
        }

        float spreadRadius = Mathf.Tan(currentWeapon.spreadAngle * Mathf.Deg2Rad);
        Vector2 circularOffset = GetCircularSpreadOffset(pelletIndex, pelletCount, spreadRotation) * spreadRadius;
        Vector3 direction = originTransform.forward
            + originTransform.right * circularOffset.x
            + originTransform.up * circularOffset.y;

        return direction.normalized;
    }

    Vector2 GetCircularSpreadOffset(int pelletIndex, int pelletCount, float spreadRotation)
    {
        if (pelletIndex <= 0)
        {
            return Vector2.zero;
        }

        int outerPelletCount = Mathf.Max(1, pelletCount - 1);
        float angle = spreadRotation + (pelletIndex - 1) * Mathf.PI * 2f / outerPelletCount;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    float GetRaycastRange()
    {
        if (currentWeapon == null)
        {
            return 0f;
        }

        return Mathf.Max(currentWeapon.range, GetZeroDamageRange());
    }

    float CalculateDamageByDistance(float baseDamage, float distance)
    {
        if (currentWeapon == null)
        {
            return baseDamage;
        }

        float fullDamageRange = Mathf.Max(0f, currentWeapon.fullDamageRange);
        if (distance <= fullDamageRange)
        {
            return baseDamage;
        }

        float zeroDamageRange = GetZeroDamageRange();
        if (distance >= zeroDamageRange)
        {
            return 0f;
        }

        float falloff = Mathf.InverseLerp(fullDamageRange, zeroDamageRange, distance);
        return baseDamage * (1f - falloff);
    }

    float ApplyCriticalDamage(Enemy enemy, float damage, out bool isCritical)
    {
        isCritical = false;
        if (currentWeapon == null || enemy == null || enemy.enemyData == null)
        {
            return damage;
        }

        float criticalResistance = GetEnemyModifiedStat(enemy, StatusEffectStat.EnemyCriticalResistance, enemy.enemyData.criticalResistance);
        float finalChance = Mathf.Clamp(currentWeapon.criticalChance - criticalResistance, 0f, 100f);
        if (!RollPercent(finalChance))
        {
            return damage;
        }

        isCritical = true;
        float bonusPercent = Mathf.Max(0f, currentWeapon.criticalDamagePercent);
        return damage + (damage * bonusPercent / 100f);
    }

    void TryApplyStun(Enemy enemy)
    {
        if (currentWeapon == null || enemy == null || enemy.enemyData == null || !enemy.enemyData.canBeStunned)
        {
            return;
        }

        float stunResistance = GetEnemyModifiedStat(enemy, StatusEffectStat.EnemyStunResistance, enemy.enemyData.stunResistance);
        float finalChance = Mathf.Clamp(currentWeapon.stunChance - stunResistance, 0f, 100f);
        if (RollPercent(finalChance))
        {
            enemy.ApplyStun(currentWeapon.stunDuration);
        }
    }

    void TryApplyKnockback(Enemy enemy, Vector3 direction)
    {
        if (currentWeapon == null || enemy == null || enemy.enemyData == null || !enemy.enemyData.canBeKnockedBack)
        {
            return;
        }

        float knockbackResistance = GetEnemyModifiedStat(enemy, StatusEffectStat.EnemyKnockbackResistance, enemy.enemyData.knockbackResistance);
        float finalChance = Mathf.Clamp(currentWeapon.knockbackChance - knockbackResistance, 0f, 100f);
        if (!RollPercent(finalChance))
        {
            return;
        }

        float powerDifference = currentWeapon.knockbackPower - knockbackResistance;
        float distance = Mathf.Clamp(
            powerDifference / 100f * currentWeapon.maxKnockbackDistance,
            0f,
            currentWeapon.maxKnockbackDistance
        );

        enemy.ApplyKnockback(direction, distance, currentWeapon.knockbackDuration);
    }

    bool RollPercent(float chance)
    {
        if (chance <= 0f)
        {
            return false;
        }

        if (chance >= 100f)
        {
            return true;
        }

        return Random.Range(0f, 100f) <= chance;
    }

    float GetEnemyModifiedStat(Enemy enemy, StatusEffectStat stat, float baseValue)
    {
        if (enemy == null)
        {
            return baseValue;
        }

        EnemyStatusEffectController statusController = enemy.StatusController;
        return statusController != null ? statusController.ModifyStat(stat, baseValue) : baseValue;
    }

    float GetZeroDamageRange()
    {
        if (currentWeapon == null)
        {
            return 0f;
        }

        float fullDamageRange = Mathf.Max(0.01f, currentWeapon.fullDamageRange);
        float percent = Mathf.Max(100f, currentWeapon.zeroDamageRangePercent);
        return fullDamageRange * (percent / 100f);
    }

    IEnumerator Reload()
    {
        if (externalActionBlocksInput
            || (weaponEquip != null && weaponEquip.IsSwitching)
            || currentWeapon == null
            || isReloading
            || IsCurrentWeaponFullyLoaded())
        {
            yield break;
        }

        if (GetAvailableReserveAmmo() <= 0)
        {
            PlayEmptyAmmoSound(currentWeapon);
            yield break;
        }

        Weapon reloadWeapon = currentWeapon;
        int reloadToken = ++reloadSequence;
        if (reloadWeapon.reloadType == WeaponReloadType.StandardSequence)
        {
            yield return ReloadStandardSequence(reloadWeapon, reloadToken);
            yield break;
        }

        if (reloadWeapon.reloadType == WeaponReloadType.ShotgunPerShell)
        {
            yield return ReloadShotgunPerShell(reloadWeapon, reloadToken);
            yield break;
        }

        yield return ReloadStandard(reloadWeapon, reloadToken);
    }

    IEnumerator ReloadStandard(Weapon reloadWeapon, int reloadToken)
    {
        isReloading = true;
        canInterruptShotgunReload = false;
        shotgunReloadInterrupted = false;
        PlaySound(reloadWeapon.reloadSound, reloadSoundVolume);
        float reloadDuration = reloadWeapon.reloadTime;
        if (weaponAnimator != null)
        {
            string stateName = string.IsNullOrEmpty(reloadWeapon.reloadStateName)
                ? GetDefaultReloadStateName(reloadWeapon)
                : reloadWeapon.reloadStateName;
            reloadDuration = weaponAnimator.PlayReloadState(stateName, reloadWeapon.reloadTime);
        }

        reloadShootBlockedUntil = Mathf.Max(reloadShootBlockedUntil, Time.time + Mathf.Max(0f, reloadDuration));
        yield return new WaitForSeconds(Mathf.Max(0f, reloadDuration));

        if (reloadToken != reloadSequence || currentWeapon != reloadWeapon)
        {
            weaponAnimator?.StopReloadState();
            isReloading = false;
            yield break;
        }

        LoadStandardAmmo(reloadWeapon);
        SaveCurrentWeaponAmmo();
        weaponAnimator?.StopReloadState();
        isReloading = false;
    }

    IEnumerator ReloadStandardSequence(Weapon reloadWeapon, int reloadToken)
    {
        isReloading = true;
        canInterruptShotgunReload = false;
        shotgunReloadInterrupted = false;
        PlaySound(reloadWeapon.reloadSound, reloadSoundVolume);

        float estimatedDuration = GetConfiguredSequenceDuration(reloadWeapon);
        reloadShootBlockedUntil = Mathf.Max(reloadShootBlockedUntil, Time.time + Mathf.Max(0f, estimatedDuration));

        float startDuration = PlayReloadAnimation(reloadWeapon.shotgunReloadStartStateName, reloadWeapon.reloadStartDuration);
        if (startDuration > 0f)
        {
            yield return new WaitForSeconds(startDuration);
        }

        if (reloadToken != reloadSequence || currentWeapon != reloadWeapon)
        {
            weaponAnimator?.StopReloadState();
            isReloading = false;
            yield break;
        }

        float insertDuration = PlayReloadAnimation(reloadWeapon.shotgunReloadInsertStateName, reloadWeapon.reloadInsertDuration);
        if (insertDuration > 0f)
        {
            yield return new WaitForSeconds(insertDuration);
        }

        if (reloadToken != reloadSequence || currentWeapon != reloadWeapon)
        {
            weaponAnimator?.StopReloadState();
            isReloading = false;
            yield break;
        }

        LoadStandardAmmo(reloadWeapon);
        SaveCurrentWeaponAmmo();

        float endDuration = PlayReloadAnimation(reloadWeapon.shotgunReloadEndStateName, reloadWeapon.reloadEndDuration);
        if (endDuration > 0f)
        {
            yield return new WaitForSeconds(endDuration);
        }

        if (reloadToken != reloadSequence || currentWeapon != reloadWeapon)
        {
            weaponAnimator?.StopReloadState();
            isReloading = false;
            yield break;
        }

        weaponAnimator?.StopReloadState();
        isReloading = false;
    }

    float GetConfiguredSequenceDuration(Weapon reloadWeapon)
    {
        if (reloadWeapon == null)
        {
            return 0f;
        }

        float duration = Mathf.Max(0f, reloadWeapon.reloadStartDuration)
            + Mathf.Max(0f, reloadWeapon.reloadInsertDuration)
            + Mathf.Max(0f, reloadWeapon.reloadEndDuration);
        return duration > 0f ? duration : reloadWeapon.reloadTime;
    }

    IEnumerator ReloadShotgunPerShell(Weapon reloadWeapon, int reloadToken)
    {
        isReloading = true;
        canInterruptShotgunReload = false;
        shotgunReloadInterrupted = false;

        float startDuration = PlayReloadAnimation(
            reloadWeapon.shotgunReloadStartStateName,
            reloadWeapon.reloadStartDuration
        );

        if (startDuration > 0f)
        {
            yield return new WaitForSeconds(startDuration);
        }

        if (shotgunReloadInterrupted || reloadToken != reloadSequence || currentWeapon != reloadWeapon)
        {
            yield break;
        }

        while (!shotgunReloadInterrupted
            && reloadToken == reloadSequence
            && currentWeapon == reloadWeapon
            && currentAmmo < reloadWeapon.magazineSize
            && GetAvailableReserveAmmo() > 0)
        {
            PlaySound(reloadWeapon.reloadSound, reloadSoundVolume);
            float insertDuration = PlayReloadAnimation(
                reloadWeapon.shotgunReloadInsertStateName,
                reloadWeapon.reloadInsertDuration
            );

            if (insertDuration <= 0f)
            {
                insertDuration = reloadWeapon.reloadTime > 0f ? reloadWeapon.reloadTime : 0.1f;
            }

            yield return new WaitForSeconds(insertDuration);

            if (shotgunReloadInterrupted || reloadToken != reloadSequence || currentWeapon != reloadWeapon)
            {
                weaponAnimator?.StopReloadState();
                canInterruptShotgunReload = false;
                shotgunReloadInterrupted = false;
                isReloading = false;
                yield break;
            }

            int consumedAmmo = ConsumeReserveAmmo(reloadWeapon, 1);
            if (consumedAmmo <= 0)
            {
                PlayEmptyAmmoSound(reloadWeapon);
                break;
            }

            currentAmmo += consumedAmmo;
            SaveCurrentWeaponAmmo();
            canInterruptShotgunReload = currentAmmo > 0;
        }

        if (reloadToken != reloadSequence || currentWeapon != reloadWeapon)
        {
            yield break;
        }

        if (!shotgunReloadInterrupted && reloadToken == reloadSequence && currentWeapon == reloadWeapon)
        {
            float endDuration = PlayReloadAnimation(
                reloadWeapon.shotgunReloadEndStateName,
                reloadWeapon.reloadEndDuration
            );

            if (endDuration > 0f)
            {
                yield return new WaitForSeconds(endDuration);
            }
        }

        weaponAnimator?.StopReloadState();
        canInterruptShotgunReload = false;
        shotgunReloadInterrupted = false;
        isReloading = false;
    }

    float PlayReloadAnimation(string stateName, float fallbackDuration)
    {
        if (weaponAnimator == null || string.IsNullOrEmpty(stateName))
        {
            return Mathf.Max(0f, fallbackDuration);
        }

        return weaponAnimator.PlayReloadState(stateName, fallbackDuration);
    }

    string GetDefaultReloadStateName(Weapon weapon)
    {
        if (weapon == null)
        {
            return string.Empty;
        }

        return weapon.holdType == WeaponHoldType.OneHand ? "Pistol-Reload-R1" : "Shooting-Reload-Start";
    }

    int GetReadyAmmo()
    {
        return currentAmmo + currentChamberAmmo;
    }

    bool IsCurrentWeaponFullyLoaded()
    {
        if (currentWeapon == null)
        {
            return true;
        }

        return currentAmmo >= currentWeapon.magazineSize
            && (!currentWeapon.useChamberedRound || currentChamberAmmo > 0);
    }

    void ConsumeShotAmmo()
    {
        if (currentWeapon != null && currentWeapon.useChamberedRound)
        {
            if (currentChamberAmmo > 0)
            {
                currentChamberAmmo = 0;
                FeedChamberFromMagazine();
                return;
            }

            if (currentAmmo > 0)
            {
                currentAmmo--;
            }

            return;
        }

        currentAmmo = Mathf.Max(0, currentAmmo - 1);
    }

    void FeedChamberFromMagazine()
    {
        if (currentWeapon == null || !currentWeapon.useChamberedRound || currentChamberAmmo > 0 || currentAmmo <= 0)
        {
            return;
        }

        currentAmmo--;
        currentChamberAmmo = 1;
    }

    void LoadStandardAmmo(Weapon reloadWeapon)
    {
        if (reloadWeapon == null)
        {
            return;
        }

        if (reloadWeapon.useChamberedRound && currentChamberAmmo <= 0 && GetAvailableReserveAmmo() > 0)
        {
            int consumedChamberAmmo = ConsumeReserveAmmo(reloadWeapon, 1);
            if (consumedChamberAmmo > 0)
            {
                currentChamberAmmo = 1;
            }
        }

        int neededAmmo = reloadWeapon.magazineSize - currentAmmo;
        int ammoToLoad = ConsumeReserveAmmo(reloadWeapon, neededAmmo);
        currentAmmo += ammoToLoad;
    }

    int GetAvailableReserveAmmo()
    {
        if (useInventoryAmmo && ammoInventory != null && currentWeapon != null)
        {
            reserveAmmo = ammoInventory.GetAmmoCountForWeapon(currentWeapon);
        }

        return Mathf.Max(0, reserveAmmo);
    }

    int ConsumeReserveAmmo(Weapon weapon, int requestedAmount)
    {
        requestedAmount = Mathf.Max(0, requestedAmount);
        if (weapon == null || requestedAmount <= 0)
        {
            return 0;
        }

        if (useInventoryAmmo && ammoInventory != null)
        {
            ammoInventory.TryConsumeAmmoForWeapon(weapon, requestedAmount, out int consumedAmount);
            reserveAmmo = ammoInventory.GetAmmoCountForWeapon(weapon);
            return consumedAmount;
        }

        int ammoToConsume = Mathf.Min(requestedAmount, reserveAmmo);
        reserveAmmo -= ammoToConsume;
        return ammoToConsume;
    }

    void PlayEmptyAmmoSound(Weapon weapon)
    {
        if (weapon == null)
        {
            return;
        }

        PlaySound(weapon.emptyAmmoSound, emptyAmmoSoundVolume);
    }

    void SpawnEffect(GameObject prefab, Transform spawnPoint)
    {
        if (prefab == null || spawnPoint == null)
        {
            return;
        }

        SpawnPooled(prefab, spawnPoint.position, spawnPoint.rotation, muzzleEffectLifetime);
    }

    void SpawnShell()
    {
        Transform spawnPoint = shellEjectPoint != null ? shellEjectPoint : firePoint;
        if (currentWeapon.bulletShell == null || spawnPoint == null)
        {
            return;
        }

        SpawnPooled(currentWeapon.bulletShell, spawnPoint.position, spawnPoint.rotation, shellLifetime);
    }

    void SpawnImpactEffect(RaycastHit hit)
    {
        if (currentWeapon.impactEffect == null)
        {
            return;
        }

        Vector3 spawnPosition = hit.point + hit.normal * impactSurfaceOffset;
        Quaternion spawnRotation = Quaternion.LookRotation(hit.normal);
        SpawnPooled(currentWeapon.impactEffect, spawnPosition, spawnRotation, impactEffectLifetime);
    }

    void SpawnEnemyHitEffect(RaycastHit hit, bool isCritical)
    {
        SpawnEnemyHitEffect(hit.point, hit.normal, isCritical);
    }

    void SpawnEnemyHitEffect(Vector3 hitPoint, Vector3 hitNormal, bool isCritical)
    {
        GameObject effect = null;
        if (isCritical && currentWeapon.criticalHitEffect != null)
        {
            effect = currentWeapon.criticalHitEffect;
        }
        else if (currentWeapon.enemyHitEffect != null)
        {
            effect = currentWeapon.enemyHitEffect;
        }
        else
        {
            effect = currentWeapon.impactEffect;
        }

        if (effect == null)
        {
            return;
        }

        if (hitNormal.sqrMagnitude < 0.0001f)
        {
            hitNormal = Vector3.up;
        }

        Vector3 spawnPosition = hitPoint + hitNormal.normalized * impactSurfaceOffset;
        Quaternion spawnRotation = Quaternion.LookRotation(hitNormal.normalized);
        SpawnPooled(effect, spawnPosition, spawnRotation, impactEffectLifetime);
    }

    GameObject SpawnPooled(GameObject prefab, Vector3 position, Quaternion rotation, float lifetime)
    {
        if (prefab == null)
        {
            return null;
        }

        GameObject instance = LeanPool.Spawn(prefab, position, rotation);
        ResetPooledObject(instance);

        if (lifetime > 0f)
        {
            LeanPool.Despawn(instance, lifetime);
        }

        return instance;
    }

    void ResetPooledObject(GameObject instance)
    {
        Rigidbody rigidbody = instance.GetComponent<Rigidbody>();
        if (rigidbody != null)
        {
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
        }

        instance.GetComponentsInChildren<TrailRenderer>(true, pooledTrails);
        for (int i = 0; i < pooledTrails.Count; i++)
        {
            pooledTrails[i].Clear();
        }
        pooledTrails.Clear();

        instance.GetComponentsInChildren<ParticleSystem>(true, pooledParticles);
        for (int i = 0; i < pooledParticles.Count; i++)
        {
            pooledParticles[i].Clear(true);
            pooledParticles[i].Play(true);
        }
        pooledParticles.Clear();
    }

    void PlaySound(AudioClip clip, float volumeScale)
    {
        if (clip == null)
        {
            return;
        }

        if (audioSource != null)
        {
            audioSource.PlayOneShot(clip, volumeScale);
        }
        else
        {
            AudioSource.PlayClipAtPoint(clip, transform.position, volumeScale);
        }
    }

    void ApplyRecoil()
    {
        if (recoil == null)
        {
            return;
        }

        recoil.ApplyRecoil(currentWeapon);
    }

    public void SetWeapon(Weapon weapon, Transform muzzlePoint, Transform shellPoint)
    {
        SaveCurrentWeaponAmmo();
        currentWeapon = weapon;

        if (muzzlePoint != null)
        {
            firePoint = muzzlePoint;
            muzzleEffectPoint = muzzlePoint;
        }

        if (shellPoint != null)
        {
            shellEjectPoint = shellPoint;
        }

        InitializeAmmo(currentWeapon);
        weaponAnimator?.SetWeapon(currentWeapon);
        nextFireTime = 0f;
        isReloading = false;
        reloadShootBlockedUntil = 0f;
        isWaitingAutoReload = false;
        autoShootAnimationPlayed = false;
        canInterruptShotgunReload = false;
        shotgunReloadInterrupted = true;
        reloadSequence++;
        weaponAnimator?.StopReloadState();
    }

    void InitializeAmmo(Weapon weapon)
    {
        if (weapon == null)
        {
            currentAmmo = 0;
            currentChamberAmmo = 0;
            reserveAmmo = 0;
            return;
        }

        if (!initializedAmmo)
        {
            reserveAmmo = initialReserveAmmo;
            initializedAmmo = true;
        }

        if (magazineAmmoByWeapon.TryGetValue(weapon, out int savedAmmo))
        {
            currentAmmo = Mathf.Clamp(savedAmmo, 0, weapon.magazineSize);
            currentChamberAmmo = chamberAmmoByWeapon.TryGetValue(weapon, out int savedChamberAmmo)
                ? Mathf.Clamp(savedChamberAmmo, 0, weapon.useChamberedRound ? 1 : 0)
                : 0;
            reserveAmmo = GetAvailableReserveAmmo();
            return;
        }

        currentAmmo = weapon.magazineSize;
        currentChamberAmmo = weapon.useChamberedRound ? 1 : 0;
        magazineAmmoByWeapon[weapon] = currentAmmo;
        chamberAmmoByWeapon[weapon] = currentChamberAmmo;
        if (subtractInitialMagazineFromReserve && (!useInventoryAmmo || ammoInventory == null))
        {
            reserveAmmo = Mathf.Max(0, reserveAmmo - GetReadyAmmo());
        }

        reserveAmmo = GetAvailableReserveAmmo();
    }

    void SaveCurrentWeaponAmmo()
    {
        if (currentWeapon == null)
        {
            return;
        }

        magazineAmmoByWeapon[currentWeapon] = Mathf.Clamp(currentAmmo, 0, currentWeapon.magazineSize);
        chamberAmmoByWeapon[currentWeapon] = Mathf.Clamp(currentChamberAmmo, 0, currentWeapon.useChamberedRound ? 1 : 0);
    }
}
