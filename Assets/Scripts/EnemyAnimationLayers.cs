using UnityEngine;

public static class EnemyAnimationLayers
{
    public static void SetExclusiveLayer(Animator animator, int activeLayerIndex)
    {
        if (animator == null)
        {
            return;
        }

        for (int layerIndex = 1; layerIndex < animator.layerCount; layerIndex++)
        {
            animator.SetLayerWeight(layerIndex, layerIndex == activeLayerIndex ? 1f : 0f);
        }
    }
}
