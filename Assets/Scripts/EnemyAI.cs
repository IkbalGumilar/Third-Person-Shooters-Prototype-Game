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
    const float DefaultPatrolSpeed = 2f;
    const float DefaultWalkSpeedMultiplier = 0.6f;
    const float DefaultChaseSpeed = 4f;
    const float DefaultPatrolSpeedMultiplier = 1.35f;
    const float DefaultCrossbowPatrolSpeedMultiplier = 1.85f;
    const float DefaultSpearPatrolSpeedMultiplier = 1.85f;
    const float DefaultMaxPatrolSpeedRelativeToChase = 0.75f;
    const float AttackEnterRangeMultiplier = 0.9f;
    const float AttackExitRangeMultiplier = 1.1f;

    private static readonly List<EnemyAI> activeEnemies = new List<EnemyAI>();
    private static Transform cachedPlayerTarget;
    private static float nextMissingPlayerLookupTime;

    public static IReadOnlyList<EnemyAI> ActiveEnemies => activeEnemies;

    public Transform playerTarget;
    public EnemyAIState currentState = EnemyAIState.Idle;
    public Transform[] patrolPoints;
    public Animator animator;
    public EnemyMeleeWeaponController meleeWeaponController;
    public EnemyRangedWeaponController rangedWeaponController;

    // Runtime cache populated from EnemyData. Animator mapping stays here
    // because every enemy shares the same controller and it is not per-type data.
    private bool autoAddNavMeshAgent = true;
    private bool useRandomPatrolWhenNoPoints = true;
    private float randomPatrolRadius = 6f;
    private float detectionRange = 10f;
    private float loseTargetRange = 14f;
    private float shotAlertRangeMultiplier = 3f;
    private float shotAlertShareRadius = 5f;
    private float shotAlertMinDuration = 5f;
    private float shotAlertMaxDuration = 60f;
    private float idleDuration = 2f;
    private float stoppingDistance = 1.5f;
    private float waypointReachDistance = 0.6f;
    private float patrolNavMeshSampleRadius = 2f;
    private int randomPatrolSampleAttempts = 6;
    private float agentDestinationUpdateInterval = 0.12f;
    private float agentDestinationChangeThreshold = 0.15f;
    private bool faceMoveDirection = true;
    private float rotationSpeed = 8f;
    private string speedParameter = "Speed";
    private string chaseParameter = "IsChasing";
    private string armedLocomotionLayerName = "Armed-Locomotion";
    private string unarmedLocomotionLayerName = "Unarmed-Locomotion";
    private string shootingLocomotionLayerName = "2Hand-Shooting-Locomotion";
    private string crossbowLocomotionLayerName = "2Hand-Crossbow-Locomotion";
    private string crossbowIdleStateName = "2Hand-Crossbow-Idle-Static";
    private string crossbowWalkStateName = "2Hand-Crossbow-Walk";
    private string crossbowRunStateName = "2Hand-Crossbow-Run-Forward";
    private string spearLocomotionLayerName = "2Hand-Spear-Locomotion";
    private string spearIdleStateName = "2Hand-Spear-Idle-Static";
    private string spearWalkStateName = "2Hand-Spear-Walk";
    private string spearRunStateName = "2Hand-Spear-Run-Forward";
    private float weaponLocomotionCrossFade = 0.16f;  // Increased from 0.12f for smoother patrol blending
    private string unarmedAttackLayerName = "Unarmed";
    private string[] unarmedAttackStateMachineNames = System.Array.Empty<string>();
    private string[] unarmedAttackStateNames = { "Unarmed-Attack-L1", "Unarmed-Attack-R1", "Unarmed-Attack-L2", "Unarmed-Attack-R2", "Unarmed-Attack-L3", "Unarmed-Attack-R3" };
    private float unarmedAttackCrossFade = 0.08f;
    private string celebrationTriggerName = "Celebration";
    private string celebrationStateName = "Celebration";
    private string celebrationLayerName = "Base Layer";
    private float celebrationFadeDuration = 0.1f;
    private float celebrationDuration = 2f;
    private string patrolStateName = "Patroli";
    private string patrolLayerName = "Base Layer";
    private float patrolFadeDuration = 0.15f;
    private float animatorSpeedDampTime = 0.12f;
    private float attackLayerFadeOut = 0.12f;
    private bool useSupportBehavior;
    private string supportBuffLayerName = "Armed";
    private string supportBuffStateName = "Armed-Boost1";
    private float supportBuffFadeDuration = 0.08f;
    private float supportBuffDuration = 1.2f;
    private float supportBuffLayerFadeOut = 0.12f;
    private float supportAllySearchRadius = 14f;
    private float supportBehindAllyDistance = 3f;
    private float supportFrontlineBuffer = 1.25f;
    private float supportFleeDistanceFromPlayer = 20f;
    private float supportRepositionOnDamageDuration = 3f;
    private float supportDestinationRefreshInterval = 0.35f;
    private float stateUpdateInterval = 0.15f;
    private float stateUpdateRandomOffset = 0.05f;
    private float activeBehaviourUpdateInterval = 0.03f;
    private float passiveBehaviourUpdateInterval = 0.1f;
    private float missingPlayerLookupInterval = 0.25f;

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
    private float fallbackStoppingDistance;
    private AudioSource audioSource;
    private float nextVoiceTime;
    private bool ignorePlayerTarget;
    private bool isCelebrating;
    private Coroutine celebrationRoutine;
    private Coroutine alertRoutine;
    private bool isAlerting;
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
    private bool persistentChaseTarget;
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
    private int activeWeaponLocomotionLayer = -1;
    private int activeWeaponLocomotionStateHash;
    private float smoothedAnimatorSpeed;
    private float animatorSpeedVelocity;

    void Awake()
    {
        enemy = GetComponent<Enemy>();
        statusController = GetComponent<EnemyStatusEffectController>();
        ApplyEnemyDataSettings();
        agent = GetComponent<NavMeshAgent>();
        if (agent == null && autoAddNavMeshAgent && IsOnNavMeshAtSpawnPosition())
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

    private bool IsOnNavMeshAtSpawnPosition()
    {
        return NavMesh.SamplePosition(transform.position, out _, 0.1f, NavMesh.AllAreas);
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
        alertRoutine = null;
        isAlerting = false;
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
        persistentChaseTarget = false;
        locomotionSuppressionCount = 0;
        activeWeaponLocomotionLayer = -1;
        activeWeaponLocomotionStateHash = 0;
        smoothedAnimatorSpeed = 0f;
        animatorSpeedVelocity = 0f;
        lastBehaviourUpdateTime = Time.time;
        nextBehaviourUpdateTime = Time.time + Random.Range(0f, Mathf.Max(0.02f, passiveBehaviourUpdateInterval));
        ConfigureLocomotionLayer();
        ScheduleNextVoice(false);
    }

    void OnDisable()
    {
        activeEnemies.Remove(this);
        if (enemy != null && enemy.IsDead)
        {
            StopForDeathAnimation();
            return;
        }

        StopMoving();
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

        if (isAlerting)
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
        }

        UpdateFallbackMovement(Time.deltaTime);

        // Navigation decisions are throttled for performance, but Animator
        // parameters must be refreshed every frame to keep blending smooth.
        UpdateAnimator(Time.deltaTime);
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
        if (!persistentChaseTarget && (currentState == EnemyAIState.Chase || currentState == EnemyAIState.Attack) && distanceToPlayerSqr > loseTargetRange * loseTargetRange)
        {
            EnterIdle();
        }
    }

    public void SetPersistentChaseTarget(Transform target)
    {
        if (target == null)
        {
            return;
        }

        ignorePlayerTarget = false;
        isCelebrating = false;
        playerTarget = target;
        persistentChaseTarget = true;
        shotAlertUntilTime = Mathf.Infinity;
        EnterChase();
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

        SetRangedAimTargetVisible(true);
        float attackRange = GetAttackRange();
        float enterAttackRange = attackRange * AttackEnterRangeMultiplier;
        if ((transform.position - playerTarget.position).sqrMagnitude <= enterAttackRange * enterAttackRange)
        {
            EnterAttack();
            return;
        }

        SetMoveSpeed(GetChaseMoveSpeed());
        MoveTo(playerTarget.position, GetModifiedEnemyStat(StatusEffectStat.EnemyStoppingDistance, stoppingDistance));
    }

    void UpdateAttack()
    {
        if (playerTarget == null)
        {
            EnterIdle();
            return;
        }

        SetRangedAimTargetVisible(true);
        float attackRange = GetAttackRange();
        float exitAttackRange = attackRange * AttackExitRangeMultiplier;
        if ((transform.position - playerTarget.position).sqrMagnitude > exitAttackRange * exitAttackRange)
        {
            EnterChase();
            return;
        }

        if (rangedWeaponController != null
            && rangedWeaponController.CurrentWeapon != null
            && !rangedWeaponController.CanHoldAttackPosition(playerTarget))
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

        SetMoveSpeed(GetChaseMoveSpeed());

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
        SetRangedAimTargetVisible(false);
        currentState = EnemyAIState.Idle;
        idleTimer = GetModifiedEnemyStat(StatusEffectStat.EnemyIdleDuration, idleDuration);
        hasDestination = false;
        StopMoving();
        RequestImmediateBehaviourUpdate();
    }

    void EnterPatrol()
    {
        SetRangedAimTargetVisible(false);
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
        SetRangedAimTargetVisible(true);
        hasDestination = false;
        RequestImmediateBehaviourUpdate();

        if (!wasAggressive)
        {
            PlayVoice(GetAngryGrowlSound());
            TryStartAlertAnimation();
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
        SetRangedAimTargetVisible(true);
        hasDestination = false;
        StopMoving();
        RequestImmediateBehaviourUpdate();
    }

    void SetRangedAimTargetVisible(bool visible)
    {
        if (rangedWeaponController == null || rangedWeaponController.CurrentWeapon == null)
        {
            return;
        }

        rangedWeaponController.SetAimTargetVisible(playerTarget, visible);
    }

    void TryStartAlertAnimation()
    {
        if (!TryGetAlertAnimation(out string layerName, out string stateName, out float duration) || animator == null)
        {
            return;
        }

        int layerIndex = animator.GetLayerIndex(layerName);
        int stateHash = layerIndex > 0 ? GetAnimatorStateHash(layerIndex, layerName, stateName) : 0;
        if (stateHash == 0)
        {
            return;
        }

        if (alertRoutine != null)
        {
            StopCoroutine(alertRoutine);
        }

        alertRoutine = StartCoroutine(AlertAnimationRoutine(layerIndex, stateHash, duration));
    }

    bool TryGetAlertAnimation(out string layerName, out string stateName, out float duration)
    {
        layerName = null;
        stateName = null;
        duration = 0f;
        EnemyAnimationLayerData[] layers = enemy != null && enemy.enemyData != null ? enemy.enemyData.animationLayers : null;
        if (layers == null)
        {
            return false;
        }

        for (int i = 0; i < layers.Length; i++)
        {
            EnemyAnimationLayerData layer = layers[i];
            if (layer == null || string.IsNullOrEmpty(layer.layerName) || string.IsNullOrEmpty(layer.alertStateName))
            {
                continue;
            }

            layerName = layer.layerName;
            stateName = layer.alertStateName;
            duration = Mathf.Max(0f, layer.alertDuration);
            return true;
        }

        return false;
    }

    IEnumerator AlertAnimationRoutine(int layerIndex, int stateHash, float duration)
    {
        isAlerting = true;
        SetLocomotionSuppressed(true);
        StopMoving();
        EnemyAnimationLayers.SetExclusiveLayer(animator, layerIndex);
        animator.CrossFadeInFixedTime(stateHash, 0.08f, layerIndex);

        yield return new WaitForSeconds(Mathf.Max(0f, duration));

        isAlerting = false;
        alertRoutine = null;
        SetLocomotionSuppressed(false);
    }

    void SetNextPatrolDestination()
    {
        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                Transform point = patrolPoints[patrolIndex % patrolPoints.Length];
                patrolIndex++;
                if (point != null && TrySetPatrolDestination(point.position))
                {
                    return;
                }
            }
        }

        if (useRandomPatrolWhenNoPoints)
        {
            float radius = GetModifiedEnemyStat(StatusEffectStat.EnemyRandomPatrolRadius, randomPatrolRadius);
            int attempts = Mathf.Clamp(randomPatrolSampleAttempts, 1, 12);
            for (int i = 0; i < attempts; i++)
            {
                Vector2 random = Random.insideUnitCircle * radius;
                if (TrySetPatrolDestination(spawnPosition + new Vector3(random.x, 0f, random.y)))
                {
                    return;
                }
            }
        }
    }

    bool TrySetPatrolDestination(Vector3 requestedDestination)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            SetDestination(requestedDestination);
            return true;
        }

        float sampleRadius = Mathf.Max(0.1f, patrolNavMeshSampleRadius);
        if (!NavMesh.SamplePosition(requestedDestination, out NavMeshHit hit, sampleRadius, agent.areaMask))
        {
            return false;
        }

        SetDestination(hit.position);
        return true;
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

        currentDestination = destination;
        hasDestination = true;
        fallbackStoppingDistance = Mathf.Max(0f, stopDistance);
    }

    void UpdateFallbackMovement(float deltaTime)
    {
        if ((agent != null && agent.enabled && agent.isOnNavMesh) || !hasDestination)
        {
            return;
        }

        if (currentState != EnemyAIState.Patrol && currentState != EnemyAIState.Chase)
        {
            return;
        }

        MoveFallbackToward(currentDestination, fallbackStoppingDistance, deltaTime);
    }

    void MoveFallbackToward(Vector3 destination, float stopDistance, float deltaTime)
    {
        Vector3 direction = destination - transform.position;
        direction.y = 0f;
        float distanceSqr = direction.sqrMagnitude;
        if (distanceSqr <= stopDistance * stopDistance)
        {
            return;
        }

        float distance = Mathf.Sqrt(distanceSqr);
        Vector3 move = direction.normalized * GetMoveSpeed() * Mathf.Max(0f, deltaTime);
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
        hasDestination = false;

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
            ? GetChaseMoveSpeed()
            : GetWalkMoveSpeed();
        return GetModifiedEnemyStat(StatusEffectStat.EnemyMoveSpeed, speed);
    }

    float GetWalkMoveSpeed()
    {
        float walkSpeed = GetModifiedEnemyStat(StatusEffectStat.EnemyPatrolSpeed, GetBasePatrolSpeed());
        float desiredSpeed = walkSpeed
            * Mathf.Clamp(GetWalkSpeedMultiplier(), 0.1f, 1f)
            * Mathf.Clamp(GetPatrolSpeedMultiplier(), 0.5f, 2f)
            * GetWeaponPatrolSpeedMultiplier();
        float chaseSpeedLimit = GetChaseMoveSpeed()
            * Mathf.Clamp(GetMaxPatrolSpeedRelativeToChase(), 0.1f, 1f);
        return Mathf.Min(desiredSpeed, chaseSpeedLimit);
    }

    float GetBasePatrolSpeed()
    {
        return enemy != null && enemy.enemyData != null ? enemy.enemyData.patrolSpeed : DefaultPatrolSpeed;
    }

    float GetWalkSpeedMultiplier()
    {
        return enemy != null && enemy.enemyData != null ? enemy.enemyData.walkSpeedMultiplier : DefaultWalkSpeedMultiplier;
    }

    float GetChaseMoveSpeed()
    {
        float baseSpeed = enemy != null && enemy.enemyData != null ? enemy.enemyData.chaseSpeed : DefaultChaseSpeed;
        return GetModifiedEnemyStat(StatusEffectStat.EnemyChaseSpeed, baseSpeed);
    }

    float GetPatrolSpeedMultiplier()
    {
        return enemy != null && enemy.enemyData != null
            ? enemy.enemyData.patrolSpeedMultiplier
            : DefaultPatrolSpeedMultiplier;
    }

    float GetMaxPatrolSpeedRelativeToChase()
    {
        return enemy != null && enemy.enemyData != null
            ? enemy.enemyData.maxPatrolSpeedRelativeToChase
            : DefaultMaxPatrolSpeedRelativeToChase;
    }

    float GetWeaponPatrolSpeedMultiplier()
    {
        EnemyRangedWeapon rangedWeapon = rangedWeaponController != null
            ? rangedWeaponController.CurrentWeapon ?? rangedWeaponController.startingWeapon
            : null;
        if (rangedWeapon != null && rangedWeapon.weaponKind == EnemyRangedWeaponKind.Crossbow)
        {
            float multiplier = enemy != null && enemy.enemyData != null
                ? enemy.enemyData.crossbowPatrolSpeedMultiplier
                : DefaultCrossbowPatrolSpeedMultiplier;
            return Mathf.Max(0.1f, multiplier);
        }

        EnemyMeleeWeapon meleeWeapon = meleeWeaponController != null
            ? meleeWeaponController.CurrentWeapon ?? meleeWeaponController.startingWeapon
            : null;
        if (meleeWeapon != null && meleeWeapon.category == EnemyMeleeWeaponCategory.Spear && meleeWeapon.holdType == WeaponHoldType.TwoHand)
        {
            float multiplier = enemy != null && enemy.enemyData != null
                ? enemy.enemyData.spearPatrolSpeedMultiplier
                : DefaultSpearPatrolSpeedMultiplier;
            return Mathf.Max(0.1f, multiplier);
        }

        return 1f;
    }

    float GetAttackRange()
    {
        if (rangedWeaponController != null && rangedWeaponController.CurrentWeapon != null)
        {
            return rangedWeaponController.EffectiveAttackRange;
        }

        if (meleeWeaponController != null && meleeWeaponController.CurrentWeapon != null)
        {
            return meleeWeaponController.EffectiveAttackRange;
        }

        if (enemy != null && enemy.enemyData != null)
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
        StopSupportBuffFade();
        EnemyAnimationLayers.SetExclusiveLayer(animator, layerIndex);

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
        currentState = EnemyAIState.Idle;
        hasDestination = false;
        hasAgentDestination = false;
        smoothedAnimatorSpeed = 0f;
        animatorSpeedVelocity = 0f;

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

        if (alertRoutine != null)
        {
            StopCoroutine(alertRoutine);
            alertRoutine = null;
        }

        isAlerting = false;
        ForceStopLocomotionAnimation();

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

        ForceStopLocomotionAnimation();
    }

    void ForceStopLocomotionAnimation()
    {
        locomotionSuppressionCount = 1;

        if (animator == null)
        {
            return;
        }

        if (hasSpeedParameter)
        {
            animator.SetFloat(speedParameter, 0f);
        }

        if (hasChaseParameter)
        {
            animator.SetBool(chaseParameter, false);
        }

        if (activeWeaponLocomotionLayer > 0)
        {
            animator.SetLayerWeight(activeWeaponLocomotionLayer, 0f);
        }

        activeWeaponLocomotionLayer = -1;
        activeWeaponLocomotionStateHash = 0;
        EnemyAnimationLayers.SetExclusiveLayer(animator, -1);
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
        if (rangedWeaponController != null && rangedWeaponController.CurrentWeapon != null)
        {
            rangedWeaponController.TryAttack(playerTarget);
            return;
        }

        if (meleeWeaponController != null)
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
        if (target == null || enemy == null || enemy.enemyData == null)
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
        EnemyAnimationLayers.SetExclusiveLayer(animator, GetUnarmedAttackLayerIndex());
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
        if (enemy == null || enemy.enemyData == null)
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
        agentDestinationUpdateInterval = data.agentDestinationUpdateInterval;
        agentDestinationChangeThreshold = data.agentDestinationChangeThreshold;
        stoppingDistance = data.stoppingDistance;
        waypointReachDistance = data.waypointReachDistance;
        patrolNavMeshSampleRadius = data.patrolNavMeshSampleRadius;
        randomPatrolSampleAttempts = data.randomPatrolSampleAttempts;
        faceMoveDirection = data.faceMoveDirection;
        rotationSpeed = data.rotationSpeed;
        ApplyUnarmedAttackProfile(data.animationLayers);
        useSupportBehavior = data.useSupportBehavior || data.enemyType == EnemyType.Support;
        supportAllySearchRadius = data.supportAllySearchRadius;
        supportBehindAllyDistance = data.supportBehindAllyDistance;
        supportFrontlineBuffer = data.supportFrontlineBuffer;
        supportFleeDistanceFromPlayer = data.supportFleeDistanceFromPlayer;
        supportRepositionOnDamageDuration = data.supportRepositionOnDamageDuration;
        supportDestinationRefreshInterval = data.supportDestinationRefreshInterval;
        stateUpdateInterval = data.stateUpdateInterval;
        stateUpdateRandomOffset = data.stateUpdateRandomOffset;
        activeBehaviourUpdateInterval = data.activeBehaviourUpdateInterval;
        passiveBehaviourUpdateInterval = data.passiveBehaviourUpdateInterval;
        missingPlayerLookupInterval = data.missingPlayerLookupInterval;
    }

    void ApplyUnarmedAttackProfile(EnemyAnimationLayerData[] layers)
    {
        EnemyAnimationLayerData attackLayer = null;
        if (layers != null)
        {
            for (int i = 0; i < layers.Length; i++)
            {
                EnemyAnimationLayerData layer = layers[i];
                if (layer != null && layer.actionType == EnemyAnimationActionType.Unarmed && !string.IsNullOrEmpty(layer.layerName) && layer.attackStateNames != null && layer.attackStateNames.Length > 0)
                {
                    attackLayer = layer;
                    break;
                }
            }
        }

        if (attackLayer == null)
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
            unarmedAttackCrossFade = 0.08f;
            unarmedAttackLayerIndex = -1;
            return;
        }

        unarmedAttackLayerName = attackLayer.layerName;
        unarmedAttackStateMachineNames = System.Array.Empty<string>();
        unarmedAttackStateNames = attackLayer.attackStateNames;
        unarmedAttackCrossFade = Mathf.Max(0f, attackLayer.attackCrossFade);
        unarmedAttackLayerIndex = -1;
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

        bool isMoving = HasLocomotionMovementIntent();

        if (hasSpeedParameter)
        {
            float speed = GetSmoothedAgentSpeed(isMoving, deltaTime);
            animator.SetFloat(speedParameter, speed);
        }

        if (hasChaseParameter)
        {
            animator.SetBool(chaseParameter, currentState == EnemyAIState.Chase || currentState == EnemyAIState.Attack);
        }

        // Reclaim locomotion after any finished reaction or weapon action.
        // The guard refuses this low-priority request while an action still owns a layer.
        if (locomotionSuppressionCount == 0)
        {
            ConfigureLocomotionLayer();
            // UpdateWeaponLocomotionState will be called via ConfigureLocomotionLayer restoration
            UpdateWeaponLocomotionState(isMoving);
        }
        else
        {
            // Ensure suppressed locomotion doesn't have stale weights
            if (activeWeaponLocomotionLayer > 0 && animator != null)
            {
                if (animator.GetLayerWeight(activeWeaponLocomotionLayer) > 0.01f)
                {
                    animator.SetLayerWeight(activeWeaponLocomotionLayer, 0f);
                }
            }
        }
    }

    float GetSmoothedAgentSpeed(bool isMoving, float deltaTime)
    {
        float targetSpeed = 0f;
        if (isMoving)
        {
            if (currentState == EnemyAIState.Patrol)
            {
                targetSpeed = GetWalkMoveSpeed();
            }
            else if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                targetSpeed = agent.speed;
            }
            else
            {
                targetSpeed = GetMoveSpeed();
            }
        }

        float smoothingTime = Mathf.Max(0.03f, animatorSpeedDampTime);
        float safeDeltaTime = Mathf.Max(0.008f, deltaTime);
        
        smoothedAnimatorSpeed = Mathf.SmoothDamp(
            smoothedAnimatorSpeed,
            targetSpeed,
            ref animatorSpeedVelocity,
            smoothingTime,
            Mathf.Infinity,
            safeDeltaTime);
        
        return smoothedAnimatorSpeed;
    }

    bool HasLocomotionMovementIntent()
    {
        if (currentState == EnemyAIState.Attack || currentState == EnemyAIState.Idle || isAlerting || isCelebrating || IsStunned)
        {
            return false;
        }

        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return currentState == EnemyAIState.Patrol ? hasDestination : currentState == EnemyAIState.Chase;
        }

        if (agent.isStopped)
        {
            return false;
        }

        // Keep locomotion active while NavMesh briefly decelerates or rebuilds
        // a path. Using raw velocity here made walk/idle alternate frame by frame.
        if (agent.pathPending)
        {
            return currentState == EnemyAIState.Patrol ? hasDestination : currentState == EnemyAIState.Chase;
        }

        float stopBuffer = Mathf.Max(0.05f, agent.stoppingDistance + 0.05f);
        return agent.hasPath && agent.remainingDistance > stopBuffer;
    }

    void UpdateWeaponLocomotionState(bool isMoving)
    {
        if (animator == null)
        {
            return;
        }

        if (TryGetProfileLocomotionState(isMoving, out string profileLayerName, out string profileStateName, out float profileCrossFade) &&
            PlayLocomotionState(profileLayerName, profileStateName, profileCrossFade))
        {
            return;
        }

        string layerName = null;
        string stateName = null;
        float crossFadeDuration = weaponLocomotionCrossFade;

        EnemyRangedWeapon rangedWeapon = rangedWeaponController != null
            ? rangedWeaponController.CurrentWeapon ?? rangedWeaponController.startingWeapon
            : null;
        if (rangedWeapon != null && rangedWeapon.weaponKind == EnemyRangedWeaponKind.Crossbow)
        {
            layerName = crossbowLocomotionLayerName;
            stateName = isMoving
                ? (currentState == EnemyAIState.Chase ? crossbowRunStateName : crossbowWalkStateName)
                : crossbowIdleStateName;
            // Increase crossfade for smoother patrol transitions
            crossFadeDuration = Mathf.Max(weaponLocomotionCrossFade, 0.15f);
        }
        else
        {
            EnemyMeleeWeapon meleeWeapon = meleeWeaponController != null
                ? meleeWeaponController.CurrentWeapon ?? meleeWeaponController.startingWeapon
                : null;
            if (meleeWeapon != null && meleeWeapon.category == EnemyMeleeWeaponCategory.Spear && meleeWeapon.holdType == WeaponHoldType.TwoHand)
            {
                layerName = spearLocomotionLayerName;
                stateName = isMoving
                    ? (currentState == EnemyAIState.Chase ? spearRunStateName : spearWalkStateName)
                    : spearIdleStateName;
                // Increase crossfade for smoother patrol transitions
                crossFadeDuration = Mathf.Max(weaponLocomotionCrossFade, 0.15f);
            }
        }

        if (string.IsNullOrEmpty(layerName) || string.IsNullOrEmpty(stateName))
        {
            // Clear invalid weapon layer
            if (activeWeaponLocomotionLayer > 0 && animator != null)
            {
                animator.SetLayerWeight(activeWeaponLocomotionLayer, 0f);
            }
            activeWeaponLocomotionLayer = -1;
            activeWeaponLocomotionStateHash = 0;
            return;
        }

        PlayLocomotionState(layerName, stateName, crossFadeDuration);
    }

    bool TryGetProfileLocomotionState(bool isMoving, out string layerName, out string stateName, out float crossFade)
    {
        layerName = null;
        stateName = null;
        crossFade = weaponLocomotionCrossFade;

        EnemyAnimationLayerData[] layers = enemy != null && enemy.enemyData != null ? enemy.enemyData.animationLayers : null;
        if (layers == null)
        {
            return false;
        }

        for (int i = 0; i < layers.Length; i++)
        {
            EnemyAnimationLayerData layer = layers[i];
            if (layer == null || string.IsNullOrEmpty(layer.layerName))
            {
                continue;
            }

            string selectedState = isMoving
                ? (currentState == EnemyAIState.Chase ? layer.chaseStateName : layer.patrolStateName)
                : layer.idleStateName;
            if (string.IsNullOrEmpty(selectedState))
            {
                continue;
            }

            layerName = layer.layerName;
            stateName = selectedState;
            crossFade = Mathf.Max(0f, layer.locomotionCrossFade);
            return true;
        }

        return false;
    }

    bool PlayLocomotionState(string layerName, string stateName, float crossFade)
    {
        if (animator == null || string.IsNullOrEmpty(layerName) || string.IsNullOrEmpty(stateName))
        {
            return false;
        }

        int layerIndex = animator.GetLayerIndex(layerName);
        if (layerIndex <= 0)
        {
            return false;
        }

        int stateHash = GetAnimatorStateHash(layerIndex, layerName, stateName);
        if (stateHash == 0)
        {
            return false;
        }

        if (activeWeaponLocomotionLayer == layerIndex && activeWeaponLocomotionStateHash == stateHash)
        {
            // Ensure layer weight is at full strength even if skipping state change
            if (animator.GetLayerWeight(layerIndex) < 0.99f)
            {
                animator.SetLayerWeight(layerIndex, 1f);
            }
            return true;
        }

        // Clear previous weapon layer if different
        if (activeWeaponLocomotionLayer > 0 && activeWeaponLocomotionLayer != layerIndex)
        {
            animator.SetLayerWeight(activeWeaponLocomotionLayer, 0f);
        }

        animator.CrossFadeInFixedTime(stateHash, crossFade, layerIndex);
        animator.SetLayerWeight(layerIndex, 1f);
        activeWeaponLocomotionLayer = layerIndex;
        activeWeaponLocomotionStateHash = stateHash;
        return true;
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

        int locomotionLayer = locomotionSuppressionCount == 0
            ? GetPreferredLocomotionLayerIndex()
            : -1;

        // Enemy layers are full-body overrides. When no reaction/action owns
        // the Animator, restore exactly one locomotion layer and remove stale
        // low-priority overlays left by interrupted actions.
        if (locomotionLayer > 0)
        {
            EnemyAnimationLayers.RestoreLocomotionLayer(animator, locomotionLayer);
            // Ensure weapon locomotion layer is cleared when restoring base locomotion
            if (activeWeaponLocomotionLayer > 0 && activeWeaponLocomotionLayer != locomotionLayer)
            {
                animator.SetLayerWeight(activeWeaponLocomotionLayer, 0f);
                activeWeaponLocomotionLayer = -1;
                activeWeaponLocomotionStateHash = 0;
            }
        }
        else
        {
            EnemyAnimationLayers.SetExclusiveLayer(animator, -1);
            // Clear weapon layer when exclusive mode is active
            if (activeWeaponLocomotionLayer > 0)
            {
                animator.SetLayerWeight(activeWeaponLocomotionLayer, 0f);
                activeWeaponLocomotionLayer = -1;
                activeWeaponLocomotionStateHash = 0;
            }
        }
    }

    int GetPreferredLocomotionLayerIndex()
    {
        string profileLayerName = GetProfileLocomotionLayerName();
        if (!string.IsNullOrEmpty(profileLayerName))
        {
            int profileLayerIndex = animator.GetLayerIndex(profileLayerName);
            if (profileLayerIndex > 0)
            {
                return profileLayerIndex;
            }
        }

        bool hasMeleeWeapon = meleeWeaponController != null &&
                              (meleeWeaponController.CurrentWeapon != null || meleeWeaponController.startingWeapon != null);
        bool hasRangedWeapon = rangedWeaponController != null &&
                               (rangedWeaponController.CurrentWeapon != null || rangedWeaponController.startingWeapon != null);

        string preferredLayerName;
        if (hasRangedWeapon)
        {
            EnemyRangedWeapon rangedWeapon = rangedWeaponController.CurrentWeapon != null
                ? rangedWeaponController.CurrentWeapon
                : rangedWeaponController.startingWeapon;
            preferredLayerName = rangedWeapon != null
                ? rangedWeapon.weaponKind switch
                {
                    EnemyRangedWeaponKind.Crossbow => crossbowLocomotionLayerName,
                    EnemyRangedWeaponKind.Shotgun => shootingLocomotionLayerName,
                    _ => armedLocomotionLayerName
                }
                : shootingLocomotionLayerName;
        }
        else if (hasMeleeWeapon)
        {
            EnemyMeleeWeapon meleeWeapon = meleeWeaponController.CurrentWeapon != null
                ? meleeWeaponController.CurrentWeapon
                : meleeWeaponController.startingWeapon;
            preferredLayerName = meleeWeapon != null
                ? meleeWeapon.category switch
                {
                    EnemyMeleeWeaponCategory.SmallAxe => "2Hand-Axe-Locomotion",
                    EnemyMeleeWeaponCategory.GreatSword => "2Hand-Sword-Locomotion",
                    EnemyMeleeWeaponCategory.Spear when meleeWeapon.holdType == WeaponHoldType.TwoHand => spearLocomotionLayerName,
                    _ => armedLocomotionLayerName
                }
                : armedLocomotionLayerName;
        }
        else
        {
            preferredLayerName = unarmedLocomotionLayerName;
        }

        int preferredLayer = animator.GetLayerIndex(preferredLayerName);
        if (preferredLayer >= 0)
        {
            return preferredLayer;
        }

        string fallbackLayerName = hasRangedWeapon
            ? shootingLocomotionLayerName
            : hasMeleeWeapon ? armedLocomotionLayerName : unarmedLocomotionLayerName;
        return animator.GetLayerIndex(fallbackLayerName);
    }

    string GetProfileLocomotionLayerName()
    {
        EnemyAnimationLayerData[] layers = enemy != null && enemy.enemyData != null
            ? enemy.enemyData.animationLayers
            : null;

        if (layers == null)
        {
            return null;
        }

        for (int i = 0; i < layers.Length; i++)
        {
            EnemyAnimationLayerData layer = layers[i];
            if (layer != null && !string.IsNullOrEmpty(layer.layerName))
            {
                return layer.layerName;
            }
        }

        return null;
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
        if (!isActiveAndEnabled)
        {
            animator.SetLayerWeight(layerIndex, targetWeight);
            unarmedAttackFadeTarget = -1f;
            return;
        }

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
        if (!isActiveAndEnabled)
        {
            animator.SetLayerWeight(layerIndex, targetWeight);
            supportBuffFadeTarget = -1f;
            return;
        }

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
