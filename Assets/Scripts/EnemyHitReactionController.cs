using System.Collections;
using UnityEngine;

public sealed class EnemyHitReactionController : MonoBehaviour
{
    private static readonly string[] unarmedHitStates =
    {
        "Unarmed-GetHit-F1",
        "Unarmed-GetHit-F2",
        "Unarmed-GetHit-B1",
        "Unarmed-GetHit-L1",
        "Unarmed-GetHit-R1"
    };

    private static readonly string[] armedHitStates =
    {
        "Armed-GetHit-F1",
        "Armed-GetHit-F2",
        "Armed-GetHit-B1",
        "Armed-GetHit-L1",
        "Armed-GetHit-R1"
    };

    private static readonly string[] twoHandAxeHitStates = CreateDirectionalHitStates("2Hand-Axe");
    private static readonly string[] twoHandCrossbowHitStates = CreateDirectionalHitStates("2Hand-Crossbow");
    private static readonly string[] twoHandShootingHitStates = CreateDirectionalHitStates("Shooting");
    private static readonly string[] twoHandSpearHitStates = CreateDirectionalHitStates("2Hand-Spear");
    private static readonly string[] twoHandStaffHitStates = CreateDirectionalHitStates("Staff");
    private static readonly string[] twoHandSwordHitStates = CreateDirectionalHitStates("2Hand-Sword");

    public Animator animator;
    [Range(0f, 0.25f)] public float crossFadeDuration = 0.05f;
    [Range(0.05f, 2f)] public float maximumReactionDuration = 0.45f;
    [Range(0f, 0.25f)] public float fadeOutDuration = 0.08f;

    private Enemy enemy;
    private EnemyAI enemyAI;
    private EnemyMeleeWeaponController meleeWeaponController;
    private EnemyRangedWeaponController rangedWeaponController;
    private Coroutine reactionRoutine;
    private int activeLayerIndex = -1;
    private bool locomotionSuppressed;

    void Awake()
    {
        enemy = GetComponent<Enemy>();
        enemyAI = GetComponent<EnemyAI>();
        meleeWeaponController = GetComponent<EnemyMeleeWeaponController>();
        rangedWeaponController = GetComponent<EnemyRangedWeaponController>();
        animator = animator != null ? animator : GetComponentInChildren<Animator>();
    }

    void OnDisable()
    {
        if (reactionRoutine != null)
        {
            StopCoroutine(reactionRoutine);
            reactionRoutine = null;
        }

        SetLayerWeight(activeLayerIndex, 0f);
        ReleaseLocomotion();
        activeLayerIndex = -1;
    }

    public void PlayHitReaction()
    {
        if (enemy == null || enemy.IsDead || animator == null || reactionRoutine != null)
        {
            return;
        }

        if (!TryResolveReaction(GetReactionLayerName(), out int layerIndex, out int stateHash) &&
            !TryResolveReaction("Armed", out layerIndex, out stateHash) &&
            !TryResolveReaction("Unarmed", out layerIndex, out stateHash))
        {
            return;
        }

        // Attack, support and hit use the same full-body weapon layers. Clear
        // their writers before this controller takes the selected layer.
        meleeWeaponController?.InterruptForHitReaction();
        rangedWeaponController?.InterruptForHitReaction();
        enemyAI?.InterruptSupportBuffForHitReaction();
        SuppressLocomotion();

        if (activeLayerIndex >= 0 && activeLayerIndex != layerIndex)
        {
            SetLayerWeight(activeLayerIndex, 0f);
        }

        activeLayerIndex = layerIndex;
        EnemyAnimationLayers.SetExclusiveLayer(animator, layerIndex);
        SetLayerWeight(layerIndex, 1f);
        animator.CrossFadeInFixedTime(stateHash, crossFadeDuration, layerIndex, 0f);
        reactionRoutine = StartCoroutine(FadeReactionAfterState(layerIndex));
    }

    public void StopHitReaction()
    {
        if (reactionRoutine != null)
        {
            StopCoroutine(reactionRoutine);
            reactionRoutine = null;
        }

        SetLayerWeight(activeLayerIndex, 0f);
        ReleaseLocomotion();
        activeLayerIndex = -1;
    }

    bool TryResolveReaction(string layerName, out int layerIndex, out int stateHash)
    {
        layerIndex = animator.GetLayerIndex(layerName);
        if (layerIndex <= 0)
        {
            stateHash = 0;
            return false;
        }

        return TryGetRandomStateHash(layerIndex, layerName, GetReactionStateNames(layerName), out stateHash);
    }

    string GetReactionLayerName()
    {
        EnemyRangedWeapon rangedWeapon = rangedWeaponController != null ? rangedWeaponController.CurrentWeapon : null;
        if (rangedWeapon != null)
        {
            return rangedWeapon.weaponKind switch
            {
                EnemyRangedWeaponKind.Crossbow => "2Hand-Crossbow",
                EnemyRangedWeaponKind.Shotgun => "2Hand-Shooting",
                _ => "Armed"
            };
        }

        EnemyMeleeWeapon meleeWeapon = meleeWeaponController != null ? meleeWeaponController.CurrentWeapon : null;
        if (meleeWeapon != null)
        {
            return meleeWeapon.category switch
            {
                EnemyMeleeWeaponCategory.SmallAxe => "2Hand-Axe",
                EnemyMeleeWeaponCategory.GreatSword => "2Hand-Sword",
                EnemyMeleeWeaponCategory.Spear when meleeWeapon.holdType == WeaponHoldType.TwoHand => "2Hand-Spear",
                _ => "Armed"
            };
        }

        return enemy != null && enemy.enemyData != null && enemy.enemyData.enemyType == EnemyType.Support
            ? "2Hand-Staff"
            : "Unarmed";
    }

    string[] GetReactionStateNames(string layerName)
    {
        return layerName switch
        {
            "Unarmed" => unarmedHitStates,
            "Armed" => armedHitStates,
            "2Hand-Axe" => twoHandAxeHitStates,
            "2Hand-Crossbow" => twoHandCrossbowHitStates,
            "2Hand-Shooting" => twoHandShootingHitStates,
            "2Hand-Spear" => twoHandSpearHitStates,
            "2Hand-Staff" => twoHandStaffHitStates,
            "2Hand-Sword" => twoHandSwordHitStates,
            _ => armedHitStates
        };
    }

    static string[] CreateDirectionalHitStates(string prefix)
    {
        return new[]
        {
            $"{prefix}-GetHit-F1",
            $"{prefix}-GetHit-F2",
            $"{prefix}-GetHit-B1",
            $"{prefix}-GetHit-L1",
            $"{prefix}-GetHit-R1"
        };
    }

    bool TryGetRandomStateHash(int layerIndex, string layerName, string[] stateNames, out int stateHash)
    {
        int startIndex = Random.Range(0, stateNames.Length);
        for (int i = 0; i < stateNames.Length; i++)
        {
            string stateName = stateNames[(startIndex + i) % stateNames.Length];
            int hash = Animator.StringToHash($"{layerName}.{stateName}");
            if (animator.HasState(layerIndex, hash))
            {
                stateHash = hash;
                return true;
            }
        }

        stateHash = 0;
        return false;
    }

    IEnumerator FadeReactionAfterState(int layerIndex)
    {
        yield return null;
        float duration = Mathf.Min(
            maximumReactionDuration,
            Mathf.Max(0.05f, animator.GetCurrentAnimatorStateInfo(layerIndex).length)
        );
        yield return new WaitForSeconds(duration);

        float startWeight = animator.GetLayerWeight(layerIndex);
        if (fadeOutDuration <= 0f)
        {
            SetLayerWeight(layerIndex, 0f);
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                SetLayerWeight(layerIndex, Mathf.Lerp(startWeight, 0f, elapsed / fadeOutDuration));
                yield return null;
            }

            SetLayerWeight(layerIndex, 0f);
        }

        if (activeLayerIndex == layerIndex)
        {
            activeLayerIndex = -1;
        }

        ReleaseLocomotion();
        reactionRoutine = null;
    }

    void SuppressLocomotion()
    {
        if (locomotionSuppressed)
        {
            return;
        }

        locomotionSuppressed = true;
        enemyAI?.SetLocomotionSuppressed(true);
    }

    void ReleaseLocomotion()
    {
        if (!locomotionSuppressed)
        {
            return;
        }

        locomotionSuppressed = false;
        enemyAI?.SetLocomotionSuppressed(false);
    }

    void SetLayerWeight(int layerIndex, float weight)
    {
        if (animator != null && layerIndex > 0)
        {
            animator.SetLayerWeight(layerIndex, Mathf.Clamp01(weight));
        }
    }
}
