using System.Collections;
using Lean.Pool;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Splines;

public class PlayerMeleeController : MonoBehaviour
{
    public bool allowInput = true;
    [Header("Input")]
    public KeyCode physicalMeleeKey = KeyCode.V;
    public KeyCode weaponMeleeKey = KeyCode.B;
    public PlayerShoot playerShoot;
    public PlayerWeaponAnimator weaponAnimator;
    public PlayerBlockController blockController;
    public PlayerMovement playerMovement;
    public CameraControler cameraControler;
    public Transform cameraTransform;

    [Header("Melee Data")]
    public PlayerMeleeData oneHandData;
    public PlayerMeleeData twoHandData;
    public PlayerMeleeData oneHandPhysicalData;
    public PlayerMeleeData twoHandPhysicalData;
    public PlayerMeleeData oneHandWeaponData;
    public PlayerMeleeData twoHandWeaponData;
    public LayerMask hitMask = ~0;
    public bool usePlayerShootHitMask = true;
    public bool drawDebugLaser = true;
    public float debugLaserDuration = 0.1f;
    public float impactSurfaceOffset = 0.01f;
    public float impactEffectLifetime = 2f;
    public float meleeAttackSoundVolume = 0.7f;
    public float meleeHitSoundVolume = 0.8f;
    public bool logMeleeHits;

    [Header("Stealth Melee")]
    public bool enableStealthMelee = true;
    public bool requireWeaponForStealth = true;
    public bool requireUnawareTarget = true;
    public bool requireBehindTarget = true;
    public bool allowBossStealthKill;
    public float stealthRange = 2f;
    public float stealthApproachStopDistance = 1f;
    [Range(1f, 180f)] public float stealthPlayerFacingAngle = 110f;
    [Range(1f, 180f)] public float stealthBackAngle = 120f;
    public TMP_Text stealthPromptText;
    public string stealthPromptMessage = "Tekan B - Stealth Melee";
    public Color stealthPromptColor = Color.white;
    public Vector2 stealthPromptAnchoredPosition = new Vector2(0f, -180f);
    public float stealthPromptFontSize = 28f;

    [Header("Stealth Cinematic")]
    public CinemachineCamera stealthCinemachineCamera;
    public CinemachineSplineDolly stealthDolly;
    public SplineContainer stealthSpline;
    public bool autoFindStealthCinemachine = true;
    public int stealthCameraPriority = 60;
    public float stealthDollyStartPosition = 0f;
    public float stealthDollyEndPosition = 1f;
    public float stealthDollyMoveDuration = 0.55f;
    public bool applyStealthCameraLocalEulerAngles = true;
    public Vector3 stealthCameraLocalEulerAngles = new Vector3(0f, -15f, 0f);
    [Range(0.05f, 1f)] public float stealthAnimatorSpeed = 0.45f;
    public bool useGlobalStealthSlowMotion = true;
    [Range(0.05f, 1f)] public float stealthGlobalTimeScale = 0.35f;
    public bool scaleFixedDeltaTimeDuringStealth = true;
    [Range(0.1f, 1f)] public float stealthKillMomentNormalized = 0.8f;
    public float stealthTargetDeathDelay = 0.12f;
    public float stealthStandDelay = 0.18f;
    public float stealthRecoveryDelay = 0.25f;

    private AudioSource audioSource;
    private CharacterController characterController;
    private Coroutine meleeAttackRoutine;
    private float nextMeleeTime;
    private PlayerMeleeData fallbackOneHandPhysicalData;
    private PlayerMeleeData fallbackTwoHandPhysicalData;
    private PlayerMeleeData fallbackOneHandWeaponData;
    private PlayerMeleeData fallbackTwoHandWeaponData;
    private PlayerMeleeData fallbackUnarmedPhysicalData;
    private KontrolPemain kontrolPemain;
    private Enemy currentStealthTarget;
    private Enemy lockedStealthTarget;
    private bool stealthReferencesResolved;
    private bool isStealthExecuting;
    private bool previousPlayerMovementInput;
    private bool previousStealthCrouching;
    private bool previousStealthCrawling;
    private bool previousCameraLookInput;
    private bool previousAimInput;
    private float previousAnimatorSpeed = 1f;
    private float previousTimeScale = 1f;
    private float previousFixedDeltaTime = 0.02f;
    private bool stealthSlowMotionApplied;
    private Quaternion previousStealthCameraLocalRotation;
    private bool hasPreviousStealthCameraLocalRotation;
    private object previousStealthDollySpline;
    private bool hasPreviousStealthDollySpline;

    public bool IsAttacking => meleeAttackRoutine != null;

    void Awake()
    {
        kontrolPemain = new KontrolPemain();
        playerShoot = playerShoot != null ? playerShoot : GetComponent<PlayerShoot>();
        weaponAnimator = weaponAnimator != null ? weaponAnimator : GetComponent<PlayerWeaponAnimator>();
        blockController = blockController != null ? blockController : GetComponent<PlayerBlockController>();
        playerMovement = playerMovement != null ? playerMovement : GetComponent<PlayerMovement>();
        cameraControler = cameraControler != null ? cameraControler : GetComponent<CameraControler>();
        characterController = GetComponent<CharacterController>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        Animator targetAnimator = weaponAnimator != null && weaponAnimator.animator != null
            ? weaponAnimator.animator
            : GetComponentInChildren<Animator>();
        AnimationEventReceiver.EnsureOn(targetAnimator);
    }

    void OnEnable()
    {
        kontrolPemain?.Pemain.Enable();
    }

    void Update()
    {
        if (!CanReadInput())
        {
            UpdateStealthPrompt(null);
            return;
        }

        currentStealthTarget = FindBestStealthTarget();
        UpdateStealthPrompt(currentStealthTarget);

        if (MobileInputBridge.ConsumeMelee())
        {
            if (playerShoot != null && playerShoot.currentWeapon != null)
            {
                if (TryStealthMelee())
                {
                    return;
                }

                TryMeleeAttack(true);
            }
            else
            {
                TryMeleeAttack(false);
            }

            return;
        }

        if (IsPhysicalMeleePressedThisFrame())
        {
            TryMeleeAttack(false);
        }

        if (IsWeaponMeleePressedThisFrame())
        {
            if (TryStealthMelee())
            {
                return;
            }

            TryMeleeAttack(true);
        }
    }

    void OnDisable()
    {
        kontrolPemain?.Pemain.Disable();
        if (meleeAttackRoutine != null)
        {
            StopCoroutine(meleeAttackRoutine);
            meleeAttackRoutine = null;
        }

        SetShootBlocked(false);
        RestoreStealthSequenceState();
        if (lockedStealthTarget != null)
        {
            lockedStealthTarget.EndStealthTargetLock();
            lockedStealthTarget = null;
        }

        UpdateStealthPrompt(null);
    }

    void OnDestroy()
    {
        kontrolPemain?.Dispose();
    }

    bool IsPhysicalMeleePressedThisFrame()
    {
        return kontrolPemain != null && kontrolPemain.Pemain.MeleePhysical.WasPressedThisFrame();
    }

    bool IsWeaponMeleePressedThisFrame()
    {
        return kontrolPemain != null && kontrolPemain.Pemain.MeleeWeapon.WasPressedThisFrame();
    }

    bool CanReadInput()
    {
        return allowInput
            && !isStealthExecuting
            && playerShoot != null
            && playerShoot.enabled
            && playerShoot.allowInput
            && !playerShoot.externalActionBlocksInput
            && !playerShoot.statusBlocksInput
            && (playerMovement == null || !playerMovement.IsGuardBroken)
            && !playerShoot.IsReloading
            && (blockController == null || !blockController.IsBlocking)
            && (weaponAnimator == null
                || (!weaponAnimator.IsPlayingSwitchAnimation && !weaponAnimator.IsPlayingReloadAnimation));
    }

    void TryMeleeAttack(bool useWeaponMelee)
    {
        PlayerMeleeData meleeData = GetCurrentMeleeData(useWeaponMelee);
        if (meleeData == null || weaponAnimator == null || Time.time < nextMeleeTime)
        {
            return;
        }

        float duration = weaponAnimator.PlayRandomMeleeState(meleeData);
        if (duration <= 0f)
        {
            return;
        }

        PlayRandomSound(meleeData.attackSounds, meleeData.attackSound, meleeAttackSoundVolume);
        float cooldown = Mathf.Max(0.01f, meleeData.cooldown);
        nextMeleeTime = Time.time + Mathf.Max(cooldown, duration);

        if (meleeAttackRoutine != null)
        {
            StopCoroutine(meleeAttackRoutine);
        }

        meleeAttackRoutine = StartCoroutine(MeleeDamageRoutine(meleeData, duration, useWeaponMelee));
    }

    bool TryStealthMelee()
    {
        if (!enableStealthMelee || isStealthExecuting)
        {
            return false;
        }

        Enemy target = IsValidStealthTarget(currentStealthTarget)
            ? currentStealthTarget
            : FindBestStealthTarget();
        if (target == null)
        {
            return false;
        }

        PlayerMeleeData meleeData = GetCurrentMeleeData(true);
        if (meleeData == null || weaponAnimator == null || Time.time < nextMeleeTime)
        {
            return false;
        }

        UpdateStealthPrompt(null);
        if (meleeAttackRoutine != null)
        {
            StopCoroutine(meleeAttackRoutine);
        }

        meleeAttackRoutine = StartCoroutine(StealthMeleeRoutine(meleeData, target));
        return true;
    }

    PlayerMeleeData GetCurrentMeleeData(bool useWeaponMelee)
    {
        Weapon weapon = playerShoot != null ? playerShoot.currentWeapon : null;
        if (weapon == null)
        {
            return useWeaponMelee
                ? null
                : GetUnarmedPhysicalData();
        }

        if (weapon.holdType == WeaponHoldType.OneHand)
        {
            return useWeaponMelee
                ? GetDataOrFallback(oneHandWeaponData, oneHandData, ref fallbackOneHandWeaponData, true, WeaponHoldType.OneHand)
                : GetDataOrFallback(oneHandPhysicalData, oneHandData, ref fallbackOneHandPhysicalData, false, WeaponHoldType.OneHand, false);
        }

        return useWeaponMelee
            ? GetDataOrFallback(twoHandWeaponData, twoHandData, ref fallbackTwoHandWeaponData, true, WeaponHoldType.TwoHand)
            : GetDataOrFallback(twoHandPhysicalData, twoHandData, ref fallbackTwoHandPhysicalData, false, WeaponHoldType.TwoHand, false);
    }

    PlayerMeleeData GetUnarmedPhysicalData()
    {
        PlayerMeleeData statSource = oneHandPhysicalData != null ? oneHandPhysicalData : oneHandData;
        PlayerMeleeData meleeData = GetDataOrFallback(
            null,
            statSource,
            ref fallbackUnarmedPhysicalData,
            false,
            WeaponHoldType.OneHand,
            true
        );

        if (meleeData.attackSound == null && (meleeData.attackSounds == null || meleeData.attackSounds.Length == 0) && oneHandData != null)
        {
            meleeData.attackSound = oneHandData.attackSound;
            meleeData.attackSounds = oneHandData.attackSounds;
            meleeData.hitSound = oneHandData.hitSound;
            meleeData.hitSounds = oneHandData.hitSounds;
        }

        return meleeData;
    }

    PlayerMeleeData GetDataOrFallback(
        PlayerMeleeData configuredData,
        PlayerMeleeData statSource,
        ref PlayerMeleeData fallbackData,
        bool useWeaponMelee,
        WeaponHoldType holdType,
        bool forceUnarmed = false)
    {
        if (configuredData != null)
        {
            return configuredData;
        }

        if (fallbackData == null)
        {
            fallbackData = CreateFallbackMeleeData(statSource, useWeaponMelee, holdType, forceUnarmed);
        }

        return fallbackData;
    }

    PlayerMeleeData CreateFallbackMeleeData(PlayerMeleeData statSource, bool useWeaponMelee, WeaponHoldType holdType, bool forceUnarmed)
    {
        PlayerMeleeData data = ScriptableObject.CreateInstance<PlayerMeleeData>();
        if (statSource != null)
        {
            CopyMeleeStats(statSource, data);
        }

        data.holdType = holdType;
        data.meleeName = GetFallbackMeleeName(useWeaponMelee, holdType);
        data.animationLayerName = GetFallbackAnimationLayer(useWeaponMelee, holdType, forceUnarmed);
        data.attackStateNames = GetFallbackAttackStates(useWeaponMelee, holdType, forceUnarmed);
        return data;
    }

    void CopyMeleeStats(PlayerMeleeData source, PlayerMeleeData target)
    {
        target.animationDuration = source.animationDuration;
        target.cooldown = source.cooldown;
        target.crossFade = source.crossFade;
        target.damage = source.damage;
        target.range = source.range;
        target.hitRadius = source.hitRadius;
        target.damageDelay = source.damageDelay;
        target.criticalChance = source.criticalChance;
        target.criticalDamagePercent = source.criticalDamagePercent;
        target.knockbackChance = source.knockbackChance;
        target.knockbackPower = source.knockbackPower;
        target.maxKnockbackDistance = source.maxKnockbackDistance;
        target.knockbackDuration = source.knockbackDuration;
        target.frontAngle = source.frontAngle;
        target.autoAimRange = source.autoAimRange;
        target.autoAimMaxStep = source.autoAimMaxStep;
        target.autoAimStopDistance = source.autoAimStopDistance;
        target.autoAimSpeed = source.autoAimSpeed;
        target.attackSound = source.attackSound;
        target.hitSound = source.hitSound;
        target.attackSounds = source.attackSounds;
        target.hitSounds = source.hitSounds;
        target.hitEffect = source.hitEffect;
        target.criticalHitEffect = source.criticalHitEffect;
    }

    string GetFallbackMeleeName(bool useWeaponMelee, WeaponHoldType holdType)
    {
        string holdName = holdType == WeaponHoldType.OneHand ? "One Hand" : "Two Hand";
        return useWeaponMelee ? $"{holdName} Weapon Melee" : $"{holdName} Physical Melee";
    }

    string GetFallbackAnimationLayer(bool useWeaponMelee, WeaponHoldType holdType, bool forceUnarmed)
    {
        if (forceUnarmed)
        {
            return "Unarmed-Melee";
        }

        if (holdType == WeaponHoldType.OneHand)
        {
            return useWeaponMelee ? "1Hand-Sword" : "Armed-Melee";
        }

        return useWeaponMelee ? "2Hand-Shooting-Melee" : "Unarmed-Melee";
    }

    string[] GetFallbackAttackStates(bool useWeaponMelee, WeaponHoldType holdType, bool forceUnarmed)
    {
        if (forceUnarmed)
        {
            return new[]
            {
                "Unarmed-Attack-R1",
                "Unarmed-Attack-R2",
                "Unarmed-Attack-R3",
                "Unarmed-Attack-L3",
                "Unarmed-Attack-Kick-L1",
                "Unarmed-Attack-Kick-R1",
                "Unarmed-Attack-Kick-R2"
            };
        }

        if (holdType == WeaponHoldType.OneHand)
        {
            return useWeaponMelee
                ? new[] { "Sword-Attack-R1", "Sword-Attack-R2", "Sword-Attack-R3" }
                : new[] { "Armed-Attack-Kick-L1", "Armed-Attack-Kick-L2", "Armed-Attack-Kick-R1", "Armed-Attack-Kick-R2" };
        }

        return useWeaponMelee
            ? new[] { "Shooting-Attack1", "Shooting-Attack2" }
            : new[] { "Shooting-Attack-Kick-L2", "Shooting-Attack-Kick-R1", "Shooting-Attack-Kick-R2" };
    }

    IEnumerator MeleeDamageRoutine(PlayerMeleeData meleeData, float animationDuration, bool useWeaponMelee)
    {
        float startTime = Time.time;
        SetShootBlocked(true);
        Enemy target = FindBestMeleeTarget(meleeData, meleeData != null ? meleeData.autoAimRange : 0f);
        float delay = meleeData != null ? Mathf.Max(0f, meleeData.damageDelay) : 0f;
        if (target != null)
        {
            float approachDuration = GetMeleeApproachDuration(meleeData, target, delay, animationDuration);
            if (approachDuration > 0f)
            {
                yield return MoveTowardMeleeTarget(meleeData, target, approachDuration);
                delay = Mathf.Max(0f, delay - approachDuration);
            }
        }

        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (GetCurrentMeleeData(useWeaponMelee) == meleeData)
        {
            DealMeleeDamage(meleeData, target);
        }

        float remainingAnimation = Mathf.Max(0f, animationDuration - (Time.time - startTime));
        if (remainingAnimation > 0f)
        {
            yield return new WaitForSeconds(remainingAnimation);
        }

        SetShootBlocked(false);
        meleeAttackRoutine = null;
    }

    IEnumerator StealthMeleeRoutine(PlayerMeleeData meleeData, Enemy target)
    {
        isStealthExecuting = true;
        BeginStealthSequenceState();
        if (target != null)
        {
            lockedStealthTarget = target;
            target.BeginStealthTargetLock();
        }

        if (target != null)
        {
            Vector3 direction = GetDirectionToEnemy(target);
            RotateTowardMeleeTarget(direction, meleeData != null ? meleeData.autoAimSpeed : 12f);
        }

        float standDelay = playerMovement != null && (playerMovement.IsCrouching || playerMovement.IsCrawling)
            ? Mathf.Max(0f, stealthStandDelay)
            : 0f;
        playerMovement?.ForceStandingStance();
        float elapsed = 0f;
        while (elapsed < standDelay)
        {
            elapsed += Time.deltaTime;
            UpdateStealthCinematicCamera(Time.deltaTime);
            yield return null;
        }

        PlayerMeleeData currentMeleeData = GetCurrentMeleeData(true);
        if (currentMeleeData != meleeData || meleeData == null || weaponAnimator == null)
        {
            EndStealthSequence();
            yield break;
        }

        float stealthApproachDuration = GetStealthApproachDuration(meleeData, target);
        if (stealthApproachDuration > 0f)
        {
            yield return MoveTowardMeleeTarget(meleeData, target, stealthApproachDuration, stealthApproachStopDistance);
        }

        float animationSpeed = Mathf.Clamp(stealthAnimatorSpeed, 0.05f, 1f);
        float durationMultiplier = 1f / animationSpeed;
        Animator targetAnimator = weaponAnimator.animator;
        if (targetAnimator != null)
        {
            previousAnimatorSpeed = targetAnimator.speed;
            targetAnimator.speed = previousAnimatorSpeed * animationSpeed;
        }

        float animationDuration = weaponAnimator.PlayRandomMeleeState(meleeData, durationMultiplier);
        if (animationDuration <= 0f)
        {
            EndStealthSequence();
            yield break;
        }

        PlayRandomSound(meleeData.attackSounds, meleeData.attackSound, meleeAttackSoundVolume);
        float cooldown = Mathf.Max(0.01f, meleeData.cooldown) * durationMultiplier;
        nextMeleeTime = Time.time + Mathf.Max(cooldown, animationDuration);

        float startTime = Time.time;
        float killMoment = animationDuration * Mathf.Clamp(stealthKillMomentNormalized, 0.1f, 1f);
        if (killMoment > 0f)
        {
            elapsed = 0f;
            while (elapsed < killMoment)
            {
                elapsed += Time.deltaTime;
                UpdateStealthCinematicCamera(Time.deltaTime);
                yield return null;
            }
        }

        if (IsValidStealthExecutionTarget(target))
        {
            PerformStealthKill(meleeData, target);
        }

        float remainingAnimation = Mathf.Max(0f, animationDuration - (Time.time - startTime));
        while (remainingAnimation > 0f)
        {
            float delta = Time.deltaTime;
            remainingAnimation -= delta;
            UpdateStealthCinematicCamera(delta);
            yield return null;
        }

        float recovery = Mathf.Max(0f, stealthRecoveryDelay);
        elapsed = 0f;
        while (elapsed < recovery)
        {
            elapsed += Time.deltaTime;
            UpdateStealthCinematicCamera(Time.deltaTime);
            yield return null;
        }

        EndStealthSequence();
    }

    void EndStealthSequence()
    {
        RestoreStealthSequenceState();
        if (lockedStealthTarget != null)
        {
            lockedStealthTarget.EndStealthTargetLock();
            lockedStealthTarget = null;
        }

        meleeAttackRoutine = null;
        isStealthExecuting = false;
    }

    void SetShootBlocked(bool blocked)
    {
        if (playerShoot != null)
        {
            playerShoot.externalActionBlocksInput = blocked;
        }
    }

    void BeginStealthSequenceState()
    {
        ResolveStealthCinemachineReferences();
        SetShootBlocked(true);
        BeginStealthSlowMotion();

        if (playerMovement != null)
        {
            previousPlayerMovementInput = playerMovement.allowInput;
            previousStealthCrouching = playerMovement.IsCrouching;
            previousStealthCrawling = playerMovement.IsCrawling;
            playerMovement.allowInput = false;
        }

        if (cameraControler != null)
        {
            previousCameraLookInput = cameraControler.allowLookInput;
            cameraControler.allowLookInput = false;
        }

        if (weaponAnimator != null)
        {
            previousAimInput = weaponAnimator.allowAimInput;
            weaponAnimator.SetAimInputEnabled(false, true);
            if (weaponAnimator.animator != null)
            {
                previousAnimatorSpeed = weaponAnimator.animator.speed;
            }
        }

        BeginStealthCinematicCamera();
    }

    void RestoreStealthSequenceState()
    {
        if (!isStealthExecuting)
        {
            return;
        }

        SetShootBlocked(false);
        RestoreStealthSlowMotion();

        if (playerMovement != null)
        {
            playerMovement.allowInput = previousPlayerMovementInput;
            playerMovement.SetStance(previousStealthCrouching, previousStealthCrawling);
        }

        if (cameraControler != null)
        {
            cameraControler.allowLookInput = previousCameraLookInput;
        }

        if (weaponAnimator != null)
        {
            weaponAnimator.SetAimInputEnabled(previousAimInput, false);
            if (weaponAnimator.animator != null)
            {
                weaponAnimator.animator.speed = previousAnimatorSpeed <= 0f ? 1f : previousAnimatorSpeed;
            }
        }

        EndStealthCinematicCamera();
        isStealthExecuting = false;
    }

    void BeginStealthSlowMotion()
    {
        if (!useGlobalStealthSlowMotion || stealthSlowMotionApplied)
        {
            return;
        }

        previousTimeScale = Time.timeScale;
        previousFixedDeltaTime = Time.fixedDeltaTime;

        float targetTimeScale = Mathf.Clamp(stealthGlobalTimeScale, 0.05f, 1f);
        Time.timeScale = targetTimeScale;
        if (scaleFixedDeltaTimeDuringStealth)
        {
            float baseScale = previousTimeScale > 0.001f ? previousTimeScale : 1f;
            Time.fixedDeltaTime = Mathf.Max(0.0001f, previousFixedDeltaTime * targetTimeScale / baseScale);
        }

        stealthSlowMotionApplied = true;
    }

    void RestoreStealthSlowMotion()
    {
        if (!stealthSlowMotionApplied)
        {
            return;
        }

        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        if (scaleFixedDeltaTimeDuringStealth)
        {
            Time.fixedDeltaTime = previousFixedDeltaTime <= 0f ? 0.02f : previousFixedDeltaTime;
        }

        stealthSlowMotionApplied = false;
    }

    void BeginStealthCinematicCamera()
    {
        if (stealthDolly != null)
        {
            CacheAndAssignStealthSpline();
            stealthDolly.CameraPosition = stealthDollyStartPosition;
        }

        if (stealthCinemachineCamera != null)
        {
            stealthCinemachineCamera.Priority = stealthCameraPriority;
            if (applyStealthCameraLocalEulerAngles)
            {
                previousStealthCameraLocalRotation = stealthCinemachineCamera.transform.localRotation;
                hasPreviousStealthCameraLocalRotation = true;
                stealthCinemachineCamera.transform.localRotation = Quaternion.Euler(stealthCameraLocalEulerAngles);
            }
        }
    }

    void UpdateStealthCinematicCamera(float deltaTime)
    {
        if (stealthCinemachineCamera != null)
        {
            stealthCinemachineCamera.Priority = stealthCameraPriority;
            if (applyStealthCameraLocalEulerAngles)
            {
                stealthCinemachineCamera.transform.localRotation = Quaternion.Euler(stealthCameraLocalEulerAngles);
            }
        }

        if (stealthDolly == null)
        {
            return;
        }

        float moveDuration = Mathf.Max(0.01f, stealthDollyMoveDuration);
        float distance = Mathf.Abs(stealthDollyEndPosition - stealthDollyStartPosition);
        float speed = distance <= 0.0001f ? 1000f : distance / moveDuration;
        stealthDolly.CameraPosition = Mathf.MoveTowards(
            stealthDolly.CameraPosition,
            stealthDollyEndPosition,
            speed * Mathf.Max(0f, deltaTime)
        );
    }

    void EndStealthCinematicCamera()
    {
        if (stealthCinemachineCamera != null)
        {
            stealthCinemachineCamera.Priority = 0;
            if (hasPreviousStealthCameraLocalRotation)
            {
                stealthCinemachineCamera.transform.localRotation = previousStealthCameraLocalRotation;
                hasPreviousStealthCameraLocalRotation = false;
            }
        }

        if (stealthDolly != null)
        {
            stealthDolly.CameraPosition = stealthDollyStartPosition;
            RestorePreviousStealthSpline();
        }
    }

    void ResolveStealthCinemachineReferences()
    {
        if (stealthReferencesResolved || !autoFindStealthCinemachine)
        {
            return;
        }

        stealthReferencesResolved = true;
        if (stealthSpline == null)
        {
            stealthSpline = FindNamedSceneComponent<SplineContainer>("Stealth");
        }

        if (stealthCinemachineCamera == null)
        {
            stealthCinemachineCamera = FindNamedSceneComponent<CinemachineCamera>("Stealth");
        }

        if (stealthDolly == null && stealthCinemachineCamera != null)
        {
            stealthDolly = stealthCinemachineCamera.GetComponent<CinemachineSplineDolly>();
        }

        if (stealthDolly == null)
        {
            CinemachineSplineDolly[] dollies = FindObjectsByType<CinemachineSplineDolly>(FindObjectsInactive.Include);
            for (int i = 0; i < dollies.Length; i++)
            {
                CinemachineSplineDolly candidate = dollies[i];
                if (candidate == null)
                {
                    continue;
                }

                string splineName = candidate.Spline != null ? candidate.Spline.name : string.Empty;
                if (candidate.name.ToLowerInvariant().Contains("stealth") || splineName.ToLowerInvariant().Contains("stealth"))
                {
                    stealthDolly = candidate;
                    break;
                }
            }
        }

        if (stealthDolly == null && stealthSpline != null)
        {
            CinemachineSplineDolly[] dollies = FindObjectsByType<CinemachineSplineDolly>(FindObjectsInactive.Include);
            if (dollies.Length > 0)
            {
                stealthDolly = dollies[0];
            }
        }

        if (stealthCinemachineCamera == null && stealthDolly != null)
        {
            stealthCinemachineCamera = stealthDolly.GetComponent<CinemachineCamera>();
        }
    }

    T FindNamedSceneComponent<T>(string namePart) where T : Component
    {
        T[] components = FindObjectsByType<T>(FindObjectsInactive.Include);
        string needle = string.IsNullOrEmpty(namePart) ? string.Empty : namePart.ToLowerInvariant();
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component != null && component.name.ToLowerInvariant().Contains(needle))
            {
                return component;
            }
        }

        return null;
    }

    void CacheAndAssignStealthSpline()
    {
        if (stealthDolly == null || stealthSpline == null)
        {
            return;
        }

        System.Reflection.PropertyInfo splineProperty = typeof(CinemachineSplineDolly).GetProperty("Spline");
        if (splineProperty == null)
        {
            return;
        }

        if (!hasPreviousStealthDollySpline && splineProperty.CanRead)
        {
            previousStealthDollySpline = splineProperty.GetValue(stealthDolly);
            hasPreviousStealthDollySpline = true;
        }

        if (splineProperty.CanWrite)
        {
            splineProperty.SetValue(stealthDolly, stealthSpline);
        }
    }

    void RestorePreviousStealthSpline()
    {
        if (!hasPreviousStealthDollySpline || stealthDolly == null)
        {
            return;
        }

        System.Reflection.PropertyInfo splineProperty = typeof(CinemachineSplineDolly).GetProperty("Spline");
        if (splineProperty != null && splineProperty.CanWrite)
        {
            splineProperty.SetValue(stealthDolly, previousStealthDollySpline);
        }

        previousStealthDollySpline = null;
        hasPreviousStealthDollySpline = false;
    }

    float GetMeleeApproachDuration(PlayerMeleeData meleeData, Enemy target, float minimumDuration, float animationDuration)
    {
        if (meleeData == null
            || target == null
            || target.IsDead
            || (playerMovement != null && !playerMovement.isGrounded))
        {
            return 0f;
        }

        Vector3 toTarget = target.transform.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        float stopDistance = Mathf.Max(0f, meleeData.autoAimStopDistance);
        float maxStep = Mathf.Max(0f, meleeData.autoAimMaxStep);
        float attackReach = Mathf.Max(meleeData.range + meleeData.hitRadius, stopDistance);

        // Only close the final step of a melee attack. Targets farther away stay
        // under player movement control instead of pulling the character forward.
        if (distance > attackReach + maxStep)
        {
            return 0f;
        }

        float moveDistance = Mathf.Min(
            maxStep,
            Mathf.Max(0f, distance - attackReach)
        );

        if (moveDistance <= 0f)
        {
            return 0f;
        }

        float speed = Mathf.Max(0.01f, meleeData.autoAimSpeed);
        float moveDuration = moveDistance / speed;
        return Mathf.Min(Mathf.Max(minimumDuration, moveDuration), Mathf.Max(minimumDuration, animationDuration));
    }

    float GetStealthApproachDuration(PlayerMeleeData meleeData, Enemy target)
    {
        if (meleeData == null
            || target == null
            || target.IsDead
            || (playerMovement != null && !playerMovement.isGrounded))
        {
            return 0f;
        }

        Vector3 toTarget = target.transform.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        float stopDistance = Mathf.Max(0f, stealthApproachStopDistance);
        float moveDistance = Mathf.Max(0f, distance - stopDistance);
        if (moveDistance <= 0.01f)
        {
            return 0f;
        }

        float speed = Mathf.Max(0.01f, meleeData.autoAimSpeed);
        return moveDistance / speed;
    }

    IEnumerator MoveTowardMeleeTarget(PlayerMeleeData meleeData, Enemy target, float duration)
    {
        float stopDistance = meleeData != null
            ? Mathf.Max(meleeData.range + meleeData.hitRadius, meleeData.autoAimStopDistance)
            : 1f;
        yield return MoveTowardMeleeTarget(meleeData, target, duration, stopDistance);
    }

    IEnumerator MoveTowardMeleeTarget(PlayerMeleeData meleeData, Enemy target, float duration, float stopDistance)
    {
        if (meleeData == null || target == null || duration <= 0f)
        {
            yield break;
        }

        float maxMove = Mathf.Max(0f, meleeData.autoAimMaxStep);
        float elapsed = 0f;
        float moved = 0f;
        while (elapsed < duration && target != null && !target.IsDead && moved < maxMove)
        {
            Vector3 toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            if (distance <= stopDistance)
            {
                break;
            }

            Vector3 direction = toTarget.normalized;
            RotateTowardMeleeTarget(direction, meleeData.autoAimSpeed);
            float remainingMove = Mathf.Min(maxMove - moved, distance - stopDistance);
            float step = Mathf.Min(remainingMove, Mathf.Max(0.01f, meleeData.autoAimSpeed) * Time.deltaTime);
            MovePlayerForMelee(direction * step);
            moved += step;
            elapsed += Time.deltaTime;
            yield return null;
        }

        float remainingTime = duration - elapsed;
        if (remainingTime > 0f)
        {
            yield return new WaitForSeconds(remainingTime);
        }
    }

    void DealMeleeDamage(PlayerMeleeData meleeData, Enemy preferredTarget)
    {
        if (meleeData == null)
        {
            return;
        }

        Enemy enemy = IsValidMeleeTarget(meleeData, preferredTarget, meleeData.range + meleeData.hitRadius, false)
            ? preferredTarget
            : FindBestMeleeTarget(meleeData, meleeData.range + meleeData.hitRadius);

        if (enemy == null)
        {
            DrawMissDebug(meleeData);
            return;
        }

        Vector3 origin = GetMeleeOrigin();
        Vector3 direction = GetDirectionToEnemy(enemy);
        Vector3 hitPoint = GetEnemyHitPoint(enemy, direction);
        float damage = Mathf.Max(0f, meleeData.damage);
        bool isCritical = false;
        if (damage > 0f)
        {
            damage = ApplyMeleeCriticalDamage(meleeData, enemy, damage, out isCritical);
            TryApplyMeleeKnockback(meleeData, enemy, direction);
            enemy.TakeDamage(damage);
            if (logMeleeHits)
            {
                Debug.Log($"Melee hit {enemy.name} for {damage} damage");
            }
        }

        SpawnEnemyHitEffect(meleeData, hitPoint, -direction, isCritical);
        PlayRandomSound(meleeData.hitSounds, meleeData.hitSound, meleeHitSoundVolume);

        if (drawDebugLaser)
        {
            Debug.DrawLine(origin, hitPoint, Color.cyan, debugLaserDuration);
        }
    }

    void PerformStealthKill(PlayerMeleeData meleeData, Enemy enemy)
    {
        if (enemy == null)
        {
            return;
        }

        Vector3 direction = GetDirectionToEnemy(enemy);
        Vector3 hitPoint = GetEnemyHitPoint(enemy, direction);
        SpawnEnemyHitEffect(meleeData, hitPoint, -direction, true);
        PlayRandomSound(meleeData != null ? meleeData.hitSounds : null, meleeData != null ? meleeData.hitSound : null, meleeHitSoundVolume);

        enemy.KillByStealth(true, Mathf.Max(0f, stealthTargetDeathDelay));
        if (logMeleeHits)
        {
            Debug.Log($"Stealth melee killed {enemy.name}");
        }

        if (drawDebugLaser)
        {
            Debug.DrawLine(GetMeleeOrigin(), hitPoint, Color.red, debugLaserDuration);
        }
    }

    Enemy FindBestMeleeTarget(PlayerMeleeData meleeData, float searchRange)
    {
        if (meleeData == null || searchRange <= 0f)
        {
            return null;
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, searchRange, GetHitMask(), QueryTriggerInteraction.Ignore);
        Enemy bestEnemy = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            Enemy enemy = hit.GetComponentInParent<Enemy>();
            if (!IsValidMeleeTarget(meleeData, enemy, searchRange, false))
            {
                continue;
            }

            Vector3 toEnemy = enemy.transform.position - transform.position;
            toEnemy.y = 0f;
            float distance = toEnemy.magnitude;
            float angle = Vector3.Angle(GetMeleeAutoForward(), toEnemy.normalized);
            float score = distance + angle * 0.02f;
            if (score < bestScore)
            {
                bestScore = score;
                bestEnemy = enemy;
            }
        }

        return bestEnemy;
    }

    Enemy FindBestStealthTarget()
    {
        if (!enableStealthMelee || stealthRange <= 0f || !HasWeaponForStealth())
        {
            return null;
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, stealthRange, GetHitMask(), QueryTriggerInteraction.Ignore);
        Enemy bestEnemy = null;
        float bestDistanceSqr = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            Enemy enemy = hit.GetComponentInParent<Enemy>();
            if (!IsValidStealthTarget(enemy))
            {
                continue;
            }

            float distanceSqr = (enemy.transform.position - transform.position).sqrMagnitude;
            if (distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                bestEnemy = enemy;
            }
        }

        return bestEnemy;
    }

    bool IsValidStealthTarget(Enemy enemy)
    {
        if (!enableStealthMelee
            || enemy == null
            || enemy.IsDead
            || !HasWeaponForStealth()
            || IsRestrictedStealthTarget(enemy))
        {
            return false;
        }

        Vector3 toEnemy = enemy.transform.position - transform.position;
        toEnemy.y = 0f;
        float distance = toEnemy.magnitude;
        if (distance <= 0.001f || distance > stealthRange)
        {
            return false;
        }

        float playerAngleLimit = Mathf.Clamp(stealthPlayerFacingAngle, 1f, 180f) * 0.5f;
        if (Vector3.Angle(GetMeleeForward(), toEnemy.normalized) > playerAngleLimit)
        {
            return false;
        }

        if (requireBehindTarget && !IsBehindEnemy(enemy))
        {
            return false;
        }

        if (requireUnawareTarget && !IsEnemyUnaware(enemy))
        {
            return false;
        }

        return true;
    }

    bool HasWeaponForStealth()
    {
        return !requireWeaponForStealth || playerShoot != null && playerShoot.currentWeapon != null;
    }

    bool IsRestrictedStealthTarget(Enemy enemy)
    {
        if (allowBossStealthKill || enemy == null || enemy.enemyData == null)
        {
            return false;
        }

        return enemy.enemyData.enemyType == EnemyType.Boss || enemy.enemyData.enemyType == EnemyType.MiniBoss;
    }

    bool IsBehindEnemy(Enemy enemy)
    {
        Vector3 enemyToPlayer = transform.position - enemy.transform.position;
        enemyToPlayer.y = 0f;
        if (enemyToPlayer.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        Vector3 enemyBackward = -enemy.transform.forward;
        enemyBackward.y = 0f;
        if (enemyBackward.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        float angleLimit = Mathf.Clamp(stealthBackAngle, 1f, 180f) * 0.5f;
        return Vector3.Angle(enemyBackward.normalized, enemyToPlayer.normalized) <= angleLimit;
    }

    bool IsEnemyUnaware(Enemy enemy)
    {
        EnemyAI enemyAI = enemy.GetComponent<EnemyAI>();
        if (enemyAI == null)
        {
            return true;
        }

        return enemyAI.currentState == EnemyAIState.Idle || enemyAI.currentState == EnemyAIState.Patrol;
    }

    bool IsValidStealthExecutionTarget(Enemy enemy)
    {
        if (enemy == null || enemy.IsDead || !HasWeaponForStealth() || IsRestrictedStealthTarget(enemy))
        {
            return false;
        }

        Vector3 toEnemy = enemy.transform.position - transform.position;
        toEnemy.y = 0f;
        return toEnemy.sqrMagnitude <= Mathf.Pow(Mathf.Max(0.01f, stealthRange + 0.35f), 2f);
    }

    bool IsValidMeleeTarget(PlayerMeleeData meleeData, Enemy enemy, float maxRange)
    {
        return IsValidMeleeTarget(meleeData, enemy, maxRange, true);
    }

    bool IsValidMeleeTarget(PlayerMeleeData meleeData, Enemy enemy, float maxRange, bool useCameraForward)
    {
        if (meleeData == null || enemy == null || enemy.IsDead)
        {
            return false;
        }

        Vector3 toEnemy = enemy.transform.position - transform.position;
        toEnemy.y = 0f;
        float distance = toEnemy.magnitude;
        if (distance <= 0.001f || distance > maxRange)
        {
            return false;
        }

        float targetAngle = useCameraForward ? meleeData.frontAngle : Mathf.Max(meleeData.frontAngle, 120f);
        float angleLimit = Mathf.Clamp(targetAngle, 1f, 180f) * 0.5f;
        Vector3 forward = useCameraForward ? GetMeleeForward() : GetMeleeAutoForward();
        return Vector3.Angle(forward, toEnemy.normalized) <= angleLimit;
    }

    float ApplyMeleeCriticalDamage(PlayerMeleeData meleeData, Enemy enemy, float damage, out bool isCritical)
    {
        isCritical = false;
        if (meleeData == null || enemy == null || enemy.enemyData == null)
        {
            return damage;
        }

        float resistance = GetEnemyModifiedStat(enemy, StatusEffectStat.EnemyCriticalResistance, enemy.enemyData.criticalResistance);
        float finalChance = Mathf.Clamp(meleeData.criticalChance - resistance, 0f, 100f);
        if (!RollPercent(finalChance))
        {
            return damage;
        }

        isCritical = true;
        return damage + (damage * Mathf.Max(0f, meleeData.criticalDamagePercent) / 100f);
    }

    void TryApplyMeleeKnockback(PlayerMeleeData meleeData, Enemy enemy, Vector3 direction)
    {
        if (meleeData == null || enemy == null || enemy.enemyData == null || !enemy.enemyData.canBeKnockedBack)
        {
            return;
        }

        float resistance = GetEnemyModifiedStat(enemy, StatusEffectStat.EnemyKnockbackResistance, enemy.enemyData.knockbackResistance);
        float finalChance = Mathf.Clamp(meleeData.knockbackChance - resistance, 0f, 100f);
        if (!RollPercent(finalChance))
        {
            return;
        }

        float powerDifference = meleeData.knockbackPower - resistance;
        float distance = Mathf.Clamp(
            powerDifference / 100f * meleeData.maxKnockbackDistance,
            0f,
            meleeData.maxKnockbackDistance
        );

        enemy.ApplyKnockback(direction, distance, meleeData.knockbackDuration);
    }

    float GetEnemyModifiedStat(Enemy enemy, StatusEffectStat stat, float baseValue)
    {
        if (enemy == null)
        {
            return baseValue;
        }

        EnemyStatusEffectController statusController = enemy.GetComponent<EnemyStatusEffectController>();
        return statusController != null ? statusController.ModifyStat(stat, baseValue) : baseValue;
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

    void RotateTowardMeleeTarget(Vector3 direction, float speed)
    {
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Mathf.Max(0.01f, speed) * Time.deltaTime);
    }

    void MovePlayerForMelee(Vector3 movement)
    {
        if (playerMovement != null && playerMovement.MoveActionAlongGround(movement))
        {
            return;
        }

        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        if (characterController != null && characterController.enabled)
        {
            movement.y = -2f * Time.deltaTime;
            characterController.Move(movement);
            return;
        }

        transform.position += movement;
    }

    Vector3 GetMeleeOrigin()
    {
        return cameraTransform != null ? cameraTransform.position : transform.position + Vector3.up;
    }

    Vector3 GetMeleeForward()
    {
        Transform originTransform = cameraTransform != null ? cameraTransform : transform;
        Vector3 forward = originTransform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = transform.forward;
            forward.y = 0f;
        }

        return forward.normalized;
    }

    Vector3 GetMeleeAutoForward()
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            return GetMeleeForward();
        }

        return forward.normalized;
    }

    Vector3 GetDirectionToEnemy(Enemy enemy)
    {
        Vector3 direction = enemy != null ? enemy.transform.position - transform.position : transform.forward;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = GetMeleeForward();
        }

        return direction.normalized;
    }

    Vector3 GetEnemyHitPoint(Enemy enemy, Vector3 direction)
    {
        if (enemy == null)
        {
            return transform.position + direction;
        }

        Collider collider = enemy.GetComponentInChildren<Collider>();
        if (collider != null)
        {
            return collider.ClosestPoint(transform.position + Vector3.up);
        }

        return enemy.transform.position - direction * 0.25f + Vector3.up;
    }

    int GetHitMask()
    {
        if (usePlayerShootHitMask && playerShoot != null)
        {
            return playerShoot.hitMask;
        }

        return hitMask;
    }

    void DrawMissDebug(PlayerMeleeData meleeData)
    {
        if (!drawDebugLaser || meleeData == null)
        {
            return;
        }

        Vector3 origin = GetMeleeOrigin();
        Debug.DrawLine(origin, origin + GetMeleeForward() * Mathf.Max(0.01f, meleeData.range), Color.cyan, debugLaserDuration);
    }

    void SpawnEnemyHitEffect(PlayerMeleeData meleeData, Vector3 hitPoint, Vector3 hitNormal, bool isCritical)
    {
        if (meleeData == null)
        {
            return;
        }

        GameObject effect = null;
        if (isCritical && meleeData.criticalHitEffect != null)
        {
            effect = meleeData.criticalHitEffect;
        }
        else if (meleeData.hitEffect != null)
        {
            effect = meleeData.hitEffect;
        }

        if (effect == null)
        {
            return;
        }

        if (hitNormal.sqrMagnitude < 0.0001f)
        {
            hitNormal = Vector3.up;
        }

        GameObject instance = LeanPool.Spawn(
            effect,
            hitPoint + hitNormal.normalized * impactSurfaceOffset,
            Quaternion.LookRotation(hitNormal.normalized)
        );

        if (impactEffectLifetime > 0f)
        {
            LeanPool.Despawn(instance, impactEffectLifetime);
        }
    }

    void UpdateStealthPrompt(Enemy target)
    {
        if (target == null && stealthPromptText == null)
        {
            return;
        }

        if (stealthPromptText == null)
        {
            EnsureStealthPromptText();
        }

        if (stealthPromptText == null)
        {
            return;
        }

        bool show = target != null && CanReadInput();
        stealthPromptText.gameObject.SetActive(show);
        if (!show)
        {
            return;
        }

        string promptMessage = string.IsNullOrEmpty(stealthPromptMessage)
            ? "Tekan B - Stealth Melee"
            : stealthPromptMessage;
        stealthPromptText.text = LocalizationManager.GetText(promptMessage);
        stealthPromptText.color = stealthPromptColor;
    }

    void EnsureStealthPromptText()
    {
        Canvas canvas = FindStealthPromptCanvas();
        if (canvas == null)
        {
            return;
        }

        GameObject textObject = new GameObject("Stealth Melee Prompt", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(canvas.transform, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = stealthPromptAnchoredPosition;
        rect.sizeDelta = new Vector2(520f, 64f);

        stealthPromptText = textObject.GetComponent<TMP_Text>();
        stealthPromptText.alignment = TextAlignmentOptions.Center;
        stealthPromptText.fontSize = stealthPromptFontSize;
        stealthPromptText.raycastTarget = false;
        stealthPromptText.color = stealthPromptColor;
        stealthPromptText.text = LocalizationManager.GetText(stealthPromptMessage);
        stealthPromptText.gameObject.SetActive(false);
    }

    Canvas FindStealthPromptCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
        Canvas fallback = null;
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace)
            {
                continue;
            }

            if (fallback == null)
            {
                fallback = canvas;
            }

            if (canvas.isActiveAndEnabled)
            {
                return canvas;
            }
        }

        return fallback;
    }

    void PlayRandomSound(AudioClip[] clips, AudioClip fallbackClip, float volume)
    {
        AudioClip clip = GetRandomClip(clips);
        if (clip == null)
        {
            clip = fallbackClip;
        }

        PlaySound(clip, volume);
    }

    AudioClip GetRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip != null)
            {
                return clip;
            }
        }

        return null;
    }

    void PlaySound(AudioClip clip, float volume)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip, volume);
        }
    }
}
