using Lean.Pool;
using UnityEngine;

/// <summary>Returns pooled particle effects after their configured lifetime.</summary>
public sealed class ParticleAutoDespawn : MonoBehaviour
{
    [Min(0f)] public float delayToDespawn = 3f;

    private void OnEnable()
    {
        if (delayToDespawn > 0f)
        {
            Invoke(nameof(Despawn), delayToDespawn);
        }
    }

    private void OnDisable()
    {
        CancelInvoke();
    }

    private void Despawn()
    {
        if (LeanPool.Links.ContainsKey(gameObject))
        {
            LeanPool.Despawn(gameObject);
            return;
        }

        Destroy(gameObject);
    }
}
