using System.Collections;
using UnityEngine;

public class PlayerBlockController : MonoBehaviour
{
    public bool allowInput = true;
    public KeyCode blockKey = KeyCode.F;
    public PlayerShoot playerShoot;
    public PlayerMovement playerMovement;
    public PlayerWeaponAnimator weaponAnimator;
    public PlayerWeaponEquip weaponEquip;
    public PlayerMeleeController meleeController;
    public PlayerAimIK aimIK;
    public Animator animator;

    [Header("Block Damage")]
    [Range(0f, 180f)] public float frontBlockAngle = 110f;
    [Range(0.85f, 0.99f)] public float minDamageAbsorbPercent = 0.85f;
    [Range(0.85f, 0.99f)] public float maxDamageAbsorbPercent = 0.99f;
    [Range(0f, 2f)] public float specialAttackExtraDrainPercent = 0.5f;
    public float sideBlockCancelDuration = 1f;

    [Header("Animation")]
    [Tooltip("Legacy fallback. Prefer the weapon-specific layer names below.")]
    public string blockLayerName = "Block";
    public string unarmedBlockLayerName = "Unarmed-Block";
    public string oneHandBlockLayerName = "Armed-Block";
    public string twoHandBlockLayerName = "2Hand-Shooting-Block";
    public string blockParameterName = "Block";
    public float blockFade = 0.08f;
    public float blockReleaseFade = 0.12f;
    public float blockHitFade = 0.04f;
    public float blockHitDuration = 0.28f;
    public string unarmedBlockState = "Unarmed-Block";
    public string unarmedBlockHitState1 = "Unarmed-GetHit-F1";
    public string unarmedBlockHitState2 = "Unarmed-GetHit-F2";
    public string oneHandBlockState = "Armed-Block-Dual";
    public string oneHandBlockHitState1 = "Armed-Block-Dual-GetHit1";
    public string oneHandBlockHitState2 = "Armed-Block-Dual-GetHit2";
    public string twoHandBlockState = "Shooting-Block";
    public string twoHandBlockHitState1 = "Shooting-Block-GetHit1";
    public string twoHandBlockHitState2 = "Shooting-Block-GetHit2";

    int blockLayerIndex = -1;
    int currentBlockLayerIndex = -1;
    int activeBlockHitLayerIndex = -1;
    bool isBlocking;
    float blockDisabledUntil;
    Coroutine fadeRoutine;
    Coroutine hitRoutine;
    bool hasBlockParameter;
    private KontrolPemain kontrolPemain;

    public bool IsBlocking => isBlocking;

    void Awake()
    {
        kontrolPemain = new KontrolPemain();
        playerShoot = playerShoot != null ? playerShoot : GetComponent<PlayerShoot>();
        playerMovement = playerMovement != null ? playerMovement : GetComponent<PlayerMovement>();
        weaponAnimator = weaponAnimator != null ? weaponAnimator : GetComponent<PlayerWeaponAnimator>();
        weaponEquip = weaponEquip != null ? weaponEquip : GetComponent<PlayerWeaponEquip>();
        meleeController = meleeController != null ? meleeController : GetComponent<PlayerMeleeController>();
        aimIK = aimIK != null ? aimIK : GetComponent<PlayerAimIK>();
        animator = animator != null
            ? animator
            : weaponAnimator != null && weaponAnimator.animator != null
                ? weaponAnimator.animator
                : GetComponentInChildren<Animator>();

        ResolveBlockLayer();
        CacheAnimatorParameters();
    }

    void OnEnable()
    {
        kontrolPemain?.Pemain.Enable();
    }

    void Update()
    {
        if (!CanReadInput())
        {
            SetBlocking(false);
            return;
        }

        bool wantsBlock = IsBlockPressed();
        if (wantsBlock && Time.time < blockDisabledUntil)
        {
            wantsBlock = false;
        }

        if (wantsBlock && playerMovement != null && !playerMovement.CanUseStaminaActionPublic)
        {
            wantsBlock = false;
        }

        SetBlocking(wantsBlock);
    }

    void LateUpdate()
    {
        if (isBlocking && currentBlockLayerIndex >= 0)
        {
            SetBlockLayerWeight(currentBlockLayerIndex, 1f);
        }
    }

    void OnDisable()
    {
        kontrolPemain?.Pemain.Disable();
        SetBlocking(false);
        StopBlockHitRoutine();
        SetBlockLayerWeight(currentBlockLayerIndex, 0f);
        SetBlockLayerWeight(activeBlockHitLayerIndex, 0f);
        currentBlockLayerIndex = -1;
        activeBlockHitLayerIndex = -1;
    }

    void OnDestroy()
    {
        kontrolPemain?.Dispose();
    }

    bool IsBlockPressed()
    {
        return kontrolPemain != null && kontrolPemain.Pemain.Block.IsPressed();
    }

    bool CanReadInput()
    {
        return allowInput
            && playerShoot != null
            && playerShoot.enabled
            && playerShoot.allowInput
            && !playerShoot.statusBlocksInput
            && playerMovement != null
            && playerMovement.enabled
            && !playerMovement.IsGuardBroken;
    }

    public bool TryBlockDamage(float originalDamage, Vector3 hitPoint, Vector3 hitNormal, bool isHeavy, bool isCritical, bool isKnockback, out float reducedDamage)
    {
        reducedDamage = originalDamage;
        if (!isBlocking
            || originalDamage <= 0f
            || Time.time < blockDisabledUntil
            || (playerMovement != null && playerMovement.IsGuardBroken))
        {
            return false;
        }

        if (!IsHitFromFront(hitPoint, hitNormal))
        {
            CancelBlockTemporarily();
            return false;
        }

        bool specialAttack = isHeavy || isCritical || isKnockback;
        float staminaDamage = originalDamage * (specialAttack ? 1f + Mathf.Max(0f, specialAttackExtraDrainPercent) : 1f);
        bool staminaCouldBeUsed = playerMovement == null || playerMovement.TryUseStamina(staminaDamage);
        bool guardBrokenBySpecialAttack = specialAttack
            && playerMovement != null
            && playerMovement.CurrentStamina <= 0.001f;
        if (!staminaCouldBeUsed || guardBrokenBySpecialAttack)
        {
            SetBlocking(false);
            if (guardBrokenBySpecialAttack)
            {
                playerMovement.TriggerGuardBreak(hitNormal, isKnockback);
            }
            return false;
        }

        float absorb = Random.Range(minDamageAbsorbPercent, maxDamageAbsorbPercent);
        if (specialAttack)
        {
            absorb += Mathf.Max(0f, specialAttackExtraDrainPercent);
        }

        reducedDamage = originalDamage * Mathf.Clamp01(1f - absorb);
        PlayBlockHitAnimation();
        return true;
    }

    bool IsHitFromFront(Vector3 hitPoint, Vector3 hitNormal)
    {
        Vector3 toHit = hitPoint - transform.position;
        toHit.y = 0f;

        if (toHit.sqrMagnitude < 0.0001f)
        {
            toHit = -hitNormal;
            toHit.y = 0f;
        }

        if (toHit.sqrMagnitude < 0.0001f)
        {
            return true;
        }

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            return true;
        }

        float angle = Vector3.Angle(forward.normalized, toHit.normalized);
        return angle <= Mathf.Clamp(frontBlockAngle, 0f, 180f) * 0.5f;
    }

    void CancelBlockTemporarily()
    {
        blockDisabledUntil = Time.time + Mathf.Max(0f, sideBlockCancelDuration);
        SetBlocking(false);
    }

    void SetBlocking(bool value)
    {
        if (value && !isBlocking && !CanStartBlocking())
        {
            value = false;
        }

        if (isBlocking == value)
        {
            return;
        }

        isBlocking = value;
        if (playerShoot != null)
        {
            playerShoot.externalActionBlocksInput = isBlocking;
        }

        weaponAnimator?.SetExternalActionOverride(isBlocking);
        aimIK?.SetExternalPoseOverride(isBlocking);
        SetBlockParameter(isBlocking);
        if (isBlocking)
        {
            PlayBlockIdleAnimation();
        }
        else
        {
            FadeBlockLayerWeight(0f, blockReleaseFade);
        }
    }

    bool CanStartBlocking()
    {
        if (playerShoot != null && playerShoot.IsReloading)
        {
            return false;
        }

        if (weaponEquip != null && weaponEquip.IsSwitching)
        {
            return false;
        }

        if (meleeController != null && meleeController.IsAttacking)
        {
            return false;
        }

        return weaponAnimator == null || !weaponAnimator.IsActionAnimationPlaying;
    }

    void PlayBlockIdleAnimation()
    {
        if (!EnsureAnimatorReady())
        {
            return;
        }

        StopBlockFade();
        string stateName = GetCurrentBlockState();
        if (TryGetStateHash(stateName, out int stateHash, out int layerIndex))
        {
            currentBlockLayerIndex = layerIndex;
            SetBlockLayerWeight(currentBlockLayerIndex, 1f);
            weaponAnimator?.LockLayerControl(blockFade + 0.02f);
            animator.CrossFadeInFixedTime(stateHash, Mathf.Max(0f, blockFade), currentBlockLayerIndex);
        }
    }

    void PlayBlockHitAnimation()
    {
        if (!EnsureAnimatorReady())
        {
            return;
        }

        StopBlockHitRoutine();
        StopBlockFade();

        string stateName = Random.value < 0.5f ? GetCurrentBlockHitState1() : GetCurrentBlockHitState2();
        float hitDuration = Mathf.Max(0f, blockHitDuration);
        if (TryGetStateHash(stateName, out int stateHash, out int layerIndex))
        {
            int previousLayerIndex = currentBlockLayerIndex;
            currentBlockLayerIndex = layerIndex;
            activeBlockHitLayerIndex = layerIndex;
            SetBlockLayerWeight(currentBlockLayerIndex, 1f);
            weaponAnimator?.LockLayerControl(blockHitFade + 0.02f);
            animator.CrossFadeInFixedTime(stateHash, Mathf.Max(0f, blockHitFade), currentBlockLayerIndex);
            animator.Update(0f);
            hitDuration = Mathf.Max(hitDuration, GetActiveStateDuration(currentBlockLayerIndex));

            // Unarmed hit clips live on a separate layer. Keep its block layer
            // active underneath, then clear the temporary hit layer on return.
            if (previousLayerIndex >= 0 && previousLayerIndex != layerIndex)
            {
                SetBlockLayerWeight(previousLayerIndex, 1f);
            }
        }

        hitRoutine = StartCoroutine(ReturnToBlockAfterHit(hitDuration));
    }

    IEnumerator ReturnToBlockAfterHit(float duration)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, duration));
        hitRoutine = null;
        int hitLayerIndex = activeBlockHitLayerIndex;
        activeBlockHitLayerIndex = -1;
        if (isBlocking)
        {
            PlayBlockIdleAnimation();
            if (hitLayerIndex >= 0 && hitLayerIndex != currentBlockLayerIndex)
            {
                SetBlockLayerWeight(hitLayerIndex, 0f);
            }
        }
        else
        {
            FadeBlockLayerWeight(0f, blockReleaseFade);
            if (hitLayerIndex >= 0 && hitLayerIndex != currentBlockLayerIndex)
            {
                SetBlockLayerWeight(hitLayerIndex, 0f);
            }
        }
    }

    float GetActiveStateDuration(int layerIndex)
    {
        if (animator == null || layerIndex < 0 || layerIndex >= animator.layerCount)
        {
            return 0f;
        }

        AnimatorStateInfo stateInfo = animator.IsInTransition(layerIndex)
            ? animator.GetNextAnimatorStateInfo(layerIndex)
            : animator.GetCurrentAnimatorStateInfo(layerIndex);
        return Mathf.Max(0f, stateInfo.length);
    }

    string GetCurrentBlockState()
    {
        Weapon weapon = playerShoot != null ? playerShoot.currentWeapon : null;
        if (weapon == null)
        {
            return unarmedBlockState;
        }

        return weapon.holdType == WeaponHoldType.OneHand ? oneHandBlockState : twoHandBlockState;
    }

    string GetCurrentBlockHitState1()
    {
        Weapon weapon = playerShoot != null ? playerShoot.currentWeapon : null;
        if (weapon == null)
        {
            return unarmedBlockHitState1;
        }

        return weapon.holdType == WeaponHoldType.OneHand ? oneHandBlockHitState1 : twoHandBlockHitState1;
    }

    string GetCurrentBlockHitState2()
    {
        Weapon weapon = playerShoot != null ? playerShoot.currentWeapon : null;
        if (weapon == null)
        {
            return unarmedBlockHitState2;
        }

        return weapon.holdType == WeaponHoldType.OneHand ? oneHandBlockHitState2 : twoHandBlockHitState2;
    }

    bool EnsureAnimatorReady()
    {
        if (animator == null)
        {
            return false;
        }

        ResolveBlockLayer();
        return animator.layerCount > 0;
    }

    void ResolveBlockLayer()
    {
        if (animator == null)
        {
            blockLayerIndex = -1;
            return;
        }

        blockLayerIndex = animator.GetLayerIndex(blockLayerName);
    }

    int GetCurrentBlockLayerIndex()
    {
        if (animator == null)
        {
            return -1;
        }

        Weapon weapon = playerShoot != null ? playerShoot.currentWeapon : null;
        string layerName = weapon == null
            ? unarmedBlockLayerName
            : weapon.holdType == WeaponHoldType.OneHand ? oneHandBlockLayerName : twoHandBlockLayerName;
        int layerIndex = animator.GetLayerIndex(layerName);
        return layerIndex >= 0 ? layerIndex : blockLayerIndex;
    }

    void CacheAnimatorParameters()
    {
        hasBlockParameter = false;
        if (animator == null || string.IsNullOrEmpty(blockParameterName))
        {
            return;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == blockParameterName && parameters[i].type == AnimatorControllerParameterType.Bool)
            {
                hasBlockParameter = true;
                return;
            }
        }
    }

    void SetBlockParameter(bool value)
    {
        if (animator != null && hasBlockParameter)
        {
            animator.SetBool(blockParameterName, value);
        }
    }

    bool TryGetStateHash(string stateName, out int stateHash, out int layerIndex)
    {
        stateHash = 0;
        layerIndex = -1;
        if (animator == null || string.IsNullOrEmpty(stateName))
        {
            return false;
        }

        int preferredLayerIndex = GetCurrentBlockLayerIndex();
        if (TryGetStateHashOnLayer(preferredLayerIndex, stateName, out stateHash))
        {
            layerIndex = preferredLayerIndex;
            return true;
        }

        // Unarmed block has no dedicated hit clips. Its configured get-hit
        // clips live on Unarmed-Hit, which is the sole explicit fallback.
        if (playerShoot != null
            && playerShoot.currentWeapon == null
            && (stateName == unarmedBlockHitState1 || stateName == unarmedBlockHitState2))
        {
            int hitLayerIndex = animator.GetLayerIndex("Unarmed-Hit");
            if (TryGetStateHashOnLayer(hitLayerIndex, stateName, out stateHash))
            {
                layerIndex = hitLayerIndex;
                return true;
            }
        }

        return false;
    }

    bool TryGetStateHashOnLayer(int layerIndex, string stateName, out int stateHash)
    {
        stateHash = 0;
        if (layerIndex < 0 || layerIndex >= animator.layerCount)
        {
            return false;
        }

        string layerName = animator.GetLayerName(layerIndex);
        if (!string.IsNullOrEmpty(layerName))
        {
            stateHash = Animator.StringToHash($"{layerName}.{stateName}");
            if (animator.HasState(layerIndex, stateHash))
            {
                return true;
            }
        }

        stateHash = Animator.StringToHash(stateName);
        return animator.HasState(layerIndex, stateHash);
    }

    void FadeBlockLayerWeight(float target, float duration)
    {
        int layerIndex = currentBlockLayerIndex >= 0 ? currentBlockLayerIndex : GetCurrentBlockLayerIndex();
        if (layerIndex < 0)
        {
            return;
        }

        StopBlockFade();
        fadeRoutine = StartCoroutine(FadeLayerWeight(layerIndex, target, duration));
    }

    IEnumerator FadeLayerWeight(int layerIndex, float target, float duration)
    {
        float start = animator.GetLayerWeight(layerIndex);
        if (duration <= 0f)
        {
            SetBlockLayerWeight(layerIndex, target);
            fadeRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetBlockLayerWeight(layerIndex, Mathf.Lerp(start, target, elapsed / duration));
            yield return null;
        }

        SetBlockLayerWeight(layerIndex, target);
        fadeRoutine = null;
        if (Mathf.Approximately(target, 0f) && currentBlockLayerIndex == layerIndex)
        {
            currentBlockLayerIndex = -1;
        }
    }

    void SetBlockLayerWeight(int layerIndex, float weight)
    {
        if (animator == null || layerIndex < 0 || layerIndex >= animator.layerCount)
        {
            return;
        }

        if (layerIndex == 0)
        {
            animator.SetLayerWeight(layerIndex, 1f);
            return;
        }

        if (animator != null)
        {
            animator.SetLayerWeight(layerIndex, Mathf.Clamp01(weight));
        }
    }

    void StopBlockFade()
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }
    }

    void StopBlockHitRoutine()
    {
        if (hitRoutine != null)
        {
            StopCoroutine(hitRoutine);
            hitRoutine = null;
        }
    }
}
