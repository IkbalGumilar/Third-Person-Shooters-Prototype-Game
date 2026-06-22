using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyRangedWeaponController : MonoBehaviour
{
    const int MaxRaycastHits = 32;

    public EnemyRangedWeapon startingWeapon;
    public Transform weaponSocket;
    public Transform muzzlePoint;
    public GameObject existingWeaponObject;
    public Animator animator;
    public bool autoSelectRangeLayer = true;
    public float rangeLayerFadeOut = 0.12f;

    private EnemyRangedWeapon currentWeapon;
    private GameObject equippedWeaponObject;
    private bool equippedObjectWasInstantiated;
    private AudioSource audioSource;
    private Enemy enemy;
    private EnemyAI enemyAI;
    private float nextAttackTime;
    private int currentAmmo;
    private Coroutine attackRoutine;
    private Coroutine rangeFadeRoutine;
    private int rangeLayerIndex = -1;
    private float rangeFadeTarget = -1f;
    private EnemyStatusEffectController statusController;
    private readonly RaycastHit[] raycastHits = new RaycastHit[MaxRaycastHits];
    private readonly Dictionary<string, int> stateHashes = new Dictionary<string, int>();

    public EnemyRangedWeapon CurrentWeapon => currentWeapon;
    public bool IsAttacking => attackRoutine != null;
    public float EffectiveAttackRange => currentWeapon != null && CanUseCurrentWeapon()
        ? GetModifiedStat(StatusEffectStat.EnemyRangedAttackRange, currentWeapon.attackRange)
        : 0f;

    void Awake()
    {
        enemy = GetComponent<Enemy>();
        enemyAI = GetComponent<EnemyAI>();
        statusController = GetComponent<EnemyStatusEffectController>();
        animator = animator != null ? animator : GetComponentInChildren<Animator>();
        AnimationEventReceiver.EnsureOn(animator);
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
        }
    }

    void Start()
    {
        if (startingWeapon != null)
        {
            EquipWeapon(startingWeapon);
        }
    }

    public void EquipWeapon(EnemyRangedWeapon weapon)
    {
        if (weapon == null)
        {
            return;
        }

        currentWeapon = weapon;
        currentAmmo = Mathf.Max(1, GetModifiedIntStat(StatusEffectStat.EnemyRangedMagazineSize, currentWeapon.magazineSize));
        rangeLayerIndex = -1;

        GameObject weaponObject = ResolveWeaponObject(weapon);
        if (weaponObject != null)
        {
            SetupWeaponTransform(weaponObject, weapon);
        }

        enemyAI?.ConfigureLocomotionLayer();
    }

    public bool TryAttack(Transform target)
    {
        if (currentWeapon == null || target == null || Time.time < nextAttackTime || IsAttacking)
        {
            return false;
        }

        float distanceSqr = (transform.position - target.position).sqrMagnitude;
        if (!CanUseCurrentWeapon())
        {
            return false;
        }

        if (ShouldUseCrossbowMelee(distanceSqr))
        {
            nextAttackTime = Time.time + Mathf.Max(0.01f, GetModifiedStat(StatusEffectStat.EnemyRangedCrossbowMeleeCooldown, currentWeapon.crossbowMeleeCooldown));
            attackRoutine = StartCoroutine(CrossbowMeleeRoutine(target));
            return true;
        }

        float attackRange = GetModifiedStat(StatusEffectStat.EnemyRangedAttackRange, currentWeapon.attackRange);
        if (distanceSqr > attackRange * attackRange || !HasLineOfSight(target))
        {
            return false;
        }

        if (currentAmmo <= 0)
        {
            nextAttackTime = Time.time + Mathf.Max(0.01f, GetModifiedStat(StatusEffectStat.EnemyRangedReloadDuration, currentWeapon.reloadDuration));
            attackRoutine = StartCoroutine(ReloadRoutine());
            return true;
        }

        nextAttackTime = Time.time + Mathf.Max(0.01f, GetModifiedStat(StatusEffectStat.EnemyRangedAttackCooldown, currentWeapon.attackCooldown));
        attackRoutine = StartCoroutine(ShootRoutine(target));
        return true;
    }

    public void CancelAttack()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
            enemyAI?.SetLocomotionSuppressed(false);
        }

        FadeRangeLayerWeight(0f);
    }

    // A hit reaction uses the same full-body weapon layer as this attack.
    // Its existing fade coroutine must not turn the reaction layer off later.
    public void InterruptForHitReaction()
    {
        bool wasAttacking = attackRoutine != null;
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        StopRangeFade();
        SetRangeLayerWeight(0f);
        if (wasAttacking)
        {
            enemyAI?.SetLocomotionSuppressed(false);
        }
    }

    IEnumerator ShootRoutine(Transform target)
    {
        StopRangeFade();
        enemyAI?.SetLocomotionSuppressed(true);
        EnemyAnimationLayers.SetExclusiveLayer(animator, GetRangeLayerIndex());
        SetRangeLayerWeight(1f);
        Vector3 lockedTargetPoint = GetTargetPoint(target);
        Vector3 lockedTargetPosition = target.position;
        float aimLockDelay = GetAimLockDelay();
        if (aimLockDelay > 0f)
        {
            yield return new WaitForSeconds(aimLockDelay);
        }

        PlayRandomShootAnimation();
        PlaySound(currentWeapon.shootSound);
        yield return null;
        float animationDuration = GetCurrentAnimationDuration(GetModifiedStat(StatusEffectStat.EnemyRangedAttackCooldown, currentWeapon.attackCooldown));
        float delay = Mathf.Max(0f, GetModifiedStat(StatusEffectStat.EnemyRangedDamageDelay, currentWeapon.damageDelay));
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        DealRangedDamage(target, lockedTargetPoint, lockedTargetPosition);
        currentAmmo = Mathf.Max(0, currentAmmo - 1);

        float totalActionDuration = Mathf.Max(
            GetModifiedStat(StatusEffectStat.EnemyRangedAttackCooldown, currentWeapon.attackCooldown),
            aimLockDelay + animationDuration
        );
        float remaining = Mathf.Max(0f, totalActionDuration - aimLockDelay - delay);
        if (remaining > 0f)
        {
            yield return new WaitForSeconds(remaining);
        }

        FadeRangeLayerWeight(0f);
        enemyAI?.SetLocomotionSuppressed(false);
        attackRoutine = null;
    }

    IEnumerator ReloadRoutine()
    {
        StopRangeFade();
        enemyAI?.SetLocomotionSuppressed(true);
        EnemyAnimationLayers.SetExclusiveLayer(animator, GetRangeLayerIndex());
        SetRangeLayerWeight(1f);
        PlayReloadAnimation();
        PlaySound(currentWeapon.reloadSound);
        yield return null;
        float reloadDuration = Mathf.Max(
            GetModifiedStat(StatusEffectStat.EnemyRangedReloadDuration, currentWeapon.reloadDuration),
            GetCurrentAnimationDuration(currentWeapon.reloadDuration)
        );
        yield return new WaitForSeconds(reloadDuration);

        currentAmmo = Mathf.Max(1, GetModifiedIntStat(StatusEffectStat.EnemyRangedMagazineSize, currentWeapon.magazineSize));
        FadeRangeLayerWeight(0f);
        enemyAI?.SetLocomotionSuppressed(false);
        attackRoutine = null;
    }

    IEnumerator CrossbowMeleeRoutine(Transform target)
    {
        StopRangeFade();
        enemyAI?.SetLocomotionSuppressed(true);
        EnemyAnimationLayers.SetExclusiveLayer(animator, GetRangeLayerIndex());
        SetRangeLayerWeight(1f);
        PlayRandomCrossbowMeleeAnimation();
        yield return null;
        float animationDuration = GetCurrentAnimationDuration(currentWeapon.crossbowMeleeCooldown);

        float delay = Mathf.Max(0f, GetModifiedStat(StatusEffectStat.EnemyRangedCrossbowMeleeDelay, currentWeapon.crossbowMeleeDelay));
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        DealCrossbowMeleeDamage(target);

        float remaining = Mathf.Max(
            0f,
            Mathf.Max(
                GetModifiedStat(StatusEffectStat.EnemyRangedCrossbowMeleeCooldown, currentWeapon.crossbowMeleeCooldown),
                animationDuration
            ) - delay
        );
        if (remaining > 0f)
        {
            yield return new WaitForSeconds(remaining);
        }

        FadeRangeLayerWeight(0f);
        enemyAI?.SetLocomotionSuppressed(false);
        attackRoutine = null;
    }

    float GetCurrentAnimationDuration(float fallbackDuration)
    {
        if (animator == null)
        {
            return Mathf.Max(0.01f, fallbackDuration);
        }

        int layerIndex = GetRangeLayerIndex();
        AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(layerIndex);
        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(layerIndex);
        float duration = animator.IsInTransition(layerIndex) && nextState.length > 0f
            ? nextState.length
            : currentState.length;
        return Mathf.Max(0.01f, duration, fallbackDuration);
    }

    bool ShouldUseCrossbowMelee(float distanceSqr)
    {
        float meleeRange = GetModifiedStat(StatusEffectStat.EnemyRangedCrossbowMeleeRange, currentWeapon.crossbowMeleeRange);
        return currentWeapon != null
            && currentWeapon.weaponKind == EnemyRangedWeaponKind.Crossbow
            && currentWeapon.bossCanUseCrossbowMelee
            && GetEnemyType() == EnemyType.Boss
            && distanceSqr <= meleeRange * meleeRange;
    }

    bool HasLineOfSight(Transform target)
    {
        if (currentWeapon == null || !currentWeapon.requireLineOfSight || target == null)
        {
            return true;
        }

        Vector3 origin = muzzlePoint != null ? muzzlePoint.position : transform.position + Vector3.up;
        Vector3 targetPoint = target.position + Vector3.up;
        Vector3 direction = targetPoint - origin;
        float distance = direction.magnitude;
        if (distance <= 0.001f)
        {
            return true;
        }

        if (TryRaycastIgnoringSelf(origin, direction.normalized, distance, out RaycastHit hit))
        {
            return hit.transform == target || hit.transform.IsChildOf(target) || target.IsChildOf(hit.transform);
        }

        return true;
    }

    void DealRangedDamage(Transform target, Vector3 lockedTargetPoint, Vector3 lockedTargetPosition)
    {
        if (currentWeapon == null || target == null)
        {
            return;
        }

        Vector3 origin = muzzlePoint != null ? muzzlePoint.position : transform.position + Vector3.up;
        Vector3 baseDirection = (lockedTargetPoint - origin).normalized;
        int pelletCount = Mathf.Max(1, GetModifiedIntStat(StatusEffectStat.EnemyRangedPelletCount, currentWeapon.pelletCount));
        float attackRange = GetModifiedStat(StatusEffectStat.EnemyRangedAttackRange, currentWeapon.attackRange);
        float spreadAngle = GetModifiedStat(StatusEffectStat.EnemyRangedSpreadAngle, currentWeapon.spreadAngle);
        float damagePerPellet = GetModifiedStat(StatusEffectStat.EnemyRangedDamage, currentWeapon.damage) / pelletCount;

        for (int i = 0; i < pelletCount; i++)
        {
            Vector3 direction = ApplySpread(baseDirection, spreadAngle);

            if (TryRaycastIgnoringSelf(origin, direction, attackRange, out RaycastHit hit))
            {
                PlayerHealth hitHealth = hit.collider.GetComponentInParent<PlayerHealth>();
                if (hitHealth != null)
                {
                    hitHealth.TakeDamage(damagePerPellet, hit.point, hit.normal);
                    TryApplyStatusEffects(hitHealth, currentWeapon.statusEffects, currentWeapon.statusEffectChance);
                    PlaySound(currentWeapon.hitSound);
                }
            }
            else if (pelletCount == 1 && ShouldUseDirectHitFallback(target, lockedTargetPosition))
            {
                PlayerHealth targetHealth = target.GetComponentInParent<PlayerHealth>();
                if (targetHealth != null && (transform.position - target.position).sqrMagnitude <= attackRange * attackRange)
                {
                    Vector3 normal = (target.position - transform.position).normalized;
                    targetHealth.TakeDamage(damagePerPellet, target.position, -normal);
                    TryApplyStatusEffects(targetHealth, currentWeapon.statusEffects, currentWeapon.statusEffectChance);
                    PlaySound(currentWeapon.hitSound);
                }
            }
        }
    }

    bool ShouldUseDirectHitFallback(Transform target, Vector3 lockedTargetPosition)
    {
        if (currentWeapon == null || target == null)
        {
            return false;
        }

        if (currentWeapon.allowDirectHitFallback)
        {
            return true;
        }

        if (!currentWeapon.allowStationaryTargetFallback)
        {
            return false;
        }

        float tolerance = Mathf.Max(0f, GetModifiedStat(StatusEffectStat.EnemyRangedStationaryFallbackMoveTolerance, currentWeapon.stationaryFallbackMoveTolerance));
        return (target.position - lockedTargetPosition).sqrMagnitude <= tolerance * tolerance;
    }

    Vector3 GetTargetPoint(Transform target)
    {
        if (target == null)
        {
            return transform.position + transform.forward;
        }

        float height = currentWeapon != null ? GetModifiedStat(StatusEffectStat.EnemyRangedAimTargetHeight, currentWeapon.aimTargetHeight) : 1f;
        return target.position + Vector3.up * height;
    }

    float GetAimLockDelay()
    {
        if (currentWeapon == null)
        {
            return 0f;
        }

        float minDelay = Mathf.Max(0f, GetModifiedStat(StatusEffectStat.EnemyRangedAimLockDelayMin, currentWeapon.aimLockDelayRange.x));
        float maxDelay = Mathf.Max(minDelay, GetModifiedStat(StatusEffectStat.EnemyRangedAimLockDelayMax, currentWeapon.aimLockDelayRange.y));
        return Random.Range(minDelay, maxDelay);
    }

    void DealCrossbowMeleeDamage(Transform target)
    {
        if (currentWeapon == null || target == null)
        {
            return;
        }

        float meleeRange = GetModifiedStat(StatusEffectStat.EnemyRangedCrossbowMeleeRange, currentWeapon.crossbowMeleeRange);
        if ((transform.position - target.position).sqrMagnitude > meleeRange * meleeRange)
        {
            return;
        }

        PlayerHealth playerHealth = target.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null)
        {
            return;
        }

        Vector3 hitNormal = target.position - transform.position;
        hitNormal.y = 0f;
        if (hitNormal.sqrMagnitude < 0.0001f)
        {
            hitNormal = transform.forward;
        }

        hitNormal.Normalize();
        Vector3 hitPoint = playerHealth.transform.position - hitNormal * 0.25f;
        playerHealth.TakeDamage(GetModifiedStat(StatusEffectStat.EnemyRangedCrossbowMeleeDamage, currentWeapon.crossbowMeleeDamage), hitPoint, hitNormal);
        TryApplyStatusEffects(playerHealth, currentWeapon.crossbowMeleeStatusEffects, currentWeapon.crossbowMeleeStatusEffectChance);
        PlaySound(currentWeapon.hitSound);
    }

    void TryApplyStatusEffects(PlayerHealth playerHealth, StatusEffectData[] effects, float chancePercent)
    {
        if (playerHealth == null)
        {
            return;
        }

        PlayerStatusEffectController statusController = GetPlayerStatusController(playerHealth);
        if (statusController == null)
        {
            return;
        }

        EnemyData data = enemy != null ? enemy.enemyData : null;
        if (TryApplyEnemyDataStatusEffects(statusController, data))
        {
            return;
        }

        if (effects == null || effects.Length == 0)
        {
            return;
        }

        if (Random.value > Mathf.Clamp01(chancePercent / 100f))
        {
            return;
        }

        for (int i = 0; i < effects.Length; i++)
        {
            if (effects[i] != null)
            {
                statusController.AddEffect(effects[i]);
            }
        }
    }

    bool TryApplyEnemyDataStatusEffects(PlayerStatusEffectController statusController, EnemyData data)
    {
        if (statusController == null || data == null || data.hitStatusEffects == null || data.hitStatusEffects.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < data.hitStatusEffects.Length; i++)
        {
            EnemyStatusEffectChance entry = data.hitStatusEffects[i];
            if (entry != null && entry.effect != null && Random.value <= Mathf.Clamp01(entry.chance / 100f))
            {
                statusController.AddEffect(entry.effect);
            }
        }

        return true;
    }

    PlayerStatusEffectController GetPlayerStatusController(PlayerHealth playerHealth)
    {
        if (playerHealth == null)
        {
            return null;
        }

        PlayerStatusEffectController statusController = playerHealth.GetComponent<PlayerStatusEffectController>();
        return statusController != null ? statusController : playerHealth.GetComponentInParent<PlayerStatusEffectController>();
    }

    float GetModifiedStat(StatusEffectStat stat, float baseValue)
    {
        statusController = statusController != null
            ? statusController
            : enemy != null ? enemy.StatusController : GetComponent<EnemyStatusEffectController>();
        return statusController != null ? statusController.ModifyStat(stat, baseValue) : baseValue;
    }

    int GetModifiedIntStat(StatusEffectStat stat, int baseValue)
    {
        statusController = statusController != null
            ? statusController
            : enemy != null ? enemy.StatusController : GetComponent<EnemyStatusEffectController>();
        return statusController != null ? statusController.ModifyIntStat(stat, baseValue) : Mathf.Max(1, baseValue);
    }

    Vector3 ApplySpread(Vector3 direction, float spreadAngle)
    {
        if (spreadAngle <= 0f)
        {
            return direction;
        }

        Quaternion spread = Quaternion.Euler(
            Random.Range(-spreadAngle, spreadAngle),
            Random.Range(-spreadAngle, spreadAngle),
            0f
        );

        return spread * direction;
    }

    bool TryRaycastIgnoringSelf(Vector3 origin, Vector3 direction, float distance, out RaycastHit closestHit)
    {
        closestHit = new RaycastHit();
        if (currentWeapon == null)
        {
            return false;
        }

        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction,
            raycastHits,
            distance,
            currentWeapon.hitMask,
            QueryTriggerInteraction.Ignore
        );

        bool hasHit = false;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = raycastHits[i];
            if (hit.transform == null || hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hit.collider != null && hit.collider.GetComponentInParent<WorldItemPickup>() != null)
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestHit = hit;
                hasHit = true;
            }
        }

        return hasHit;
    }

    void PlayRandomShootAnimation()
    {
        if (currentWeapon == null)
        {
            return;
        }

        if (currentWeapon.weaponKind == EnemyRangedWeaponKind.Crossbow)
        {
            PlayRandomState(currentWeapon.crossbowShootStateNames);
            return;
        }

        if (currentWeapon.weaponKind == EnemyRangedWeaponKind.Shotgun)
        {
            PlayRandomState(currentWeapon.shotgunShootStateNames);
            return;
        }

        if (UseDualPistol())
        {
            PlayRandomState(currentWeapon.dualPistolShootStateNames);
            return;
        }

        PlayRandomState(currentWeapon.handgunShootStateNames);
    }

    void PlayReloadAnimation()
    {
        if (currentWeapon == null)
        {
            return;
        }

        if (currentWeapon.weaponKind == EnemyRangedWeaponKind.Crossbow)
        {
            PlayState(currentWeapon.crossbowReloadStateName);
            return;
        }

        if (currentWeapon.weaponKind == EnemyRangedWeaponKind.Shotgun)
        {
            PlayState(currentWeapon.shotgunReloadStateName);
            return;
        }

        if (UseDualPistol())
        {
            PlayRandomState(currentWeapon.dualPistolReloadStateNames);
            return;
        }

        PlayState(currentWeapon.handgunReloadStateName);
    }

    void PlayRandomCrossbowMeleeAnimation()
    {
        if (currentWeapon != null)
        {
            PlayRandomState(currentWeapon.crossbowMeleeStateNames);
        }
    }

    bool UseDualPistol()
    {
        if (currentWeapon == null || currentWeapon.weaponKind != EnemyRangedWeaponKind.DualPistol)
        {
            return false;
        }

        if (!currentWeapon.eliteAndMiniBossUseDualPistol)
        {
            return true;
        }

        EnemyType enemyType = GetEnemyType();
        return enemyType == EnemyType.Elite || enemyType == EnemyType.MiniBoss;
    }

    bool CanUseCurrentWeapon()
    {
        if (currentWeapon == null || !currentWeapon.miniBossAndBossOnly)
        {
            return true;
        }

        EnemyType enemyType = GetEnemyType();
        return enemyType == EnemyType.MiniBoss || enemyType == EnemyType.Boss;
    }

    EnemyType GetEnemyType()
    {
        if (enemy == null)
        {
            enemy = GetComponent<Enemy>();
        }

        return enemy != null && enemy.enemyData != null ? enemy.enemyData.enemyType : EnemyType.Basic;
    }

    void PlayRandomState(string[] stateNames)
    {
        if (stateNames == null || stateNames.Length == 0)
        {
            return;
        }

        int startIndex = Random.Range(0, stateNames.Length);
        for (int i = 0; i < stateNames.Length; i++)
        {
            string stateName = stateNames[(startIndex + i) % stateNames.Length];
            if (PlayState(stateName))
            {
                return;
            }
        }
    }

    bool PlayState(string stateName)
    {
        if (animator == null || currentWeapon == null || string.IsNullOrEmpty(stateName))
        {
            return false;
        }

        int stateHash = GetStateHash(stateName);
        if (stateHash == 0)
        {
            return false;
        }

        animator.CrossFadeInFixedTime(stateHash, Mathf.Max(0f, GetModifiedStat(StatusEffectStat.EnemyRangedAnimationCrossFade, currentWeapon.animationCrossFade)), GetRangeLayerIndex());
        return true;
    }

    int GetStateHash(string stateName)
    {
        int layerIndex = GetRangeLayerIndex();
        if (!stateHashes.TryGetValue(stateName, out int shortHash))
        {
            shortHash = Animator.StringToHash(stateName);
            stateHashes[stateName] = shortHash;
        }

        if (animator.HasState(layerIndex, shortHash))
        {
            return shortHash;
        }

        string layerName = GetRangeLayerName();
        string fullPath = $"{layerName}.{stateName}";
        if (!stateHashes.TryGetValue(fullPath, out int fullPathHash))
        {
            fullPathHash = Animator.StringToHash(fullPath);
            stateHashes[fullPath] = fullPathHash;
        }

        if (animator.HasState(layerIndex, fullPathHash))
        {
            return fullPathHash;
        }

        return 0;
    }

    int GetRangeLayerIndex()
    {
        if (rangeLayerIndex < 0)
        {
            rangeLayerIndex = animator != null ? animator.GetLayerIndex(GetRangeLayerName()) : -1;
        }

        return rangeLayerIndex >= 0 ? rangeLayerIndex : 0;
    }

    string GetRangeLayerName()
    {
        if (!autoSelectRangeLayer || currentWeapon == null)
        {
            return currentWeapon != null ? currentWeapon.rangeLayerName : "Range";
        }

        return currentWeapon.weaponKind switch
        {
            EnemyRangedWeaponKind.Handgun => "1Hand-Pistol",
            EnemyRangedWeaponKind.DualPistol => "1Hand-Pistol",
            EnemyRangedWeaponKind.Crossbow => "2Hand-Crossbow",
            EnemyRangedWeaponKind.Shotgun => "2Hand-Shooting",
            _ => currentWeapon.rangeLayerName
        };
    }

    void SetRangeLayerWeight(float weight)
    {
        if (animator == null)
        {
            return;
        }

        int layerIndex = GetRangeLayerIndex();
        if (layerIndex > 0)
        {
            animator.SetLayerWeight(layerIndex, weight);
        }
    }

    void FadeRangeLayerWeight(float targetWeight)
    {
        int layerIndex = GetRangeLayerIndex();
        if (layerIndex <= 0 || animator == null)
        {
            return;
        }

        float currentWeight = animator.GetLayerWeight(layerIndex);
        if (Mathf.Abs(currentWeight - targetWeight) <= 0.001f)
        {
            return;
        }

        if (rangeFadeRoutine != null && Mathf.Approximately(rangeFadeTarget, targetWeight))
        {
            return;
        }

        StopRangeFade();
        rangeFadeTarget = targetWeight;
        rangeFadeRoutine = StartCoroutine(FadeAnimatorLayerWeight(layerIndex, targetWeight, rangeLayerFadeOut));
    }

    IEnumerator FadeAnimatorLayerWeight(int layerIndex, float targetWeight, float duration)
    {
        if (animator == null || layerIndex < 0)
        {
            yield break;
        }

        float startWeight = animator.GetLayerWeight(layerIndex);
        if (duration <= 0f)
        {
            animator.SetLayerWeight(layerIndex, targetWeight);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            animator.SetLayerWeight(layerIndex, Mathf.Lerp(startWeight, targetWeight, t));
            yield return null;
        }

        animator.SetLayerWeight(layerIndex, targetWeight);
    }

    void StopRangeFade()
    {
        if (rangeFadeRoutine == null)
        {
            return;
        }

        StopCoroutine(rangeFadeRoutine);
        rangeFadeRoutine = null;
        rangeFadeTarget = -1f;
    }

    GameObject ResolveWeaponObject(EnemyRangedWeapon weapon)
    {
        ClearEquippedWeapon();
        equippedObjectWasInstantiated = false;

        if (weapon.weaponPrefab != null)
        {
            equippedWeaponObject = Instantiate(weapon.weaponPrefab);
            equippedObjectWasInstantiated = true;
            return equippedWeaponObject;
        }

        equippedWeaponObject = existingWeaponObject;
        return equippedWeaponObject;
    }

    void ClearEquippedWeapon()
    {
        if (equippedWeaponObject == null)
        {
            return;
        }

        if (equippedObjectWasInstantiated)
        {
            Destroy(equippedWeaponObject);
        }
        else
        {
            equippedWeaponObject.SetActive(false);
        }

        equippedWeaponObject = null;
        equippedObjectWasInstantiated = false;
    }

    void SetupWeaponTransform(GameObject weaponObject, EnemyRangedWeapon weapon)
    {
        weaponObject.SetActive(true);

        if (weaponSocket != null)
        {
            weaponObject.transform.SetParent(weaponSocket, false);
        }

        weaponObject.transform.localPosition = weapon.holdPosition;
        weaponObject.transform.localRotation = Quaternion.Euler(weapon.holdRotation);
        weaponObject.transform.localScale = weapon.holdScale;
    }

    void PlaySound(AudioClip clip)
    {
        if (clip == null || audioSource == null)
        {
            return;
        }

        audioSource.PlayOneShot(clip);
    }
}
