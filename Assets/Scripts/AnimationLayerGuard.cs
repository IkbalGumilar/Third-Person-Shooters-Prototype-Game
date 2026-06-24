using System.Collections.Generic;
using UnityEngine;

public enum AnimationLayerPriority
{
    Locomotion = 10,
    Aim = 20,
    WeaponAction = 40,
    Reaction = 60,
    GuardBreak = 100,
    Death = 1000
}

/// <summary>
/// Arbitrates manual Animator layer writes. A higher-priority owner can preempt
/// a lower-priority owner on the same layer, but unrelated layers keep running.
/// </summary>
[DisallowMultipleComponent]
public sealed class AnimationLayerGuard : MonoBehaviour
{
    private sealed class Claim
    {
        public string owner;
        public AnimationLayerPriority priority;
    }

    private readonly Dictionary<int, Claim> claims = new Dictionary<int, Claim>();

    public static AnimationLayerGuard GetOrAdd(Animator animator)
    {
        if (animator == null)
        {
            return null;
        }

        AnimationLayerGuard guard = animator.GetComponent<AnimationLayerGuard>();
        return guard != null ? guard : animator.gameObject.AddComponent<AnimationLayerGuard>();
    }

    public bool TryClaim(int layerIndex, string owner, AnimationLayerPriority priority, bool setWeightToOne = true)
    {
        if (!CanUseLayer(layerIndex) || string.IsNullOrEmpty(owner))
        {
            return false;
        }

        if (claims.TryGetValue(layerIndex, out Claim current))
        {
            if (current.owner != owner && current.priority > priority)
            {
                return false;
            }

            if (current.owner != owner)
            {
                SetLayerWeight(layerIndex, 0f);
            }
        }

        claims[layerIndex] = new Claim { owner = owner, priority = priority };
        if (setWeightToOne)
        {
            SetLayerWeight(layerIndex, 1f);
        }

        return true;
    }

    public bool TryClaimExclusive(int layerIndex, string owner, AnimationLayerPriority priority)
    {
        if (!TryClaim(layerIndex, owner, priority))
        {
            return false;
        }

        List<int> ownedLayers = null;
        foreach (KeyValuePair<int, Claim> pair in claims)
        {
            if (pair.Key != layerIndex && pair.Value.owner == owner)
            {
                ownedLayers ??= new List<int>();
                ownedLayers.Add(pair.Key);
            }
        }

        if (ownedLayers != null)
        {
            for (int i = 0; i < ownedLayers.Count; i++)
            {
                Release(ownedLayers[i], owner);
            }
        }

        return true;
    }

    public bool IsClaimedByOther(int layerIndex, string owner)
    {
        return claims.TryGetValue(layerIndex, out Claim current) && current.owner != owner;
    }

    public bool SetWeight(int layerIndex, string owner, float weight)
    {
        if (!claims.TryGetValue(layerIndex, out Claim current) || current.owner != owner)
        {
            return false;
        }

        SetLayerWeight(layerIndex, weight);
        return true;
    }

    public void Release(int layerIndex, string owner, bool clearWeight = true)
    {
        if (!claims.TryGetValue(layerIndex, out Claim current) || current.owner != owner)
        {
            return;
        }

        claims.Remove(layerIndex);
        if (clearWeight)
        {
            SetLayerWeight(layerIndex, 0f);
        }
    }

    public void ReleaseOwner(string owner, bool clearWeight = true)
    {
        if (string.IsNullOrEmpty(owner))
        {
            return;
        }

        List<int> ownedLayers = null;
        foreach (KeyValuePair<int, Claim> pair in claims)
        {
            if (pair.Value.owner == owner)
            {
                ownedLayers ??= new List<int>();
                ownedLayers.Add(pair.Key);
            }
        }

        if (ownedLayers == null)
        {
            return;
        }

        for (int i = 0; i < ownedLayers.Count; i++)
        {
            Release(ownedLayers[i], owner, clearWeight);
        }
    }

    public void ReleaseLowerPriority(AnimationLayerPriority priority)
    {
        List<int> lowerPriorityLayers = null;
        foreach (KeyValuePair<int, Claim> pair in claims)
        {
            if (pair.Value.priority < priority)
            {
                lowerPriorityLayers ??= new List<int>();
                lowerPriorityLayers.Add(pair.Key);
            }
        }

        if (lowerPriorityLayers == null)
        {
            return;
        }

        for (int i = 0; i < lowerPriorityLayers.Count; i++)
        {
            Claim claim = claims[lowerPriorityLayers[i]];
            Release(lowerPriorityLayers[i], claim.owner);
        }
    }

    private void OnDisable()
    {
        ClearClaims();
    }

    private void ClearClaims()
    {
        foreach (int layerIndex in claims.Keys)
        {
            SetLayerWeight(layerIndex, 0f);
        }

        claims.Clear();
    }

    private bool CanUseLayer(int layerIndex)
    {
        Animator animator = GetComponent<Animator>();
        return animator != null && layerIndex > 0 && layerIndex < animator.layerCount;
    }

    private void SetLayerWeight(int layerIndex, float weight)
    {
        Animator animator = GetComponent<Animator>();
        if (animator != null && layerIndex > 0 && layerIndex < animator.layerCount)
        {
            animator.SetLayerWeight(layerIndex, Mathf.Clamp01(weight));
        }
    }
}
