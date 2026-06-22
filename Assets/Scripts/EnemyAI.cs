using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum EnemyAIState
{
    Idle,
    Patrol,
    Chase,
    Attack
}

public class EnemyAI : MonoBehaviour
{
    private static readonly List<EnemyAI> activeEnemies = new List<EnemyAI>();
    private static Transform cachedPlayerTarget;
    private static float nextMissingPlayerLookupTime;

    public static IReadOnlyList<EnemyAI> ActiveEnemies => activeEnemies;

    public Transform playerTarget;
    public EnemyAIState currentState = EnemyAIState.Idle;
    public Transform[] patrolPoints;
    public bool useEnemyDataSettings = true;
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
    public float stoppingDistance = 1.5f;
    public float waypointReachDistance = 0.6f;
    public float agentDestinationUpdateInterval = 0.12f;
    public float agentDestinationChangeThreshold = 0.15f;
    public bool faceMoveDirection = true;
    public float rotationSpeed = 8f;
    public Animator animator;
    public string speedParameter = "Speed";
    public string chaseParameter = "IsChasing";
    public string armedLocomotionLayerName = "Armed-Locomotion";
    public string unarmedLocomotionLayerName = "Unarmed-Locomotion";
    public string shootingLocomotionLayerName = "2Hand-Shooting-Locomotion";
    public EnemyMeleeWeaponController meleeWeaponController;
    public EnemyRangedWeaponController rangedWeaponController;
    public bool useRangedWeaponAttack = true;
    public bool useMeleeWeaponAttack = true;
    public bool useUnarmedAttackWhenNoWeapon = true;
    public string unarmedAttackLayerName = "Unarmed";
    public string[] unarmedAttackStateMachineNames = System.Array.Empty<string>();
    public string[] unarmedAttackStateNames = { "Unarmed-Attack-L1", "Unarmed-Attack-R1", "Unarmed-Attack-L2", "Unarmed-Attack-R2", "Unarmed-Attack-L3", "Unarmed-Attack-R3" };
    public float unarmedAttackCrossFade = 0.08f;
    public string celebrationTriggerName = "Celebration";
    public string celebrationStateName = "Celebration";
    public string celebrationLayerName = "Base Layer";
    public float celebrationFadeDuration = 0.1f;
    public float celebrationDuration = 2f;
    public string patrolStateName = "Patroli";
    public string patrolLayerName = "Base Layer";
    public float patrolFadeDuration = 0.15f;
    public float animatorSpeedDampTime = 0.12f;
    public float attackLayerFadeOut = 0.12f;
    public bool useSupportBehavior;
    public string supportBuffLayerName = "Armed";
    public string supportBuffStateName = "Armed-Boost1";
    public float supportBuffFadeDuration = 0.08f;
    public float supportBuffDuration = 1.2f;
    public float supportBuffLayerFadeOut = 0.12f;
    public float supportAllySearchRadius = 14f;
    public float supportBehindAllyDistance = 3f;
    public float supportFrontlineBuffer = 1.25f;
    public float supportFleeDistanceFromPlayer = 20f;
    public float supportRepositionOnDamageDuration = 3f;
    public float supportDestinationRefreshInterval = 0.35f;
    public float stateUpdateInterval = 0.15f;
    public float stateUpdateRandomOffset = 0.05f;

    [Header("Performance")]
    [Min(0.01f)] public float activeBehaviourUpdateInterval = 0.03f;
    [Min(0.02f)] public float passiveBehaviourUpdateInterval = 0.1f;
    [Min(0.02f)] public float missingPlayerLookupInterval = 0.25f;

    private Enemy enemy;
    private EnemyStatusEffectController statusController;
    private NavMeshAgent agent;
    private bool hasSpeedParameter;
    private bool hasChaseParameter;
    private Vector3 spawnPosition;
    private Vector3 currentDestination;
    private int patrolIndex;
    private float idleTimer;
    private float stunnedUntilTime;
    private float shotAlertUntilTime;
    private Coroutine knockbackCoroutine;
    private bool hasDestination;
    private AudioSource audioSource;
    private float nextVoiceTime;
    private bool ignorePlayerTarget;
    private bool isCelebrating;
    private Coroutine celebrationRoutine;
    private Coroutine unarmedAttackRoutine;
    private Coroutine unarmedAttackFadeRoutine;
    // This attack owns one locomotion suppression while it is active. Other
    // systems (shield, stun, celebration) must never release that ownership.
    private bool unarmedAttackLocomotionSuppressed;
    private float nextUnarmedAttackTime;
    private int unarmedAttackLayerIndex = -1;
    private EnemyAI supportAlly;
    private float nextSupportDestinationUpdateTime;
    private float supportRepositionUntilTime;
    private Coroutine supportBuffRoutine;
    private Coroutine supportBuffFadeRoutine;
    private int supportBuffLayerIndex = -1;
    private bool supportBuffLocomotionSuppressed;
    private Vector3 lastAgentDestination;
    private bool hasAgentDestination;
    private float nextAgentDestinationUpdateTime;
    private float stateUpdateTimer;
    private float nextBehaviourUpdateTime;
    private float lastBehaviourUpdateTime;
    private float unarmedAttackFadeTarget = -1f;
    private float supportBuffFadeTarget = -1f;
    private int locomotionSuppressionCount;
    private System.Collections.Generic.Dictionary<string, int> stateHashes = new System.Collections.Generic.Dictionary<string, int>();

    void Awake()
    {
        enemy = GetComponent<Enemy>();
        statusController = GetComponent<EnemyStatusEffectController>();
        ApplyEnemyDataSettings();
        agent = GetComponent<NavMeshAgent>();
        if (agent == null && autoAddNavMeshAgent)
        {
            agent = gameObject.AddComponent<NavMeshAgent>();
        }

        animator = animator != null ? animator : GetComponentInChildren<Animator>();
        AnimationEventReceiver.EnsureOn(animator);
        UpgradeLegacyAnimationConfiguration();
        meleeWeaponController = meleeWeaponController != null ? meleeWeaponController : GetComponent<EnemyMeleeWeaponController>();
        rangedWeaponController = rangedWeaponController != null ? rangedWeaponController : GetComponent<EnemyRangedWeaponController>();
        audioSource = GetComponent<AudioSource>();
        statusController = statusController != null ? statusController : GetComponent<EnemyStatusEffectController>();
        CacheAnimatorParameters();
        ConfigureLocomotionLayer();
        spawnPosition = transform.position;
        FindPlayer();
        SetupAgent();
        ScheduleNextVoice(false);
    }

    void OnValidate()
    {
        UpgradeLegacyAnimationConfiguration();
    }

    void OnEnable()
    {
        RegisterActiveEnemy();
        ApplyEnemyDataSettings();
        UpgradeLegacyAnimationConfiguration();
        statusController = GetComponent<EnemyStatusEffectController>();
        audioSource = GetComponent<AudioSource>();
        idleTimer = idleDuration;
        hasDestination = false;
        stunnedUntilTime = 0f;
        shotAlertUntilTime = 0f;
        knockbackCoroutine = null;
        currentState = EnemyAIState.Idle;
        ignorePlayerTarget = false;
        isCelebrating = false;
        celebrationRoutine = null;
        supportAlly = null;
        nextSupportDestinationUpdateTime = 0f;
        supportRepositionUntilTime = 0f;
        supportBuffRoutine = null;
        supportBuffFadeRoutine = null;
        hasAgentDestination = false;
        nextAgentDestinationUpdateTime = 0f;
        stateUpdateTimer = Random.Range(0f, Mathf.Max(0f, stateUpdateRandomOffset));
        unarmedAttackFadeTarget = -1f;
        supportBuffFadeTarget = -1f;
        unarmedAttackLocomotionSuppressed = false;
        supportBuffLocomotionSuppressed = false;
        locomotionSuppressionCount = 0;
        lastBehaviourUpdateTime = Time.time;
        nextBehaviourUpdateTime = Time.time + Random.Range(0f, Mathf.Max(0.02f, passiveBehaviourUpdateInterval));
        ConfigureLocomotionLayer();
        ScheduleNextVoice(false);
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
        if (enemy != null && enemy.IsDead)
        {
            StopMoving();
            CancelAllAttacks();
            return;
        }

        if (IsStunned)
        {
            StopMoving();
            CancelAllAttacks();
            UpdateAnimator(Time.deltaTime);
            return;
        }

        if (isCelebrating)
        {
            StopMoving();
            UpdateAnimator(Time.deltaTime);
            return;
        }

        if ((playerTarget == null || !playerTarget.gameObject.activeInHierarchy) && !ignorePlayerTarget)
        {
            playerTarget = null;
            FindPlayer();
        }

        stateUpdateTimer -= Time.deltaTime;
        if (stateUpdateTimer <= 0f)
        {
            UpdateState();
            stateUpdateTimer = Mathf.Max(0.02f, stateUpdateInterval) + Random.Range(0f, Mathf.Max(0f, stateUpdateRandomOffset));
        }

        if (TryRunBehaviourUpdate(out float behaviourDeltaTime))
        {
            UpdateBehaviour(behaviourDeltaTime);
            UpdateAnimator(behaviourDeltaTime);
        }

        UpdateVoice();
    }

    bool TryRunBehaviourUpdate(out float behaviourDeltaTime)
    {
        behaviourDeltaTime = 0f;
        if (Time.time < nextBehaviourUpdateTime)
        {
            return false;
        }

        behaviourDeltaTime = Mathf.Max(0.001f, Time.time - lastBehaviourUpdateTime);
        lastBehaviourUpdateTime = Time.time;
        nextBehaviourUpdateTime = Time.time + GetBehaviourUpdateInterval();
        return true;
    }

    float GetBehaviourUpdateInterval()
    {
        bool needsFastResponse = currentState == EnemyAIState.Chase || currentState == EnemyAIState.Attack;
        return Mathf.Max(0.01f, needsFastResponse ? activeBehaviourUpdateInterval : passiveBehaviourUpdateInterval);
    }

    void RequestImmediateBehaviourUpdate()
    {
        nextBehaviourUpdateTime = 0f;
    }

    void UpdateState()
    {
        if (playerTarget == null)
        {
            if (currentState == EnemyAIState.Chase)
            {
                EnterIdle();
            }
            return;
        }

        float distanceToPlayerSqr = (transform.position - playerTarget.position).sqrMagnitude;
        float detectionRange = GetDetectionRange();
        if (currentState != EnemyAIState.Chase && currentState != EnemyAIState.Attack && distanceToPlayerSqr <= detectionRange * detectionRange)
        {
            EnterChase();
            return;
        }

        float loseTargetRange = GetLoseTargetRange();
        if ((currentState == EnemyAIState.Chase || currentState == EnemyAIState.Attack) && distanceToPlayerSqr > loseTargetRange * loseTargetRange)
        {
            EnterIdle();
        }
    }

    public void ApplyShotAlert(bool broadcastToNearby)
    {
        if (ignorePlayerTarget || isCelebrating)
        {
            return;
        }

        float minDuration = Mathf.Max(0f, GetModifiedEnemyStat(StatusEffectStat.EnemyShotAlertMinDuration, shotAlertMinDuration));
        float maxDuration = Mathf.Max(minDuration, GetModifiedEnemyStat(StatusEffectStat.EnemyShotAlertMaxDuration, shotAlertMaxDuration));
        float duration = Random.Range(minDuration, maxDuration);

        shotAlertUntilTime = Time.time + duration;
        FindPlayer();

        if (playerTarget != null)
        {
            EnterChase();
        }

        if (broadcastToNearby)
        {
            BroadcastShotAlertToNearby();
        }
    }

    public void ApplyStun(float duration)
    {
        if (duration <= 0f)
        {
            return;
        }

        stunnedUntilTime = Mathf.Max(stunnedUntilTime, Time.time + duration);
        StopMoving();
        CancelAllAttacks();
    }

    public void ApplyKnockback(Vector3 direction, float distance, float duration)
    {
        if (distance <= 0f)
        {
            return;
        }

        if (knockbackCoroutine != null)
        {
            StopCoroutine(knockbackCoroutine);
        }

        ApplyStun(duration);
        knockbackCoroutine = StartCoroutine(KnockbackRoutine(direction, distance, duration));
    }

    public void OnPlayerDied(Transform deadPlayer)
    {
        if (enemy != null && enemy.IsDead)
        {
            return;
        }

        ignorePlayerTarget = true;
        playerTarget = null;
        shotAlertUntilTime = 0f;
        stunnedUntilTime = 0f;
        hasDestination = false;
        currentState = EnemyAIState.Idle;
        StopMoving();

        CancelAllAttacks();

        if (celebrationRoutine != null)
        {
            StopCoroutine(celebrationRoutine);
        }

        celebrationRoutine = StartCoroutine(CelebrationRoutine(deadPlayer));
    }

    IEnumerator KnockbackRoutine(Vector3 direction, float distance, float duration)
    {
        Vector3 flatDirection = direction;
        flatDirection.y = 0f;
        if (flatDirection.sqrMagnitude < 0.001f)
        {
            knockbackCoroutine = null;
            yield break;
        }

        flatDirection.Normalize();
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        float moved = 0f;

        while (elapsed < safeDuration)
        {
            float t = elapsed / safeDuration;
            float targetMoved = Mathf.Lerp(0f, distance, t);
            float stepDistance = Mathf.Max(0f, targetMoved - moved);
            Vector3 step = flatDirection * stepDistance;

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

        Vector3 remainingStep = flatDirection * Mathf.Max(0f, distance - moved);
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.Move(remainingStep);
        }
        else
        {
            transform.position += remainingStep;
        }

        knockbackCoroutine = null;
    }

    bool IsStunned => Time.time < stunnedUntilTime;
    bool IsShotAlerted => Time.time < shotAlertUntilTime;

    float GetDetectionRange()
    {
        float range = GetModifiedEnemyStat(StatusEffectStat.EnemyDetectionRange, detectionRange);
        float alertMultiplier = GetModifiedEnemyStat(StatusEffectStat.EnemyShotAlertRangeMultiplier, shotAlertRangeMultiplier);
        return IsShotAlerted ? range * Mathf.Max(1f, alertMultiplier) : range;
    }

    float GetLoseTargetRange()
    {
        float range = GetModifiedEnemyStat(StatusEffectStat.EnemyLoseTargetRange, loseTargetRange);
        float alertMultiplier = GetModifiedEnemyStat(StatusEffectStat.EnemyShotAlertRangeMultiplier, shotAlertRangeMultiplier);
        return IsShotAlerted ? range * Mathf.Max(1f, alertMultiplier) : range;
    }

    void BroadcastShotAlertToNearby()
    {
        float shareRadius = GetModifiedEnemyStat(StatusEffectStat.EnemyShotAlertShareRadius, shotAlertShareRadius);
        float shareRadiusSqr = shareRadius * shareRadius;

        for (int i = 0; i < activeEnemies.Count; i++)
        {
            EnemyAI other = activeEnemies[i];
            if (other == null || other == this || !other.gameObject.activeInHierarchy)
            {
                continue;
            }

            if ((other.transform.position - transform.position).sqrMagnitude <= shareRadiusSqr)
            {
                other.ApplyShotAlert(false);
            }
        }
    }

    void UpdateBehaviour(float behaviourDeltaTime)
    {
        if (UsesSupportBehavior() && (currentState == EnemyAIState.Chase || currentState == EnemyAIState.Attack))
        {
            UpdateSupport();
            return;
        }

        switch (currentState)
        {
            case EnemyAIState.Idle:
                UpdateIdle(behaviourDeltaTime);
                break;
            case EnemyAIState.Patrol:
                UpdatePatrol();
                break;
            case EnemyAIState.Chase:
                UpdateChase();
                break;
            case EnemyAIState.Attack:
                UpdateAttack();
                break;
        }
    }

    void UpdateIdle(float behaviourDeltaTime)
    {
        StopMoving();
        idleTimer -= behaviourDeltaTime;
        if (idleTimer <= 0f)
        {
            EnterPatrol();
        }
    }

    void UpdatePatrol()
    {
        SetMoveSpeed(GetWalkMoveSpeed());

        if (!hasDestination)
        {
            SetNextPatrolDestination();
            if (!hasDestination)
            {
                EnterIdle();
                return;
            }
        }

        float reachDistance = GetModifiedEnemyStat(StatusEffectStat.EnemyWaypointReachDistance, waypointReachDistance);
        MoveTo(currentDestination, reachDistance);
        if (HasReachedDestination(reachDistance))
        {
            EnterIdle();
        }
    }

    void UpdateChase()
    {
        if (playerTarget == null)
        {
            EnterIdle();
            return;
        }

        float attackRange = GetAttackRange();
        if ((transform.position - playerTarget.position).sqrMagnitude <= attackRange * attackRange)
        {
            EnterAttack();
            return;
        }

        SetMoveSpeed(GetModifiedEnemyStat(StatusEffectStat.EnemyChaseSpeed, chaseSpeed));
        MoveTo(playerTarget.position, GetModifiedEnemyStat(StatusEffectStat.EnemyStoppingDistance, stoppingDistance));
    }

    void UpdateAttack()
    {
        if (playerTarget == null)
        {
            EnterIdle();
            return;
        }

        float attackRange = GetAttackRange();
        if ((transform.position - playerTarget.position).sqrMagnitude > attackRange * attackRange)
        {
            EnterChase();
            return;
        }

        StopMoving();
        RotateToward(playerTarget.position - transform.position);
        TryAttackPlayer();
    }

    void UpdateSupport()
    {
        if (playerTarget == null)
        {
            EnterIdle();
            return;
        }

        currentState = EnemyAIState.Chase;
        CancelAllAttacks();

        if (supportBuffRoutine != null)
        {
            StopMoving();
            RotateToward(playerTarget.position - transform.position);
            return;
        }

        SetMoveSpeed(GetModifiedEnemyStat(StatusEffectStat.EnemyChaseSpeed, chaseSpeed));

        if (!IsValidSupportAlly(supportAlly))
        {
            supportAlly = null;
            nextSupportDestinationUpdateTime = 0f;
        }

        if (Time.time >= nextSupportDestinationUpdateTime)
        {
            supportAlly = FindSupportAlly();
            nextSupportDestinationUpdateTime = Time.time + Mathf.Max(0.05f, supportDestinationRefreshInterval);

            if (supportAlly != null)
            {
                SetDestination(GetSupportPositionBehindAlly(supportAlly));
            }
            else
            {
                SetDestination(GetSupportFleePosition());
            }
        }

        if (supportAlly != null && IsSupportInFrontOfAlly(supportAlly))
        {
            SetDestination(GetSupportPositionBehindAlly(supportAlly));
        }

        if (supportAlly == null && (transform.position - playerTarget.position).sqrMagnitude >= supportFleeDistanceFromPlayer * supportFleeDistanceFromPlayer)
        {
            StopMoving();
            RotateToward(playerTarget.position - transform.position);
            return;
        }

        MoveTo(currentDestination, GetModifiedEnemyStat(StatusEffectStat.EnemyStoppingDistance, stoppingDistance));
        RotateToward(playerTarget.position - transform.position);
    }

    void EnterIdle()
    {
        currentState = EnemyAIState.Idle;
        idleTimer = GetModifiedEnemyStat(StatusEffectStat.EnemyIdleDuration, idleDuration);
        hasDestination = false;
        StopMoving();
        RequestImmediateBehaviourUpdate();
    }

    void EnterPatrol()
    {
        currentState = EnemyAIState.Patrol;
        hasDestination = false;
        RequestImmediateBehaviourUpdate();
    }

    void EnterChase()
    {
        if (ignorePlayerTarget || isCelebrating)
        {
            return;
        }

        bool wasAggressive = currentState == EnemyAIState.Chase || currentState == EnemyAIState.Attack;
        currentState = EnemyAIState.Chase;
        hasDestination = false;
        RequestImmediateBehaviourUpdate();

        if (!wasAggressive)
        {
            PlayVoice(GetAngryGrowlSound());
        }

        ScheduleNextVoice(true);
    }

    void EnterAttack()
    {
        if (ignorePlayerTarget || isCelebrating)
        {
            return;
        }

        if (UsesSupportBehavior())
        {
            EnterChase();
            return;
        }

        currentState = EnemyAIState.Attack;
        hasDestination = false;
        StopMoving();
        RequestImmediateBehaviourUpdate();
    }

    void SetNextPatrolDestination()
    {
        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            Transform point = patrolPoints[patrolIndex % patrolPoints.Length];
            patrolIndex++;
            if (point != null)
            {
                SetDestination(point.position);
                return;
            }
        }

        if (useRandomPatrolWhenNoPoints)
        {
            Vector2 random = Random.insideUnitCircle * GetModifiedEnemyStat(StatusEffectStat.EnemyRandomPatrolRadius, randomPatrolRadius);
            SetDestination(spawnPosition + new Vector3(random.x, 0f, random.y));
        }
    }

    void SetDestination(Vector3 destination)
    {
        currentDestination = destination;
        hasDestination = true;
    }

    void MoveTo(Vector3 destination, float stopDistance)
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            if (agent.isStopped)
            {
                agent.isStopped = false;
            }

            if (Mathf.Abs(agent.stoppingDistance - stopDistance) > 0.01f)
            {
                agent.stoppingDistance = stopDistance;
            }

            TrySetAgentDestination(destination);
            return;
        }

        Vector3 direction = destination - transform.position;
        direction.y = 0f;
        float distanceSqr = direction.sqrMagnitude;
        if (distanceSqr <= stopDistance * stopDistance)
        {
            return;
        }

        float distance = Mathf.Sqrt(distanceSqr);
        Vector3 move = direction.normalized * GetMoveSpeed() * Time.deltaTime;
        transform.position += Vector3.ClampMagnitude(move, distance);

        if (faceMoveDirection)
        {
            RotateToward(direction);
        }
    }

    void TrySetAgentDestination(Vector3 destination)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        float threshold = Mathf.Max(0f, agentDestinationChangeThreshold);
        bool destinationChanged = !hasAgentDestination
            || (destination - lastAgentDestination).sqrMagnitude >= threshold * threshold;
        bool shouldRefresh = Time.time >= nextAgentDestinationUpdateTime;

        if (!destinationChanged && !shouldRefresh && agent.hasPath)
        {
            return;
        }

        if (agent.SetDestination(destination))
        {
            lastAgentDestination = destination;
            hasAgentDestination = true;
            nextAgentDestinationUpdateTime = Time.time + Mathf.Max(0.02f, agentDestinationUpdateInterval);
        }
    }

    bool HasReachedDestination(float reachDistance)
    {
        if (!hasDestination)
        {
            return true;
        }

        if (agent != null && agent.enabled && agent.isOnNavMesh && agent.pathPending)
        {
            return false;
        }

        return (transform.position - currentDestination).sqrMagnitude <= reachDistance * reachDistance;
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

    void SetMoveSpeed(float speed)
    {
        if (agent != null)
        {
            float modifiedSpeed = GetModifiedEnemyStat(StatusEffectStat.EnemyMoveSpeed, speed);
            if (Mathf.Abs(agent.speed - modifiedSpeed) > 0.01f)
            {
                agent.speed = modifiedSpeed;
            }
        }
    }

    float GetMoveSpeed()
    {
        float speed = currentState == EnemyAIState.Chase
            ? GetModifiedEnemyStat(StatusEffectStat.EnemyChaseSpeed, chaseSpeed)
            : GetWalkMoveSpeed();
        return GetModifiedEnemyStat(StatusEffectStat.EnemyMoveSpeed, speed);
    }

    float GetWalkMoveSpeed()
    {
        float walkSpeed = GetModifiedEnemyStat(StatusEffectStat.EnemyPatrolSpeed, patrolSpeed);
        return walkSpeed * Mathf.Clamp(walkSpeedMultiplier, 0.1f, 1f);
    }

    float GetAttackRange()
    {
        if (useRangedWeaponAttack && rangedWeaponController != null && rangedWeaponController.CurrentWeapon != null)
        {
            return rangedWeaponController.EffectiveAttackRange;
        }

        if (useMeleeWeaponAttack && meleeWeaponController != null && meleeWeaponController.CurrentWeapon != null)
        {
            return meleeWeaponController.EffectiveAttackRange;
        }

        if (useUnarmedAttackWhenNoWeapon && enemy != null && enemy.enemyData != null)
        {
            return GetModifiedEnemyStat(StatusEffectStat.EnemyAttackRange, enemy.enemyData.attackRange)
                + GetModifiedEnemyStat(StatusEffectStat.EnemyAttackHitRadius, enemy.enemyData.attackHitRadius);
        }

        return enemy != null && enemy.enemyData != null
            ? GetModifiedEnemyStat(StatusEffectStat.EnemyAttackRange, enemy.enemyData.attackRange)
            : stoppingDistance;
    }

    bool UsesSupportBehavior()
    {
        if (useSupportBehavior)
        {
            return true;
        }

        return enemy != null
            && enemy.enemyData != null
            && (enemy.enemyData.useSupportBehavior || enemy.enemyData.enemyType == EnemyType.Support);
    }

    EnemyAI FindSupportAlly()
    {
        EnemyAI bestNonSupport = null;
        EnemyAI bestFallback = null;
        float bestNonSupportDistanceSqr = float.MaxValue;
        float bestFallbackDistanceSqr = float.MaxValue;
        float radiusSqr = supportAllySearchRadius * supportAllySearchRadius;

        for (int i = 0; i < activeEnemies.Count; i++)
        {
            EnemyAI other = activeEnemies[i];
            if (other == null || other == this || !other.gameObject.activeInHierarchy || other.enemy == null || other.enemy.IsDead)
            {
                continue;
            }

            float distanceSqr = (other.transform.position - transform.position).sqrMagnitude;
            if (distanceSqr > radiusSqr)
            {
                continue;
            }

            if (!other.UsesSupportBehavior() && distanceSqr < bestNonSupportDistanceSqr)
            {
                bestNonSupport = other;
                bestNonSupportDistanceSqr = distanceSqr;
            }

            if (distanceSqr < bestFallbackDistanceSqr)
            {
                bestFallback = other;
                bestFallbackDistanceSqr = distanceSqr;
            }
        }

        if (IsSupportRepositionForced)
        {
            return bestFallback;
        }

        return bestNonSupport != null ? bestNonSupport : bestFallback;
    }

    bool IsSupportRepositionForced => Time.time < supportRepositionUntilTime;

    bool IsValidSupportAlly(EnemyAI ally)
    {
        return ally != null
            && ally != this
            && ally.enemy != null
            && !ally.enemy.IsDead
            && ally.gameObject.activeInHierarchy;
    }

    bool IsSupportInFrontOfAlly(EnemyAI ally)
    {
        if (ally == null || playerTarget == null)
        {
            return false;
        }

        float supportDistanceSqr = (transform.position - playerTarget.position).sqrMagnitude;
        float allyDistanceSqr = (ally.transform.position - playerTarget.position).sqrMagnitude;
        if (supportDistanceSqr >= allyDistanceSqr)
        {
            return false;
        }

        float supportDistanceToPlayer = Mathf.Sqrt(supportDistanceSqr);
        float allyDistanceToPlayer = Mathf.Sqrt(allyDistanceSqr);
        return supportDistanceToPlayer + supportFrontlineBuffer < allyDistanceToPlayer;
    }

    Vector3 GetSupportPositionBehindAlly(EnemyAI ally)
    {
        if (ally == null || playerTarget == null)
        {
            return transform.position;
        }

        Vector3 awayFromPlayer = ally.transform.position - playerTarget.position;
        awayFromPlayer.y = 0f;
        if (awayFromPlayer.sqrMagnitude < 0.001f)
        {
            awayFromPlayer = -ally.transform.forward;
        }

        Vector3 destination = ally.transform.position + awayFromPlayer.normalized * Mathf.Max(0.5f, supportBehindAllyDistance);
        return GetNavMeshPosition(destination);
    }

    Vector3 GetSupportFleePosition()
    {
        if (playerTarget == null)
        {
            return transform.position;
        }

        Vector3 awayFromPlayer = transform.position - playerTarget.position;
        awayFromPlayer.y = 0f;
        if (awayFromPlayer.sqrMagnitude < 0.001f)
        {
            awayFromPlayer = -transform.forward;
        }

        Vector3 destination = playerTarget.position + awayFromPlayer.normalized * Mathf.Max(1f, supportFleeDistanceFromPlayer);
        return GetNavMeshPosition(destination);
    }

    Vector3 GetNavMeshPosition(Vector3 position)
    {
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 4f, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return position;
    }

    public void NotifyDamaged()
    {
        if (!UsesSupportBehavior())
        {
            return;
        }

        supportRepositionUntilTime = Time.time + Mathf.Max(0f, supportRepositionOnDamageDuration);
        nextSupportDestinationUpdateTime = 0f;
        FindPlayer();

        if (playerTarget != null && !ignorePlayerTarget && !isCelebrating)
        {
            EnterChase();
        }
    }

    public bool TryPlaySupportBuffAnimation()
    {
        if (!isActiveAndEnabled || enemy == null || enemy.IsDead || !UsesSupportBehavior() || animator == null || supportBuffRoutine != null)
        {
            return false;
        }

        supportBuffRoutine = StartCoroutine(SupportBuffRoutine());
        return true;
    }

    IEnumerator SupportBuffRoutine()
    {
        if (enemy == null || enemy.IsDead)
        {
            supportBuffRoutine = null;
            yield break;
        }

        SetLocomotionSuppressed(true);
        supportBuffLocomotionSuppressed = true;
        currentState = EnemyAIState.Idle;
        hasDestination = false;
        StopMoving();
        CancelAllAttacks();

        if (hasSpeedParameter)
        {
            animator.SetFloat(speedParameter, 0f, animatorSpeedDampTime, Time.deltaTime);
        }

        int layerIndex = GetSupportBuffLayerIndex();
        if (layerIndex > 0)
        {
            StopSupportBuffFade();
            animator.SetLayerWeight(layerIndex, 1f);
        }

        int stateHash = GetAnimatorStateHash(layerIndex, supportBuffLayerName, supportBuffStateName);
        if (stateHash != 0)
        {
            animator.CrossFadeInFixedTime(stateHash, Mathf.Max(0f, supportBuffFadeDuration), layerIndex);
        }

        yield return new WaitForSeconds(Mathf.Max(0f, supportBuffDuration));

        if (enemy == null || enemy.IsDead)
        {
            supportBuffRoutine = null;
            yield break;
        }

        FadeSupportBuffLayerWeight(0f);
        ReleaseSupportBuffLocomotion();
        supportBuffRoutine = null;

        if (playerTarget != null && !ignorePlayerTarget && !isCelebrating)
        {
            EnterChase();
        }
    }

    // Enemy death owns the Animator. Stop every AI coroutine that could write
    // an overlay layer after Enemy has selected its death state.
    public void StopForDeathAnimation()
    {
        StopMoving();
        isCelebrating = false;

        if (knockbackCoroutine != null)
        {
            StopCoroutine(knockbackCoroutine);
            knockbackCoroutine = null;
        }

        if (celebrationRoutine != null)
        {
            StopCoroutine(celebrationRoutine);
            celebrationRoutine = null;
        }

        CancelUnarmedAttack();

        if (supportBuffRoutine != null)
        {
            StopCoroutine(supportBuffRoutine);
            supportBuffRoutine = null;
        }

        ReleaseSupportBuffLocomotion();

        StopSupportBuffFade();
        int layerIndex = GetSupportBuffLayerIndex();
        if (animator != null && layerIndex > 0)
        {
            animator.SetLayerWeight(layerIndex, 0f);
        }
    }

    // Hit reactions temporarily own a full-body layer. Clear only transient
    // actions here so their coroutines cannot overwrite that reaction later.
    public void InterruptSupportBuffForHitReaction()
    {
        if (supportBuffRoutine == null)
        {
            return;
        }

        StopCoroutine(supportBuffRoutine);
        supportBuffRoutine = null;
        StopSupportBuffFade();
        int layerIndex = GetSupportBuffLayerIndex();
        if (animator != null && layerIndex > 0)
        {
            animator.SetLayerWeight(layerIndex, 0f);
        }

        ReleaseSupportBuffLocomotion();
    }

    void ReleaseSupportBuffLocomotion()
    {
        if (!supportBuffLocomotionSuppressed)
        {
            return;
        }

        supportBuffLocomotionSuppressed = false;
        SetLocomotionSuppressed(false);
    }

    void TryAttackPlayer()
    {
        if (useRangedWeaponAttack && rangedWeaponController != null && rangedWeaponController.CurrentWeapon != null)
        {
            rangedWeaponController.TryAttack(playerTarget);
            return;
        }

        if (useMeleeWeaponAttack && meleeWeaponController != null)
        {
            if (meleeWeaponController.CurrentWeapon != null)
            {
                meleeWeaponController.TryAttack(playerTarget);
                return;
            }
        }

        TryUnarmedAttack(playerTarget);
    }

    void CancelAllAttacks()
    {
        if (meleeWeaponController != null)
        {
            meleeWeaponController.CancelAttack();
        }

        if (rangedWeaponController != null)
        {
            rangedWeaponController.CancelAttack();
        }

        CancelUnarmedAttack();
    }

    void TryUnarmedAttack(Transform target)
    {
        if (!useUnarmedAttackWhenNoWeapon || target == null || enemy == null || enemy.enemyData == null)
        {
            return;
        }

        if (Time.time < nextUnarmedAttackTime || unarmedAttackRoutine != null)
        {
            return;
        }

        float cooldown = Mathf.Max(0.01f, GetModifiedEnemyStat(StatusEffectStat.EnemyAttackCooldown, enemy.enemyData.attackCooldown));
        nextUnarmedAttackTime = Time.time + cooldown;
        unarmedAttackRoutine = StartCoroutine(UnarmedAttackRoutine(target, cooldown));
    }

    IEnumerator UnarmedAttackRoutine(Transform target, float cooldown)
    {
        StopUnarmedAttackFade();
        SetUnarmedAttackLocomotionSuppressed(true);
        SetUnarmedAttackLayerWeight(1f);
        PlayRandomUnarmedAttackAnimation();

        float windup = enemy != null && enemy.enemyData != null
            ? Mathf.Max(0f, GetModifiedEnemyStat(StatusEffectStat.EnemyAttackWindup, enemy.enemyData.attackWindup))
            : 0f;
        if (windup > 0f)
        {
            yield return new WaitForSeconds(windup);
        }

        if (enemy != null && !enemy.IsDead && !IsStunned && !isCelebrating)
        {
            DealUnarmedDamage(target);
        }

        float remainingAttackTime = Mathf.Max(0f, cooldown - windup);
        if (remainingAttackTime > 0f)
        {
            yield return new WaitForSeconds(remainingAttackTime);
        }

        FadeUnarmedAttackLayerWeight(0f);
        SetUnarmedAttackLocomotionSuppressed(false);
        unarmedAttackRoutine = null;
    }

    void DealUnarmedDamage(Transform target)
    {
        if (target == null || enemy == null || enemy.enemyData == null)
        {
            return;
        }

        EnemyData data = enemy.enemyData;
        float attackRange = GetModifiedEnemyStat(StatusEffectStat.EnemyAttackRange, data.attackRange);
        float hitRadius = GetModifiedEnemyStat(StatusEffectStat.EnemyAttackHitRadius, data.attackHitRadius);
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
        float damage = ApplyEnemyCriticalDamage(
            GetModifiedEnemyStat(StatusEffectStat.EnemyAttackDamage, data.attackDamage),
            data.attackCriticalChance,
            data.attackCriticalDamagePercent,
            out isCritical
        );
        bool isKnockback = TryGetEnemyKnockbackDistance(
            data.attackKnockbackChance,
            data.attackKnockbackPower,
            data.attackMaxKnockbackDistance,
            out float knockbackDistance
        );
        bool isHeavy = RollPercent(data.attackHeavyChance);

        playerHealth.TakeDamage(damage, hitPoint, hitNormal, isHeavy, isCritical, isKnockback);
        if (isKnockback && !playerHealth.LastDamageWasBlocked && !playerHealth.IsDead)
        {
            ApplyEnemyKnockback(playerHealth, hitNormal, knockbackDistance, data.attackKnockbackDuration);
        }
        TryApplyAttackStatusEffects(playerHealth, data);
    }

    float ApplyEnemyCriticalDamage(float damage, float criticalChance, float criticalDamagePercent, out bool isCritical)
    {
        isCritical = RollPercent(criticalChance);
        if (!isCritical)
        {
            return damage;
        }

        return damage + damage * Mathf.Max(0f, criticalDamagePercent) / 100f;
    }

    bool TryGetEnemyKnockbackDistance(float chance, float power, float maxDistance, out float distance)
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

    void ApplyEnemyKnockback(PlayerHealth playerHealth, Vector3 direction, float distance, float duration)
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

    float GetModifiedEnemyStat(StatusEffectStat stat, float baseValue)
    {
        statusController = statusController != null
            ? statusController
            : enemy != null ? enemy.StatusController : GetComponent<EnemyStatusEffectController>();
        return statusController != null ? statusController.ModifyStat(stat, baseValue) : baseValue;
    }

    void TryApplyAttackStatusEffects(PlayerHealth playerHealth, EnemyData data)
    {
        if (playerHealth == null || data == null)
        {
            return;
        }

        PlayerStatusEffectController statusController = GetPlayerStatusController(playerHealth);
        if (statusController == null)
        {
            return;
        }

        if (data.hitStatusEffects != null && data.hitStatusEffects.Length > 0)
        {
            for (int i = 0; i < data.hitStatusEffects.Length; i++)
            {
                EnemyStatusEffectChance entry = data.hitStatusEffects[i];
                if (entry != null && entry.effect != null && Random.value <= Mathf.Clamp01(entry.chance / 100f))
                {
                    statusController.AddEffect(entry.effect);
                }
            }

            return;
        }

        if (data.attackStatusEffects == null || data.attackStatusEffects.Length == 0)
        {
            return;
        }

        if (Random.value > Mathf.Clamp01(data.attackStatusEffectChance / 100f))
        {
            return;
        }

        for (int i = 0; i < data.attackStatusEffects.Length; i++)
        {
            if (data.attackStatusEffects[i] != null)
            {
                statusController.AddEffect(data.attackStatusEffects[i]);
            }
        }
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

    void CancelUnarmedAttack()
    {
        if (unarmedAttackRoutine != null)
        {
            StopCoroutine(unarmedAttackRoutine);
            unarmedAttackRoutine = null;
        }

        FadeUnarmedAttackLayerWeight(0f);
        SetUnarmedAttackLocomotionSuppressed(false);
    }

    void SetUnarmedAttackLocomotionSuppressed(bool suppressed)
    {
        if (suppressed)
        {
            if (unarmedAttackLocomotionSuppressed)
            {
                return;
            }

            unarmedAttackLocomotionSuppressed = true;
            SetLocomotionSuppressed(true);
            return;
        }

        if (!unarmedAttackLocomotionSuppressed)
        {
            return;
        }

        unarmedAttackLocomotionSuppressed = false;
        SetLocomotionSuppressed(false);
    }

    void PlayRandomUnarmedAttackAnimation()
    {
        if (animator == null || unarmedAttackStateNames == null || unarmedAttackStateNames.Length == 0)
        {
            return;
        }

        int startIndex = Random.Range(0, unarmedAttackStateNames.Length);
        for (int i = 0; i < unarmedAttackStateNames.Length; i++)
        {
            string stateName = unarmedAttackStateNames[(startIndex + i) % unarmedAttackStateNames.Length];
            int stateHash = GetAnimatorStateHash(GetUnarmedAttackLayerIndex(), unarmedAttackLayerName, stateName);
            if (stateHash == 0)
            {
                continue;
            }

            animator.CrossFadeInFixedTime(stateHash, Mathf.Max(0f, unarmedAttackCrossFade), GetUnarmedAttackLayerIndex());
            return;
        }
    }

    int GetAnimatorStateHash(int layerIndex, string layerName, string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
        {
            return 0;
        }

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

        if (unarmedAttackStateMachineNames != null)
        {
            for (int i = 0; i < unarmedAttackStateMachineNames.Length; i++)
            {
                string stateMachineName = unarmedAttackStateMachineNames[i];
                if (string.IsNullOrEmpty(stateMachineName))
                {
                    continue;
                }

                string layerMachinePath = $"{layerName}.{stateMachineName}.{stateName}";
                if (!stateHashes.TryGetValue(layerMachinePath, out int layerMachinePathHash))
                {
                    layerMachinePathHash = Animator.StringToHash(layerMachinePath);
                    stateHashes[layerMachinePath] = layerMachinePathHash;
                }

                if (animator.HasState(layerIndex, layerMachinePathHash))
                {
                    return layerMachinePathHash;
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

        return 0;
    }

    int GetUnarmedAttackLayerIndex()
    {
        if (unarmedAttackLayerIndex < 0)
        {
            unarmedAttackLayerIndex = animator != null ? animator.GetLayerIndex(unarmedAttackLayerName) : -1;
        }

        return unarmedAttackLayerIndex >= 0 ? unarmedAttackLayerIndex : 0;
    }

    int GetSupportBuffLayerIndex()
    {
        if (supportBuffLayerIndex < 0)
        {
            supportBuffLayerIndex = animator != null ? animator.GetLayerIndex(supportBuffLayerName) : -1;
        }

        return supportBuffLayerIndex >= 0 ? supportBuffLayerIndex : 0;
    }

    void SetUnarmedAttackLayerWeight(float weight)
    {
        if (animator == null)
        {
            return;
        }

        int layerIndex = GetUnarmedAttackLayerIndex();
        if (layerIndex > 0)
        {
            animator.SetLayerWeight(layerIndex, weight);
        }
    }

    void RotateToward(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, GetModifiedEnemyStat(StatusEffectStat.EnemyRotationSpeed, rotationSpeed) * Time.deltaTime);
    }

    void SetupAgent()
    {
        if (agent == null)
        {
            return;
        }

        agent.speed = GetModifiedEnemyStat(StatusEffectStat.EnemyMoveSpeed, GetWalkMoveSpeed());
        agent.stoppingDistance = GetModifiedEnemyStat(StatusEffectStat.EnemyStoppingDistance, stoppingDistance);
        agent.updateRotation = true;
    }

    void ApplyEnemyDataSettings()
    {
        if (!useEnemyDataSettings || enemy == null || enemy.enemyData == null)
        {
            return;
        }

        EnemyData data = enemy.enemyData;
        autoAddNavMeshAgent = data.autoAddNavMeshAgent;
        useRandomPatrolWhenNoPoints = data.useRandomPatrolWhenNoPoints;
        randomPatrolRadius = data.randomPatrolRadius;
        detectionRange = data.detectionRange;
        loseTargetRange = data.loseTargetRange;
        shotAlertRangeMultiplier = data.shotAlertRangeMultiplier;
        shotAlertShareRadius = data.shotAlertShareRadius;
        shotAlertMinDuration = data.shotAlertMinDuration;
        shotAlertMaxDuration = data.shotAlertMaxDuration;
        idleDuration = data.idleDuration;
        patrolSpeed = data.patrolSpeed;
        walkSpeedMultiplier = data.walkSpeedMultiplier;
        chaseSpeed = data.chaseSpeed;
        stoppingDistance = data.stoppingDistance;
        waypointReachDistance = data.waypointReachDistance;
        faceMoveDirection = data.faceMoveDirection;
        rotationSpeed = data.rotationSpeed;
        useSupportBehavior = data.useSupportBehavior || data.enemyType == EnemyType.Support;
        supportAllySearchRadius = data.supportAllySearchRadius;
        supportBehindAllyDistance = data.supportBehindAllyDistance;
        supportFrontlineBuffer = data.supportFrontlineBuffer;
        supportFleeDistanceFromPlayer = data.supportFleeDistanceFromPlayer;
        supportRepositionOnDamageDuration = data.supportRepositionOnDamageDuration;
        supportDestinationRefreshInterval = data.supportDestinationRefreshInterval;
    }

    void UpdateVoice()
    {
        if (enemy == null || enemy.enemyData == null || Time.time < nextVoiceTime)
        {
            return;
        }

        bool aggressive = currentState == EnemyAIState.Chase || currentState == EnemyAIState.Attack;
        PlayVoice(aggressive ? GetAngryGrowlSound() : enemy.enemyData.growlSound);
        ScheduleNextVoice(aggressive);
    }

    void ScheduleNextVoice(bool aggressive)
    {
        if (enemy == null || enemy.enemyData == null)
        {
            nextVoiceTime = Time.time + 1f;
            return;
        }

        EnemyData data = enemy.enemyData;
        float minInterval = aggressive
            ? GetModifiedEnemyStat(StatusEffectStat.EnemyAngryGrowlMinInterval, data.angryGrowlMinInterval)
            : GetModifiedEnemyStat(StatusEffectStat.EnemyGrowlMinInterval, data.growlMinInterval);
        float maxInterval = aggressive
            ? GetModifiedEnemyStat(StatusEffectStat.EnemyAngryGrowlMaxInterval, data.angryGrowlMaxInterval)
            : GetModifiedEnemyStat(StatusEffectStat.EnemyGrowlMaxInterval, data.growlMaxInterval);
        minInterval = Mathf.Max(0.1f, minInterval);
        maxInterval = Mathf.Max(minInterval, maxInterval);
        nextVoiceTime = Time.time + Random.Range(minInterval, maxInterval);
    }

    AudioClip GetAngryGrowlSound()
    {
        if (enemy == null || enemy.enemyData == null)
        {
            return null;
        }

        return enemy.enemyData.angryGrowlSound != null ? enemy.enemyData.angryGrowlSound : enemy.enemyData.growlSound;
    }

    void PlayVoice(AudioClip clip)
    {
        if (clip == null || enemy == null || enemy.enemyData == null)
        {
            return;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
        }

        audioSource.PlayOneShot(clip, Mathf.Clamp01(GetModifiedEnemyStat(StatusEffectStat.EnemyVoiceVolume, enemy.enemyData.voiceVolume)));
    }

    IEnumerator CelebrationRoutine(Transform deadPlayer)
    {
        isCelebrating = true;
        SetLocomotionSuppressed(true);
        StopMoving();

        if (deadPlayer != null)
        {
            RotateToward(deadPlayer.position - transform.position);
        }

        PlayCelebrationAnimation();

        yield return new WaitForSeconds(Mathf.Max(0f, celebrationDuration));

        isCelebrating = false;
        SetLocomotionSuppressed(false);
        celebrationRoutine = null;
        EnterPatrol();
        PlayPatrolAnimation();
    }

    void PlayCelebrationAnimation()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator == null)
        {
            return;
        }

        if (HasAnimatorTrigger(celebrationTriggerName))
        {
            animator.SetTrigger(celebrationTriggerName);
            return;
        }

        if (!CrossFadeAnimatorState(celebrationLayerName, celebrationStateName, celebrationFadeDuration))
        {
            CrossFadeAnimatorState(celebrationLayerName, "Alert", celebrationFadeDuration);
        }
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

    bool CrossFadeAnimatorState(string layerName, string stateName, float fadeDuration)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
        {
            return false;
        }

        int layerIndex = animator.GetLayerIndex(layerName);
        if (layerIndex < 0)
        {
            layerIndex = 0;
        }

        string fullPath = $"{layerName}.{stateName}";
        if (!stateHashes.TryGetValue(fullPath, out int fullPathHash))
        {
            fullPathHash = Animator.StringToHash(fullPath);
            stateHashes[fullPath] = fullPathHash;
        }

        int stateHash = animator.HasState(layerIndex, fullPathHash)
            ? fullPathHash
            : GetCachedAnimatorStateHash(stateName);

        if (animator.HasState(layerIndex, stateHash))
        {
            animator.CrossFade(stateHash, fadeDuration, layerIndex);
            return true;
        }

        return false;
    }

    int GetCachedAnimatorStateHash(string stateName)
    {
        if (string.IsNullOrEmpty(stateName))
        {
            return 0;
        }

        if (!stateHashes.TryGetValue(stateName, out int hash))
        {
            hash = Animator.StringToHash(stateName);
            stateHashes[stateName] = hash;
        }

        return hash;
    }

    void PlayPatrolAnimation()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator == null)
        {
            return;
        }

        if (hasSpeedParameter)
        {
            animator.SetFloat(speedParameter, patrolSpeed);
        }

        if (hasChaseParameter)
        {
            animator.SetBool(chaseParameter, false);
        }

        CrossFadeAnimatorState(patrolLayerName, patrolStateName, patrolFadeDuration);
    }

    void FindPlayer()
    {
        if (cachedPlayerTarget != null && cachedPlayerTarget.gameObject.activeInHierarchy)
        {
            playerTarget = cachedPlayerTarget;
            return;
        }

        cachedPlayerTarget = null;
        if (Time.time < nextMissingPlayerLookupTime)
        {
            return;
        }

        nextMissingPlayerLookupTime = Time.time + Mathf.Max(0.02f, missingPlayerLookupInterval);

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            cachedPlayerTarget = player.transform;
            playerTarget = cachedPlayerTarget;
        }
    }

    void UpdateAnimator(float deltaTime)
    {
        if (animator == null)
        {
            return;
        }

        float speed = 0f;
        bool isMoving = agent == null || !agent.enabled || !agent.isOnNavMesh || agent.velocity.sqrMagnitude > 0.01f;
        if (isMoving && currentState == EnemyAIState.Patrol)
        {
            speed = GetModifiedEnemyStat(StatusEffectStat.EnemyPatrolSpeed, patrolSpeed);
        }
        else if (isMoving && currentState == EnemyAIState.Chase)
        {
            speed = GetModifiedEnemyStat(StatusEffectStat.EnemyChaseSpeed, chaseSpeed);
        }

        if (hasSpeedParameter)
        {
            animator.SetFloat(speedParameter, speed, animatorSpeedDampTime, Mathf.Max(0.001f, deltaTime));
        }

        if (hasChaseParameter)
        {
            animator.SetBool(chaseParameter, currentState == EnemyAIState.Chase || currentState == EnemyAIState.Attack);
        }
    }

    void CacheAnimatorParameters()
    {
        if (animator == null)
        {
            return;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == AnimatorControllerParameterType.Float && parameter.name == speedParameter)
            {
                hasSpeedParameter = true;
            }
            else if (parameter.type == AnimatorControllerParameterType.Bool && parameter.name == chaseParameter)
            {
                hasChaseParameter = true;
            }
        }
    }

    void UpgradeLegacyAnimationConfiguration()
    {
        if (unarmedAttackLayerName == "Attack")
        {
            unarmedAttackLayerName = "Unarmed";
            unarmedAttackStateMachineNames = System.Array.Empty<string>();
            unarmedAttackStateNames = new[]
            {
                "Unarmed-Attack-L1",
                "Unarmed-Attack-R1",
                "Unarmed-Attack-L2",
                "Unarmed-Attack-R2",
                "Unarmed-Attack-L3",
                "Unarmed-Attack-R3"
            };
        }

        if (supportBuffLayerName == "Support" && supportBuffStateName == "Buff")
        {
            supportBuffLayerName = "Armed";
            supportBuffStateName = "Armed-Boost1";
        }
    }

    public void ConfigureLocomotionLayer()
    {
        if (animator == null)
        {
            return;
        }

        bool hasMeleeWeapon = meleeWeaponController != null &&
                              (meleeWeaponController.CurrentWeapon != null || meleeWeaponController.startingWeapon != null);
        bool hasRangedWeapon = rangedWeaponController != null &&
                               (rangedWeaponController.CurrentWeapon != null || rangedWeaponController.startingWeapon != null);

        bool showLocomotion = locomotionSuppressionCount == 0;
        int armedLayer = animator.GetLayerIndex(armedLocomotionLayerName);
        if (armedLayer >= 0)
        {
            animator.SetLayerWeight(armedLayer, showLocomotion && hasMeleeWeapon && !hasRangedWeapon ? 1f : 0f);
        }

        int shootingLayer = animator.GetLayerIndex(shootingLocomotionLayerName);
        if (shootingLayer >= 0)
        {
            animator.SetLayerWeight(shootingLayer, showLocomotion && hasRangedWeapon ? 1f : 0f);
        }

        int unarmedLayer = animator.GetLayerIndex(unarmedLocomotionLayerName);
        if (unarmedLayer >= 0)
        {
            animator.SetLayerWeight(unarmedLayer, showLocomotion && !hasMeleeWeapon && !hasRangedWeapon ? 1f : 0f);
        }
    }

    public void SetLocomotionSuppressed(bool suppressed)
    {
        locomotionSuppressionCount = Mathf.Max(0, locomotionSuppressionCount + (suppressed ? 1 : -1));
        ConfigureLocomotionLayer();
    }

    void FadeUnarmedAttackLayerWeight(float targetWeight)
    {
        int layerIndex = GetUnarmedAttackLayerIndex();
        if (layerIndex <= 0 || animator == null)
        {
            return;
        }

        float currentWeight = animator.GetLayerWeight(layerIndex);
        if (Mathf.Abs(currentWeight - targetWeight) <= 0.001f)
        {
            return;
        }

        if (unarmedAttackFadeRoutine != null && Mathf.Approximately(unarmedAttackFadeTarget, targetWeight))
        {
            return;
        }

        StopUnarmedAttackFade();
        unarmedAttackFadeTarget = targetWeight;
        unarmedAttackFadeRoutine = StartCoroutine(FadeAnimatorLayerWeight(layerIndex, targetWeight, attackLayerFadeOut));
    }

    void FadeSupportBuffLayerWeight(float targetWeight)
    {
        int layerIndex = GetSupportBuffLayerIndex();
        if (layerIndex <= 0 || animator == null)
        {
            return;
        }

        float currentWeight = animator.GetLayerWeight(layerIndex);
        if (Mathf.Abs(currentWeight - targetWeight) <= 0.001f)
        {
            return;
        }

        if (supportBuffFadeRoutine != null && Mathf.Approximately(supportBuffFadeTarget, targetWeight))
        {
            return;
        }

        StopSupportBuffFade();
        supportBuffFadeTarget = targetWeight;
        supportBuffFadeRoutine = StartCoroutine(FadeAnimatorLayerWeight(layerIndex, targetWeight, supportBuffLayerFadeOut));
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

    void StopUnarmedAttackFade()
    {
        if (unarmedAttackFadeRoutine == null)
        {
            return;
        }

        StopCoroutine(unarmedAttackFadeRoutine);
        unarmedAttackFadeRoutine = null;
        unarmedAttackFadeTarget = -1f;
    }

    void StopSupportBuffFade()
    {
        if (supportBuffFadeRoutine == null)
        {
            return;
        }

        StopCoroutine(supportBuffFadeRoutine);
        supportBuffFadeRoutine = null;
        supportBuffFadeTarget = -1f;
    }
}
