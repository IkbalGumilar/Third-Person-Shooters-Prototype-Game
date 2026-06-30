using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[System.Serializable]
public class FootstepSurfaceAudio
{
    public string surfaceName = "Ground";
    public LayerMask layerMask;
    public AudioClip[] clips;
}

public class PlayerMovement : MonoBehaviour
{
    private const string ReactionLayerOwner = "Player.Reaction";

    private static readonly string[] unarmedMovementLayers =
    {
        "Unarmed-Locomotion",
        "Unarmed-Air",
        "Unarmed-Roll-Dodge",
        "Unarmed-Death-Revive",
        "Unarmed"
    };

    private static readonly string[] armedMovementLayers =
    {
        "Armed-Locomotion",
        "Armed-Air",
        "Armed-Roll-Dodge",
        "Armed-Death-Revive",
        "Armed"
    };

    private static readonly string[] twoHandShootingMovementLayers =
    {
        "2Hand-Shooting-Locomotion",
        "2Hand-Shooting-Air",
        "2Hand-Shooting-Roll-Dodge",
        "2Hand-Shooting-Other",
        "2Hand-Shooting-Death-Revive",
        "2Hand-Shooting"
    };

    private static readonly string[] crawlMovementLayers = { "Crawl" };

    public bool allowInput = true;
    public bool statusBlocksMovement;
    public bool statusBlocksRun;
    public bool statusBlocksJump;
    public bool disableAttachedRigidbodyGravity = true;
    public bool makeAttachedRigidbodyKinematic = true;
    public float moveSpeed = 5f;
    public float runSpeed = 8f;
    public CharacterController controller;
    [Header("Collider Stance")]
    [Min(0.05f)] public float crouchControllerHeight = 0.8f;
    [Min(0.05f)] public float crawlControllerHeight = 0.5f;
    public Transform cameraTransform;
    public float rotationSpeed = 12f;
    public float aimRotationSpeed = 18f;
    public bool rotateToMovementWhenNotAiming = true;
    public float jumpForce = 5f;
    public float gravity = -20f;
    public bool isGrounded;
    public float maxStamina = 100f;
    public float currentStamina = 100f;
    public float runStaminaDrainPerSecond = 20f;
    public float jumpStaminaCost = 18f;
    public float rollStaminaCost = 22f;
    public float staminaRegenPerSecond = 25f;
    public float staminaRegenDelay = 3f;
    [Range(0f, 1f)] public float exhaustedRecoveryPercent = 0.31f;
    [Range(0f, 1f)] public float exhaustedMoveSpeedMultiplier = 0.9f;
    public string baseLayerName = "Base Layer";
    public string jumpStateName = "Unarmed-Jump";
    public string fallStateName = "Unarmed-Fall";
    public string landStateName = "Unarmed-Land";
    public string groundedStateName = "Unarmed Locomotion";
    public string runLocomotionStateName = "Unarmed Run Locomotion";
    public string rollStateName = "Unarmed-Roll-Forward";
    public string crouchLocomotionStateName = "Unarmed Crouch Locomotion";
    public string crawlLocomotionStateName = "Crawl Locomotion";
    [Header("Weapon Locomotion Profiles")]
    public bool useWeaponSpecificLocomotion = true;
    public string unarmedGroundedStateName = "Unarmed Locomotion";
    public string unarmedRunLocomotionStateName = "Unarmed Run Locomotion";
    public string unarmedCrouchLocomotionStateName = "Unarmed Crouch Locomotion";
    public string unarmedCrawlLocomotionStateName = "Crawl Locomotion";
    public string oneHandGroundedStateName = "Armed Locomotion";
    public string oneHandRunLocomotionStateName = "Armed Run Locomotion";
    public string oneHandCrouchLocomotionStateName = "Armed Crouch Locomotion";
    public string oneHandCrawlLocomotionStateName = "Crawl Locomotion";
    public string twoHandGroundedStateName = "2Hand Shooting Locomotion";
    public string twoHandRunLocomotionStateName = "2Hand Shooting Run Locomotion";
    public string twoHandCrouchLocomotionStateName = "2Hand Shooting Crouch Locomotion";
    public string twoHandCrawlLocomotionStateName = "Crawl Locomotion";
    [Header("Weapon Action Profiles")]
    public string unarmedJumpStateName = "Unarmed-Jump";
    public string unarmedFallStateName = "Unarmed-Fall";
    public string unarmedLandStateName = "Unarmed-Land";
    public string unarmedRollForwardStateName = "Unarmed-Roll-Forward";
    public string unarmedRollLeftStateName = "Unarmed-Roll-Left";
    public string unarmedRollRightStateName = "Unarmed-Roll-Right";
    public string unarmedRollBackwardStateName = "Unarmed-Dodge-Backward";
    public string oneHandJumpStateName = "Armed-Jump";
    public string oneHandFallStateName = "Armed-Fall";
    public string oneHandLandStateName = "Armed-Land";
    public string oneHandRollForwardStateName = "Armed-Roll-Forward";
    public string oneHandRollLeftStateName = "Armed-Roll-Left";
    public string oneHandRollRightStateName = "Armed-Roll-Right";
    public string oneHandRollBackwardStateName = "Armed-Roll-Backward";
    public string twoHandJumpStateName = "Shooting-Jump";
    public string twoHandFallStateName = "";
    public string twoHandLandStateName = "Shooting-Land";
    public string twoHandRollForwardStateName = "";
    public string twoHandRollLeftStateName = "Shooting-Roll-Left";
    public string twoHandRollRightStateName = "Shooting-Roll-Right";
    public string twoHandRollBackwardStateName = "Shooting-Roll-Backward";
    public float jumpAnimationFade = 0.1f;
    public float jumpAnimationStartOffset = 0.04f;
    public float jumpToFallDelay = 0.18f;
    public float fallAnimationFade = 0.08f;
    public float minimumJumpAirTime = 0.08f;
    public float landAnimationDuration = 0.45f;
    public float rollAnimationDuration = 0.50f;
    public float rollAnimationFade = 0.02f;
    public float rollExitFade = 0.02f;
    public float rollLandSuppressDuration = 0.2f;
    public float rollInputBufferDuration = 1.1f;
    public float rollDistance = 2.4f;
    [Header("Unarmed Animator")]
    public KeyCode crouchKey = KeyCode.LeftControl;
    public KeyCode crawlKey = KeyCode.Z;
    public KeyCode rollKey = KeyCode.Q;
    [Header("Extra New Input Bindings")]
    public string rollBinding = "<Keyboard>/q";
    public string crawlBinding = "<Keyboard>/z";
    public bool useRawMovementInput = true;
    public float baseLocomotionFade = 0.08f;
    public bool useAnimatorLocomotionTransitions = true;
    public float animatorInputDampTime = 0.1f;
    public float animatorSpeedDampTime = 0.1f;
    [Header("Footstep Audio")]
    public Transform footstepRayOrigin;
    public LayerMask footstepSurfaceMask = ~0;
    public float footstepRayDistance = 1.5f;
    public AudioClip[] defaultFootstepClips;
    public FootstepSurfaceAudio[] footstepSurfaces;
    public float walkStepInterval = 0.45f;
    public float runStepInterval = 0.28f;
    public float footstepVolume = 0.65f;
    public Vector2 footstepPitchRange = new Vector2(0.95f, 1.08f);

    [Header("Hit Reactions")]
    public PlayerShoot playerShoot;
    public PlayerWeaponEquip weaponEquip;
    public PlayerWeaponAnimator weaponAnimator;
    public PlayerAimIK aimIK;
    public PlayerGuardBreak guardBreak;
    public string unarmedReactionLayerName = "Unarmed-Hit";
    public string oneHandReactionLayerName = "Armed-Hit";
    public string twoHandReactionLayerName = "2Hand-Shooting-Hit";
    public string unarmedKnockbackState1 = "Unarmed-Knockback-Back1";
    public string unarmedKnockbackState2 = "Unarmed-Knockback-Back2";
    public string oneHandKnockbackState1 = "Armed-Knockback-Back1";
    public string oneHandKnockbackState2 = "Armed-Knockback-Back2";
    public string twoHandKnockbackState1 = "Shooting-Knockback-Back2";
    public string twoHandKnockbackState2 = "Shooting-Knockback-Back2";
    public float knockbackAnimationFade = 0.06f;
    [Min(0f)] public float knockdownKnockbackDistanceThreshold = 2f;

    // Retained only to migrate old scenes that do not yet have PlayerGuardBreak.
    [HideInInspector] public float guardBreakDuration = 3f;
    [HideInInspector] public float guardBreakInputReduction = 1f;
    [HideInInspector] public float guardBreakInputDelay = 0.1f;
    [HideInInspector] public float guardBreakKnockbackDistance = 1.5f;
    [HideInInspector] public float guardBreakKnockbackDuration = 0.25f;
    [HideInInspector] public float guardBreakGetUpFallbackDuration = 0.8f;
    [HideInInspector] public float guardBreakGetUpMaxDuration = 3f;
    [HideInInspector] public float guardBreakGetUpStartSpeed = 3f;
    [HideInInspector] public float guardBreakGetUpInputSpeedStep = 1f;
    [HideInInspector] public float guardBreakGetUpMaxSpeed = 5f;
    public string unarmedKnockdownState = "Unarmed-Knockdown1";
    public string unarmedGetUpState = "Unarmed-Getup1";
    public string unarmedGetUpLayerName = "Unarmed-Death-Revive";
    public string oneHandKnockdownState = "Armed-Knockdown1";
    public string oneHandGetUpState = "Armed-Getup1";
    public string oneHandGetUpLayerName = "Armed-Death-Revive";
    public string twoHandKnockdownState = "Shooting-Knockdown1";
    public string twoHandGetUpState = "Shooting-Getup1";
    public string twoHandGetUpLayerName = "2Hand-Shooting-Other";
    Animator animator;
    private KontrolPemain kontrolPemain;
    private System.Collections.Generic.Dictionary<string, int> stateHashes = new System.Collections.Generic.Dictionary<string, int>();

    private float verticalVelocity;
    private bool wasGrounded;
    private int baseLayerIndex;
    private float staminaRegenTimer;
    private bool isStaminaExhausted;
    private bool usedStaminaThisFrame;
    private float footstepTimer;
    private AudioClip lastFootstepClip;
    private AudioSource footstepAudioSource;
    private Coroutine baseActionRoutine;
    private Coroutine knockbackRoutine;
    private Coroutine reactionFadeRoutine;
    private string currentBaseStateName;
    private int currentBaseStateLayerIndex = -1;
    private bool currentIsMoving;
    private bool isRollingAction;
    private float suppressLandUntil;
    private bool queuedRollAfterLanding;
    private Vector2 queuedRollInput;
    private float queuedRollExpireTime;
    private float ignoreGroundedUntil;
    private float jumpStartedAt = -1f;
    private bool fallAnimationStarted;
    private bool isCrouching;
    private bool isCrawling;
    private AnimationLayerGuard animationLayerGuard;
    private int activeReactionLayerIndex = -1;
    private int activeReactionStateHash;
    private float standingControllerHeight;
    private Vector3 standingControllerCenter;

    public bool IsCrouching => isCrouching;
    public bool IsCrawling => isCrawling;
    public float StandingControllerHeight => standingControllerHeight;
    public float CurrentControllerHeight => controller != null ? controller.height : standingControllerHeight;
    private bool hasHorizontalParameter;
    private bool hasVerticalParameter;
    private bool hasSpeedParameter;
    private bool hasIsMovingParameter;
    private bool hasIsRunningParameter;
    private bool hasIsGroundedParameter;
    private bool hasIsCrouchingParameter;
    private bool hasIsCrawlingParameter;
    private bool hasIsRollingParameter;
    private bool hasJumpParameter;
    private bool hasRollParameter;
    private string activeGroundedStateName;
    private string activeRunLocomotionStateName;
    private string activeCrouchLocomotionStateName;
    private string activeCrawlLocomotionStateName;
    private string activeJumpStateName;
    private string activeFallStateName;
    private string activeLandStateName;
    private string activeRollForwardStateName;
    private string activeRollLeftStateName;
    private string activeRollRightStateName;
    private string activeRollBackwardStateName;

    public float CurrentStamina => currentStamina;
    public bool IsStaminaExhausted => isStaminaExhausted;
    public bool IsGuardBroken => guardBreak != null && guardBreak.IsActive;
    public float ExhaustedRecoveryStamina => GetExhaustedRecoveryStamina();
    public bool CanUseStaminaActionPublic => CanUseStaminaAction();

    public void SetEquippedWeapon(Weapon weapon)
    {
        ApplyLocomotionProfile(weapon, true);
    }

    public void ForceStandingStance()
    {
        SetStance(false, false);
    }

    public void SetStance(bool crouching, bool crawling)
    {
        isCrawling = crawling;
        isCrouching = crouching || crawling;
        UpdateControllerStance();
        SetAnimatorBool("IsCrouching", isCrouching);
        SetAnimatorBool("IsCrawling", isCrawling);
    }

    void Awake()
    {
        kontrolPemain = new KontrolPemain();

        if (disableAttachedRigidbodyGravity)
        {
            Rigidbody attachedRigidbody = GetComponent<Rigidbody>();
            if (attachedRigidbody != null)
            {
                attachedRigidbody.useGravity = false;
                if (!attachedRigidbody.isKinematic)
                {
                    attachedRigidbody.linearVelocity = Vector3.zero;
                    attachedRigidbody.angularVelocity = Vector3.zero;
                }
                if (makeAttachedRigidbodyKinematic)
                {
                    attachedRigidbody.isKinematic = true;
                }
            }
        }

        if (controller == null)
        {
            controller = GetComponent<CharacterController>();
        }

        if (controller != null)
        {
            standingControllerHeight = controller.height;
            standingControllerCenter = controller.center;
        }

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator != null)
        {
            AnimationEventReceiver.EnsureOn(animator);
            animationLayerGuard = AnimationLayerGuard.GetOrAdd(animator);
            baseLayerIndex = animator.GetLayerIndex(baseLayerName);
            if (baseLayerIndex < 0)
            {
                baseLayerIndex = 0;
            }

            CacheAnimatorParameters();
        }

        playerShoot = playerShoot != null ? playerShoot : GetComponent<PlayerShoot>();
        weaponEquip = weaponEquip != null ? weaponEquip : GetComponent<PlayerWeaponEquip>();
        weaponAnimator = weaponAnimator != null ? weaponAnimator : GetComponent<PlayerWeaponAnimator>();
        aimIK = aimIK != null ? aimIK : GetComponent<PlayerAimIK>();
        if (guardBreak == null)
        {
            guardBreak = GetComponent<PlayerGuardBreak>();
        }

        if (guardBreak == null)
        {
            guardBreak = gameObject.AddComponent<PlayerGuardBreak>();
            guardBreak.ApplyLegacySettings(
                guardBreakDuration,
                guardBreakInputReduction,
                guardBreakInputDelay,
                guardBreakKnockbackDistance,
                guardBreakKnockbackDuration,
                guardBreakGetUpFallbackDuration,
                guardBreakGetUpMaxDuration,
                guardBreakGetUpStartSpeed,
                guardBreakGetUpInputSpeedStep,
                guardBreakGetUpMaxSpeed
            );
        }

        guardBreak.Initialize(this);

        ApplyLocomotionProfile(null, false);

        if (footstepAudioSource == null)
        {
            footstepAudioSource = GetComponent<AudioSource>();
            if (footstepAudioSource == null)
            {
                footstepAudioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        footstepAudioSource.playOnAwake = false;
        footstepAudioSource.spatialBlend = 0f;
    }

    void OnEnable()
    {
        kontrolPemain?.Enable();
    }

    void OnDisable()
    {
        kontrolPemain?.Disable();
        StopAnimationRoutinesForCleanup();
        guardBreak?.CancelImmediate();
    }

    void OnDestroy()
    {
        kontrolPemain?.Dispose();
    }

    void Update()
    {
        if (IsGuardBroken)
        {
            return;
        }

        if (!allowInput || statusBlocksMovement)
        {
            return;
        }

        usedStaminaThisFrame = false;
        UpdateGroundedState();
        UpdateStanceInput();
        TryRoll();
        Jump();
        UpdateControllerStance();
        Move();
        ApplyGravity();
        UpdateAirAnimation();
        RegenerateStamina();
        UpdateAnimatorGrounded();
    }

    public void ApplyKnockback(Vector3 direction, float distance, float duration)
    {
        if (controller == null || !controller.enabled || distance <= 0f)
        {
            return;
        }

        if (!IsGuardBroken)
        {
            PlayKnockbackAnimation();
        }

        if (knockbackRoutine != null)
        {
            StopCoroutine(knockbackRoutine);
        }

        knockbackRoutine = StartCoroutine(KnockbackRoutine(direction, distance, duration));
    }

    public void TriggerGuardBreak(Vector3 knockbackDirection, bool usesIncomingKnockback)
    {
        guardBreak?.Trigger(knockbackDirection, usesIncomingKnockback);
    }

    public bool MoveActionAlongGround(Vector3 horizontalDisplacement)
    {
        if (controller == null || !controller.enabled)
        {
            return false;
        }

        horizontalDisplacement.y = 0f;
        if (isGrounded)
        {
            float groundedVelocity = Mathf.Min(verticalVelocity, -2f);
            horizontalDisplacement.y = groundedVelocity * Time.deltaTime;
        }

        controller.Move(horizontalDisplacement);
        return true;
    }

    public void StopLocomotionForInputFreeze()
    {
        currentIsMoving = false;
        UpdateFootstepAudio(false, false);

        if (animator == null)
        {
            return;
        }

        SetAnimatorFloat("Horizontal", 0f, 0f);
        SetAnimatorFloat("Vertical", 0f, 0f);
        SetAnimatorFloat("Speed", 0f, 0f);
        SetAnimatorBool("IsMoving", false);
        SetAnimatorBool("IsRunning", false);
        SetAnimatorBool("IsCrouching", isCrouching);
        SetAnimatorBool("IsCrawling", isCrawling);

        if (isGrounded && !isRollingAction && baseActionRoutine == null)
        {
            PlayBaseLayerState(GetBaseLocomotionState(false), baseLocomotionFade, false);
        }
    }

    IEnumerator KnockbackRoutine(Vector3 direction, float distance, float duration)
    {
        Vector3 flatDirection = direction;
        flatDirection.y = 0f;
        if (flatDirection.sqrMagnitude < 0.001f)
        {
            knockbackRoutine = null;
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
            MoveActionAlongGround(flatDirection * stepDistance);
            moved += stepDistance;
            elapsed += Time.deltaTime;
            yield return null;
        }

        MoveActionAlongGround(flatDirection * Mathf.Max(0f, distance - moved));
        knockbackRoutine = null;
        TryTriggerKnockdownAfterKnockback(flatDirection, distance);
    }

    void TryTriggerKnockdownAfterKnockback(Vector3 knockbackDirection, float distance)
    {
        if (IsGuardBroken || guardBreak == null || distance <= knockdownKnockbackDistanceThreshold)
        {
            return;
        }

        PlayerHealth health = GetComponent<PlayerHealth>();
        if (health != null && health.IsDead)
        {
            return;
        }

        guardBreak.Trigger(knockbackDirection, true);
    }

    public void UpdateExternalActionGravity()
    {
        if (controller == null || !controller.enabled)
        {
            return;
        }

        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }
        else if (!controller.isGrounded)
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
    }

    void StopAnimationRoutinesForCleanup()
    {
        StopBaseActionRoutine();
        StopReactionFade();
        if (knockbackRoutine != null)
        {
            StopCoroutine(knockbackRoutine);
            knockbackRoutine = null;
        }

        ReleaseActiveReactionLayer();
        isRollingAction = false;
        SetAnimatorBool("IsRolling", false);
    }

    public void StopBaseActionForExternalAction()
    {
        StopBaseActionRoutine();
        StopReactionFade();
        UpdateFootstepAudio(false, false);
    }

    public bool PlayGuardBreakReaction(bool getUp, out float duration)
    {
        return getUp
            ? PlayReactionState(GetGetUpState(), out duration, true, GetGetUpLayerName())
            : PlayReactionState(GetKnockdownState(), out duration);
    }

    public bool HasGuardBreakReactionFinished()
    {
        return HasActiveReactionFinished();
    }

    public void ClearGuardBreakReaction()
    {
        StopReactionFade();
        ReleaseActiveReactionLayer();
    }

    void PlayKnockbackAnimation()
    {
        StopReactionFade();
        if (!PlayReactionState(GetKnockbackState(), out float duration))
        {
            return;
        }

        aimIK?.SuppressPose(duration);
        reactionFadeRoutine = StartCoroutine(FadeReactionLayerAfter(duration));
    }

    bool PlayReactionState(
        string stateName,
        out float stateDuration,
        bool restartFromBeginning = false,
        string layerNameOverride = null)
    {
        stateDuration = 0f;
        if (animator == null || string.IsNullOrEmpty(stateName))
        {
            return false;
        }

        string layerName = string.IsNullOrEmpty(layerNameOverride)
            ? GetReactionLayerName()
            : layerNameOverride;
        int layerIndex = animator.GetLayerIndex(layerName);
        if (layerIndex < 0)
        {
            return false;
        }

        int stateHash = Animator.StringToHash($"{animator.GetLayerName(layerIndex)}.{stateName}");
        if (!animator.HasState(layerIndex, stateHash))
        {
            return false;
        }

        AnimationLayerPriority priority = IsGuardBroken
            ? AnimationLayerPriority.GuardBreak
            : AnimationLayerPriority.Reaction;
        animationLayerGuard = animationLayerGuard != null
            ? animationLayerGuard
            : AnimationLayerGuard.GetOrAdd(animator);
        if (animationLayerGuard != null && !animationLayerGuard.TryClaim(layerIndex, ReactionLayerOwner, priority))
        {
            return false;
        }

        if (activeReactionLayerIndex >= 0 && activeReactionLayerIndex != layerIndex)
        {
            ReleaseActiveReactionLayer();
        }

        activeReactionLayerIndex = layerIndex;
        activeReactionStateHash = stateHash;
        if (animationLayerGuard != null)
        {
            animationLayerGuard.SetWeight(layerIndex, ReactionLayerOwner, 1f);
        }
        else
        {
            animator.SetLayerWeight(layerIndex, 1f);
        }
        if (restartFromBeginning)
        {
            animator.Play(stateHash, layerIndex, 0f);
        }
        else
        {
            animator.CrossFadeInFixedTime(stateHash, Mathf.Max(0f, knockbackAnimationFade), layerIndex);
        }
        animator.Update(0f);
        AnimatorStateInfo stateInfo = animator.IsInTransition(layerIndex)
            ? animator.GetNextAnimatorStateInfo(layerIndex)
            : animator.GetCurrentAnimatorStateInfo(layerIndex);
        stateDuration = Mathf.Max(0.05f, stateInfo.length);
        return true;
    }

    bool HasActiveReactionFinished()
    {
        if (animator == null || activeReactionLayerIndex < 0 || activeReactionStateHash == 0)
        {
            return true;
        }

        if (animator.IsInTransition(activeReactionLayerIndex))
        {
            return false;
        }

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(activeReactionLayerIndex);
        return stateInfo.fullPathHash == activeReactionStateHash && stateInfo.normalizedTime >= 0.98f;
    }

    IEnumerator FadeReactionLayerAfter(float duration)
    {
        yield return new WaitForSeconds(Mathf.Max(0.05f, duration));
        reactionFadeRoutine = null;
        if (!IsGuardBroken && activeReactionLayerIndex >= 0 && animator != null)
        {
            ReleaseActiveReactionLayer();
        }
    }

    void StopReactionFade()
    {
        if (reactionFadeRoutine != null)
        {
            StopCoroutine(reactionFadeRoutine);
            reactionFadeRoutine = null;
        }
    }

    void ReleaseActiveReactionLayer()
    {
        if (activeReactionLayerIndex >= 0)
        {
            if (animationLayerGuard != null)
            {
                animationLayerGuard.Release(activeReactionLayerIndex, ReactionLayerOwner);
            }
            else if (animator != null)
            {
                animator.SetLayerWeight(activeReactionLayerIndex, 0f);
            }
        }

        activeReactionLayerIndex = -1;
        activeReactionStateHash = 0;
    }

    string GetReactionLayerName()
    {
        Weapon weapon = weaponEquip != null && weaponEquip.CurrentWeapon != null
            ? weaponEquip.CurrentWeapon
            : playerShoot != null ? playerShoot.currentWeapon : null;
        if (weapon == null)
        {
            return unarmedReactionLayerName;
        }

        return weapon.holdType == WeaponHoldType.OneHand
            ? oneHandReactionLayerName
            : twoHandReactionLayerName;
    }

    string GetKnockbackState()
    {
        Weapon weapon = weaponEquip != null && weaponEquip.CurrentWeapon != null
            ? weaponEquip.CurrentWeapon
            : playerShoot != null ? playerShoot.currentWeapon : null;
        bool first = Random.value < 0.5f;
        if (weapon == null)
        {
            return first ? unarmedKnockbackState1 : unarmedKnockbackState2;
        }

        return weapon.holdType == WeaponHoldType.OneHand
            ? first ? oneHandKnockbackState1 : oneHandKnockbackState2
            : first ? twoHandKnockbackState1 : twoHandKnockbackState2;
    }

    string GetKnockdownState()
    {
        Weapon weapon = weaponEquip != null && weaponEquip.CurrentWeapon != null
            ? weaponEquip.CurrentWeapon
            : playerShoot != null ? playerShoot.currentWeapon : null;
        if (weapon == null)
        {
            return unarmedKnockdownState;
        }

        return weapon.holdType == WeaponHoldType.OneHand ? oneHandKnockdownState : twoHandKnockdownState;
    }

    string GetGetUpState()
    {
        Weapon weapon = weaponEquip != null && weaponEquip.CurrentWeapon != null
            ? weaponEquip.CurrentWeapon
            : playerShoot != null ? playerShoot.currentWeapon : null;
        if (weapon == null)
        {
            return unarmedGetUpState;
        }

        return weapon.holdType == WeaponHoldType.OneHand ? oneHandGetUpState : twoHandGetUpState;
    }

    string GetGetUpLayerName()
    {
        Weapon weapon = weaponEquip != null && weaponEquip.CurrentWeapon != null
            ? weaponEquip.CurrentWeapon
            : playerShoot != null ? playerShoot.currentWeapon : null;
        if (weapon == null)
        {
            return unarmedGetUpLayerName;
        }

        return weapon.holdType == WeaponHoldType.OneHand ? oneHandGetUpLayerName : twoHandGetUpLayerName;
    }

    void Move()
    {
        if (isRollingAction)
        {
            UpdateFootstepAudio(false, false);
            return;
        }

        Vector2 gerakInput = GetMovementInput();
        float moveX = gerakInput.x;
        float moveZ = gerakInput.y;
        bool isMoving = Mathf.Abs(moveX) > 0.01f || Mathf.Abs(moveZ) > 0.01f;
        currentIsMoving = isMoving;
        bool wantsToRun = isMoving && !isCrouching && !isCrawling && !statusBlocksRun && IsRunHeld();
        bool isRunning = wantsToRun && CanUseStaminaAction();

        Vector3 camForward = (cameraTransform != null) ? cameraTransform.forward : transform.forward;
        Vector3 camRight = (cameraTransform != null) ? cameraTransform.right : transform.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 move = Vector3.ClampMagnitude(camRight * moveX + camForward * moveZ, 1f);

        bool isAiming = (kontrolPemain != null && kontrolPemain.Pemain.Aim.IsPressed()) || MobileInputBridge.AimHeld;
        if (rotateToMovementWhenNotAiming && isMoving && !isAiming && move.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(move, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                Mathf.Max(0f, rotationSpeed) * Time.deltaTime
            );
        }

        float currentMoveSpeed = isRunning ? runSpeed : moveSpeed;
        if (isCrouching)
        {
            currentMoveSpeed *= 0.55f;
        }
        else if (isCrawling)
        {
            currentMoveSpeed *= 0.35f;
        }

        if (ShouldApplyExhaustedMoveSlow())
        {
            currentMoveSpeed *= exhaustedMoveSpeedMultiplier;
        }

        Vector3 velocity = move * currentMoveSpeed;
        velocity.y = verticalVelocity;
        controller.Move(velocity * Time.deltaTime);

        if (isRunning)
        {
            UseStamina(runStaminaDrainPerSecond * Time.deltaTime);
        }

        UpdateFootstepAudio(isMoving && move.sqrMagnitude > 0.01f, isRunning);

        if (animator != null)
        {
            Vector2 animatorInput = gerakInput;
            if (!useRawMovementInput)
            {
                Vector3 localMove = transform.InverseTransformDirection(move);
                animatorInput = new Vector2(localMove.x, localMove.z);
            }

            float normalizedSpeed = isMoving ? (isRunning ? 2f : 1f) : 0f;
            SetAnimatorFloat("Horizontal", animatorInput.x, animatorInputDampTime);
            SetAnimatorFloat("Vertical", animatorInput.y, animatorInputDampTime);
            SetAnimatorFloat("Speed", normalizedSpeed, animatorSpeedDampTime);
            SetAnimatorBool("IsMoving", isMoving);
            SetAnimatorBool("IsRunning", isRunning);
            SetAnimatorBool("IsCrouching", isCrouching);
            SetAnimatorBool("IsCrawling", isCrawling);
            UpdateBaseLocomotionState(isRunning);
        }
    }

    void Jump()
    {
        if (!isRollingAction && !statusBlocksJump && IsJumpPressedThisFrame() && isGrounded && CanUseStaminaAction() && currentStamina >= jumpStaminaCost)
        {
            BeginJump(true);
        }
    }

    void BeginJump(bool playAnimation)
    {
        isCrawling = false;
        isCrouching = false;
        UseStamina(jumpStaminaCost);
        verticalVelocity = jumpForce;
        isGrounded = false;
        ignoreGroundedUntil = Time.time + Mathf.Max(0f, minimumJumpAirTime);
        jumpStartedAt = Time.time;
        fallAnimationStarted = false;
        StopBaseActionRoutine();
        SetAnimatorBool("IsGrounded", false);
        if (playAnimation)
        {
            ResetAnimatorTrigger("Jump");
            PlayBaseLayerState(ActiveJumpStateName, jumpAnimationFade, true, jumpAnimationStartOffset);
        }
        else
        {
            SetAnimatorTrigger("Jump");
        }
    }

    void TryRoll()
    {
        if (statusBlocksMovement || statusBlocksJump || !IsRollPressedThisFrame())
        {
            return;
        }

        Vector2 rollInput = GetMovementInput();
        if (!isGrounded)
        {
            QueueRollAfterLanding(rollInput);
            return;
        }

        BeginRoll(rollInput, true);
    }

    void QueueRollAfterLanding(Vector2 rollInput)
    {
        queuedRollAfterLanding = true;
        queuedRollInput = NormalizeRollInput(rollInput);
        queuedRollExpireTime = Time.time + Mathf.Max(0.01f, rollInputBufferDuration);
    }

    bool TryConsumeQueuedRollAfterLanding()
    {
        if (!queuedRollAfterLanding)
        {
            return false;
        }

        if (Time.time > queuedRollExpireTime)
        {
            queuedRollAfterLanding = false;
            return false;
        }

        queuedRollAfterLanding = false;
        return BeginRoll(queuedRollInput, true);
    }

    bool BeginRoll(Vector2 rollInput, bool allowBaseActionInterrupt = false)
    {
        if (isRollingAction || !CanUseStaminaAction() || currentStamina < rollStaminaCost)
        {
            return false;
        }

        if (baseActionRoutine != null && !allowBaseActionInterrupt)
        {
            return false;
        }

        rollInput = NormalizeRollInput(rollInput);
        Vector3 rollDirection = GetCameraRelativeDirection(rollInput);

        isCrawling = false;
        isCrouching = false;
        UseStamina(rollStaminaCost);
        StopBaseActionRoutine();
        isRollingAction = true;
        suppressLandUntil = Time.time + rollAnimationDuration + rollLandSuppressDuration;
        verticalVelocity = -2f;
        SetAnimatorBool("IsRolling", true);
        SetAnimatorFloat("Horizontal", rollInput.x, 0f);
        SetAnimatorFloat("Vertical", rollInput.y, 0f);
        string rollAnimationState = GetRollStateName(rollInput);
        baseActionRoutine = StartCoroutine(PlayRollThenGrounded(rollDirection.normalized, rollAnimationState, rollAnimationDuration));
        return true;
    }

    Vector2 GetMovementInput()
    {
        Vector2 keyboardInput = kontrolPemain != null ? kontrolPemain.Pemain.Gerak.ReadValue<Vector2>() : Vector2.zero;
        return MobileInputBridge.MoveInput.sqrMagnitude > 0.0001f ? MobileInputBridge.MoveInput : keyboardInput;
    }

    Vector2 NormalizeRollInput(Vector2 rollInput)
    {
        if (rollInput.sqrMagnitude < 0.01f)
        {
            rollInput = Vector2.up;
        }
        else
        {
            rollInput.Normalize();
        }

        return rollInput;
    }

    Vector3 GetCameraRelativeDirection(Vector2 input)
    {
        Vector3 camForward = cameraTransform != null ? cameraTransform.forward : transform.forward;
        Vector3 camRight = cameraTransform != null ? cameraTransform.right : transform.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 direction = Vector3.ClampMagnitude(camRight * input.x + camForward * input.y, 1f);
        if (direction.sqrMagnitude < 0.01f)
        {
            direction = transform.forward;
        }

        return direction;
    }

    void ApplyGravity()
    {
        if (isRollingAction)
        {
            verticalVelocity = -2f;
            return;
        }

        verticalVelocity += gravity * Time.deltaTime;
    }

    void UseStamina(float amount)
    {
        usedStaminaThisFrame = true;
        currentStamina = Mathf.Max(0f, currentStamina - amount);

        if (currentStamina <= 0f)
        {
            isStaminaExhausted = true;
            staminaRegenTimer = staminaRegenDelay;
        }
    }

    void RegenerateStamina()
    {
        if (usedStaminaThisFrame)
        {
            return;
        }

        if (staminaRegenTimer > 0f)
        {
            staminaRegenTimer -= Time.deltaTime;
            return;
        }

        if (currentStamina < maxStamina)
        {
            currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRegenPerSecond * Time.deltaTime);
        }

        if (HasRecoveredFromExhaustion())
        {
            isStaminaExhausted = false;
        }
    }

    bool ShouldApplyExhaustedMoveSlow()
    {
        return isStaminaExhausted && currentStamina < GetExhaustedRecoveryStamina();
    }

    bool HasRecoveredFromExhaustion()
    {
        return isStaminaExhausted && currentStamina >= GetExhaustedRecoveryStamina();
    }

    float GetExhaustedRecoveryStamina()
    {
        return maxStamina * exhaustedRecoveryPercent;
    }

    bool CanUseStaminaAction()
    {
        return !isStaminaExhausted && currentStamina > 0f;
    }

    public bool TryUseStamina(float amount)
    {
        if (amount <= 0f)
        {
            return CanUseStaminaAction();
        }

        if (!CanUseStaminaAction())
        {
            return false;
        }

        UseStamina(amount);
        return true;
    }

    void UpdateStanceInput()
    {
        if (IsCrawlPressedThisFrame())
        {
            isCrawling = !isCrawling;
            isCrouching = isCrawling || isCrouching;
        }

        if (IsCrouchPressedThisFrame())
        {
            isCrouching = !isCrouching;
            if (!isCrouching)
            {
                isCrawling = false;
            }
        }
    }

    void UpdateControllerStance()
    {
        if (controller == null || standingControllerHeight <= 0f)
        {
            return;
        }

        float targetHeight = isCrawling
            ? crawlControllerHeight
            : isCrouching ? crouchControllerHeight : standingControllerHeight;
        targetHeight = Mathf.Clamp(targetHeight, 0.05f, standingControllerHeight);

        float standingBottom = standingControllerCenter.y - standingControllerHeight * 0.5f;
        Vector3 targetCenter = standingControllerCenter;
        targetCenter.y = standingBottom + targetHeight * 0.5f;

        controller.height = targetHeight;
        controller.center = targetCenter;
    }

    void UpdateGroundedState()
    {
        wasGrounded = isGrounded;
        bool rawGrounded = controller.isGrounded;
        isGrounded = rawGrounded && Time.time >= ignoreGroundedUntil;
        if (isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        if (!wasGrounded && isGrounded && !isRollingAction && Time.time >= suppressLandUntil)
        {
            SetAnimatorBool("IsGrounded", true);
            if (TryConsumeQueuedRollAfterLanding())
            {
                return;
            }

            jumpStartedAt = -1f;
            fallAnimationStarted = false;
            ignoreGroundedUntil = 0f;
            PlayLandThenGrounded();
        }
    }

    void UpdateAirAnimation()
    {
        if (animator == null || isGrounded || isRollingAction || baseActionRoutine != null || fallAnimationStarted)
        {
            return;
        }

        string activeFallState = ActiveFallStateName;
        if (string.IsNullOrEmpty(activeFallState))
        {
            return;
        }

        bool jumpStartFinished = jumpStartedAt < 0f || Time.time - jumpStartedAt >= Mathf.Max(0f, jumpToFallDelay);
        if (!jumpStartFinished || verticalVelocity > 0f)
        {
            return;
        }

        if (!TryResolveAnimatorState(activeFallState, out _, out _))
        {
            return;
        }

        fallAnimationStarted = true;
        PlayBaseLayerState(activeFallState, fallAnimationFade, false);
    }

    void UpdateAnimatorGrounded()
    {
        SetAnimatorBool("IsGrounded", isGrounded);
    }

    void PlayBaseLayerState(string stateName)
    {
        PlayBaseLayerState(stateName, jumpAnimationFade, true, 0f);
    }

    public bool PlayTemporaryBaseState(string stateName, float duration, float fadeDuration)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
        {
            return false;
        }

        if (!TryResolveAnimatorState(stateName, out _, out _))
        {
            return false;
        }

        StopBaseActionRoutine();
        baseActionRoutine = StartCoroutine(PlayBaseStateThenGrounded(stateName, Mathf.Max(0f, duration), Mathf.Max(0f, fadeDuration)));
        return true;
    }

    void PlayBaseLayerState(string stateName, float fadeDuration, bool forceRestart)
    {
        PlayBaseLayerState(stateName, fadeDuration, forceRestart, 0f);
    }

    void PlayBaseLayerState(string stateName, float fadeDuration, bool forceRestart, float fixedTimeOffset)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
        {
            return;
        }

        if (!TryResolveAnimatorState(stateName, out int layerIndex, out int stateHash))
        {
            return;
        }

        if (!forceRestart && currentBaseStateName == stateName && currentBaseStateLayerIndex == layerIndex)
        {
            return;
        }

        if (currentBaseStateLayerIndex > 0 && currentBaseStateLayerIndex != layerIndex)
        {
            animator.SetLayerWeight(currentBaseStateLayerIndex, 0f);
        }

        if (layerIndex > 0)
        {
            animator.SetLayerWeight(layerIndex, 1f);
        }

        currentBaseStateName = stateName;
        currentBaseStateLayerIndex = layerIndex;
        animator.CrossFadeInFixedTime(stateHash, Mathf.Max(0f, fadeDuration), layerIndex, Mathf.Max(0f, fixedTimeOffset));
    }

    bool TryResolveAnimatorState(string stateName, out int layerIndex, out int stateHash)
    {
        layerIndex = -1;
        stateHash = 0;
        if (animator == null || string.IsNullOrEmpty(stateName))
        {
            return false;
        }

        if (TryResolveAnimatorStateOnLayer(baseLayerIndex, baseLayerName, stateName, out stateHash))
        {
            layerIndex = baseLayerIndex;
            return true;
        }

        return TryResolveAnimatorStateOnPreferredLayers(stateName, out layerIndex, out stateHash);
    }

    // Movement actions must never search every Animator layer. The controller
    // contains duplicate clip names, so an unrestricted fallback can activate
    // an unrelated full-body action layer.
    bool TryResolveAnimatorStateOnPreferredLayers(string stateName, out int layerIndex, out int stateHash)
    {
        layerIndex = -1;
        stateHash = 0;

        string[] candidateLayers;
        if (stateName.StartsWith("Unarmed"))
        {
            candidateLayers = unarmedMovementLayers;
        }
        else if (stateName.StartsWith("Armed"))
        {
            candidateLayers = armedMovementLayers;
        }
        else if (stateName.StartsWith("Shooting") || stateName.StartsWith("2Hand Shooting"))
        {
            candidateLayers = twoHandShootingMovementLayers;
        }
        else if (stateName.StartsWith("Crawl"))
        {
            candidateLayers = crawlMovementLayers;
        }
        else
        {
            return false;
        }

        for (int i = 0; i < candidateLayers.Length; i++)
        {
            int candidateLayerIndex = animator.GetLayerIndex(candidateLayers[i]);
            if (TryResolveAnimatorStateOnLayer(candidateLayerIndex, candidateLayers[i], stateName, out stateHash))
            {
                layerIndex = candidateLayerIndex;
                return true;
            }
        }

        return false;
    }

    bool TryResolveAnimatorStateOnLayer(int layerIndex, string layerName, string stateName, out int stateHash)
    {
        stateHash = 0;
        if (layerIndex < 0 || layerIndex >= animator.layerCount || string.IsNullOrEmpty(stateName))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(layerName))
        {
            string fullPath = $"{layerName}.{stateName}";
            if (!stateHashes.TryGetValue(fullPath, out stateHash))
            {
                stateHash = Animator.StringToHash(fullPath);
                stateHashes[fullPath] = stateHash;
            }

            if (animator.HasState(layerIndex, stateHash))
            {
                return true;
            }
        }

        if (!stateHashes.TryGetValue(stateName, out stateHash))
        {
            stateHash = Animator.StringToHash(stateName);
            stateHashes[stateName] = stateHash;
        }

        return animator.HasState(layerIndex, stateHash);
    }

    void UpdateBaseLocomotionState(bool isRunning)
    {
        if (!isGrounded || baseActionRoutine != null)
        {
            return;
        }

        string stateName = GetBaseLocomotionState(isRunning);
        if (useAnimatorLocomotionTransitions && IsAnimatorDrivenBaseLocomotionState(stateName))
        {
            if (!IsAnimatorDrivenBaseLocomotionState(currentBaseStateName) || currentBaseStateLayerIndex < 0)
            {
                PlayBaseLayerState(stateName, baseLocomotionFade, false);
            }
            else if (currentBaseStateLayerIndex > 0)
            {
                animator.SetLayerWeight(currentBaseStateLayerIndex, 1f);
            }

            return;
        }

        PlayBaseLayerState(stateName, baseLocomotionFade, false);
    }

    bool IsAnimatorDrivenBaseLocomotionState(string stateName)
    {
        return !string.IsNullOrEmpty(stateName)
            && (stateName == ActiveGroundedStateName
                || stateName == ActiveRunLocomotionStateName
                || stateName == ActiveCrouchLocomotionStateName
                || stateName == ActiveCrawlLocomotionStateName);
    }

    bool IsRunInputActive()
    {
        return currentIsMoving
            && !isCrouching
            && !isCrawling
            && !statusBlocksRun
            && IsRunHeld()
            && CanUseStaminaAction();
    }

    bool IsRunHeld()
    {
        return MobileInputBridge.RunHeld || kontrolPemain != null && kontrolPemain.Pemain.Lari.IsPressed();
    }

    bool IsJumpPressedThisFrame()
    {
        return MobileInputBridge.ConsumeJump()
            || kontrolPemain != null && kontrolPemain.Pemain.Lompat.WasPressedThisFrame();
    }

    bool IsRollPressedThisFrame()
    {
        return MobileInputBridge.ConsumeRoll()
            || kontrolPemain != null && kontrolPemain.Pemain.Roll.WasPressedThisFrame();
    }

    bool IsCrouchPressedThisFrame()
    {
        return MobileInputBridge.ConsumeCrouch()
            || kontrolPemain != null && kontrolPemain.Pemain.Crouch.WasPressedThisFrame();
    }

    bool IsCrawlPressedThisFrame()
    {
        return MobileInputBridge.ConsumeCrawl()
            || kontrolPemain != null && kontrolPemain.Pemain.Crawl.WasPressedThisFrame();
    }

    string GetBaseLocomotionState(bool isRunning)
    {
        if (isCrawling)
        {
            return ActiveCrawlLocomotionStateName;
        }

        if (isCrouching)
        {
            return ActiveCrouchLocomotionStateName;
        }

        if (isRunning)
        {
            return ActiveRunLocomotionStateName;
        }

        return ActiveGroundedStateName;
    }

    string ActiveGroundedStateName => GetActiveOrDefault(activeGroundedStateName, groundedStateName);
    string ActiveRunLocomotionStateName => GetActiveOrDefault(activeRunLocomotionStateName, runLocomotionStateName);
    string ActiveCrouchLocomotionStateName => GetActiveOrDefault(activeCrouchLocomotionStateName, crouchLocomotionStateName);
    string ActiveCrawlLocomotionStateName => GetActiveOrDefault(activeCrawlLocomotionStateName, crawlLocomotionStateName);
    string ActiveJumpStateName => GetActiveOrDefault(activeJumpStateName, jumpStateName);
    string ActiveFallStateName => GetActiveOrDefault(activeFallStateName, fallStateName);
    string ActiveLandStateName => GetActiveOrDefault(activeLandStateName, landStateName);
    string ActiveRollForwardStateName => GetActiveOrDefault(activeRollForwardStateName, rollStateName);
    string ActiveRollLeftStateName => GetActiveOrDefault(activeRollLeftStateName, rollStateName);
    string ActiveRollRightStateName => GetActiveOrDefault(activeRollRightStateName, rollStateName);
    string ActiveRollBackwardStateName => GetActiveOrDefault(activeRollBackwardStateName, rollStateName);

    string GetActiveOrDefault(string activeStateName, string fallbackStateName)
    {
        return string.IsNullOrEmpty(activeStateName) ? fallbackStateName : activeStateName;
    }

    void ApplyLocomotionProfile(Weapon weapon, bool refreshState)
    {
        if (!useWeaponSpecificLocomotion)
        {
            activeGroundedStateName = groundedStateName;
            activeRunLocomotionStateName = runLocomotionStateName;
            activeCrouchLocomotionStateName = crouchLocomotionStateName;
            activeCrawlLocomotionStateName = crawlLocomotionStateName;
            activeJumpStateName = jumpStateName;
            activeFallStateName = fallStateName;
            activeLandStateName = landStateName;
            activeRollForwardStateName = ResolveProfileState(unarmedRollForwardStateName, rollStateName);
            activeRollLeftStateName = ResolveProfileState(unarmedRollLeftStateName, activeRollForwardStateName);
            activeRollRightStateName = ResolveProfileState(unarmedRollRightStateName, activeRollForwardStateName);
            activeRollBackwardStateName = ResolveProfileState(unarmedRollBackwardStateName, activeRollForwardStateName);
        }
        else if (weapon == null)
        {
            activeGroundedStateName = ResolveProfileState(unarmedGroundedStateName, groundedStateName);
            activeRunLocomotionStateName = ResolveProfileState(unarmedRunLocomotionStateName, runLocomotionStateName);
            activeCrouchLocomotionStateName = ResolveProfileState(unarmedCrouchLocomotionStateName, crouchLocomotionStateName);
            activeCrawlLocomotionStateName = ResolveProfileState(unarmedCrawlLocomotionStateName, crawlLocomotionStateName);
            activeJumpStateName = ResolveProfileState(unarmedJumpStateName, jumpStateName);
            activeFallStateName = ResolveProfileState(unarmedFallStateName, fallStateName);
            activeLandStateName = ResolveProfileState(unarmedLandStateName, landStateName);
            activeRollForwardStateName = ResolveProfileState(unarmedRollForwardStateName, rollStateName);
            activeRollLeftStateName = ResolveProfileState(unarmedRollLeftStateName, activeRollForwardStateName);
            activeRollRightStateName = ResolveProfileState(unarmedRollRightStateName, activeRollForwardStateName);
            activeRollBackwardStateName = ResolveProfileState(unarmedRollBackwardStateName, activeRollForwardStateName);
        }
        else if (weapon.holdType == WeaponHoldType.OneHand)
        {
            activeGroundedStateName = ResolveProfileState(oneHandGroundedStateName, groundedStateName);
            activeRunLocomotionStateName = ResolveProfileState(oneHandRunLocomotionStateName, runLocomotionStateName);
            activeCrouchLocomotionStateName = ResolveProfileState(oneHandCrouchLocomotionStateName, crouchLocomotionStateName);
            activeCrawlLocomotionStateName = ResolveProfileState(oneHandCrawlLocomotionStateName, crawlLocomotionStateName);
            activeJumpStateName = ResolveProfileState(oneHandJumpStateName, jumpStateName);
            activeFallStateName = ResolveProfileState(oneHandFallStateName, fallStateName);
            activeLandStateName = ResolveProfileState(oneHandLandStateName, landStateName);
            activeRollForwardStateName = ResolveProfileState(oneHandRollForwardStateName, rollStateName);
            activeRollLeftStateName = ResolveProfileState(oneHandRollLeftStateName, activeRollForwardStateName);
            activeRollRightStateName = ResolveProfileState(oneHandRollRightStateName, activeRollForwardStateName);
            activeRollBackwardStateName = ResolveProfileState(oneHandRollBackwardStateName, activeRollForwardStateName);
        }
        else
        {
            activeGroundedStateName = ResolveProfileState(twoHandGroundedStateName, groundedStateName);
            activeRunLocomotionStateName = ResolveProfileState(twoHandRunLocomotionStateName, runLocomotionStateName);
            activeCrouchLocomotionStateName = ResolveProfileState(twoHandCrouchLocomotionStateName, crouchLocomotionStateName);
            activeCrawlLocomotionStateName = ResolveProfileState(twoHandCrawlLocomotionStateName, crawlLocomotionStateName);
            activeJumpStateName = ResolveProfileState(twoHandJumpStateName, jumpStateName);
            activeFallStateName = ResolveProfileState(twoHandFallStateName, fallStateName);
            activeLandStateName = ResolveProfileState(twoHandLandStateName, landStateName);
            activeRollForwardStateName = ResolveProfileState(twoHandRollForwardStateName, rollStateName);
            activeRollLeftStateName = ResolveProfileState(twoHandRollLeftStateName, activeRollForwardStateName);
            activeRollRightStateName = ResolveProfileState(twoHandRollRightStateName, activeRollForwardStateName);
            activeRollBackwardStateName = ResolveProfileState(twoHandRollBackwardStateName, activeRollForwardStateName);
        }

        if (refreshState)
        {
            RefreshBaseLocomotionState();
        }
    }

    string ResolveProfileState(string preferredStateName, string fallbackStateName)
    {
        if (!string.IsNullOrEmpty(preferredStateName) && CanUseAnimatorState(preferredStateName))
        {
            return preferredStateName;
        }

        if (!string.IsNullOrEmpty(fallbackStateName) && CanUseAnimatorState(fallbackStateName))
        {
            return fallbackStateName;
        }

        return !string.IsNullOrEmpty(preferredStateName) ? preferredStateName : fallbackStateName;
    }

    bool CanUseAnimatorState(string stateName)
    {
        return animator == null || TryResolveAnimatorState(stateName, out _, out _);
    }

    void RefreshBaseLocomotionState()
    {
        if (animator == null || !isGrounded || isRollingAction || baseActionRoutine != null)
        {
            return;
        }

        PlayBaseLayerState(GetBaseLocomotionState(IsRunInputActive()), baseLocomotionFade, false);
    }

    string GetRollStateName(Vector2 rollInput)
    {
        if (Mathf.Abs(rollInput.x) > Mathf.Abs(rollInput.y))
        {
            return rollInput.x < 0f
                ? FirstAvailableState(ActiveRollLeftStateName, ActiveRollForwardStateName, rollStateName, "Unarmed-Roll-Forward")
                : FirstAvailableState(ActiveRollRightStateName, ActiveRollForwardStateName, rollStateName, "Unarmed-Roll-Forward");
        }

        if (rollInput.y < -0.35f)
        {
            return FirstAvailableState(ActiveRollBackwardStateName, ActiveRollForwardStateName, rollStateName, "Unarmed-Roll-Forward");
        }

        return FirstAvailableState(ActiveRollForwardStateName, rollStateName, "Unarmed-Roll-Forward");
    }

    string FirstAvailableState(params string[] stateNames)
    {
        if (stateNames != null)
        {
            for (int i = 0; i < stateNames.Length; i++)
            {
                string stateName = stateNames[i];
                if (!string.IsNullOrEmpty(stateName) && TryResolveAnimatorState(stateName, out _, out _))
                {
                    return stateName;
                }
            }
        }

        return string.Empty;
    }

    void PlayLandThenGrounded()
    {
        StopBaseActionRoutine();

        string activeLandState = ActiveLandStateName;
        if (string.IsNullOrEmpty(activeLandState) || landAnimationDuration <= 0f || !TryResolveAnimatorState(activeLandState, out _, out _))
        {
            PlayBaseLayerState(GetBaseLocomotionState(IsRunInputActive()));
            return;
        }

        baseActionRoutine = StartCoroutine(PlayBaseStateThenGrounded(activeLandState, landAnimationDuration));
    }

    IEnumerator PlayBaseStateThenGrounded(string stateName, float duration)
    {
        yield return PlayBaseStateThenGrounded(stateName, duration, jumpAnimationFade);
    }

    IEnumerator PlayBaseStateThenGrounded(string stateName, float duration, float fadeDuration)
    {
        PlayBaseLayerState(stateName, fadeDuration, true);
        yield return new WaitForSeconds(Mathf.Max(0f, duration));
        PlayBaseLayerState(GetBaseLocomotionState(IsRunInputActive()));
        baseActionRoutine = null;
    }

    IEnumerator PlayRollThenGrounded(Vector3 rollDirection, string rollAnimationState, float duration)
    {
        PlayBaseLayerState(rollAnimationState, rollAnimationFade, true);

        float safeDuration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;
        float moved = 0f;

        while (elapsed < safeDuration)
        {
            verticalVelocity = -2f;
            float stepDistance = (rollDistance / safeDuration) * Time.deltaTime;
            if (controller != null && controller.enabled)
            {
                controller.Move(rollDirection * stepDistance);
                moved += stepDistance;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (controller != null && controller.enabled && moved < rollDistance)
        {
            controller.Move(rollDirection * (rollDistance - moved));
        }

        Vector2 gerakInput = GetMovementInput();
        float moveX = gerakInput.x;
        float moveZ = gerakInput.y;
        bool isMoving = Mathf.Abs(moveX) > 0.01f || Mathf.Abs(moveZ) > 0.01f;
        bool wantsToRun = isMoving && !isCrouching && !isCrawling && !statusBlocksRun && IsRunHeld();
        bool isRunning = wantsToRun && CanUseStaminaAction();

        SetAnimatorBool("IsRolling", false);
        SetAnimatorFloat("Horizontal", moveX, 0f);
        SetAnimatorFloat("Vertical", moveZ, 0f);
        SetAnimatorFloat("Speed", isMoving ? (isRunning ? 2f : 1f) : 0f, 0f);
        SetAnimatorBool("IsMoving", isMoving);
        SetAnimatorBool("IsRunning", isRunning);
        SetAnimatorBool("IsCrouching", isCrouching);
        SetAnimatorBool("IsCrawling", isCrawling);
        verticalVelocity = -2f;
        suppressLandUntil = Time.time + rollLandSuppressDuration;
        isRollingAction = false;
        baseActionRoutine = null;

        if (!statusBlocksJump && kontrolPemain.Pemain.Lompat.IsPressed() && isGrounded && CanUseStaminaAction() && currentStamina >= jumpStaminaCost)
        {
            BeginJump(true);
            yield break;
        }

    }

    void StopBaseActionRoutine()
    {
        if (baseActionRoutine == null)
        {
            return;
        }

        StopCoroutine(baseActionRoutine);
        baseActionRoutine = null;
        isRollingAction = false;
        SetAnimatorBool("IsRolling", false);
        if (animator != null && currentBaseStateLayerIndex > 0)
        {
            animator.SetLayerWeight(currentBaseStateLayerIndex, 0f);
        }

        currentBaseStateLayerIndex = -1;
        currentBaseStateName = null;
    }

    void CacheAnimatorParameters()
    {
        hasHorizontalParameter = false;
        hasVerticalParameter = false;
        hasSpeedParameter = false;
        hasIsMovingParameter = false;
        hasIsRunningParameter = false;
        hasIsGroundedParameter = false;
        hasIsCrouchingParameter = false;
        hasIsCrawlingParameter = false;
        hasIsRollingParameter = false;
        hasJumpParameter = false;
        hasRollParameter = false;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == AnimatorControllerParameterType.Float)
            {
                if (parameter.name == "Horizontal") hasHorizontalParameter = true;
                else if (parameter.name == "Vertical") hasVerticalParameter = true;
                else if (parameter.name == "Speed") hasSpeedParameter = true;
            }
            else if (parameter.type == AnimatorControllerParameterType.Bool)
            {
                if (parameter.name == "IsMoving") hasIsMovingParameter = true;
                else if (parameter.name == "IsRunning") hasIsRunningParameter = true;
                else if (parameter.name == "IsGrounded") hasIsGroundedParameter = true;
                else if (parameter.name == "IsCrouching") hasIsCrouchingParameter = true;
                else if (parameter.name == "IsCrawling") hasIsCrawlingParameter = true;
                else if (parameter.name == "IsRolling") hasIsRollingParameter = true;
            }
            else if (parameter.type == AnimatorControllerParameterType.Trigger)
            {
                if (parameter.name == "Jump") hasJumpParameter = true;
                else if (parameter.name == "Roll") hasRollParameter = true;
            }
        }
    }

    void SetAnimatorFloat(string parameterName, float value, float dampTime)
    {
        if (animator == null || !HasFloatParameter(parameterName))
        {
            return;
        }

        animator.SetFloat(parameterName, value, Mathf.Max(0f, dampTime), Time.deltaTime);
    }

    void SetAnimatorBool(string parameterName, bool value)
    {
        if (animator == null || !HasBoolParameter(parameterName))
        {
            return;
        }

        animator.SetBool(parameterName, value);
    }

    void SetAnimatorTrigger(string parameterName)
    {
        if (animator == null || !HasTriggerParameter(parameterName))
        {
            return;
        }

        animator.SetTrigger(parameterName);
    }

    void ResetAnimatorTrigger(string parameterName)
    {
        if (animator == null || !HasTriggerParameter(parameterName))
        {
            return;
        }

        animator.ResetTrigger(parameterName);
    }

    bool HasFloatParameter(string parameterName)
    {
        return parameterName == "Horizontal" ? hasHorizontalParameter
            : parameterName == "Vertical" ? hasVerticalParameter
            : parameterName == "Speed" && hasSpeedParameter;
    }

    bool HasBoolParameter(string parameterName)
    {
        return parameterName == "IsMoving" ? hasIsMovingParameter
            : parameterName == "IsRunning" ? hasIsRunningParameter
            : parameterName == "IsGrounded" ? hasIsGroundedParameter
            : parameterName == "IsCrouching" ? hasIsCrouchingParameter
            : parameterName == "IsCrawling" ? hasIsCrawlingParameter
            : parameterName == "IsRolling" && hasIsRollingParameter;
    }

    bool HasTriggerParameter(string parameterName)
    {
        return parameterName == "Jump" ? hasJumpParameter
            : parameterName == "Roll" && hasRollParameter;
    }

    void UpdateFootstepAudio(bool isMoving, bool isRunning)
    {
        if (!isGrounded || !isMoving)
        {
            footstepTimer = 0f;
            return;
        }

        float interval = Mathf.Max(0.01f, isRunning ? runStepInterval : walkStepInterval);
        footstepTimer -= Time.deltaTime;
        if (footstepTimer > 0f)
        {
            return;
        }

        PlayFootstep();
        footstepTimer = interval;
    }

    void PlayFootstep()
    {
        if (footstepAudioSource == null)
        {
            return;
        }

        AudioClip[] clips = GetFootstepClipsForCurrentSurface();
        AudioClip clip = GetRandomClip(clips, lastFootstepClip);
        if (clip == null)
        {
            return;
        }

        lastFootstepClip = clip;
        footstepAudioSource.pitch = Random.Range(footstepPitchRange.x, footstepPitchRange.y);
        footstepAudioSource.PlayOneShot(clip, footstepVolume);
    }

    AudioClip[] GetFootstepClipsForCurrentSurface()
    {
        Transform origin = footstepRayOrigin != null ? footstepRayOrigin : transform;
        Vector3 rayOrigin = origin.position + Vector3.up * 0.1f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, footstepRayDistance, footstepSurfaceMask, QueryTriggerInteraction.Ignore))
        {
            int hitLayerMask = 1 << hit.collider.gameObject.layer;
            if (footstepSurfaces != null)
            {
                for (int i = 0; i < footstepSurfaces.Length; i++)
                {
                    FootstepSurfaceAudio surface = footstepSurfaces[i];
                    if (surface != null && (surface.layerMask.value & hitLayerMask) != 0 && HasClips(surface.clips))
                    {
                        return surface.clips;
                    }
                }
            }
        }

        return defaultFootstepClips;
    }

    AudioClip GetRandomClip(AudioClip[] clips, AudioClip previousClip)
    {
        if (!HasClips(clips))
        {
            return null;
        }

        if (clips.Length == 1)
        {
            return clips[0];
        }

        AudioClip clip = previousClip;
        for (int i = 0; i < 6 && clip == previousClip; i++)
        {
            clip = clips[Random.Range(0, clips.Length)];
        }

        return clip;
    }

    bool HasClips(AudioClip[] clips)
    {
        return clips != null && clips.Length > 0;
    }
}
