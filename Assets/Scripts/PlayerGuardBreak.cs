using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public sealed class PlayerGuardBreak : MonoBehaviour
{
    [Header("Timing")]
    public float duration = 3f;
    public float inputReduction = 1f;
    public float inputDelay = 0.1f;
    public float knockbackDistance = 1.5f;
    public float knockbackDuration = 0.25f;

    [Header("Get Up")]
    public float getUpFallbackDuration = 0.8f;
    public float getUpMaxDuration = 3f;
    [Min(0.1f)] public float getUpStartSpeed = 3f;
    [Min(0.1f)] public float getUpInputSpeedStep = 1f;
    [Min(0.1f)] public float getUpMaxSpeed = 5f;

    private PlayerMovement movement;
    private PlayerShoot playerShoot;
    private PlayerWeaponAnimator weaponAnimator;
    private PlayerAimIK aimIK;
    private Animator animator;
    private bool isActive;
    private bool isGettingUp;
    private float endsAt;
    private float inputAllowedAt;
    private float getUpEndsAt;
    private float getUpForceEndAt;
    private float getUpSpeed;
    private float previousAnimatorSpeed = 1f;
    private bool overridesAnimatorSpeed;

    public bool IsActive => isActive;

    private void Awake()
    {
        Initialize(GetComponent<PlayerMovement>());
    }

    private void OnDisable()
    {
        CancelImmediate();
    }

    private void Update()
    {
        if (!isActive)
        {
            return;
        }

        movement?.UpdateExternalActionGravity();

        if (!isGettingUp)
        {
            if (Time.time >= inputAllowedAt && WasAnyRecoveryInputPressed())
            {
                endsAt = Mathf.Max(Time.time, endsAt - Mathf.Max(0f, inputReduction));
            }

            if (Time.time >= endsAt)
            {
                BeginGetUp();
            }

            return;
        }

        if (WasAnyRecoveryInputPressed())
        {
            getUpSpeed = Mathf.Min(
                Mathf.Max(getUpStartSpeed, getUpMaxSpeed),
                getUpSpeed + Mathf.Max(0f, getUpInputSpeedStep)
            );
            ApplyGetUpSpeed();
            getUpEndsAt = Mathf.Min(getUpEndsAt, Time.time + GetScaledDuration(getUpFallbackDuration));
        }

        if (Time.time >= getUpEndsAt
            && ((movement != null && movement.HasGuardBreakReactionFinished()) || Time.time >= getUpForceEndAt))
        {
            End();
        }
    }

    public void Initialize(PlayerMovement owner)
    {
        movement = owner != null ? owner : movement;
        playerShoot = playerShoot != null ? playerShoot : GetComponent<PlayerShoot>();
        weaponAnimator = weaponAnimator != null ? weaponAnimator : GetComponent<PlayerWeaponAnimator>();
        aimIK = aimIK != null ? aimIK : GetComponent<PlayerAimIK>();
        animator = animator != null
            ? animator
            : weaponAnimator != null && weaponAnimator.animator != null
                ? weaponAnimator.animator
                : GetComponentInChildren<Animator>();
    }

    public void ApplyLegacySettings(
        float legacyDuration,
        float legacyInputReduction,
        float legacyInputDelay,
        float legacyKnockbackDistance,
        float legacyKnockbackDuration,
        float legacyGetUpFallbackDuration,
        float legacyGetUpMaxDuration,
        float legacyGetUpStartSpeed,
        float legacyGetUpInputSpeedStep,
        float legacyGetUpMaxSpeed)
    {
        duration = legacyDuration;
        inputReduction = legacyInputReduction;
        inputDelay = legacyInputDelay;
        knockbackDistance = legacyKnockbackDistance;
        knockbackDuration = legacyKnockbackDuration;
        getUpFallbackDuration = legacyGetUpFallbackDuration;
        getUpMaxDuration = legacyGetUpMaxDuration;
        getUpStartSpeed = legacyGetUpStartSpeed;
        getUpInputSpeedStep = legacyGetUpInputSpeedStep;
        getUpMaxSpeed = legacyGetUpMaxSpeed;
    }

    public void Trigger(Vector3 knockbackDirection, bool usesIncomingKnockback)
    {
        if (isActive)
        {
            return;
        }

        Initialize(movement);
        isActive = true;
        isGettingUp = false;
        endsAt = Time.time + Mathf.Max(0f, duration);
        inputAllowedAt = Time.time + Mathf.Max(0f, inputDelay);
        movement?.StopBaseActionForExternalAction();
        movement?.StopLocomotionForInputFreeze();

        if (playerShoot != null)
        {
            playerShoot.externalActionBlocksInput = true;
        }

        weaponAnimator?.SetExternalActionOverride(true);
        aimIK?.SetExternalPoseOverride(true);
        movement?.PlayGuardBreakReaction(false, out _);

        if (!usesIncomingKnockback)
        {
            movement?.ApplyKnockback(knockbackDirection, knockbackDistance, knockbackDuration);
        }
    }

    public void CancelImmediate()
    {
        if (!isActive && !overridesAnimatorSpeed)
        {
            return;
        }

        End();
    }

    private void BeginGetUp()
    {
        isGettingUp = true;
        getUpSpeed = Mathf.Max(0.1f, getUpStartSpeed);
        ApplyGetUpSpeed();
        float stateDuration = getUpFallbackDuration;
        if (movement != null && movement.PlayGuardBreakReaction(true, out float playedDuration))
        {
            stateDuration = playedDuration;
        }

        getUpEndsAt = Time.time + GetScaledDuration(getUpFallbackDuration);
        getUpForceEndAt = Time.time + GetScaledDuration(Mathf.Max(
            Mathf.Max(0.05f, stateDuration),
            Mathf.Max(0.1f, getUpMaxDuration)
        ));
    }

    private void End()
    {
        isActive = false;
        isGettingUp = false;
        endsAt = 0f;
        inputAllowedAt = 0f;
        getUpEndsAt = 0f;
        getUpForceEndAt = 0f;
        getUpSpeed = 0f;
        movement?.ClearGuardBreakReaction();
        RestoreAnimatorSpeed();

        if (playerShoot != null)
        {
            playerShoot.externalActionBlocksInput = false;
        }

        weaponAnimator?.SetExternalActionOverride(false);
        aimIK?.SetExternalPoseOverride(false);
    }

    private void ApplyGetUpSpeed()
    {
        if (animator == null)
        {
            return;
        }

        if (!overridesAnimatorSpeed)
        {
            previousAnimatorSpeed = animator.speed;
            overridesAnimatorSpeed = true;
        }

        animator.speed = Mathf.Max(0.1f, getUpSpeed);
    }

    private void RestoreAnimatorSpeed()
    {
        if (animator != null && overridesAnimatorSpeed)
        {
            animator.speed = previousAnimatorSpeed;
        }

        overridesAnimatorSpeed = false;
        previousAnimatorSpeed = 1f;
    }

    private float GetScaledDuration(float baseDuration)
    {
        return Mathf.Max(0.05f, baseDuration) / Mathf.Max(0.1f, getUpSpeed);
    }

    private static bool WasAnyRecoveryInputPressed()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            return true;
        }

        if (Mouse.current != null
            && (Mouse.current.leftButton.wasPressedThisFrame
                || Mouse.current.rightButton.wasPressedThisFrame
                || Mouse.current.middleButton.wasPressedThisFrame
                || Mouse.current.forwardButton.wasPressedThisFrame
                || Mouse.current.backButton.wasPressedThisFrame))
        {
            return true;
        }

        return WasButtonPressed(Gamepad.current) || WasButtonPressed(Joystick.current);
    }

    private static bool WasButtonPressed(InputDevice device)
    {
        if (device == null)
        {
            return false;
        }

        foreach (InputControl control in device.allControls)
        {
            if (control is ButtonControl button && button.wasPressedThisFrame)
            {
                return true;
            }
        }

        return false;
    }
}
