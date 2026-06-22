using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class ShieldAttackCoordinator : MonoBehaviour
{
    const float GatherWindow = 0.08f;

    class AttackBatch
    {
        public PlayerHealth target;
        public readonly List<EnemyShieldController> attackers = new List<EnemyShieldController>();
    }

    class ImpactBatch
    {
        public PlayerHealth target;
        public readonly List<Impact> impacts = new List<Impact>();
        public bool resolving;
    }

    struct Impact
    {
        public float damage;
        public float knockbackDistance;
        public Vector3 direction;
        public float damageBonusMaxPercent;
    }

    static ShieldAttackCoordinator instance;
    readonly Dictionary<PlayerHealth, AttackBatch> pendingAttacks = new Dictionary<PlayerHealth, AttackBatch>();
    readonly Dictionary<PlayerHealth, ImpactBatch> pendingImpacts = new Dictionary<PlayerHealth, ImpactBatch>();

    static ShieldAttackCoordinator Instance
    {
        get
        {
            if (instance != null)
            {
                return instance;
            }

            GameObject coordinatorObject = new GameObject("Shield Attack Coordinator");
            instance = coordinatorObject.AddComponent<ShieldAttackCoordinator>();
            DontDestroyOnLoad(coordinatorObject);
            return instance;
        }
    }

    public static void RequestAttack(EnemyShieldController attacker, PlayerHealth target)
    {
        if (attacker == null || target == null || !attacker.TryReserveSynchronizedAttack(target))
        {
            return;
        }

        ShieldAttackCoordinator coordinator = Instance;
        if (!coordinator.pendingAttacks.TryGetValue(target, out AttackBatch batch))
        {
            batch = new AttackBatch { target = target };
            coordinator.pendingAttacks.Add(target, batch);
            coordinator.StartCoroutine(coordinator.ExecuteAttackBatch(batch));
        }

        batch.attackers.Add(attacker);
    }

    public static void SubmitImpact(
        EnemyShieldController attacker,
        PlayerHealth target,
        float damage,
        float knockbackDistance,
        Vector3 direction,
        float damageBonusMaxPercent)
    {
        if (attacker == null || target == null || target.IsDead || damage <= 0f)
        {
            return;
        }

        ShieldAttackCoordinator coordinator = Instance;
        if (!coordinator.pendingImpacts.TryGetValue(target, out ImpactBatch batch))
        {
            batch = new ImpactBatch { target = target };
            coordinator.pendingImpacts.Add(target, batch);
        }

        batch.impacts.Add(new Impact
        {
            damage = damage,
            knockbackDistance = Mathf.Max(0f, knockbackDistance),
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward,
            damageBonusMaxPercent = Mathf.Clamp(damageBonusMaxPercent, 0f, 100f)
        });

        if (!batch.resolving)
        {
            batch.resolving = true;
            coordinator.StartCoroutine(coordinator.ResolveImpactBatch(batch));
        }
    }

    IEnumerator ExecuteAttackBatch(AttackBatch batch)
    {
        yield return new WaitForSeconds(GatherWindow);
        pendingAttacks.Remove(batch.target);

        for (int i = batch.attackers.Count - 1; i >= 0; i--)
        {
            EnemyShieldController attacker = batch.attackers[i];
            if (attacker == null || !attacker.StartSynchronizedAttack(batch.target))
            {
                attacker?.ReleaseSynchronizedAttackReservation();
            }
        }
    }

    IEnumerator ResolveImpactBatch(ImpactBatch batch)
    {
        yield return new WaitForEndOfFrame();
        pendingImpacts.Remove(batch.target);

        if (batch.target == null || batch.target.IsDead || batch.impacts.Count == 0)
        {
            yield break;
        }

        float highestDamage = 0f;
        float totalDamage = 0f;
        float highestKnockback = 0f;
        float totalKnockback = 0f;
        float damageBonusPercent = 0f;
        Vector3 combinedDirection = Vector3.zero;

        for (int i = 0; i < batch.impacts.Count; i++)
        {
            Impact impact = batch.impacts[i];
            highestDamage = Mathf.Max(highestDamage, impact.damage);
            totalDamage += impact.damage;
            highestKnockback = Mathf.Max(highestKnockback, impact.knockbackDistance);
            totalKnockback += impact.knockbackDistance;
            damageBonusPercent = Mathf.Max(damageBonusPercent, impact.damageBonusMaxPercent);
            combinedDirection += impact.direction * Mathf.Max(0.01f, impact.knockbackDistance);
        }

        float additionalDamage = Mathf.Max(0f, totalDamage - highestDamage);
        float maxAdditionalDamage = highestDamage * damageBonusPercent / 100f;
        float combinedDamage = highestDamage + Mathf.Min(additionalDamage, maxAdditionalDamage);
        float knockbackCap = highestKnockback * batch.impacts.Count;
        float combinedKnockback = Mathf.Min(totalKnockback, knockbackCap);
        bool hasKnockback = combinedKnockback > 0f;

        Vector3 direction = combinedDirection.sqrMagnitude > 0.0001f
            ? combinedDirection.normalized
            : Vector3.forward;
        Vector3 hitPoint = batch.target.transform.position - direction * 0.15f;
        batch.target.TakeDamage(combinedDamage, hitPoint, direction, false, false, hasKnockback);

        if (hasKnockback && !batch.target.LastDamageWasBlocked && !batch.target.IsDead)
        {
            PlayerMovement movement = batch.target.GetComponent<PlayerMovement>();
            if (movement == null)
            {
                movement = batch.target.GetComponentInParent<PlayerMovement>();
            }

            movement?.ApplyKnockback(direction, combinedKnockback, 0.3f);
        }
    }
}
