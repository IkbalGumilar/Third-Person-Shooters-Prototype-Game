using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerWeaponAnimator : MonoBehaviour
{
    private const string SwitchLayerOwner = "Player.WeaponSwitch";
    private const string ReloadLayerOwner = "Player.WeaponReload";
    private const string MeleeLayerOwner = "Player.WeaponMelee";
    private const string ShootLayerOwner = "Player.WeaponShoot";

    public Animator animator;
    public string aimLayerName = "lapisan Bidik";
    public string switchLayerName = "Tukar Senjata";
    public string reloadLayerName = "Reload";
    public string meleeOneHandLayerName = "Armed-Melee";
    public string meleeTwoHandLayerName = "2Hand-Shooting-Melee";
    public float aimLayerBlendSpeed = 8f;
    public float shootAimThreshold = 0.95f;
    public float switchCrossFade = 0.08f;
    public float reloadCrossFade = 0.08f;
    public float switchReturnCrossFade = 0.16f;
    public float reloadReturnCrossFade = 0.16f;
    public float switchLayerFadeOut = 0.12f;
    public float reloadLayerFadeOut = 0.12f;
    public float meleeLayerFadeOut = 0.12f;
    public float switchEndHold = 0.05f;
    public float reloadEndHold = 0.05f;
    [Range(0.8f, 1.05f)] public float reloadFinishNormalizedTime = 0.98f;
    public float reloadFinishMaxWait = 0.25f;
    public bool allowAimInput = true;
    public bool statusBlocksAiming;

    [Header("Shoot Layer Blending")]
    public bool keepOneHandAimLayerDuringShoot = true;
    public bool keepTwoHandAimLayerDuringShoot;

    [Header("Weapon Layer Defaults")]
    public string defaultOneHandAimLayerName = "Armed-Pistol-Idle";
    public string defaultTwoHandAimLayerName = "2Hand-Shooting-Aim";
    public string defaultOneHandShootLayerName = "1Hand-Pistol-Single-Fire";
    public string defaultTwoHandShootLayerName = "2Hand-Shooting-Fire";
    public string defaultOneHandReloadLayerName = "1Hand-Pistol-Reload";
    public string defaultTwoHandReloadLayerName = "2Hand-Shooting-Reload";
    public string defaultOneHandSwitchLayerName = "Armed-Equip-Switch";
    public string defaultTwoHandSwitchLayerName = "2Hand-Shooting-Equip-Interact";
    public string[] managedWeaponLayerNames =
    {
        "lapisan Bidik",
        "Tukar Senjata",
        "Reload",
        "1Hand-Pistol",
        "1Hand-Pistol-Single-Fire",
        "1Hand-Pistol-Dual-Fire",
        "1Hand-Pistol-Reload",
        "1Hand-Sword",
        "Armed-Pistol-Idle",
        "Armed-Melee",
        "Armed-Equip-Switch",
        "2Hand-Shooting-Fire",
        "2Hand-Shooting-Equip-Interact",
        "2Hand-Shooting-Aim",
        "2Hand-Shooting-Melee",
        "2Hand-Shooting-Reload",
        "Unarmed-Melee"
    };

    private int aimLayerIndex = -1;
    private int shootLayerIndex = -1;
    private int switchLayerIndex = -1;
    private int reloadLayerIndex = -1;
    private int meleeOneHandLayerIndex = -1;
    private int meleeTwoHandLayerIndex = -1;
    private float aimLayerWeight;
    private float externalLayerControlLockedUntil;
    private string currentAimLayerName = "lapisan Bidik";
    private string currentShootLayerName = "lapisan Bidik";
    private string currentReloadLayerName = "Reload";
    private string currentSwitchLayerName = "Tukar Senjata";
    private string currentAimStateName = "Shooting-Aiming-CM";
    private string currentShootStateName = "Shooting-Aiming-Fire-CM";
    private WeaponHoldType currentHoldType = WeaponHoldType.TwoHand;
    private float currentShootAnimationDuration = 0.12f;
    private bool hasIsAimingParameter;
    private bool isPlayingSwitchState;
    private bool isReturningFromSwitchState;
    private bool isPlayingReloadState;
    private bool isPlayingMeleeState;
    private bool switchSuppressedAimLayer;
    private bool externalActionOverride;
    private bool holdAimWeightWhileInputDisabled;
    private bool shootSuppressedAimLayer;
    private bool reloadSuppressedAimLayer;
    private readonly List<int> managedLayerIndices = new List<int>();
    private Coroutine shootRoutine;
    private Coroutine shootFadeRoutine;
    private Coroutine switchFadeRoutine;
    private Coroutine reloadFadeRoutine;
    private Coroutine reloadReturnRoutine;
    private Coroutine meleeRoutine;
    private Coroutine meleeOneHandFadeRoutine;
    private Coroutine meleeTwoHandFadeRoutine;
    private readonly Dictionary<int, Coroutine> meleeFadeRoutines = new Dictionary<int, Coroutine>();
    private int currentMeleeLayerIndex = -1;
    private AnimationLayerGuard animationLayerGuard;
    private KontrolPemain kontrolPemain;

    public bool IsAiming => aimLayerWeight >= shootAimThreshold;
    public float AimWeight => aimLayerWeight;
    public bool IsPlayingSwitchAnimation => isPlayingSwitchState;
    public bool IsPlayingReloadAnimation => isPlayingReloadState;
    public bool IsPlayingMeleeAnimation => isPlayingMeleeState;
    public bool IsActionAnimationPlaying => isPlayingSwitchState || isPlayingReloadState || isPlayingMeleeState;

    void Awake()
    {
        kontrolPemain = new KontrolPemain();
        animator = animator != null ? animator : GetComponent<Animator>();
        AnimationEventReceiver.EnsureOn(animator);
        animationLayerGuard = AnimationLayerGuard.GetOrAdd(animator);
        ResolveAnimatorLayers();
        SetManagedWeaponLayerWeights(0f);
        SetAimLayerWeight(0f);
        SetSwitchLayerWeight(0f);
        SetReloadLayerWeight(0f);
        SetMeleeLayerWeight(meleeOneHandLayerIndex, 0f);
        SetMeleeLayerWeight(meleeTwoHandLayerIndex, 0f);
    }

    void OnEnable()
    {
        kontrolPemain?.Pemain.Enable();
    }

    void OnDisable()
    {
        kontrolPemain?.Pemain.Disable();
        StopAllActionCoroutinesForCleanup();
        animationLayerGuard?.ReleaseOwner(SwitchLayerOwner);
        animationLayerGuard?.ReleaseOwner(ReloadLayerOwner);
        animationLayerGuard?.ReleaseOwner(MeleeLayerOwner);
        animationLayerGuard?.ReleaseOwner(ShootLayerOwner);
        isPlayingSwitchState = false;
        isReturningFromSwitchState = false;
        isPlayingReloadState = false;
        isPlayingMeleeState = false;
        currentMeleeLayerIndex = -1;
        shootSuppressedAimLayer = false;
        reloadSuppressedAimLayer = false;
        switchSuppressedAimLayer = false;
        aimLayerWeight = 0f;
        SetAimLayerWeight(0f);
        SetAnimatorBool("IsAiming", false);
    }

    void OnDestroy()
    {
        kontrolPemain?.Dispose();
    }

    void Update()
    {
        float targetWeight = !allowAimInput && holdAimWeightWhileInputDisabled
            ? aimLayerWeight
            : allowAimInput && !statusBlocksAiming && IsAimPressed() ? 1f : 0f;
        aimLayerWeight = Mathf.MoveTowards(
            aimLayerWeight,
            targetWeight,
            aimLayerBlendSpeed * Time.deltaTime
        );

        if (externalActionOverride)
        {
            SetAnimatorBool("IsAiming", aimLayerWeight > 0.01f);
            return;
        }

        if (IsLayerControlLockedByExternalAnimation())
        {
            SetAnimatorBool("IsAiming", aimLayerWeight > 0.01f);
            return;
        }

        if (isPlayingSwitchState)
        {
            if (switchSuppressedAimLayer)
            {
                SetAimLayerWeight(0f);
            }
            else if (switchLayerIndex == aimLayerIndex)
            {
                SetAimLayerWeight(1f);
            }

            SetAnimatorBool("IsAiming", aimLayerWeight > 0.01f);
            return;
        }

        if (isReturningFromSwitchState && switchLayerIndex == aimLayerIndex)
        {
            SetAnimatorBool("IsAiming", aimLayerWeight > 0.01f);
            return;
        }

        if (IsMeleeUsingAimLayer())
        {
            SetAimLayerWeight(1f);
            SetAnimatorBool("IsAiming", aimLayerWeight > 0.01f);
            return;
        }

        if (IsAimLayerLockedByAction())
        {
            SetAimLayerWeight(1f);
        }
        else
        {
            SetAimLayerWeight(IsAimLayerSuppressed() ? 0f : aimLayerWeight);
        }

        SetAnimatorBool("IsAiming", aimLayerWeight > 0.01f);
    }

    public void LockLayerControl(float duration)
    {
        externalLayerControlLockedUntil = Mathf.Max(externalLayerControlLockedUntil, Time.time + Mathf.Max(0f, duration));
        StopShootFade();
        StopSwitchFade();
        StopReloadFade();
        StopMeleeFade(meleeOneHandLayerIndex);
        StopMeleeFade(meleeTwoHandLayerIndex);
    }

    // A held action such as blocking owns its animation layer until it releases.
    public void SetExternalActionOverride(bool active)
    {
        externalActionOverride = active;
        if (!active)
        {
            return;
        }

        StopShootFade();
        StopMeleeFade(meleeOneHandLayerIndex);
        StopMeleeFade(meleeTwoHandLayerIndex);
    }

    public void SetWeapon(Weapon weapon)
    {
        if (weapon == null || externalActionOverride)
        {
            return;
        }

        ResolveAnimatorLayers();
        currentHoldType = weapon.holdType;
        shootSuppressedAimLayer = false;
        reloadSuppressedAimLayer = false;
        StopReloadReturn();
        StopReloadFade();

        currentAimStateName = string.IsNullOrEmpty(weapon.aimStateName)
            ? GetDefaultAimState(weapon.holdType)
            : weapon.aimStateName;
        currentShootStateName = string.IsNullOrEmpty(weapon.shootStateName)
            ? GetDefaultShootState(weapon.holdType)
            : weapon.shootStateName;
        currentShootAnimationDuration = weapon.shootAnimationDuration;

        currentAimLayerName = ResolveConfiguredLayerName(
            weapon.animationLayerName,
            GetDefaultAimLayerName(weapon.holdType),
            aimLayerName
        );
        currentShootLayerName = ResolveConfiguredLayerName(
            weapon.shootLayerName,
            GetDefaultShootLayerName(weapon.holdType),
            currentAimLayerName
        );
        currentReloadLayerName = ResolveConfiguredLayerName(
            weapon.reloadLayerName,
            GetDefaultReloadLayerName(weapon.holdType),
            reloadLayerName
        );
        currentSwitchLayerName = ResolveConfiguredLayerName(
            weapon.switchLayerName,
            GetDefaultSwitchLayerName(weapon.holdType),
            switchLayerName
        );

        aimLayerIndex = ResolveLayerIndexForState(currentAimLayerName, aimLayerName, currentAimStateName);
        shootLayerIndex = ResolveLayerIndexForState(currentShootLayerName, currentAimLayerName, currentShootStateName);
        reloadLayerIndex = ResolveLayerIndex(currentReloadLayerName, reloadLayerName);
        switchLayerIndex = ResolveLayerIndex(currentSwitchLayerName, currentAimLayerName, switchLayerName);

        bool preserveActiveSwitch = isPlayingSwitchState && switchLayerIndex >= 0;
        SetManagedWeaponLayerWeights(0f, preserveActiveSwitch ? switchLayerIndex : -1);
        if (!preserveActiveSwitch)
        {
            SetAimLayerWeight(aimLayerWeight);
        }

        if (animator == null || aimLayerIndex < 0)
        {
            return;
        }

        if (!preserveActiveSwitch)
        {
            PlayAimState();
        }
    }

    public void SetAimInputEnabled(bool enabled, bool resetAimWeight)
    {
        SetAimInputEnabled(enabled, resetAimWeight, false);
    }

    public void SetAimInputEnabled(bool enabled, bool resetAimWeight, bool preserveCurrentAimWeight)
    {
        allowAimInput = enabled;
        holdAimWeightWhileInputDisabled = !enabled && preserveCurrentAimWeight;

        if (resetAimWeight)
        {
            shootSuppressedAimLayer = false;
            reloadSuppressedAimLayer = false;
            aimLayerWeight = 0f;
            holdAimWeightWhileInputDisabled = false;
            SetAimLayerWeight(0f);
            SetAnimatorBool("IsAiming", false);
        }
    }

    public float PlaySwitchState(Weapon weapon)
    {
        if (externalActionOverride || weapon == null || animator == null || string.IsNullOrEmpty(weapon.switchInStateName))
        {
            return 0f;
        }

        string targetSwitchLayerName = ResolveConfiguredLayerName(
            weapon.switchLayerName,
            GetDefaultSwitchLayerName(weapon.holdType),
            switchLayerName
        );
        int layerIndex = ResolveLayerIndexForState(targetSwitchLayerName, currentAimLayerName, weapon.switchInStateName);
        if (layerIndex < 0 || !TryGetStateHash(layerIndex, GetLayerName(layerIndex, targetSwitchLayerName), weapon.switchInStateName, out int stateHash))
        {
            return 0f;
        }

        if (!TryClaimActionLayer(layerIndex, SwitchLayerOwner))
        {
            return 0f;
        }

        currentSwitchLayerName = GetLayerName(layerIndex, targetSwitchLayerName);
        switchLayerIndex = layerIndex;
        isPlayingSwitchState = true;
        switchSuppressedAimLayer = switchLayerIndex != aimLayerIndex;
        StopSwitchFade();
        if (switchSuppressedAimLayer)
        {
            SetAimLayerWeight(0f);
        }

        SetLayerWeight(layerIndex, 1f);
        PlayStateSmooth(stateHash, layerIndex, switchCrossFade);

        float stateDuration = GetActiveStateDuration(layerIndex);
        return Mathf.Max(weapon.switchDuration, stateDuration) + Mathf.Max(0f, switchEndHold);
    }

    public void StopSwitchState()
    {
        if (switchLayerIndex < 0)
        {
            isPlayingSwitchState = false;
            switchSuppressedAimLayer = false;
            return;
        }

        StopSwitchFade();
        isPlayingSwitchState = false;
        bool restoreSuppressedAimLayer = switchSuppressedAimLayer;
        switchSuppressedAimLayer = false;
        if (switchLayerIndex == aimLayerIndex)
        {
            isReturningFromSwitchState = true;
            PlayAimState(switchReturnCrossFade);
            switchFadeRoutine = StartCoroutine(FadeSwitchAimLayerToCurrentAimWeight());
            return;
        }

        PlayAimState(switchReturnCrossFade);
        float targetAimWeight = restoreSuppressedAimLayer
            ? aimLayerWeight
            : aimLayerIndex >= 0 ? animator.GetLayerWeight(aimLayerIndex) : 0f;
        switchFadeRoutine = StartCoroutine(FadeWeaponActionLayers(
            switchLayerIndex,
            0f,
            aimLayerIndex,
            targetAimWeight,
            Mathf.Max(switchLayerFadeOut, switchReturnCrossFade),
            SwitchLayerOwner
        ));
    }

    public void PlayAimState()
    {
        PlayAimState(0.1f);
    }

    public void PlayAimState(float crossFade)
    {
        if (animator == null || aimLayerIndex < 0 || string.IsNullOrEmpty(currentAimStateName))
        {
            return;
        }

        if (!TryGetStateHash(aimLayerIndex, currentAimLayerName, currentAimStateName, out int stateHash))
        {
            return;
        }

        animator.CrossFadeInFixedTime(stateHash, Mathf.Max(0f, crossFade), aimLayerIndex);
    }

    void PlayAimStateIfNeeded(float crossFade)
    {
        if (IsStateActiveOrNext(aimLayerIndex, currentAimLayerName, currentAimStateName))
        {
            return;
        }

        PlayAimState(crossFade);
    }

    public void PlayShootState()
    {
        if (externalActionOverride || animator == null || string.IsNullOrEmpty(currentShootStateName))
        {
            return;
        }

        if (shootRoutine != null)
        {
            StopCoroutine(shootRoutine);
            shootRoutine = null;
            shootSuppressedAimLayer = false;
            SetAimLayerWeight(aimLayerWeight);
        }

        shootRoutine = StartCoroutine(PlayShootThenAim());
    }

    public float PlayReloadState(string stateName, float fallbackDuration = 0f)
    {
        if (externalActionOverride || animator == null || string.IsNullOrEmpty(stateName))
        {
            return Mathf.Max(0f, fallbackDuration);
        }

        int layerIndex = reloadLayerIndex;
        if (layerIndex < 0 || !TryGetStateHash(layerIndex, GetLayerName(layerIndex, currentReloadLayerName), stateName, out int stateHash))
        {
            layerIndex = ResolveLayerIndexForState(currentReloadLayerName, currentAimLayerName, stateName);
            if (layerIndex < 0 || !TryGetStateHash(layerIndex, GetLayerName(layerIndex, currentReloadLayerName), stateName, out stateHash))
            {
                return Mathf.Max(0f, fallbackDuration);
            }
        }

        reloadLayerIndex = layerIndex;
        if (!TryClaimActionLayer(reloadLayerIndex, ReloadLayerOwner))
        {
            return Mathf.Max(0f, fallbackDuration);
        }

        reloadSuppressedAimLayer = layerIndex != aimLayerIndex;
        StopReloadFade();
        StopReloadReturn();
        isPlayingReloadState = true;
        if (reloadSuppressedAimLayer)
        {
            SetAimLayerWeight(0f);
        }

        SetLayerWeight(layerIndex, 1f);
        PlayStateSmooth(stateHash, layerIndex, reloadCrossFade);

        float stateDuration = GetActiveStateDuration(layerIndex);
        return Mathf.Max(fallbackDuration, stateDuration) + Mathf.Max(0f, reloadEndHold);
    }

    public void StopReloadState()
    {
        if (reloadLayerIndex < 0)
        {
            return;
        }

        StopReloadFade();
        StopReloadReturn();
        bool restoreAimLayer = reloadSuppressedAimLayer;
        int stoppingReloadLayerIndex = reloadLayerIndex;
        isPlayingReloadState = false;
        reloadSuppressedAimLayer = false;
        reloadReturnRoutine = StartCoroutine(FinishReloadTransition(stoppingReloadLayerIndex, restoreAimLayer));
    }

    public float PlayRandomMeleeState(PlayerMeleeData meleeData)
    {
        if (externalActionOverride || meleeData == null || animator == null)
        {
            return 0f;
        }

        string fallbackLayerName = meleeData.holdType == WeaponHoldType.OneHand ? meleeOneHandLayerName : meleeTwoHandLayerName;
        string layerName = string.IsNullOrEmpty(meleeData.animationLayerName) ? fallbackLayerName : meleeData.animationLayerName;
        int layerIndex = ResolveLayerIndexForState(layerName, fallbackLayerName, GetFirstStateName(meleeData.attackStateNames));
        string[] stateNames = meleeData.attackStateNames;

        if (layerIndex < 0 || stateNames == null || stateNames.Length == 0)
        {
            return 0f;
        }

        layerName = GetLayerName(layerIndex, layerName);
        if (!TryGetRandomStateHash(layerIndex, layerName, stateNames, out int stateHash))
        {
            return 0f;
        }

        if (!TryClaimActionLayer(layerIndex, MeleeLayerOwner))
        {
            return 0f;
        }

        if (meleeRoutine != null)
        {
            StopCoroutine(meleeRoutine);
        }

        StopMeleeFade(layerIndex);
        SetMeleeLayerWeight(layerIndex, 1f);
        isPlayingMeleeState = true;
        currentMeleeLayerIndex = layerIndex;
        animator.CrossFadeInFixedTime(stateHash, Mathf.Max(0f, meleeData.crossFade), layerIndex);

        float duration = Mathf.Max(meleeData.animationDuration, GetActiveStateDuration(layerIndex));
        meleeRoutine = StartCoroutine(StopMeleeAfterDelay(layerIndex, duration));
        return duration;
    }

    string GetFirstStateName(string[] stateNames)
    {
        if (stateNames == null)
        {
            return string.Empty;
        }

        for (int i = 0; i < stateNames.Length; i++)
        {
            if (!string.IsNullOrEmpty(stateNames[i]))
            {
                return stateNames[i];
            }
        }

        return string.Empty;
    }

    public void StopMeleeState()
    {
        if (meleeRoutine != null)
        {
            StopCoroutine(meleeRoutine);
            meleeRoutine = null;
        }

        FadeMeleeLayerWeight(meleeOneHandLayerIndex, 0f);
        FadeMeleeLayerWeight(meleeTwoHandLayerIndex, 0f);
        StopMeleeLayer(currentMeleeLayerIndex);
        animationLayerGuard?.ReleaseOwner(MeleeLayerOwner);
        isPlayingMeleeState = false;
        currentMeleeLayerIndex = -1;
    }

    IEnumerator PlayShootThenAim()
    {
        int layerIndex = shootLayerIndex >= 0 ? shootLayerIndex : aimLayerIndex;
        if (layerIndex < 0 || !TryGetStateHash(layerIndex, GetLayerName(layerIndex, currentShootLayerName), currentShootStateName, out int stateHash))
        {
            shootRoutine = null;
            yield break;
        }

        if (!TryClaimActionLayer(layerIndex, ShootLayerOwner))
        {
            shootRoutine = null;
            yield break;
        }

        StopShootFade();
        bool usesSeparateShootLayer = layerIndex != aimLayerIndex;
        bool suppressAimLayerForShoot = usesSeparateShootLayer && !ShouldKeepAimLayerDuringShoot();
        if (suppressAimLayerForShoot)
        {
            shootSuppressedAimLayer = true;
        }

        SetLayerWeight(layerIndex, 1f);
        animator.CrossFadeInFixedTime(stateHash, 0.03f, layerIndex, 0f);

        if (currentShootAnimationDuration > 0f)
        {
            yield return new WaitForSeconds(currentShootAnimationDuration);
        }

        if (layerIndex == aimLayerIndex)
        {
            PlayAimStateIfNeeded(0.08f);
            SetAimLayerWeight(aimLayerWeight);
        }
        else
        {
            if (suppressAimLayerForShoot)
            {
                shootSuppressedAimLayer = false;
            }

            shootFadeRoutine = StartCoroutine(FadeLayerWeight(layerIndex, 0f, switchLayerFadeOut));
            PlayAimStateIfNeeded(0.08f);
            SetAimLayerWeight(aimLayerWeight);
        }

        ReleaseActionLayer(layerIndex, ShootLayerOwner);
        shootRoutine = null;
    }

    bool ShouldKeepAimLayerDuringShoot()
    {
        return currentHoldType == WeaponHoldType.OneHand
            ? keepOneHandAimLayerDuringShoot
            : keepTwoHandAimLayerDuringShoot;
    }

    bool TryClaimActionLayer(int layerIndex, string owner)
    {
        if (layerIndex <= 0 || layerIndex == aimLayerIndex)
        {
            return true;
        }

        animationLayerGuard = animationLayerGuard != null
            ? animationLayerGuard
            : AnimationLayerGuard.GetOrAdd(animator);
        return animationLayerGuard == null || animationLayerGuard.TryClaim(layerIndex, owner, AnimationLayerPriority.WeaponAction);
    }

    void ReleaseActionLayer(int layerIndex, string owner)
    {
        if (layerIndex > 0 && layerIndex != aimLayerIndex)
        {
            animationLayerGuard?.Release(layerIndex, owner);
        }
    }

    void ResolveAnimatorLayers()
    {
        if (animator == null)
        {
            return;
        }

        aimLayerIndex = ResolveLayerIndex(aimLayerName);
        shootLayerIndex = aimLayerIndex;
        switchLayerIndex = ResolveLayerIndex(switchLayerName);
        reloadLayerIndex = ResolveLayerIndex(reloadLayerName);
        meleeOneHandLayerIndex = ResolveLayerIndex(meleeOneHandLayerName);
        meleeTwoHandLayerIndex = ResolveLayerIndex(meleeTwoHandLayerName);
        ResolveManagedWeaponLayers();
        CacheAnimatorParameters();
    }

    bool IsAimLayerSuppressed()
    {
        return shootSuppressedAimLayer || reloadSuppressedAimLayer || switchSuppressedAimLayer;
    }

    bool IsAimLayerLockedByAction()
    {
        return aimLayerIndex >= 0
            && ((isPlayingSwitchState && switchLayerIndex == aimLayerIndex)
                || (isPlayingReloadState && reloadLayerIndex == aimLayerIndex));
    }

    bool IsLayerControlLockedByExternalAnimation()
    {
        return Time.time < externalLayerControlLockedUntil;
    }

    bool IsMeleeUsingAimLayer()
    {
        return isPlayingMeleeState && currentMeleeLayerIndex >= 0 && currentMeleeLayerIndex == aimLayerIndex;
    }

    void ResolveManagedWeaponLayers()
    {
        managedLayerIndices.Clear();
        AddManagedLayer(aimLayerName);
        AddManagedLayer(switchLayerName);
        AddManagedLayer(reloadLayerName);
        AddManagedLayer(defaultOneHandAimLayerName);
        AddManagedLayer(defaultTwoHandAimLayerName);
        AddManagedLayer(defaultOneHandShootLayerName);
        AddManagedLayer(defaultTwoHandShootLayerName);
        AddManagedLayer(defaultOneHandReloadLayerName);
        AddManagedLayer(defaultTwoHandReloadLayerName);
        AddManagedLayer(defaultOneHandSwitchLayerName);
        AddManagedLayer(defaultTwoHandSwitchLayerName);

        if (managedWeaponLayerNames == null)
        {
            return;
        }

        for (int i = 0; i < managedWeaponLayerNames.Length; i++)
        {
            AddManagedLayer(managedWeaponLayerNames[i]);
        }
    }

    void AddManagedLayer(string layerName)
    {
        int layerIndex = ResolveLayerIndex(layerName);
        if (layerIndex < 0 || managedLayerIndices.Contains(layerIndex))
        {
            return;
        }

        managedLayerIndices.Add(layerIndex);
    }

    void SetManagedWeaponLayerWeights(float weight)
    {
        SetManagedWeaponLayerWeights(weight, -1);
    }

    void SetManagedWeaponLayerWeights(float weight, int excludedLayerIndex)
    {
        for (int i = 0; i < managedLayerIndices.Count; i++)
        {
            if (managedLayerIndices[i] == excludedLayerIndex)
            {
                continue;
            }

            SetLayerWeight(managedLayerIndices[i], weight);
        }
    }

    int ResolveLayerIndex(params string[] layerNames)
    {
        if (animator == null || layerNames == null)
        {
            return -1;
        }

        for (int i = 0; i < layerNames.Length; i++)
        {
            if (string.IsNullOrEmpty(layerNames[i]))
            {
                continue;
            }

            int layerIndex = animator.GetLayerIndex(layerNames[i]);
            if (layerIndex >= 0)
            {
                return layerIndex;
            }
        }

        return -1;
    }

    int ResolveLayerIndexForState(string preferredLayerName, string fallbackLayerName, string stateName)
    {
        int preferredLayerIndex = ResolveLayerIndex(preferredLayerName);
        if (HasState(preferredLayerIndex, preferredLayerName, stateName))
        {
            return preferredLayerIndex;
        }

        int fallbackLayerIndex = ResolveLayerIndex(fallbackLayerName);
        if (HasState(fallbackLayerIndex, fallbackLayerName, stateName))
        {
            return fallbackLayerIndex;
        }

        for (int i = 0; i < managedLayerIndices.Count; i++)
        {
            int layerIndex = managedLayerIndices[i];
            if (HasState(layerIndex, GetLayerName(layerIndex, string.Empty), stateName))
            {
                return layerIndex;
            }
        }

        if (animator != null)
        {
            for (int i = 0; i < animator.layerCount; i++)
            {
                if (HasState(i, animator.GetLayerName(i), stateName))
                {
                    return i;
                }
            }
        }

        if (preferredLayerIndex >= 0)
        {
            return preferredLayerIndex;
        }

        return fallbackLayerIndex;
    }

    bool HasState(int layerIndex, string layerName, string stateName)
    {
        if (animator == null || layerIndex < 0 || string.IsNullOrEmpty(stateName))
        {
            return false;
        }

        return TryGetStateHash(layerIndex, layerName, stateName, out _);
    }

    string ResolveConfiguredLayerName(string configuredLayerName, string defaultLayerName, string fallbackLayerName)
    {
        if (!string.IsNullOrEmpty(configuredLayerName))
        {
            return configuredLayerName;
        }

        if (!string.IsNullOrEmpty(defaultLayerName))
        {
            return defaultLayerName;
        }

        return fallbackLayerName;
    }

    string GetDefaultAimLayerName(WeaponHoldType holdType)
    {
        return holdType == WeaponHoldType.OneHand ? defaultOneHandAimLayerName : defaultTwoHandAimLayerName;
    }

    string GetDefaultShootLayerName(WeaponHoldType holdType)
    {
        return holdType == WeaponHoldType.OneHand ? defaultOneHandShootLayerName : defaultTwoHandShootLayerName;
    }

    string GetDefaultReloadLayerName(WeaponHoldType holdType)
    {
        return holdType == WeaponHoldType.OneHand ? defaultOneHandReloadLayerName : defaultTwoHandReloadLayerName;
    }

    string GetDefaultSwitchLayerName(WeaponHoldType holdType)
    {
        return holdType == WeaponHoldType.OneHand ? defaultOneHandSwitchLayerName : defaultTwoHandSwitchLayerName;
    }

    string GetDefaultAimState(WeaponHoldType holdType)
    {
        return holdType == WeaponHoldType.OneHand ? "Armed-Idle-Pistol-R-Static" : "Shooting-Aiming-CM";
    }

    string GetDefaultShootState(WeaponHoldType holdType)
    {
        return holdType == WeaponHoldType.OneHand ? "Pistol-Attack-R1" : "Shooting-Aiming-Fire-CM";
    }

    void SetAimLayerWeight(float weight)
    {
        SetLayerWeight(aimLayerIndex, weight);
    }

    void SetSwitchLayerWeight(float weight)
    {
        SetLayerWeight(switchLayerIndex, weight);
    }

    void SetReloadLayerWeight(float weight)
    {
        SetLayerWeight(reloadLayerIndex, weight);
    }

    void SetMeleeLayerWeight(int layerIndex, float weight)
    {
        SetLayerWeight(layerIndex, weight);
    }

    void SetLayerWeight(int layerIndex, float weight)
    {
        if (animator != null && layerIndex >= 0)
        {
            animator.SetLayerWeight(layerIndex, Mathf.Clamp01(weight));
        }
    }

    void PlayStateSmooth(int stateHash, int layerIndex, float crossFade)
    {
        if (animator == null || layerIndex < 0)
        {
            return;
        }

        float transition = Mathf.Max(0f, crossFade);
        if (transition > 0f)
        {
            animator.CrossFadeInFixedTime(stateHash, transition, layerIndex, 0f);
        }
        else
        {
            animator.Play(stateHash, layerIndex, 0f);
        }

        animator.Update(0f);
    }

    float GetActiveStateDuration(int layerIndex)
    {
        if (animator == null || layerIndex < 0)
        {
            return 0f;
        }

        AnimatorStateInfo stateInfo = animator.IsInTransition(layerIndex)
            ? animator.GetNextAnimatorStateInfo(layerIndex)
            : animator.GetCurrentAnimatorStateInfo(layerIndex);
        return stateInfo.length > 0f ? stateInfo.length : 0f;
    }

    void FadeMeleeLayerWeight(int layerIndex, float targetWeight)
    {
        if (layerIndex < 0 || animator == null)
        {
            return;
        }

        StopMeleeFade(layerIndex);
        Coroutine fadeRoutine = StartCoroutine(FadeLayerWeight(layerIndex, targetWeight, meleeLayerFadeOut));
        meleeFadeRoutines[layerIndex] = fadeRoutine;
        if (layerIndex == meleeOneHandLayerIndex)
        {
            meleeOneHandFadeRoutine = fadeRoutine;
        }
        else if (layerIndex == meleeTwoHandLayerIndex)
        {
            meleeTwoHandFadeRoutine = fadeRoutine;
        }
    }

    IEnumerator StopMeleeAfterDelay(int layerIndex, float duration)
    {
        if (duration > 0f)
        {
            yield return new WaitForSeconds(duration);
        }

        StopMeleeLayer(layerIndex);
        ReleaseActionLayer(layerIndex, MeleeLayerOwner);
        if (currentMeleeLayerIndex == layerIndex)
        {
            currentMeleeLayerIndex = -1;
        }

        isPlayingMeleeState = false;
        meleeRoutine = null;
    }

    void StopMeleeLayer(int layerIndex)
    {
        if (layerIndex < 0)
        {
            return;
        }

        if (layerIndex == aimLayerIndex)
        {
            PlayAimState(meleeLayerFadeOut);
            SetAimLayerWeight(aimLayerWeight);
            return;
        }

        FadeMeleeLayerWeight(layerIndex, 0f);
    }

    IEnumerator FadeLayerWeight(int layerIndex, float targetWeight, float duration)
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

    IEnumerator FadeSwitchAimLayerToCurrentAimWeight()
    {
        if (animator == null || aimLayerIndex < 0)
        {
            isReturningFromSwitchState = false;
            yield break;
        }

        float elapsed = 0f;
        float duration = Mathf.Max(0f, switchLayerFadeOut);
        float startWeight = animator.GetLayerWeight(aimLayerIndex);

        if (duration <= 0f)
        {
            SetAimLayerWeight(aimLayerWeight);
            isReturningFromSwitchState = false;
            switchFadeRoutine = null;
            yield break;
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetAimLayerWeight(Mathf.Lerp(startWeight, aimLayerWeight, t));
            yield return null;
        }

        SetAimLayerWeight(aimLayerWeight);
        isReturningFromSwitchState = false;
        switchFadeRoutine = null;
    }

    IEnumerator FinishReloadTransition(int reloadLayer, bool restoreAimLayer)
    {
        if (animator == null || reloadLayer < 0)
        {
            ReleaseActionLayer(reloadLayer, ReloadLayerOwner);
            reloadReturnRoutine = null;
            yield break;
        }

        yield return WaitForReloadStateToFinish(reloadLayer);

        bool reloadUsesAimLayer = reloadLayer == aimLayerIndex;
        bool shouldAim = ShouldReturnToAimAfterReload();
        float targetAimWeight = shouldAim ? GetReloadReturnAimWeight() : 0f;
        float fadeDuration = Mathf.Max(0f, reloadLayerFadeOut);

        if (reloadUsesAimLayer)
        {
            if (shouldAim)
            {
                PlayAimState(reloadReturnCrossFade);
                yield return FadeSingleLayer(reloadLayer, targetAimWeight, fadeDuration);
            }
            else
            {
                yield return FadeSingleLayer(reloadLayer, 0f, fadeDuration);
                PlayAimState(0f);
            }

            reloadReturnRoutine = null;
            ReleaseActionLayer(reloadLayer, ReloadLayerOwner);
            yield break;
        }

        if (restoreAimLayer)
        {
            if (shouldAim)
            {
                PlayAimState(reloadReturnCrossFade);
            }

            yield return FadeTwoLayers(reloadLayer, 0f, aimLayerIndex, targetAimWeight, fadeDuration);
        }
        else
        {
            yield return FadeSingleLayer(reloadLayer, 0f, fadeDuration);
        }

        ReleaseActionLayer(reloadLayer, ReloadLayerOwner);
        reloadReturnRoutine = null;
    }

    IEnumerator WaitForReloadStateToFinish(int layerIndex)
    {
        if (animator == null || layerIndex < 0 || reloadFinishMaxWait <= 0f)
        {
            yield break;
        }

        float elapsed = 0f;
        float targetNormalizedTime = Mathf.Clamp(reloadFinishNormalizedTime, 0.8f, 1.05f);
        while (elapsed < reloadFinishMaxWait)
        {
            AnimatorStateInfo stateInfo = animator.IsInTransition(layerIndex)
                ? animator.GetNextAnimatorStateInfo(layerIndex)
                : animator.GetCurrentAnimatorStateInfo(layerIndex);

            if (stateInfo.length <= 0f || stateInfo.normalizedTime >= targetNormalizedTime)
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    bool ShouldReturnToAimAfterReload()
    {
        if (!allowAimInput || statusBlocksAiming)
        {
            return false;
        }

        return aimLayerWeight > 0.01f || IsAimPressed();
    }

    float GetReloadReturnAimWeight()
    {
        if (allowAimInput && !statusBlocksAiming && IsAimPressed())
        {
            return 1f;
        }

        return Mathf.Clamp01(aimLayerWeight);
    }

    bool IsAimPressed()
    {
        return kontrolPemain != null && kontrolPemain.Pemain.Aim.IsPressed();
    }

    IEnumerator FadeSingleLayer(int layerIndex, float targetWeight, float duration)
    {
        yield return FadeTwoLayers(layerIndex, targetWeight, -1, 0f, duration);
    }

    IEnumerator FadeWeaponActionLayers(
        int firstLayerIndex,
        float firstTargetWeight,
        int secondLayerIndex,
        float secondTargetWeight,
        float duration,
        string owner)
    {
        yield return FadeTwoLayers(firstLayerIndex, firstTargetWeight, secondLayerIndex, secondTargetWeight, duration);
        ReleaseActionLayer(firstLayerIndex, owner);
    }

    IEnumerator FadeTwoLayers(int firstLayerIndex, float firstTargetWeight, int secondLayerIndex, float secondTargetWeight, float duration)
    {
        if (animator == null)
        {
            yield break;
        }

        bool hasFirstLayer = firstLayerIndex >= 0;
        bool hasSecondLayer = secondLayerIndex >= 0 && secondLayerIndex != firstLayerIndex;
        float firstStartWeight = hasFirstLayer ? animator.GetLayerWeight(firstLayerIndex) : 0f;
        float secondStartWeight = hasSecondLayer ? animator.GetLayerWeight(secondLayerIndex) : 0f;
        firstTargetWeight = Mathf.Clamp01(firstTargetWeight);
        secondTargetWeight = Mathf.Clamp01(secondTargetWeight);

        if (duration <= 0f)
        {
            if (hasFirstLayer)
            {
                animator.SetLayerWeight(firstLayerIndex, firstTargetWeight);
            }

            if (hasSecondLayer)
            {
                animator.SetLayerWeight(secondLayerIndex, secondTargetWeight);
            }

            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (hasFirstLayer)
            {
                animator.SetLayerWeight(firstLayerIndex, Mathf.Lerp(firstStartWeight, firstTargetWeight, t));
            }

            if (hasSecondLayer)
            {
                animator.SetLayerWeight(secondLayerIndex, Mathf.Lerp(secondStartWeight, secondTargetWeight, t));
            }

            yield return null;
        }

        if (hasFirstLayer)
        {
            animator.SetLayerWeight(firstLayerIndex, firstTargetWeight);
        }

        if (hasSecondLayer)
        {
            animator.SetLayerWeight(secondLayerIndex, secondTargetWeight);
        }
    }

    void PlayStateOnLayerIfAvailable(int layerIndex, string stateName, float crossFade)
    {
        if (animator == null || layerIndex < 0 || string.IsNullOrEmpty(stateName))
        {
            return;
        }

        if (!TryGetStateHash(layerIndex, GetLayerName(layerIndex, string.Empty), stateName, out int stateHash))
        {
            return;
        }

        animator.CrossFadeInFixedTime(stateHash, Mathf.Max(0f, crossFade), layerIndex);
    }

    void StopShootFade()
    {
        if (shootFadeRoutine != null)
        {
            StopCoroutine(shootFadeRoutine);
            shootFadeRoutine = null;
        }
    }

    void StopAllActionCoroutinesForCleanup()
    {
        StopRoutine(ref shootRoutine);
        StopRoutine(ref shootFadeRoutine);
        StopRoutine(ref switchFadeRoutine);
        StopRoutine(ref reloadFadeRoutine);
        StopRoutine(ref reloadReturnRoutine);
        StopRoutine(ref meleeRoutine);
        StopRoutine(ref meleeOneHandFadeRoutine);
        StopRoutine(ref meleeTwoHandFadeRoutine);

        foreach (Coroutine routine in meleeFadeRoutines.Values)
        {
            if (routine != null)
            {
                StopCoroutine(routine);
            }
        }

        meleeFadeRoutines.Clear();
    }

    void StopRoutine(ref Coroutine routine)
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }

    void StopSwitchFade()
    {
        if (switchFadeRoutine != null)
        {
            StopCoroutine(switchFadeRoutine);
            switchFadeRoutine = null;
        }

        isReturningFromSwitchState = false;
    }

    void StopReloadFade()
    {
        if (reloadFadeRoutine != null)
        {
            StopCoroutine(reloadFadeRoutine);
            reloadFadeRoutine = null;
        }
    }

    void StopReloadReturn()
    {
        if (reloadReturnRoutine != null)
        {
            StopCoroutine(reloadReturnRoutine);
            reloadReturnRoutine = null;
        }
    }

    void StopMeleeFade(int layerIndex)
    {
        if (meleeFadeRoutines.TryGetValue(layerIndex, out Coroutine fadeRoutine) && fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            meleeFadeRoutines.Remove(layerIndex);
        }

        if (layerIndex == meleeOneHandLayerIndex && meleeOneHandFadeRoutine != null)
        {
            StopCoroutine(meleeOneHandFadeRoutine);
            meleeOneHandFadeRoutine = null;
        }

        if (layerIndex == meleeTwoHandLayerIndex && meleeTwoHandFadeRoutine != null)
        {
            StopCoroutine(meleeTwoHandFadeRoutine);
            meleeTwoHandFadeRoutine = null;
        }
    }

    int GetStateHash(int layerIndex, string layerName, string stateName)
    {
        if (TryGetStateHash(layerIndex, layerName, stateName, out int stateHash))
        {
            return stateHash;
        }

        return Animator.StringToHash(stateName);
    }

    bool TryGetStateHash(int layerIndex, string layerName, string stateName, out int stateHash)
    {
        if (animator == null || layerIndex < 0 || string.IsNullOrEmpty(stateName))
        {
            stateHash = 0;
            return false;
        }

        string resolvedLayerName = GetLayerName(layerIndex, layerName);
        if (!string.IsNullOrEmpty(resolvedLayerName))
        {
            int fullPathHash = Animator.StringToHash($"{resolvedLayerName}.{stateName}");
            if (animator.HasState(layerIndex, fullPathHash))
            {
                stateHash = fullPathHash;
                return true;
            }
        }

        int shortHash = Animator.StringToHash(stateName);
        if (animator.HasState(layerIndex, shortHash))
        {
            stateHash = shortHash;
            return true;
        }

        stateHash = 0;
        return false;
    }

    bool IsStateActiveOrNext(int layerIndex, string layerName, string stateName)
    {
        if (!TryGetStateHash(layerIndex, layerName, stateName, out int stateHash))
        {
            return false;
        }

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(layerIndex);
        if (currentState.fullPathHash == stateHash || currentState.shortNameHash == stateHash)
        {
            return true;
        }

        if (!animator.IsInTransition(layerIndex))
        {
            return false;
        }

        AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(layerIndex);
        return nextState.fullPathHash == stateHash || nextState.shortNameHash == stateHash;
    }

    bool TryGetRandomStateHash(int layerIndex, string layerName, string[] stateNames, out int stateHash)
    {
        int startIndex = Random.Range(0, stateNames.Length);
        for (int i = 0; i < stateNames.Length; i++)
        {
            string stateName = stateNames[(startIndex + i) % stateNames.Length];
            if (TryGetStateHash(layerIndex, layerName, stateName, out stateHash))
            {
                return true;
            }
        }

        stateHash = 0;
        return false;
    }

    string GetLayerName(int layerIndex, string fallbackLayerName)
    {
        if (animator == null || layerIndex < 0 || layerIndex >= animator.layerCount)
        {
            return fallbackLayerName;
        }

        string layerName = animator.GetLayerName(layerIndex);
        return string.IsNullOrEmpty(layerName) ? fallbackLayerName : layerName;
    }

    void CacheAnimatorParameters()
    {
        hasIsAimingParameter = false;
        if (animator == null)
        {
            return;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == "IsAiming" && parameters[i].type == AnimatorControllerParameterType.Bool)
            {
                hasIsAimingParameter = true;
                return;
            }
        }
    }

    void SetAnimatorBool(string parameterName, bool value)
    {
        if (animator == null || parameterName != "IsAiming" || !hasIsAimingParameter)
        {
            return;
        }

        animator.SetBool(parameterName, value);
    }
}
