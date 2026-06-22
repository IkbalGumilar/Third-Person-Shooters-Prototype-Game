using System.Collections;
using System.Collections.Generic;
using Lean.Pool;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    private static readonly List<Enemy> activeEnemies = new List<Enemy>();

    public EnemyData enemyData;
    public bool destroyOnDeath = true;
    public bool despawnWithLeanPool;
    public bool createHealthBarIfMissing = true;
    public EnemyWorldHealthBar healthBar;
    [Range(0f, 1f)] public float regenDamageLossPercent = 0.1f;
    public float healthCatchUpSpeed = 10f;
    public bool usePercentHealthCatchUpSpeed = true;
    public float healthCatchUpSpeedPercent = 10f;
    public Animator animator;
    public string deadTriggerName = "Dead";
    public string deadStateName = "Dead";
    public string deadLayerName = "Base Layer";
    public float deadFadeDuration = 0.1f;
    public float deathDespawnDelay = 2f;

    [SerializeField] private List<float> physicalShieldStacks = new List<float>();
    [SerializeField] private float currentPhysicalShield;

    public float CurrentHealth => currentHealth;
    public float RegenHealth => regenHealth;
    public float MaxHealth => enemyData != null ? GetModifiedStat(StatusEffectStat.EnemyMaxHealth, enemyData.maxHealth) : 0f;
    public bool IsDead => isDead;
    public float CurrentPhysicalShield => currentPhysicalShield;
    public bool HasPhysicalShield => currentPhysicalShield > 0f;
    public static IReadOnlyList<Enemy> ActiveEnemies => activeEnemies;
    public EnemyMeleeWeaponController MeleeWeaponController
    {
        get
        {
            meleeWeaponController = meleeWeaponController != null ? meleeWeaponController : GetComponent<EnemyMeleeWeaponController>();
            return meleeWeaponController;
        }
    }
    public EnemyRangedWeaponController RangedWeaponController
    {
        get
        {
            rangedWeaponController = rangedWeaponController != null ? rangedWeaponController : GetComponent<EnemyRangedWeaponController>();
            return rangedWeaponController;
        }
    }
    public EnemyStatusEffectController StatusController
    {
        get
        {
            statusController = statusController != null ? statusController : GetComponent<EnemyStatusEffectController>();
            return statusController;
        }
    }

    private float currentHealth;
    private float regenHealth;
    private bool isDead;
    private AudioSource audioSource;
    private EnemyAI enemyAI;
    private EnemyMeleeWeaponController meleeWeaponController;
    private EnemyRangedWeaponController rangedWeaponController;
    private EnemyShieldController shieldController;
    private EnemyHitReactionController hitReactionController;
    private EnemyStatusEffectController statusController;
    private EnemyItemDropper itemDropper;
    private EnemyDeathRitual deathRitual;
    private Coroutine deathRoutine;
    private bool deathFinalized;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        enemyAI = GetComponent<EnemyAI>();
        meleeWeaponController = GetComponent<EnemyMeleeWeaponController>();
        rangedWeaponController = GetComponent<EnemyRangedWeaponController>();
        shieldController = GetComponent<EnemyShieldController>();
        EnsureHitReactionController();
        statusController = GetComponent<EnemyStatusEffectController>();
        itemDropper = GetComponent<EnemyItemDropper>();
        deathRitual = GetComponent<EnemyDeathRitual>();
        animator = animator != null ? animator : GetComponentInChildren<Animator>();
        AnimationEventReceiver.EnsureOn(animator);
        SetupHealthBar();
        ResetHealth();
    }

    void OnEnable()
    {
        RegisterActiveEnemy();
        SetupHealthBar();
        statusController = GetComponent<EnemyStatusEffectController>();
        itemDropper = GetComponent<EnemyItemDropper>();
        deathRitual = GetComponent<EnemyDeathRitual>();
        EnsureHitReactionController();
        animator = animator != null ? animator : GetComponentInChildren<Animator>();
        AnimationEventReceiver.EnsureOn(animator);
        ResetHealth();
    }

    void OnDisable()
    {
        activeEnemies.Remove(this);
    }

    void RegisterActiveEnemy()
    {
        if (!activeEnemies.Contains(this))
        {
            activeEnemies.Add(this);
        }
    }

    void Update()
    {
        if (!isDead && currentHealth < regenHealth)
        {
            ClampHealthToCurrentMax();
            currentHealth = Mathf.MoveTowards(currentHealth, regenHealth, GetHealthCatchUpSpeed() * Time.deltaTime);
            UpdateHealthBar();
        }
    }

    public void TakeDamage(float damage)
    {
        if (enemyData == null || damage <= 0f)
        {
            return;
        }

        if (isDead)
        {
            if (deathRitual != null && deathRitual.IsDeathRitualActive)
            {
                deathRitual.TakeRitualDamage(ApplyDamageReduction(damage));
            }

            return;
        }

        damage = ApplyDamageReduction(damage);
        if (damage <= 0f)
        {
            return;
        }

        if (HasPhysicalShield)
        {
            ConsumePhysicalShieldDamage(damage);
            UpdateHealthBar();
            PlaySound(enemyData.hitSound);
            ApplyShotAlert();
            NotifyDamagedAI();
            bool playedShieldBlockHit = shieldController != null && shieldController.NotifyBlockedHit();
            if (!playedShieldBlockHit)
            {
                PlayWeaponHitReaction();
            }
            if (!HasPhysicalShield)
            {
                shieldController?.NotifyPhysicalShieldBroken();
            }
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        regenHealth = Mathf.Max(currentHealth, regenHealth - damage * GetModifiedStat(StatusEffectStat.EnemyRegenDamageLossPercent, regenDamageLossPercent));
        UpdateHealthBar();
        PlayWeaponHitReaction();
        PlaySound(enemyData.hitSound);
        ApplyShotAlert();
        NotifyDamagedAI();

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    public void RestoreHealthForDeathRitual(float healthPercent)
    {
        float restoredHealth = MaxHealth * Mathf.Clamp01(healthPercent / 100f);
        currentHealth = restoredHealth;
        regenHealth = restoredHealth;
        UpdateHealthBar();
    }

    public bool TakeDeathRitualDamage(float damage)
    {
        if (!isDead || damage <= 0f)
        {
            return false;
        }

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        regenHealth = currentHealth;
        UpdateHealthBar();
        return currentHealth <= 0f;
    }

    public void ClearDeathRitualHealth()
    {
        currentHealth = 0f;
        regenHealth = 0f;
        UpdateHealthBar();
    }

    void EnsureHitReactionController()
    {
        if (hitReactionController == null)
        {
            hitReactionController = GetComponent<EnemyHitReactionController>();
        }

        if (hitReactionController == null)
        {
            hitReactionController = gameObject.AddComponent<EnemyHitReactionController>();
        }
    }

    void PlayWeaponHitReaction()
    {
        EnsureHitReactionController();
        hitReactionController?.PlayHitReaction();
    }

    public void AddPhysicalShield(float amount)
    {
        if (isDead || amount <= 0f)
        {
            return;
        }

        physicalShieldStacks.Add(amount);
        RecalculatePhysicalShield();
    }

    void ConsumePhysicalShieldDamage(float damage)
    {
        while (damage > 0f && physicalShieldStacks.Count > 0)
        {
            float stack = physicalShieldStacks[0];
            float consumed = Mathf.Min(stack, damage);
            stack -= consumed;
            damage -= consumed;

            if (stack <= 0f)
            {
                physicalShieldStacks.RemoveAt(0);
            }
            else
            {
                physicalShieldStacks[0] = stack;
            }
        }

        RecalculatePhysicalShield();
        if (!HasPhysicalShield)
        {
            StatusController?.RemovePhysicalShieldDepletedEffects();
        }
    }

    void RecalculatePhysicalShield()
    {
        currentPhysicalShield = 0f;
        for (int i = 0; i < physicalShieldStacks.Count; i++)
        {
            currentPhysicalShield += Mathf.Max(0f, physicalShieldStacks[i]);
        }
    }

    float ApplyDamageReduction(float damage)
    {
        float reductionPercent = Mathf.Clamp(GetModifiedStat(StatusEffectStat.EnemyDamageReductionPercent, 0f), 0f, 90f);
        return damage * (1f - reductionPercent / 100f);
    }

    float GetHealthCatchUpSpeed()
    {
        float flatSpeed = GetModifiedStat(StatusEffectStat.EnemyHealthCatchUpSpeed, healthCatchUpSpeed);
        float percentSpeed = GetModifiedStat(StatusEffectStat.EnemyHealthCatchUpSpeedPercent, healthCatchUpSpeedPercent);
        float scaledSpeed = MaxHealth * percentSpeed / 100f;
        if (usePercentHealthCatchUpSpeed && percentSpeed > 0f)
        {
            return Mathf.Max(0f, scaledSpeed);
        }

        return Mathf.Max(0f, flatSpeed + scaledSpeed);
    }

    public void Heal(float amount)
    {
        if (isDead || enemyData == null || amount <= 0f)
        {
            return;
        }

        regenHealth = Mathf.Min(MaxHealth, regenHealth + amount);
        UpdateHealthBar();
    }

    public void ResetHealth()
    {
        deathRoutine = null;
        deathFinalized = false;
        physicalShieldStacks.Clear();
        currentPhysicalShield = 0f;
        SetEnemyAIEnabled(true);

        if (enemyData == null)
        {
            currentHealth = 0f;
            regenHealth = 0f;
            isDead = false;
            SetMeleeWeaponControllerEnabled(true);
            SetRangedWeaponControllerEnabled(true);
            return;
        }

        currentHealth = MaxHealth;
        regenHealth = MaxHealth;
        isDead = false;
        SetMeleeWeaponControllerEnabled(true);
        SetRangedWeaponControllerEnabled(true);
        UpdateHealthBar();
    }

    void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        UpdateHealthBar();
        PlaySound(enemyData.deathSound);
        shieldController?.StopForDeathAnimation();
        enemyAI?.StopForDeathAnimation();
        hitReactionController?.StopHitReaction();
        CancelMeleeAttack();
        CancelRangedAttack();
        enemyAI?.SetLocomotionSuppressed(true);
        SetEnemyAIEnabled(false);
        SetMeleeWeaponControllerEnabled(false);
        SetRangedWeaponControllerEnabled(false);

        if (deathRitual != null && deathRitual.TryStartDeathRitual())
        {
            return;
        }

        CompleteDeathAfterRitual();
    }

    public void CompleteDeathAfterRitual()
    {
        if (!isDead || deathFinalized)
        {
            return;
        }

        deathFinalized = true;
        itemDropper?.DropLoot();
        PlayDeathAnimation();

        if (enemyData.deathEffect != null)
        {
            Instantiate(enemyData.deathEffect, transform.position, transform.rotation);
        }

        if (destroyOnDeath)
        {
            if (deathRoutine == null)
            {
                deathRoutine = StartCoroutine(DespawnAfterDeathDelay());
            }
        }
    }

    void PlayDeathAnimation()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        ClearAnimatorOverlayLayers();

        if (PlayDeathState())
        {
            animator.Update(0f);
            return;
        }

        if (animator != null && HasAnimatorTrigger(deadTriggerName))
        {
            animator.SetTrigger(deadTriggerName);
        }
    }

    void ClearAnimatorOverlayLayers()
    {
        if (animator == null)
        {
            return;
        }

        animator.speed = 1f;
        EnemyAnimationLayers.SetExclusiveLayer(animator, -1);
    }

    bool HasAnimatorTrigger(string parameterName)
    {
        if (animator == null || string.IsNullOrEmpty(parameterName))
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == parameterName)
            {
                return true;
            }
        }

        return false;
    }

    bool PlayDeathState()
    {
        if (animator == null)
        {
            return false;
        }

        if (TryPlayDeathState(deadLayerName, deadStateName))
        {
            return true;
        }

        if (TryPlayPreferredDeathState())
        {
            return true;
        }

        return TryPlayDeathState("Armed", "Armed-Death1") ||
               TryPlayDeathState("Unarmed", "Unarmed-Death1");
    }

    bool TryPlayPreferredDeathState()
    {
        if (shieldController != null && shieldController.enabled && shieldController.enableShieldBehavior &&
            HasPhysicalShield && TryPlayDeathState("Defense", "Shield-Death1"))
        {
            return true;
        }

        if (enemyData != null && enemyData.enemyType == EnemyType.Support &&
            TryPlayDeathState("2Hand-Staff", "Staff-Death1"))
        {
            return true;
        }

        EnemyRangedWeapon rangedWeapon = rangedWeaponController != null ? rangedWeaponController.CurrentWeapon : null;
        if (rangedWeapon != null)
        {
            if (rangedWeapon.weaponKind == EnemyRangedWeaponKind.Crossbow &&
                TryPlayDeathState("2Hand-Crossbow", "2Hand-Crossbow-Death1"))
            {
                return true;
            }

            if (rangedWeapon.weaponKind == EnemyRangedWeaponKind.Shotgun &&
                TryPlayDeathState("2Hand-Shooting", "Shooting-Death1"))
            {
                return true;
            }
        }

        EnemyMeleeWeapon meleeWeapon = meleeWeaponController != null ? meleeWeaponController.CurrentWeapon : null;
        if (meleeWeapon != null)
        {
            if (meleeWeapon.category == EnemyMeleeWeaponCategory.SmallAxe &&
                TryPlayDeathState("2Hand-Axe", "2Hand-Axe-Death1"))
            {
                return true;
            }

            if (meleeWeapon.category == EnemyMeleeWeaponCategory.GreatSword &&
                TryPlayDeathState("2Hand-Sword", "2Hand-Sword-Death1"))
            {
                return true;
            }

            if (meleeWeapon.category == EnemyMeleeWeaponCategory.Spear &&
                meleeWeapon.holdType == WeaponHoldType.TwoHand &&
                TryPlayDeathState("2Hand-Spear", "2Hand-Spear-Death1"))
            {
                return true;
            }
        }

        return false;
    }

    bool TryPlayDeathState(string layerName, string stateName)
    {
        if (string.IsNullOrEmpty(layerName) || string.IsNullOrEmpty(stateName))
        {
            return false;
        }

        int layerIndex = animator.GetLayerIndex(layerName);
        if (layerIndex < 0)
        {
            return false;
        }

        int fullPathHash = Animator.StringToHash($"{layerName}.{stateName}");
        int stateHash = animator.HasState(layerIndex, fullPathHash)
            ? fullPathHash
            : Animator.StringToHash(stateName);
        if (!animator.HasState(layerIndex, stateHash))
        {
            return false;
        }

        EnemyAnimationLayers.SetExclusiveLayer(animator, layerIndex);
        animator.Play(stateHash, layerIndex, 0f);
        return true;
    }

    void SetEnemyAIEnabled(bool enabled)
    {
        if (enemyAI == null)
        {
            enemyAI = GetComponent<EnemyAI>();
        }

        if (enemyAI != null && enemyAI.enabled != enabled)
        {
            enemyAI.enabled = enabled;
        }
    }

    IEnumerator DespawnAfterDeathDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, deathDespawnDelay));

        if (despawnWithLeanPool)
        {
            LeanPool.Despawn(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void SetMeleeWeaponControllerEnabled(bool enabled)
    {
        if (meleeWeaponController == null)
        {
            meleeWeaponController = GetComponent<EnemyMeleeWeaponController>();
        }

        if (meleeWeaponController != null)
        {
            meleeWeaponController.enabled = enabled;
        }
    }

    void SetRangedWeaponControllerEnabled(bool enabled)
    {
        if (rangedWeaponController == null)
        {
            rangedWeaponController = GetComponent<EnemyRangedWeaponController>();
        }

        if (rangedWeaponController != null)
        {
            rangedWeaponController.enabled = enabled;
        }
    }

    void CancelMeleeAttack()
    {
        if (meleeWeaponController == null)
        {
            meleeWeaponController = GetComponent<EnemyMeleeWeaponController>();
        }

        if (meleeWeaponController != null)
        {
            meleeWeaponController.CancelAttack();
        }
    }

    void CancelRangedAttack()
    {
        if (rangedWeaponController == null)
        {
            rangedWeaponController = GetComponent<EnemyRangedWeaponController>();
        }

        if (rangedWeaponController != null)
        {
            rangedWeaponController.CancelAttack();
        }
    }

    public void ApplyStun(float duration)
    {
        if (isDead || enemyData == null || !enemyData.canBeStunned || duration <= 0f)
        {
            return;
        }

        if (enemyAI == null)
        {
            enemyAI = GetComponent<EnemyAI>();
        }

        if (enemyAI != null)
        {
            enemyAI.ApplyStun(duration);
        }
    }

    public void ApplyKnockback(Vector3 direction, float distance, float duration)
    {
        if (isDead || enemyData == null || !enemyData.canBeKnockedBack || distance <= 0f)
        {
            return;
        }

        if (enemyAI == null)
        {
            enemyAI = GetComponent<EnemyAI>();
        }

        if (enemyAI != null)
        {
            enemyAI.ApplyKnockback(direction, distance, duration);
        }
    }

    void ApplyShotAlert()
    {
        if (enemyAI == null)
        {
            enemyAI = GetComponent<EnemyAI>();
        }

        if (enemyAI != null)
        {
            enemyAI.ApplyShotAlert(true);
        }
    }

    void NotifyDamagedAI()
    {
        if (enemyAI == null)
        {
            enemyAI = GetComponent<EnemyAI>();
        }

        if (enemyAI != null)
        {
            enemyAI.NotifyDamaged();
        }
    }

    void ClampHealthToCurrentMax()
    {
        float maxHealth = MaxHealth;
        if (maxHealth <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Min(currentHealth, maxHealth);
        regenHealth = Mathf.Min(regenHealth, maxHealth);
    }

    float GetModifiedStat(StatusEffectStat stat, float baseValue)
    {
        statusController = statusController != null ? statusController : GetComponent<EnemyStatusEffectController>();
        return statusController != null ? statusController.ModifyStat(stat, baseValue) : baseValue;
    }

    void SetupHealthBar()
    {
        if (healthBar == null)
        {
            healthBar = GetComponent<EnemyWorldHealthBar>();
        }

        if (healthBar == null && createHealthBarIfMissing)
        {
            healthBar = gameObject.AddComponent<EnemyWorldHealthBar>();
        }

        if (healthBar != null)
        {
            healthBar.SetEnemy(this);
        }
    }

    void UpdateHealthBar()
    {
        if (healthBar != null)
        {
            healthBar.SetHealth(currentHealth, MaxHealth, regenHealth);
        }
    }

    void PlaySound(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        if (audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
        else
        {
            AudioSource.PlayClipAtPoint(clip, transform.position);
        }
    }
}
