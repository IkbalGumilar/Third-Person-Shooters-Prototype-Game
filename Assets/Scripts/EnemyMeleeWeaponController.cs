using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyMeleeWeaponController : MonoBehaviour
{
    public EnemyMeleeWeapon startingWeapon;
    public Transform weaponSocket;
    public GameObject existingWeaponObject;
    public Animator animator;
    public string attackLayerName = "Attack";
    public bool autoSelectAttackLayer = true;
    public string[] attackStateMachineNames = { "Tombak", "Sword", "Gada", "Kapak", "Dagger", "Great Sword", "Unarmed" };
    public string fallbackAttackStateName = "Melee Attack";
    public float attackLayerFadeOut = 0.12f;

    private EnemyMeleeWeapon currentWeapon;
    private GameObject equippedWeaponObject;
    private bool equippedObjectWasInstantiated;
    private float nextAttackTime;
    private int attackLayerIndex = -1;
    private Coroutine attackRoutine;
    private Coroutine attackFadeRoutine;
    private float attackFadeTarget = -1f;
    private AudioSource audioSource;
    private Enemy enemy;
    private EnemyAI enemyAI;
    private EnemyStatusEffectController statusController;
    private readonly Dictionary<string, int> stateHashes = new Dictionary<string, int>();

    public EnemyMeleeWeapon CurrentWeapon => currentWeapon;
    public bool IsAttacking => attackRoutine != null;
    public float AttackRange => currentWeapon != null ? GetModifiedStat(StatusEffectStat.EnemyMeleeAttackRange, currentWeapon.attackRange) : 1.5f;
    public float EffectiveAttackRange => currentWeapon != null
        ? GetModifiedStat(StatusEffectStat.EnemyMeleeAttackRange, currentWeapon.attackRange)
            + GetModifiedStat(StatusEffectStat.EnemyMeleeHitRadius, currentWeapon.hitRadius)
        : 1.5f;

    void Awake()
    {
        enemy = GetComponent<Enemy>();
        enemyAI = GetComponent<EnemyAI>();
        statusController = GetComponent<EnemyStatusEffectController>();
        animator = animator != null ? animator : GetComponentInChildren<Animator>();
        AnimationEventReceiver.EnsureOn(animator);
        ResolveAttackLayer();
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

    public void EquipWeapon(EnemyMeleeWeapon weapon)
    {
        if (weapon == null)
        {
            return;
        }

        currentWeapon = weapon;
        attackLayerIndex = -1;
        stateHashes.Clear();
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

        nextAttackTime = Time.time + Mathf.Max(0.01f, GetModifiedStat(StatusEffectStat.EnemyMeleeAttackCooldown, currentWeapon.attackCooldown));
        attackRoutine = StartCoroutine(AttackRoutine(target));
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

        FadeAttackLayerWeight(0f);
    }

    // A hit reaction uses the same full-body weapon layer as this attack.
    // Stop every pending writer before another controller takes that layer.
    public void InterruptForHitReaction()
    {
        bool wasAttacking = attackRoutine != null;
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        StopAttackFade();
        SetAttackLayerWeight(0f);
        if (wasAttacking)
        {
            enemyAI?.SetLocomotionSuppressed(false);
        }
    }

    IEnumerator AttackRoutine(Transform target)
    {
        float cooldown = currentWeapon != null ? Mathf.Max(0.01f, GetModifiedStat(StatusEffectStat.EnemyMeleeAttackCooldown, currentWeapon.attackCooldown)) : 0.01f;
        StopAttackFade();
        enemyAI?.SetLocomotionSuppressed(true);
        SetAttackLayerWeight(1f);
        PlayRandomAttackAnimation();
        PlaySound(currentWeapon.attackSound);

        float delay = Mathf.Max(0f, GetModifiedStat(StatusEffectStat.EnemyMeleeDamageDelay, currentWeapon.damageDelay));
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        DealDamage(target);

        float remainingAttackTime = Mathf.Max(0f, cooldown - delay);
        if (remainingAttackTime > 0f)
        {
            yield return new WaitForSeconds(remainingAttackTime);
        }

        FadeAttackLayerWeight(0f);
        enemyAI?.SetLocomotionSuppressed(false);
        attackRoutine = null;
    }

    void DealDamage(Transform target)
    {
        if (target == null || currentWeapon == null)
        {
            return;
        }

        float attackRange = GetModifiedStat(StatusEffectStat.EnemyMeleeAttackRange, currentWeapon.attackRange);
        float hitRadius = GetModifiedStat(StatusEffectStat.EnemyMeleeHitRadius, currentWeapon.hitRadius);
        float maxHitDistance = attackRange + hitRadius;
        if ((transform.position - target.position).sqrMagnitude > maxHitDistance * maxHitDistance)
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
        Vector3 hitPoint = playerHealth.transform.position - hitNormal * Mathf.Max(0.05f, hitRadius * 0.5f);

        bool isCritical;
        float damage = ApplyCriticalDamage(
            GetModifiedStat(StatusEffectStat.EnemyMeleeDamage, currentWeapon.damage),
            currentWeapon.criticalChance,
            currentWeapon.criticalDamagePercent,
            out isCritical
        );
        bool isKnockback = TryGetKnockbackDistance(
            currentWeapon.knockbackChance,
            currentWeapon.knockbackPower,
            currentWeapon.maxKnockbackDistance,
            out float knockbackDistance
        );
        bool isHeavy = RollPercent(currentWeapon.heavyChance);

        playerHealth.TakeDamage(damage, hitPoint, hitNormal, isHeavy, isCritical, isKnockback);
        if (isKnockback && !playerHealth.LastDamageWasBlocked && !playerHealth.IsDead)
        {
            ApplyKnockback(playerHealth, hitNormal, knockbackDistance, currentWeapon.knockbackDuration);
        }

        TryApplyStatusEffects(playerHealth, currentWeapon);
        PlaySound(currentWeapon.hitSound);
    }

    float ApplyCriticalDamage(float damage, float criticalChance, float criticalDamagePercent, out bool isCritical)
    {
        isCritical = RollPercent(criticalChance);
        if (!isCritical)
        {
            return damage;
        }

        return damage + damage * Mathf.Max(0f, criticalDamagePercent) / 100f;
    }

    bool TryGetKnockbackDistance(float chance, float power, float maxDistance, out float distance)
    {
        distance = 0f;
        if (!RollPercent(chance))
        {
            return false;
        }

        distance = Mathf.Clamp(power / 100f * maxDistance, 0f, maxDistance);
        if (distance <= 0f)
        {
            return false;
        }

        return true;
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

        if (movement != null)
        {
            movement.ApplyKnockback(direction, distance, duration);
        }
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

    void TryApplyStatusEffects(PlayerHealth playerHealth, EnemyMeleeWeapon weapon)
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

        if (weapon == null || weapon.statusEffects == null || weapon.statusEffects.Length == 0)
        {
            return;
        }

        if (Random.value > Mathf.Clamp01(weapon.statusEffectChance / 100f))
        {
            return;
        }

        for (int i = 0; i < weapon.statusEffects.Length; i++)
        {
            if (weapon.statusEffects[i] != null)
            {
                statusController.AddEffect(weapon.statusEffects[i]);
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

    void PlayRandomAttackAnimation()
    {
        if (animator == null)
        {
            return;
        }

        int stateHash = GetRandomAttackStateHash();
        if (stateHash == 0)
        {
            return;
        }

        animator.CrossFadeInFixedTime(stateHash, Mathf.Max(0f, GetModifiedStat(StatusEffectStat.EnemyMeleeAttackCrossFade, currentWeapon.attackCrossFade)), GetAttackLayerIndex());
    }

    int GetRandomAttackStateHash()
    {
        if (currentWeapon != null && currentWeapon.attackStateNames != null && currentWeapon.attackStateNames.Length > 0)
        {
            int startIndex = Random.Range(0, currentWeapon.attackStateNames.Length);
            for (int i = 0; i < currentWeapon.attackStateNames.Length; i++)
            {
                string stateName = currentWeapon.attackStateNames[(startIndex + i) % currentWeapon.attackStateNames.Length];
                int stateHash = GetStateHash(stateName);
                if (stateHash != 0)
                {
                    return stateHash;
                }
            }
        }

        return GetStateHash(fallbackAttackStateName);
    }

    int GetStateHash(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
        {
            return 0;
        }

        int layerIndex = GetAttackLayerIndex();
        if (!stateHashes.TryGetValue(stateName, out int shortHash))
        {
            shortHash = Animator.StringToHash(stateName);
            stateHashes[stateName] = shortHash;
        }

        if (animator.HasState(layerIndex, shortHash))
        {
            return shortHash;
        }

        string attackFullPath = $"{GetAttackLayerName()}.{stateName}";
        if (!stateHashes.TryGetValue(attackFullPath, out int attackFullPathHash))
        {
            attackFullPathHash = Animator.StringToHash(attackFullPath);
            stateHashes[attackFullPath] = attackFullPathHash;
        }

        if (animator.HasState(layerIndex, attackFullPathHash))
        {
            return attackFullPathHash;
        }

        if (attackStateMachineNames != null)
        {
            for (int i = 0; i < attackStateMachineNames.Length; i++)
            {
                string stateMachineName = attackStateMachineNames[i];
                if (string.IsNullOrEmpty(stateMachineName))
                {
                    continue;
                }

                string attackMachinePath = $"{GetAttackLayerName()}.{stateMachineName}.{stateName}";
                if (!stateHashes.TryGetValue(attackMachinePath, out int attackMachinePathHash))
                {
                    attackMachinePathHash = Animator.StringToHash(attackMachinePath);
                    stateHashes[attackMachinePath] = attackMachinePathHash;
                }

                if (animator.HasState(layerIndex, attackMachinePathHash))
                {
                    return attackMachinePathHash;
                }

                string machinePath = $"{stateMachineName}.{stateName}";
                if (!stateHashes.TryGetValue(machinePath, out int machinePathHash))
                {
                    machinePathHash = Animator.StringToHash(machinePath);
                    stateHashes[machinePath] = machinePathHash;
                }

                if (animator.HasState(layerIndex, machinePathHash))
                {
                    return machinePathHash;
                }
            }
        }

        string baseFullPath = $"Base Layer.{stateName}";
        if (!stateHashes.TryGetValue(baseFullPath, out int baseFullPathHash))
        {
            baseFullPathHash = Animator.StringToHash(baseFullPath);
            stateHashes[baseFullPath] = baseFullPathHash;
        }

        if (animator.HasState(layerIndex, baseFullPathHash))
        {
            return baseFullPathHash;
        }

        return 0;
    }

    void ResolveAttackLayer()
    {
        attackLayerIndex = animator != null ? animator.GetLayerIndex(GetAttackLayerName()) : -1;
    }

    string GetAttackLayerName()
    {
        if (!autoSelectAttackLayer || currentWeapon == null)
        {
            return attackLayerName;
        }

        return currentWeapon.category switch
        {
            EnemyMeleeWeaponCategory.Sword => "1Hand-Sword",
            EnemyMeleeWeaponCategory.Dagger => "1Hand-Dagger",
            EnemyMeleeWeaponCategory.Mace => "1Hand-Mace",
            EnemyMeleeWeaponCategory.SmallAxe => "2Hand-Axe",
            EnemyMeleeWeaponCategory.GreatSword => "2Hand-Sword",
            EnemyMeleeWeaponCategory.Spear => currentWeapon.holdType == WeaponHoldType.OneHand ? "1Hand-Spear" : "2Hand-Spear",
            _ => attackLayerName
        };
    }

    int GetAttackLayerIndex()
    {
        if (attackLayerIndex < 0)
        {
            ResolveAttackLayer();
        }

        return attackLayerIndex >= 0 ? attackLayerIndex : 0;
    }

    void SetAttackLayerWeight(float weight)
    {
        if (animator == null)
        {
            return;
        }

        int layerIndex = GetAttackLayerIndex();
        if (layerIndex > 0)
        {
            animator.SetLayerWeight(layerIndex, weight);
        }
    }

    void FadeAttackLayerWeight(float targetWeight)
    {
        int layerIndex = GetAttackLayerIndex();
        if (layerIndex <= 0 || animator == null)
        {
            return;
        }

        float currentWeight = animator.GetLayerWeight(layerIndex);
        if (Mathf.Abs(currentWeight - targetWeight) <= 0.001f)
        {
            return;
        }

        if (attackFadeRoutine != null && Mathf.Approximately(attackFadeTarget, targetWeight))
        {
            return;
        }

        StopAttackFade();
        attackFadeTarget = targetWeight;
        attackFadeRoutine = StartCoroutine(FadeAnimatorLayerWeight(layerIndex, targetWeight, attackLayerFadeOut));
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

    void StopAttackFade()
    {
        if (attackFadeRoutine == null)
        {
            return;
        }

        StopCoroutine(attackFadeRoutine);
        attackFadeRoutine = null;
        attackFadeTarget = -1f;
    }

    GameObject ResolveWeaponObject(EnemyMeleeWeapon weapon)
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

    void SetupWeaponTransform(GameObject weaponObject, EnemyMeleeWeapon weapon)
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
