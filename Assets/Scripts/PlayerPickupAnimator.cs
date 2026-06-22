using System.Collections;
using UnityEngine;

public class PlayerPickupAnimator : MonoBehaviour
{
    public Animator animator;
    public PlayerWeaponEquip weaponEquip;
    public PlayerShoot playerShoot;
    public PlayerMovement playerMovement;
    public PlayerWeaponAnimator weaponAnimator;
    public PlayerAimIK aimIK;
    public PlayerBlockController blockController;
    public string[] layerNameHints =
    {
        "Base Layer",
        "Armed",
        "Armed-Interact",
        "1Hand-Pistol",
        "2Hand-Shooting",
        "Unarmed",
        "Unarmed-Interact",
        "lapisan Bidik"
    };

    public string[] unarmedPickupStateNames = { "Relax-Pickup" };
    public string[] oneHandPickupStateNames = { "Armed-Pickup-Right", "Relax-Pickup" };
    public string[] twoHandPickupStateNames =
    {
        "2Hand-Sword-Pickup",
        "2Hand-Axe-Pickup"
    };

    public float crossFade = 0.08f;
    public float fallbackDuration = 0.75f;
    public float layerFadeOut = 0.12f;
    public bool lockWeaponLayersDuringPickup = true;
    public bool suppressAimIKDuringPickup = true;

    private Coroutine pickupRoutine;

    void Awake()
    {
        FindMissingReferences();
    }

    public void PlayPickup()
    {
        FindMissingReferences();
        if (animator == null
            || (blockController != null && blockController.IsBlocking)
            || (playerShoot != null && playerShoot.IsReloading)
            || (weaponEquip != null && weaponEquip.IsSwitching)
            || (weaponAnimator != null && weaponAnimator.IsActionAnimationPlaying))
        {
            return;
        }

        if (!TryFindPickupState(out int layerIndex, out int stateHash, out string stateName))
        {
            return;
        }

        if (pickupRoutine != null)
        {
            StopCoroutine(pickupRoutine);
        }

        pickupRoutine = StartCoroutine(PlayPickupRoutine(layerIndex, stateHash, stateName));
    }

    IEnumerator PlayPickupRoutine(int layerIndex, int stateHash, string stateName)
    {
        float holdDuration = Mathf.Max(0f, fallbackDuration);
        SuppressConflictingAnimationSystems(holdDuration + Mathf.Max(0f, layerFadeOut) + Mathf.Max(0f, crossFade));
        if (layerIndex == 0 && playerMovement != null && playerMovement.PlayTemporaryBaseState(stateName, holdDuration, crossFade))
        {
            yield return new WaitForSeconds(holdDuration);
            pickupRoutine = null;
            yield break;
        }

        if (layerIndex > 0)
        {
            animator.SetLayerWeight(layerIndex, 1f);
        }

        animator.CrossFadeInFixedTime(stateHash, Mathf.Max(0f, crossFade), layerIndex, 0f);
        animator.Update(0f);
        yield return null;

        float duration = GetActiveStateDuration(layerIndex);
        holdDuration = Mathf.Max(holdDuration, duration);
        SuppressConflictingAnimationSystems(holdDuration + Mathf.Max(0f, layerFadeOut));

        yield return new WaitForSeconds(holdDuration);

        if (layerIndex > 0)
        {
            float elapsed = 0f;
            float startWeight = animator.GetLayerWeight(layerIndex);
            float fadeDuration = Mathf.Max(0.01f, layerFadeOut);
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                animator.SetLayerWeight(layerIndex, Mathf.Lerp(startWeight, 0f, elapsed / fadeDuration));
                yield return null;
            }

            animator.SetLayerWeight(layerIndex, 0f);
        }

        pickupRoutine = null;
    }

    void SuppressConflictingAnimationSystems(float duration)
    {
        if (lockWeaponLayersDuringPickup && weaponAnimator != null)
        {
            weaponAnimator.LockLayerControl(duration);
        }

        if (suppressAimIKDuringPickup && aimIK != null)
        {
            aimIK.SuppressAim(duration);
        }
    }

    bool TryFindPickupState(out int layerIndex, out int stateHash, out string stateName)
    {
        string[] states = GetPickupStateCandidates();
        if (states == null || states.Length == 0)
        {
            layerIndex = -1;
            stateHash = 0;
            stateName = string.Empty;
            return false;
        }

        if (TryFindStateInHintLayers(states, out layerIndex, out stateHash, out stateName))
        {
            return true;
        }

        string[] fallbackLayers = { "Relax", "Armed-Interact", "Unarmed-Interact", "2Hand-Shooting-Equip-Interact" };
        for (int i = 0; i < fallbackLayers.Length; i++)
        {
            int fallbackLayerIndex = animator.GetLayerIndex(fallbackLayers[i]);
            if (fallbackLayerIndex < 0)
            {
                continue;
            }

            for (int j = 0; j < states.Length; j++)
            {
                if (TryGetStateHash(fallbackLayerIndex, fallbackLayers[i], states[j], out stateHash))
                {
                    layerIndex = fallbackLayerIndex;
                    stateName = states[j];
                    return true;
                }
            }
        }

        layerIndex = -1;
        stateHash = 0;
        stateName = string.Empty;
        return false;
    }

    bool TryFindStateInHintLayers(string[] states, out int layerIndex, out int stateHash, out string stateName)
    {
        if (layerNameHints != null)
        {
            for (int i = 0; i < layerNameHints.Length; i++)
            {
                if (string.IsNullOrEmpty(layerNameHints[i]))
                {
                    continue;
                }

                int hintedLayer = animator.GetLayerIndex(layerNameHints[i]);
                if (hintedLayer < 0)
                {
                    continue;
                }

                for (int j = 0; j < states.Length; j++)
                {
                    if (TryGetStateHash(hintedLayer, layerNameHints[i], states[j], out stateHash))
                    {
                        layerIndex = hintedLayer;
                        stateName = states[j];
                        return true;
                    }
                }
            }
        }

        layerIndex = -1;
        stateHash = 0;
        stateName = string.Empty;
        return false;
    }

    string[] GetPickupStateCandidates()
    {
        Weapon weapon = weaponEquip != null ? weaponEquip.CurrentWeapon : playerShoot != null ? playerShoot.currentWeapon : null;
        if (weapon == null)
        {
            return unarmedPickupStateNames;
        }

        return weapon.holdType == WeaponHoldType.OneHand ? oneHandPickupStateNames : twoHandPickupStateNames;
    }

    bool TryGetStateHash(int layerIndex, string layerName, string stateName, out int stateHash)
    {
        if (layerIndex < 0 || string.IsNullOrEmpty(stateName))
        {
            stateHash = 0;
            return false;
        }

        if (!string.IsNullOrEmpty(layerName))
        {
            int fullPathHash = Animator.StringToHash($"{layerName}.{stateName}");
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

    float GetActiveStateDuration(int layerIndex)
    {
        if (layerIndex < 0)
        {
            return 0f;
        }

        AnimatorStateInfo stateInfo = animator.IsInTransition(layerIndex)
            ? animator.GetNextAnimatorStateInfo(layerIndex)
            : animator.GetCurrentAnimatorStateInfo(layerIndex);
        return stateInfo.length > 0f ? stateInfo.length : 0f;
    }

    void FindMissingReferences()
    {
        animator = animator != null ? animator : GetComponentInChildren<Animator>();
        weaponEquip = weaponEquip != null ? weaponEquip : GetComponent<PlayerWeaponEquip>();
        playerShoot = playerShoot != null ? playerShoot : GetComponent<PlayerShoot>();
        playerMovement = playerMovement != null ? playerMovement : GetComponent<PlayerMovement>();
        weaponAnimator = weaponAnimator != null ? weaponAnimator : GetComponent<PlayerWeaponAnimator>();
        aimIK = aimIK != null ? aimIK : GetComponent<PlayerAimIK>();
        blockController = blockController != null ? blockController : GetComponent<PlayerBlockController>();
    }
}
