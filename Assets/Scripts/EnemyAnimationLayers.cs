using UnityEngine;

public static class EnemyAnimationLayers
{
    private const string LegacyOwner = "Enemy.Legacy";

    public static bool TryClaimLayer(
        Animator animator,
        int layerIndex,
        string owner,
        AnimationLayerPriority priority,
        bool exclusiveForOwner = false)
    {
        AnimationLayerGuard guard = AnimationLayerGuard.GetOrAdd(animator);
        if (guard == null)
        {
            return false;
        }

        return exclusiveForOwner
            ? guard.TryClaimExclusive(layerIndex, owner, priority)
            : guard.TryClaim(layerIndex, owner, priority);
    }

    public static void ReleaseLayer(Animator animator, int layerIndex, string owner)
    {
        AnimationLayerGuard guard = animator != null ? animator.GetComponent<AnimationLayerGuard>() : null;
        guard?.Release(layerIndex, owner);
    }

    public static void ReleaseOwner(Animator animator, string owner)
    {
        AnimationLayerGuard guard = animator != null ? animator.GetComponent<AnimationLayerGuard>() : null;
        guard?.ReleaseOwner(owner);
    }

    public static void ReleaseLowerPriority(Animator animator, AnimationLayerPriority priority)
    {
        AnimationLayerGuard guard = animator != null ? animator.GetComponent<AnimationLayerGuard>() : null;
        guard?.ReleaseLowerPriority(priority);
    }

    public static void SetExclusiveLayer(Animator animator, int activeLayerIndex)
    {
        AnimationLayerGuard guard = AnimationLayerGuard.GetOrAdd(animator);
        if (guard == null)
        {
            return;
        }

        if (activeLayerIndex <= 0)
        {
            guard.ReleaseOwner(LegacyOwner);
            return;
        }

        // Existing AI, melee, and ranged calls route through one low-priority
        // owner. Reactions, defense, and death can now preempt them safely.
        guard.TryClaimExclusive(activeLayerIndex, LegacyOwner, AnimationLayerPriority.Locomotion);
    }
}
