using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyShieldController : MonoBehaviour
{
    static Transform cachedPlayerTarget;

    public bool enableShieldBehavior = true;
    public Transform playerTarget;
    public LayerMask enemyMask = ~0;
    public float detectionRange = 10f;
    public float loseTargetRange = 14f;
    public float rangedAllySearchRadius = 12f;
    public float rangedAllySearchInterval = 0.35f;
    public float holdDistanceFromPlayer = 1.15f;
    public float holdPositionRefreshInterval = 0.2f;
    public float blockMoveSpeed = 3f;
    public float soloKnockbackRange = 1.35f;
    public float soloKnockbackCooldown = 1.8f;
    public float soloKnockbackDamage = 6f;
    [Range(0f, 100f)] public float soloKnockbackChance = 100f;
    public float soloKnockbackPower = 100f;
    public float soloKnockbackMaxDistance = 2f;
    public float soloKnockbackDuration = 0.3f;
    public float soloKnockbackDamageDelay = 0.35f;
    [Range(0f, 100f)] public float synchronizedDamageBonusMaxPercent = 50f;

    [Header("Frontline Attack")]
    public bool useEnemyDataAttackStats = true;

    [Header("Defense Animation")]
    public string defenseLayerName = "Defense";
    public string blockStateName = "Shield-Block";
    public string blockWalkStateName = "Shield-Walk-Block";
    public string blockRunStateName = "Shield-Run-Forward-Block";
    public string[] blockHitStateNames = { "Shield-Block-GetHit1", "Shield-Block-GetHit2" };
    public float blockHitDuration = 0.3f;
    public string knockbackStateName = "Shield-Attack1";
    public string shieldBuffStateName = "Shield-Boost1";
    public float shieldBuffAnimationDuration = 0.8f;
    public float shieldBuffApplyDelay = 0.25f;
    public string shieldBreakKnockbackStateName = "Shield-Knockback-Back1";
    public float shieldBreakKnockbackDistance = 1f;
    public float shieldBreakKnockbackDuration = 0.2f;
    public float shieldBreakKnockdownDuration = 0.5f;
    public float shieldBreakGetupDuration = 0.5f;
    public float shieldBreakGetupSpeed = 3f;
    public float defenseCrossFade = 0.08f;
    public float defenseLayerFadeOut = 0.12f;

    [Header("Physical Shield Buff")]
    public float shieldBuffRadius = 3f;
    [Range(0f, 500f)] public float selfShieldPercent = 100f;
    [Range(0f, 500f)] public float selfShieldBoostPercent = 20f;
    [Range(0f, 500f)] public float allyShieldPercent = 20f;
    public float shieldBuffInterval = 30f;
    public StatusEffectData shieldStatusEffect;

    [Header("After Shield Break")]
    public EnemyMeleeWeapon meleeWeaponAfterShieldBreak;
    public EnemyRangedWeapon rangedWeaponAfterShieldBreak;

    Enemy enemy;
    EnemyAI enemyAI;
    EnemyMeleeWeaponController meleeWeaponController;
    EnemyRangedWeaponController rangedWeaponController;
    Animator animator;
    NavMeshAgent agent;
    PlayerHealth playerHealth;
    float nextDestinationRefreshTime;
    float nextKnockbackTime;
    float nextShieldBuffTime;
    int defenseLayerIndex = -1;
    string activeDefenseStateName;
    Coroutine knockbackRoutine;
    Coroutine defenseFadeRoutine;
    Enemy cachedRangedAlly;
    bool locomotionSuppressed;
    float nextRangedAllySearchTime;
    float blockHitUntilTime;
    float shieldBuffUntilTime;
    bool ownShieldInitialized;
    bool ownShieldBroken;
    bool shieldExitHandled;
    Coroutine shieldBuffRoutine;
    Coroutine shieldBreakRoutine;
    bool synchronizedAttackQueued;
    Vector3 lastAgentDestination;
    bool hasAgentDestination;
    readonly Collider[] hits = new Collider[64];
    readonly HashSet<Enemy> buffedEnemies = new HashSet<Enemy>();

    void Awake()
    {
        enemy = GetComponent<Enemy>();
        enemyAI = GetComponent<EnemyAI>();
        meleeWeaponController = GetComponent<EnemyMeleeWeaponController>();
        rangedWeaponController = GetComponent<EnemyRangedWeaponController>();
        animator = GetComponentInChildren<Animator>();
        if (blockStateName == "Block")
        {
            blockStateName = "Shield-Block";
        }
        if (knockbackStateName == "Knocback")
        {
            knockbackStateName = "Shield-Attack1";
        }
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            agent = gameObject.AddComponent<NavMeshAgent>();
        }
    }

    void OnEnable()
    {
        locomotionSuppressed = false;
        blockHitUntilTime = 0f;
        shieldBuffUntilTime = 0f;
        ownShieldInitialized = false;
        ownShieldBroken = false;
        shieldExitHandled = false;
        shieldBuffRoutine = null;
        shieldBreakRoutine = null;
        synchronizedAttackQueued = false;
        nextDestinationRefreshTime = 0f;
        nextKnockbackTime = 0f;
        nextShieldBuffTime = Time.time + shieldBuffInterval;
        nextRangedAllySearchTime = 0f;
        cachedRangedAlly = null;
        hasAgentDestination = false;
        FindPlayer();
    }

    void Update()
    {
        EnsureOwnPhysicalShield();
        if (ownShieldBroken)
        {
            if (shieldBreakRoutine == null)
            {
                ExitShieldBehavior();
            }
            return;
        }

        if (enemy == null || enemy.IsDead)
        {
            return;
        }

        if (!enableShieldBehavior)
        {
            SetEnemyAIEnabled(true);
            return;
        }

        if (playerTarget == null)
        {
            FindPlayer();
        }

        if (playerTarget == null)
        {
            SetEnemyAIEnabled(true);
            return;
        }

        float distanceToPlayerSqr = (transform.position - playerTarget.position).sqrMagnitude;
        if (distanceToPlayerSqr > loseTargetRange * loseTargetRange)
        {
            SetEnemyAIEnabled(true);
            return;
        }

        if (distanceToPlayerSqr > detectionRange * detectionRange && !IsShieldCombatActive())
        {
            SetEnemyAIEnabled(true);
            return;
        }

        SetEnemyAIEnabled(false);
        TryApplyShieldBuff();

        if (meleeWeaponController != null && meleeWeaponController.IsAttacking)
        {
            StopMoving();
            RotateToward(playerTarget.position - transform.position);
            UpdateAnimator();
            return;
        }

        Enemy protectedAlly = FindProtectedAlly();
        if (protectedAlly != null)
        {
            HoldPlayerForAlly(protectedAlly, distanceToPlayerSqr);
        }
        else
        {
            SoloKnockbackBehavior(distanceToPlayerSqr);
        }

        UpdateAnimator();
    }

    void UpdateAnimator()
    {
        if (animator == null)
        {
            return;
        }

        float speed = 0f;
        bool isMoving = agent != null && agent.enabled && agent.isOnNavMesh && agent.velocity.sqrMagnitude > 0.01f;
        if (isMoving)
        {
            speed = blockMoveSpeed;
        }

        animator.SetFloat("Speed", speed, 0.12f, Time.deltaTime);
        animator.SetBool("IsChasing", true);
    }

    bool IsShieldCombatActive()
    {
        return enemyAI != null && (enemyAI.currentState == EnemyAIState.Chase || enemyAI.currentState == EnemyAIState.Attack);
    }

    void HoldPlayerForAlly(Enemy ally, float distanceToPlayerSqr)
    {
        if (ally == null || playerTarget == null)
        {
            return;
        }

        float attackRange = GetShieldAttackRange();
        if (distanceToPlayerSqr <= attackRange * attackRange)
        {
            StopMoving();
            RotateToward(playerTarget.position - transform.position);
            TryShieldAttack();
            return;
        }

        // The shield goes between the player and an ally behind it, rather than
        // merely moving toward its current direction.
        Vector3 fromPlayerToAlly = ally.transform.position - playerTarget.position;
        fromPlayerToAlly.y = 0f;
        if (fromPlayerToAlly.sqrMagnitude < 0.001f)
        {
            fromPlayerToAlly = transform.position - playerTarget.position;
            fromPlayerToAlly.y = 0f;
        }

        if (fromPlayerToAlly.sqrMagnitude < 0.001f)
        {
            fromPlayerToAlly = -transform.forward;
        }

        Vector3 destination = playerTarget.position + fromPlayerToAlly.normalized * holdDistanceFromPlayer;
        MoveTo(destination, 0.15f);
        RotateToward(playerTarget.position - transform.position);
        PlayBlockingMovementState(destination, 0.15f, blockWalkStateName);
    }

    void SoloKnockbackBehavior(float distanceToPlayerSqr)
    {
        if (playerTarget == null)
        {
            return;
        }

        float attackRange = GetShieldAttackRange();
        if (distanceToPlayerSqr > attackRange * attackRange)
        {
            float stopDistance = Mathf.Min(0.35f, attackRange * 0.5f);
            MoveTo(playerTarget.position, stopDistance);
            RotateToward(playerTarget.position - transform.position);
            PlayBlockingMovementState(playerTarget.position, stopDistance, blockRunStateName);
            return;
        }

        StopMoving();
        RotateToward(playerTarget.position - transform.position);

        TryShieldAttack();
    }

    float GetShieldAttackRange()
    {
        float weaponRange = meleeWeaponController != null && meleeWeaponController.CurrentWeapon != null
            ? meleeWeaponController.EffectiveAttackRange
            : 0f;
        float unarmedRange = enemy != null && enemy.enemyData != null
            ? enemy.enemyData.attackRange + enemy.enemyData.attackHitRadius
            : 0f;
        return Mathf.Max(soloKnockbackRange, weaponRange, unarmedRange);
    }

    void TryShieldAttack()
    {
        if (playerTarget == null || Time.time < blockHitUntilTime || Time.time < shieldBuffUntilTime)
        {
            return;
        }

        if (meleeWeaponController != null && meleeWeaponController.CurrentWeapon != null)
        {
            if (meleeWeaponController.TryAttack(playerTarget))
            {
                SetDefenseLayerWeightImmediate(0f);
            }
            return;
        }

        float distanceToPlayerSqr = (transform.position - playerTarget.position).sqrMagnitude;
        float attackRange = GetShieldAttackRange();
        if (distanceToPlayerSqr > attackRange * attackRange)
        {
            return;
        }

        PlayerHealth health = GetPlayerHealth();
        if (health == null)
        {
            return;
        }

        ShieldAttackCoordinator.RequestAttack(this, health);
    }

    internal bool TryReserveSynchronizedAttack(PlayerHealth target)
    {
        if (synchronizedAttackQueued || target == null || enemy == null || enemy.IsDead || ownShieldBroken ||
            knockbackRoutine != null || Time.time < nextKnockbackTime || Time.time < shieldBuffUntilTime ||
            GetPlayerHealth() != target)
        {
            return false;
        }

        float attackRange = GetShieldAttackRange();
        if ((transform.position - target.transform.position).sqrMagnitude > attackRange * attackRange)
        {
            return false;
        }

        synchronizedAttackQueued = true;
        return true;
    }

    internal void ReleaseSynchronizedAttackReservation()
    {
        synchronizedAttackQueued = false;
    }

    internal bool StartSynchronizedAttack(PlayerHealth target)
    {
        synchronizedAttackQueued = false;
        if (target == null || enemy == null || enemy.IsDead || ownShieldBroken || knockbackRoutine != null ||
            Time.time < shieldBuffUntilTime || GetPlayerHealth() != target)
        {
            return false;
        }

        float attackCooldown = useEnemyDataAttackStats && enemy.enemyData != null
            ? enemy.enemyData.attackCooldown
            : soloKnockbackCooldown;
        nextKnockbackTime = Time.time + Mathf.Max(0.1f, attackCooldown);
        knockbackRoutine = StartCoroutine(SynchronizedShieldAttackRoutine(target));
        return true;
    }

    IEnumerator SynchronizedShieldAttackRoutine(PlayerHealth target)
    {
        PlayDefenseState(knockbackStateName);
        if (soloKnockbackDamageDelay > 0f)
        {
            yield return new WaitForSeconds(soloKnockbackDamageDelay);
        }

        if (TryGetSynchronizedImpact(target, out float damage, out float knockbackDistance, out Vector3 direction))
        {
            ShieldAttackCoordinator.SubmitImpact(this, target, damage, knockbackDistance, direction, synchronizedDamageBonusMaxPercent);
        }

        yield return new WaitForSeconds(0.25f);
        FadeDefenseLayerWeight(0f);
        knockbackRoutine = null;
    }

    internal bool TryGetSynchronizedImpact(PlayerHealth health, out float damage, out float knockbackDistance, out Vector3 direction)
    {
        damage = 0f;
        knockbackDistance = 0f;
        direction = transform.forward;
        if (health == null || enemy == null || enemy.IsDead || ownShieldBroken)
        {
            return false;
        }

        float hitRange = GetShieldAttackRange() + 0.2f;
        if ((transform.position - health.transform.position).sqrMagnitude > hitRange * hitRange)
        {
            return false;
        }

        direction = health.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
        {
            direction = transform.forward;
        }

        direction.Normalize();
        if (RollPercent(soloKnockbackChance))
        {
            knockbackDistance = Mathf.Clamp(soloKnockbackPower / 100f * soloKnockbackMaxDistance, 0f, soloKnockbackMaxDistance);
        }

        damage = useEnemyDataAttackStats && enemy.enemyData != null
            ? enemy.enemyData.attackDamage
            : soloKnockbackDamage;
        return damage > 0f;
    }

    void TryApplyShieldBuff()
    {
        if (ownShieldBroken || enemy == null || !enemy.HasPhysicalShield || shieldBuffRoutine != null || Time.time < nextShieldBuffTime)
        {
            return;
        }

        nextShieldBuffTime = Time.time + Mathf.Max(0.1f, shieldBuffInterval);
        shieldBuffRoutine = StartCoroutine(ShieldBuffRoutine());
    }

    IEnumerator ShieldBuffRoutine()
    {
        shieldBuffUntilTime = Time.time + Mathf.Max(0.05f, shieldBuffAnimationDuration);
        PlayDefenseState(shieldBuffStateName);

        if (shieldBuffApplyDelay > 0f)
        {
            yield return new WaitForSeconds(shieldBuffApplyDelay);
        }

        if (ownShieldBroken || enemy == null || enemy.IsDead || !enemy.HasPhysicalShield)
        {
            shieldBuffRoutine = null;
            yield break;
        }

        buffedEnemies.Clear();
        Collider[] nearbyEnemies = Physics.OverlapSphere(transform.position, shieldBuffRadius, enemyMask, QueryTriggerInteraction.Ignore);
        int appliedCount = 0;
        for (int i = 0; i < nearbyEnemies.Length; i++)
        {
            Collider hit = nearbyEnemies[i];
            Enemy targetEnemy = hit != null ? hit.GetComponentInParent<Enemy>() : null;
            if (targetEnemy == null || targetEnemy.IsDead || !buffedEnemies.Add(targetEnemy))
            {
                continue;
            }

            if (targetEnemy == enemy)
            {
                continue;
            }

            float allyShieldAmount = targetEnemy.MaxHealth * Mathf.Max(0f, allyShieldPercent) / 100f;
            targetEnemy.AddPhysicalShield(allyShieldAmount);
            AddShieldStatusEffect(targetEnemy);
            appliedCount++;
        }

        if (appliedCount == 0)
        {
            enemy.AddPhysicalShield(enemy.MaxHealth * Mathf.Max(0f, selfShieldBoostPercent) / 100f);
            AddShieldStatusEffect(enemy);
        }

        float remainingAnimationTime = shieldBuffUntilTime - Time.time;
        if (remainingAnimationTime > 0f)
        {
            yield return new WaitForSeconds(remainingAnimationTime);
        }

        shieldBuffRoutine = null;
    }

    void EnsureOwnPhysicalShield()
    {
        if (enemy == null || enemy.IsDead)
        {
            return;
        }

        if (!ownShieldInitialized)
        {
            enemy.AddPhysicalShield(enemy.MaxHealth * Mathf.Max(0f, selfShieldPercent) / 100f);
            AddShieldStatusEffect(enemy);
            ownShieldInitialized = true;
        }

        if (ownShieldInitialized && !enemy.HasPhysicalShield)
        {
            ownShieldBroken = true;
        }
    }

    void AddShieldStatusEffect(Enemy targetEnemy)
    {
        if (targetEnemy == null || shieldStatusEffect == null)
        {
            return;
        }

        EnemyStatusEffectController targetStatus = targetEnemy.StatusController;
        if (targetStatus == null)
        {
            targetStatus = targetEnemy.gameObject.AddComponent<EnemyStatusEffectController>();
        }

        targetStatus.AddEffect(shieldStatusEffect);
    }

    Enemy FindProtectedAlly()
    {
        if (IsValidCachedProtectedAlly())
        {
            return cachedRangedAlly;
        }

        if (Time.time < nextRangedAllySearchTime)
        {
            return null;
        }

        nextRangedAllySearchTime = Time.time + Mathf.Max(0.05f, rangedAllySearchInterval);
        int count = Physics.OverlapSphereNonAlloc(transform.position, rangedAllySearchRadius, hits, enemyMask, QueryTriggerInteraction.Ignore);
        Enemy best = null;
        float bestDistanceSqr = float.MaxValue;
        float shieldDistanceToPlayerSqr = (transform.position - playerTarget.position).sqrMagnitude;
        for (int i = 0; i < count; i++)
        {
            Collider hit = hits[i];
            Enemy candidate = hit != null ? hit.GetComponentInParent<Enemy>() : null;
            if (candidate == null || candidate == enemy || candidate.IsDead)
            {
                continue;
            }

            float candidateDistanceToPlayerSqr = (candidate.transform.position - playerTarget.position).sqrMagnitude;
            // A protector only needs to guard allies behind it. If every ally is
            // already in front, chasing the player keeps this enemy at the front.
            if (candidateDistanceToPlayerSqr <= shieldDistanceToPlayerSqr + 0.25f)
            {
                continue;
            }

            float distanceSqr = (candidate.transform.position - transform.position).sqrMagnitude;
            if (distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                best = candidate;
            }
        }

        cachedRangedAlly = best;
        return cachedRangedAlly;
    }

    bool IsValidCachedProtectedAlly()
    {
        if (cachedRangedAlly == null || cachedRangedAlly == enemy || cachedRangedAlly.IsDead)
        {
            return false;
        }

        if ((cachedRangedAlly.transform.position - transform.position).sqrMagnitude > rangedAllySearchRadius * rangedAllySearchRadius)
        {
            return false;
        }

        return playerTarget != null &&
               (cachedRangedAlly.transform.position - playerTarget.position).sqrMagnitude >
               (transform.position - playerTarget.position).sqrMagnitude + 0.25f;
    }

    void MoveTo(Vector3 destination, float stopDistance)
    {
        // Direct movement is a per-frame fallback. It must not use the NavMesh
        // destination refresh interval, otherwise it advances only once per 0.2s.
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            MoveDirectlyTo(destination, stopDistance);
            return;
        }

        if (Time.time < nextDestinationRefreshTime)
        {
            return;
        }

        nextDestinationRefreshTime = Time.time + Mathf.Max(0.02f, holdPositionRefreshInterval);
        if (agent.isStopped)
        {
            agent.isStopped = false;
        }

        if (Mathf.Abs(agent.speed - blockMoveSpeed) > 0.01f)
        {
            agent.speed = blockMoveSpeed;
        }

        if (Mathf.Abs(agent.stoppingDistance - stopDistance) > 0.01f)
        {
            agent.stoppingDistance = stopDistance;
        }

        if (!hasAgentDestination || (destination - lastAgentDestination).sqrMagnitude > 0.04f)
        {
            if (agent.SetDestination(destination))
            {
                lastAgentDestination = destination;
                hasAgentDestination = true;
                return;
            }
        }

        if (hasAgentDestination && (agent.pathPending || agent.pathStatus != NavMeshPathStatus.PathInvalid))
        {
            return;
        }

        hasAgentDestination = false;
        MoveDirectlyTo(destination, stopDistance);
    }

    void MoveDirectlyTo(Vector3 destination, float stopDistance)
    {
        Vector3 currentPosition = transform.position;
        Vector3 planarOffset = destination - currentPosition;
        planarOffset.y = 0f;
        float stopDistanceSqr = Mathf.Max(0f, stopDistance) * Mathf.Max(0f, stopDistance);
        if (planarOffset.sqrMagnitude <= stopDistanceSqr)
        {
            return;
        }

        float step = Mathf.Max(0f, blockMoveSpeed) * Time.deltaTime;
        Vector3 nextPosition = Vector3.MoveTowards(currentPosition, destination, step);
        nextPosition.y = currentPosition.y;
        transform.position = nextPosition;
        RotateToward(planarOffset);
        hasAgentDestination = false;
    }

    void StopMoving()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            if (!agent.isStopped)
            {
                agent.isStopped = true;
            }

            if (agent.hasPath)
            {
                agent.ResetPath();
            }

            hasAgentDestination = false;
        }
    }

    void RotateToward(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction.normalized), 8f * Time.deltaTime);
    }

    void PlayDefenseState(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
        {
            return;
        }

        int layer = GetDefenseLayerIndex();
        if (layer < 0)
        {
            return;
        }

        StopDefenseFade();
        animator.SetLayerWeight(layer, 1f);
        if (activeDefenseStateName == stateName)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(layer);
            AnimatorStateInfo nextStateInfo = animator.GetNextAnimatorStateInfo(layer);
            int shortHash = Animator.StringToHash(stateName);
            int fullHash = Animator.StringToHash($"{defenseLayerName}.{stateName}");
            
            bool isCurrent = stateInfo.shortNameHash == shortHash || stateInfo.fullPathHash == fullHash;
            bool isNext = animator.IsInTransition(layer) && (nextStateInfo.shortNameHash == shortHash || nextStateInfo.fullPathHash == fullHash);
            
            if (isCurrent || isNext)
            {
                return;
            }
        }

        int stateHash = GetStateHash(layer, stateName);
        if (stateHash != 0)
        {
            activeDefenseStateName = stateName;
            animator.CrossFadeInFixedTime(stateHash, defenseCrossFade, layer);
        }
    }

    void PlayBlockingMovementState(Vector3 destination, float stopDistance, string movingStateName)
    {
        if (Time.time < blockHitUntilTime || Time.time < shieldBuffUntilTime)
        {
            return;
        }

        Vector3 offset = destination - transform.position;
        offset.y = 0f;
        float threshold = Mathf.Max(0f, stopDistance) + 0.1f;
        PlayDefenseState(offset.sqrMagnitude > threshold * threshold ? movingStateName : blockStateName);
    }

    public bool NotifyBlockedHit()
    {
        if (!enableShieldBehavior || enemy == null || enemy.IsDead || enemyAI == null || enemyAI.enabled)
        {
            return false;
        }

        if (blockHitStateNames == null || blockHitStateNames.Length == 0)
        {
            return false;
        }

        string stateName = blockHitStateNames[Random.Range(0, blockHitStateNames.Length)];
        if (string.IsNullOrEmpty(stateName))
        {
            return false;
        }

        blockHitUntilTime = Time.time + Mathf.Max(0.05f, blockHitDuration);
        PlayDefenseState(stateName);
        return true;
    }

    public void NotifyPhysicalShieldBroken()
    {
        if (ownShieldBroken || shieldBreakRoutine != null || enemy == null || enemy.IsDead)
        {
            return;
        }

        ownShieldBroken = true;
        StopShieldActions();
        SetEnemyAIEnabled(false);
        meleeWeaponController?.CancelAttack();
        rangedWeaponController?.CancelAttack();
        shieldBreakRoutine = StartCoroutine(ShieldBreakRoutine());
    }

    // Called by Enemy before the death state is played. No shield coroutine may
    // restore a layer after death has made the Base Layer exclusive.
    public void StopForDeathAnimation()
    {
        StopShieldActions();
        StopMoving();
        SetDefenseLayerWeightImmediate(0f);

        if (locomotionSuppressed && enemyAI != null)
        {
            enemyAI.SetLocomotionSuppressed(false);
            locomotionSuppressed = false;
        }
    }

    IEnumerator ShieldBreakRoutine()
    {
        StopMoving();
        int defenseLayer = GetDefenseLayerIndex();
        ClearAnimatorLayersExcept(defenseLayer);
        PlayDefenseStateImmediately(shieldBreakKnockbackStateName);
        animator?.Update(0f);
        yield return StartCoroutine(MoveBackFromPlayer(shieldBreakKnockbackDistance, shieldBreakKnockbackDuration));

        EquipPostShieldBreakWeapons();

        string reactionLayer = HasPostShieldBreakWeapon() ? "Armed" : "Unarmed";
        string knockdownState = reactionLayer == "Armed" ? "Armed-Knockdown1" : "Unarmed-Knockdown1";
        string getupState = reactionLayer == "Armed" ? "Armed-Getup1" : "Unarmed-Getup1";
        int reactionLayerIndex = animator != null ? animator.GetLayerIndex(reactionLayer) : -1;

        ClearAnimatorLayersExcept(reactionLayerIndex);
        PlayPostShieldBreakState(reactionLayer, knockdownState);
        animator?.Update(0f);
        yield return new WaitForSeconds(Mathf.Max(0f, shieldBreakKnockdownDuration));

        float originalAnimatorSpeed = animator != null ? animator.speed : 1f;
        if (animator != null)
        {
            animator.speed = Mathf.Max(0.01f, shieldBreakGetupSpeed);
        }

        PlayPostShieldBreakState(reactionLayer, getupState);
        animator?.Update(0f);
        yield return new WaitForSeconds(Mathf.Max(0f, shieldBreakGetupDuration));

        if (animator != null)
        {
            animator.speed = originalAnimatorSpeed;
            if (reactionLayerIndex > 0)
            {
                animator.SetLayerWeight(reactionLayerIndex, 0f);
            }
        }

        shieldBreakRoutine = null;
        ExitShieldBehavior();
    }

    IEnumerator MoveBackFromPlayer(float distance, float duration)
    {
        if (distance <= 0f || duration <= 0f)
        {
            yield break;
        }

        Vector3 direction = playerTarget != null ? transform.position - playerTarget.position : -transform.forward;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
        {
            direction = -transform.forward;
        }

        direction.Normalize();
        float elapsed = 0f;
        float moved = 0f;
        while (elapsed < duration)
        {
            float targetDistance = Mathf.Lerp(0f, distance, elapsed / duration);
            float stepDistance = Mathf.Max(0f, targetDistance - moved);
            Vector3 step = direction * stepDistance;
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.Move(step);
            }
            else
            {
                transform.position += step;
            }

            moved += stepDistance;
            elapsed += Time.deltaTime;
            yield return null;
        }

        Vector3 remaining = direction * Mathf.Max(0f, distance - moved);
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.Move(remaining);
        }
        else
        {
            transform.position += remaining;
        }
    }

    bool HasPostShieldBreakWeapon()
    {
        return (meleeWeaponController != null && meleeWeaponController.CurrentWeapon != null) ||
               (rangedWeaponController != null && rangedWeaponController.CurrentWeapon != null);
    }

    void PlayPostShieldBreakState(string layerName, string stateName)
    {
        if (animator == null)
        {
            return;
        }

        int layer = animator.GetLayerIndex(layerName);
        if (layer < 0)
        {
            return;
        }

        PlayStateImmediately(layer, layerName, stateName);
    }

    void PlayDefenseStateImmediately(string stateName)
    {
        int layer = GetDefenseLayerIndex();
        if (layer < 0)
        {
            return;
        }

        activeDefenseStateName = stateName;
        PlayStateImmediately(layer, defenseLayerName, stateName);
    }

    bool PlayStateImmediately(int layer, string layerName, string stateName)
    {
        if (animator == null || layer < 0 || string.IsNullOrEmpty(layerName) || string.IsNullOrEmpty(stateName))
        {
            return false;
        }

        int fullHash = Animator.StringToHash($"{layerName}.{stateName}");
        int stateHash = animator.HasState(layer, fullHash) ? fullHash : Animator.StringToHash(stateName);
        if (!animator.HasState(layer, stateHash))
        {
            return false;
        }

        StopDefenseFade();
        animator.SetLayerWeight(layer, 1f);
        animator.Play(stateHash, layer, 0f);
        return true;
    }

    void ClearAnimatorLayersExcept(int preservedLayer)
    {
        if (animator == null)
        {
            return;
        }

        for (int layer = 1; layer < animator.layerCount; layer++)
        {
            if (layer != preservedLayer)
            {
                animator.SetLayerWeight(layer, 0f);
            }
        }
    }

    void SetDefenseLayerWeightImmediate(float weight)
    {
        StopDefenseFade();
        int layer = GetDefenseLayerIndex();
        if (layer >= 0 && animator != null)
        {
            animator.SetLayerWeight(layer, weight);
        }

        if (weight <= 0f)
        {
            activeDefenseStateName = null;
        }
    }

    void ExitShieldBehavior()
    {
        if (shieldExitHandled)
        {
            return;
        }

        shieldExitHandled = true;
        StopShieldActions();

        shieldBuffUntilTime = 0f;
        FadeDefenseLayerWeight(0f);
        EquipPostShieldBreakWeapons();
        if (enemy == null || !enemy.IsDead)
        {
            SetEnemyAIEnabled(true);
        }
    }

    void StopShieldActions()
    {
        if (shieldBuffRoutine != null)
        {
            StopCoroutine(shieldBuffRoutine);
            shieldBuffRoutine = null;
        }

        if (knockbackRoutine != null)
        {
            StopCoroutine(knockbackRoutine);
            knockbackRoutine = null;
        }

        if (shieldBreakRoutine != null)
        {
            StopCoroutine(shieldBreakRoutine);
            shieldBreakRoutine = null;
        }

        StopDefenseFade();
    }

    void EquipPostShieldBreakWeapons()
    {
        if (rangedWeaponAfterShieldBreak != null)
        {
            rangedWeaponController = rangedWeaponController != null
                ? rangedWeaponController
                : GetComponent<EnemyRangedWeaponController>();
            rangedWeaponController?.EquipWeapon(rangedWeaponAfterShieldBreak);
        }

        if (meleeWeaponAfterShieldBreak != null)
        {
            meleeWeaponController = meleeWeaponController != null
                ? meleeWeaponController
                : GetComponent<EnemyMeleeWeaponController>();
            meleeWeaponController?.EquipWeapon(meleeWeaponAfterShieldBreak);
        }
    }

    int GetStateHash(int layer, string stateName)
    {
        int shortHash = Animator.StringToHash(stateName);
        if (animator.HasState(layer, shortHash))
        {
            return shortHash;
        }

        int fullHash = Animator.StringToHash($"{defenseLayerName}.{stateName}");
        return animator.HasState(layer, fullHash) ? fullHash : 0;
    }

    int GetDefenseLayerIndex()
    {
        if (defenseLayerIndex < 0)
        {
            defenseLayerIndex = animator != null ? animator.GetLayerIndex(defenseLayerName) : -1;
        }

        return defenseLayerIndex;
    }

    void FadeDefenseLayerWeight(float targetWeight)
    {
        StopDefenseFade();
        defenseFadeRoutine = StartCoroutine(FadeDefenseRoutine(targetWeight));
    }

    IEnumerator FadeDefenseRoutine(float targetWeight)
    {
        int layer = GetDefenseLayerIndex();
        if (layer < 0)
        {
            yield break;
        }

        float startWeight = animator.GetLayerWeight(layer);
        float duration = Mathf.Max(0.01f, defenseLayerFadeOut);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            animator.SetLayerWeight(layer, Mathf.Lerp(startWeight, targetWeight, elapsed / duration));
            yield return null;
        }

        animator.SetLayerWeight(layer, targetWeight);
        if (targetWeight <= 0f)
        {
            activeDefenseStateName = null;
        }

        defenseFadeRoutine = null;
    }

    void StopDefenseFade()
    {
        if (defenseFadeRoutine != null)
        {
            StopCoroutine(defenseFadeRoutine);
            defenseFadeRoutine = null;
        }
    }

    void SetEnemyAIEnabled(bool enabled)
    {
        if (enabled && animator != null)
        {
            int layer = GetDefenseLayerIndex();
            if (layer >= 0 && animator.GetLayerWeight(layer) > 0.01f && defenseFadeRoutine == null)
            {
                FadeDefenseLayerWeight(0f);
            }
        }

        if (enemyAI != null)
        {
            if (!enabled && !locomotionSuppressed)
            {
                // A weapon routine that started before shield mode can keep an
                // action layer alive and hide the defense locomotion animation.
                meleeWeaponController?.CancelAttack();
                rangedWeaponController?.CancelAttack();
                enemyAI.SetLocomotionSuppressed(true);
                locomotionSuppressed = true;
            }
            else if (enabled && locomotionSuppressed)
            {
                enemyAI.SetLocomotionSuppressed(false);
                locomotionSuppressed = false;
            }

            if (enemyAI.enabled != enabled)
            {
                enemyAI.enabled = enabled;
            }
        }
    }

    void FindPlayer()
    {
        if (cachedPlayerTarget != null && cachedPlayerTarget.gameObject.activeInHierarchy)
        {
            playerTarget = cachedPlayerTarget;
        }
        else
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            cachedPlayerTarget = player != null ? player.transform : null;
            playerTarget = cachedPlayerTarget;
        }

        playerHealth = null;
    }

    PlayerHealth GetPlayerHealth()
    {
        if (playerHealth == null && playerTarget != null)
        {
            playerHealth = playerTarget.GetComponentInParent<PlayerHealth>();
        }

        return playerHealth;
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
}
