using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyRangedWeaponController : MonoBehaviour
{
    const int MaxRaycastHits = 32;
    const string AimLayerOwner = "Enemy.RangedAim";
    const string WeaponActionLayerOwner = "Enemy.RangedAction";

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
    private int aimLayerIndex = -1;
    private float rangeFadeTarget = -1f;
    private EnemyStatusEffectController statusController;
    private bool wantsAimPose;
    private bool aimPoseActive;
    private int activeAimStateHash;
    private int activeAimLayerIndex = -1;
    private bool weaponActionLayerClaimed;
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

    void OnDisable()
    {
        CancelAttack();
    }

    public void EquipWeapon(EnemyRangedWeapon weapon)
    {
        if (weapon == null)
        {
            return;
        }

        DeactivateAimPose();
        ReleaseWeaponActionLayer();
        currentWeapon = weapon;
        currentAmmo = Mathf.Max(1, GetModifiedIntStat(StatusEffectStat.EnemyRangedMagazineSize, currentWeapon.magazineSize));
        rangeLayerIndex = -1;
        aimLayerIndex = -1;
        activeAimStateHash = 0;
        activeAimLayerIndex = -1;
        wantsAimPose = false;
        aimPoseActive = false;

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

        if (ShouldUseRangedBossMelee(distanceSqr))
        {
            nextAttackTime = Time.time + Mathf.Max(0.01f, GetModifiedStat(StatusEffectStat.EnemyRangedCrossbowMeleeCooldown, currentWeapon.crossbowMeleeCooldown));
            attackRoutine = StartCoroutine(RangedBossMeleeRoutine(target));
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

    public bool CanHoldAttackPosition(Transform target)
    {
        if (currentWeapon == null || target == null || !CanUseCurrentWeapon())
        {
            return false;
        }

        float attackRange = GetModifiedStat(StatusEffectStat.EnemyRangedAttackRange, currentWeapon.attackRange);
        if ((transform.position - target.position).sqrMagnitude > attackRange * attackRange)
        {
            return false;
        }

        return HasLineOfSight(target);
    }

    public void CancelAttack()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
            enemyAI?.SetLocomotionSuppressed(false);
        }

        ReleaseWeaponActionLayer();
        SetAimTargetVisible(null, false);
        FadeRangeLayerWeight(0f);
    }

    public void SetAimTargetVisible(Transform target, bool visible)
    {
        bool shouldAim = visible
            && target != null
            && currentWeapon != null
            && CanUseCurrentWeapon()
            && HasLineOfSight(target);

        wantsAimPose = shouldAim;
        if (IsAttacking)
        {
            return;
        }

        if (shouldAim)
        {
            ActivateAimPose();
        }
        else
        {
            DeactivateAimPose();
        }
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
        DeactivateAimPose();
        ReleaseWeaponActionLayer();
        SetRangeLayerWeight(0f);
        if (wasAttacking)
        {
            enemyAI?.SetLocomotionSuppressed(false);
        }
    }

    IEnumerator ShootRoutine(Transform target)
    {
        StopRangeFade();
        Vector3 lockedTargetPoint = GetTargetPoint(target);
        Vector3 lockedTargetPosition = target.position;
        float aimLockDelay = GetAimLockDelay();
        if (aimLockDelay > 0f)
        {
            yield return new WaitForSeconds(aimLockDelay);
        }

        // 1Hand-Pistol currently defaults to an attack state. Do not expose
        // that layer while the enemy is merely locking its aim; activate it
        // only in the same frame as the selected firing animation.
        BeginWeaponAction();
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

        EndWeaponAction();
        attackRoutine = null;
    }

    IEnumerator ReloadRoutine()
    {
        StopRangeFade();
        BeginWeaponAction();
        PlayReloadAnimation();
        PlaySound(currentWeapon.reloadSound);
        yield return null;
        float reloadDuration = Mathf.Max(
            GetModifiedStat(StatusEffectStat.EnemyRangedReloadDuration, currentWeapon.reloadDuration),
            GetCurrentAnimationDuration(currentWeapon.reloadDuration)
        );
        yield return new WaitForSeconds(reloadDuration);

        currentAmmo = Mathf.Max(1, GetModifiedIntStat(StatusEffectStat.EnemyRangedMagazineSize, currentWeapon.magazineSize));
        EndWeaponAction();
        attackRoutine = null;
    }

    IEnumerator RangedBossMeleeRoutine(Transform target)
    {
        StopRangeFade();
        BeginWeaponAction();
        PlayRandomRangedBossMeleeAnimation();
        yield return null;
        float animationDuration = GetCurrentAnimationDuration(currentWeapon.crossbowMeleeCooldown);

        float delay = Mathf.Max(0f, GetModifiedStat(StatusEffectStat.EnemyRangedCrossbowMeleeDelay, currentWeapon.crossbowMeleeDelay));
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        DealRangedBossMeleeDamage(target);

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

        EndWeaponAction();
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

    bool ShouldUseRangedBossMelee(float distanceSqr)
    {
        float meleeRange = GetModifiedStat(StatusEffectStat.EnemyRangedCrossbowMeleeRange, currentWeapon.crossbowMeleeRange);
        return currentWeapon != null
            && (currentWeapon.weaponKind == EnemyRangedWeaponKind.Crossbow || currentWeapon.weaponKind == EnemyRangedWeaponKind.Shotgun)
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
        float spreadRotation = Random.Range(0f, Mathf.PI * 2f);
        bool isKnockback = TryGetKnockbackDistance(
            currentWeapon.knockbackChance,
            currentWeapon.knockbackPower,
            currentWeapon.maxKnockbackDistance,
            out float knockbackDistance);
        float knockbackPerPellet = isKnockback ? knockbackDistance / pelletCount : 0f;
        float accumulatedKnockbackDistance = 0f;
        PlayerHealth knockbackTarget = null;
        Vector3 knockbackDirection = Vector3.zero;

        for (int i = 0; i < pelletCount; i++)
        {
            Vector3 direction = GetSpreadDirection(baseDirection, i, pelletCount, spreadAngle, spreadRotation);

            if (TryRaycastIgnoringSelf(origin, direction, attackRange, out RaycastHit hit))
            {
                PlayerHealth hitHealth = hit.collider.GetComponentInParent<PlayerHealth>();
                if (hitHealth != null)
                {
                    ApplyRangedHitDamage(hitHealth, damagePerPellet, hit.point, hit.normal, isKnockback);
                    if (isKnockback && !hitHealth.LastDamageWasBlocked && !hitHealth.IsDead)
                    {
                        accumulatedKnockbackDistance += knockbackPerPellet;
                        knockbackTarget = hitHealth;
                        knockbackDirection = GetKnockbackDirection(hitHealth.transform);
                    }

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
                    ApplyRangedHitDamage(targetHealth, damagePerPellet, target.position, -normal, isKnockback);
                    if (isKnockback && !targetHealth.LastDamageWasBlocked && !targetHealth.IsDead)
                    {
                        accumulatedKnockbackDistance += knockbackPerPellet;
                        knockbackTarget = targetHealth;
                        knockbackDirection = normal;
                    }

                    TryApplyStatusEffects(targetHealth, currentWeapon.statusEffects, currentWeapon.statusEffectChance);
                    PlaySound(currentWeapon.hitSound);
                }
            }
        }

        if (knockbackTarget != null && !knockbackTarget.IsDead && accumulatedKnockbackDistance > 0f)
        {
            float finalKnockbackDistance = Mathf.Min(accumulatedKnockbackDistance, knockbackDistance);
            ApplyKnockback(knockbackTarget, knockbackDirection, finalKnockbackDistance, currentWeapon.knockbackDuration);
        }
    }

    void ApplyRangedHitDamage(PlayerHealth playerHealth, float damage, Vector3 hitPoint, Vector3 hitNormal, bool isKnockback)
    {
        if (playerHealth == null)
        {
            return;
        }

        playerHealth.TakeDamage(damage, hitPoint, hitNormal, false, false, isKnockback);
    }

    bool TryGetKnockbackDistance(float chance, float power, float maxDistance, out float distance)
    {
        distance = 0f;
        if (!RollPercent(chance))
        {
            return false;
        }

        distance = Mathf.Clamp(power / 100f * maxDistance, 0f, maxDistance);
        return distance > 0f;
    }

    Vector3 GetKnockbackDirection(Transform playerTransform)
    {
        Vector3 direction = playerTransform != null
            ? playerTransform.position - transform.position
            : transform.forward;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
        {
            direction = transform.forward;
        }

        return direction.normalized;
    }

    void ApplyKnockback(PlayerHealth playerHealth, Vector3 direction, float distance, float duration)
    {
        if (playerHealth == null || distance <= 0f)
        {
            return;
        }

        PlayerMovement movement = playerHealth.GetComponent<PlayerMovement>();
        if (movement == null)
        {
            movement = playerHealth.GetComponentInParent<PlayerMovement>();
        }

        movement?.ApplyKnockback(direction, distance, duration);
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

    void DealRangedBossMeleeDamage(Transform target)
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
        float knockbackDistance = Mathf.Max(0f, currentWeapon.rangedMeleeKnockbackDistance);
        bool isKnockback = knockbackDistance > 0f;
        playerHealth.TakeDamage(
            GetModifiedStat(StatusEffectStat.EnemyRangedCrossbowMeleeDamage, currentWeapon.crossbowMeleeDamage),
            hitPoint,
            hitNormal,
            false,
            false,
            isKnockback);
        if (isKnockback && !playerHealth.LastDamageWasBlocked && !playerHealth.IsDead)
        {
            ApplyKnockback(playerHealth, hitNormal, knockbackDistance, currentWeapon.rangedMeleeKnockbackDuration);
        }

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

    Vector3 GetSpreadDirection(Vector3 baseDirection, int pelletIndex, int pelletCount, float spreadAngle, float spreadRotation)
    {
        if (spreadAngle <= 0f || pelletCount <= 1)
        {
            return baseDirection;
        }

        Vector3 forward = baseDirection.sqrMagnitude > 0.0001f ? baseDirection.normalized : transform.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        if (right.sqrMagnitude <= 0.0001f)
        {
            right = transform.right;
        }

        right.Normalize();
        Vector3 up = Vector3.Cross(forward, right).normalized;
        float spreadRadius = Mathf.Tan(spreadAngle * Mathf.Deg2Rad);
        Vector2 circularOffset = GetCircularSpreadOffset(pelletIndex, pelletCount, spreadRotation) * spreadRadius;
        Vector3 direction = forward
            + right * circularOffset.x
            + up * circularOffset.y;

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

        if (TryPlayEnemyDataShootAnimation())
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

    bool TryPlayEnemyDataShootAnimation()
    {
        return TryPlayEnemyDataRangedStates(RangedAnimationSlot.Attack);
    }

    void PlayReloadAnimation()
    {
        if (currentWeapon == null)
        {
            return;
        }

        if (TryPlayEnemyDataRangedStates(RangedAnimationSlot.Reload))
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

    void PlayRandomRangedBossMeleeAnimation()
    {
        if (TryPlayEnemyDataRangedStates(RangedAnimationSlot.SpecialAttack))
        {
            return;
        }

        if (currentWeapon == null)
        {
            return;
        }

        if (currentWeapon.weaponKind == EnemyRangedWeaponKind.Shotgun && TryPlayRandomState(currentWeapon.shotgunMeleeStateNames))
        {
            return;
        }

        PlayRandomState(currentWeapon.crossbowMeleeStateNames);
    }

    enum RangedAnimationSlot
    {
        Attack,
        Reload,
        SpecialAttack
    }

    bool TryPlayEnemyDataRangedStates(RangedAnimationSlot slot)
    {
        EnemyAnimationLayerData[] layers = enemy != null && enemy.enemyData != null
            ? enemy.enemyData.animationLayers
            : null;
        if (layers == null)
        {
            return false;
        }

        string weaponLayerName = GetRangeLayerName();
        for (int i = 0; i < layers.Length; i++)
        {
            EnemyAnimationLayerData layer = layers[i];
            if (layer == null || layer.actionType != EnemyAnimationActionType.Ranged || layer.layerName != weaponLayerName)
            {
                continue;
            }

            string[] stateNames = slot switch
            {
                RangedAnimationSlot.Reload => layer.reloadStateNames,
                RangedAnimationSlot.SpecialAttack => layer.specialAttackStateNames,
                _ => layer.attackStateNames
            };
            if (stateNames == null || stateNames.Length == 0)
            {
                continue;
            }

            return TryPlayRandomState(stateNames);
        }

        return false;
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
        TryPlayRandomState(stateNames);
    }

    bool TryPlayRandomState(string[] stateNames)
    {
        if (stateNames == null || stateNames.Length == 0)
        {
            return false;
        }

        int startIndex = Random.Range(0, stateNames.Length);
        for (int i = 0; i < stateNames.Length; i++)
        {
            string stateName = stateNames[(startIndex + i) % stateNames.Length];
            if (PlayState(stateName))
            {
                return true;
            }
        }

        return false;
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

    void BeginWeaponAction()
    {
        DeactivateAimPose();
        enemyAI?.SetLocomotionSuppressed(true);

        int layerIndex = GetRangeLayerIndex();
        weaponActionLayerClaimed = EnemyAnimationLayers.TryClaimLayer(
            animator,
            layerIndex,
            WeaponActionLayerOwner,
            AnimationLayerPriority.WeaponAction);

        SetRangeLayerWeight(1f);
    }

    void EndWeaponAction()
    {
        ReleaseWeaponActionLayer();
        enemyAI?.SetLocomotionSuppressed(false);

        if (wantsAimPose)
        {
            ActivateAimPose();
        }
        else
        {
            FadeRangeLayerWeight(0f);
        }
    }

    void ReleaseWeaponActionLayer()
    {
        if (!weaponActionLayerClaimed)
        {
            return;
        }

        EnemyAnimationLayers.ReleaseLayer(animator, GetRangeLayerIndex(), WeaponActionLayerOwner);
        weaponActionLayerClaimed = false;
    }

    void ActivateAimPose()
    {
        if (animator == null || currentWeapon == null)
        {
            return;
        }

        int layerIndex = GetAimLayerIndex();
        if (layerIndex <= 0)
        {
            return;
        }

        string stateName = GetAimStateName();
        int stateHash = GetStateHash(layerIndex, GetAimLayerName(), stateName);
        if (stateHash == 0)
        {
            return;
        }

        if (!EnemyAnimationLayers.TryClaimLayer(animator, layerIndex, AimLayerOwner, AnimationLayerPriority.Aim))
        {
            return;
        }

        StopRangeFade();
        if (!aimPoseActive || activeAimLayerIndex != layerIndex || activeAimStateHash != stateHash)
        {
            animator.CrossFadeInFixedTime(
                stateHash,
                Mathf.Max(0f, GetModifiedStat(StatusEffectStat.EnemyRangedAnimationCrossFade, currentWeapon.animationCrossFade)),
                layerIndex);
        }

        animator.SetLayerWeight(layerIndex, 1f);
        activeAimLayerIndex = layerIndex;
        activeAimStateHash = stateHash;
        aimPoseActive = true;
    }

    void DeactivateAimPose()
    {
        if (!aimPoseActive && activeAimLayerIndex < 0)
        {
            return;
        }

        int layerIndex = activeAimLayerIndex >= 0 ? activeAimLayerIndex : GetAimLayerIndex();
        EnemyAnimationLayers.ReleaseLayer(animator, layerIndex, AimLayerOwner);
        activeAimLayerIndex = -1;
        activeAimStateHash = 0;
        aimPoseActive = false;
    }

    int GetStateHash(string stateName)
    {
        int layerIndex = GetRangeLayerIndex();
        return GetStateHash(layerIndex, GetRangeLayerName(), stateName);
    }

    int GetStateHash(int layerIndex, string layerName, string stateName)
    {
        if (!stateHashes.TryGetValue(stateName, out int shortHash))
        {
            shortHash = Animator.StringToHash(stateName);
            stateHashes[stateName] = shortHash;
        }

        if (animator.HasState(layerIndex, shortHash))
        {
            return shortHash;
        }

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

    int GetAimLayerIndex()
    {
        if (aimLayerIndex < 0)
        {
            aimLayerIndex = animator != null ? animator.GetLayerIndex(GetAimLayerName()) : -1;
        }

        return aimLayerIndex >= 0 ? aimLayerIndex : 0;
    }

    string GetAimLayerName()
    {
        if (currentWeapon == null)
        {
            return GetRangeLayerName();
        }

        return currentWeapon.weaponKind switch
        {
            EnemyRangedWeaponKind.Handgun => "Armed",
            EnemyRangedWeaponKind.DualPistol => "Armed",
            EnemyRangedWeaponKind.Crossbow => "2Hand-Crossbow",
            EnemyRangedWeaponKind.Shotgun => "2Hand-Shooting",
            _ => GetRangeLayerName()
        };
    }

    string GetAimStateName()
    {
        if (currentWeapon == null)
        {
            return string.Empty;
        }

        return currentWeapon.weaponKind switch
        {
            EnemyRangedWeaponKind.Handgun => "Armed-Idle-Pistol-R-Static",
            EnemyRangedWeaponKind.DualPistol => "Armed-Idle-Pistol-Dual-Static",
            EnemyRangedWeaponKind.Crossbow => "2Hand-Crossbow-Aiming-CM",
            EnemyRangedWeaponKind.Shotgun => "Shooting-Aiming-CM",
            _ => string.Empty
        };
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
        if (!isActiveAndEnabled)
        {
            animator.SetLayerWeight(layerIndex, targetWeight);
            rangeFadeTarget = -1f;
            return;
        }

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
