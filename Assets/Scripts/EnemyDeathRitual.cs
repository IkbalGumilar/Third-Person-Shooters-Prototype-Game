using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyDeathRitual : MonoBehaviour
{
    private const string DeathRitualLayerOwner = "Enemy.DeathRitual";

    [Header("References")]
    public Enemy enemy;
    public Animator animator;
    public EnemyAuraStatusEmitter auraEmitter;
    public StatusEffectData regenerationEffect;

    [Header("Animation")]
    public string animationLayerName = "2Hand-Staff";
    public string getupStateName = "Staff-Getup1";
    public string boostStateName = "Staff-Boost1";
    [Range(0.1f, 1f)] public float boostPlaybackSpeed = 0.5f;
    public float freezeAtBoostEndDuration = 2f;
    [Range(1f, 100f)] public float ritualHealthPercent = 50f;

    [Header("Final Regeneration")]
    public float regenerationRadius = 10f;
    public LayerMask targetMask = ~0;
    public bool includeSelf;

    Coroutine ritualRoutine;
    readonly HashSet<Enemy> affectedEnemies = new HashSet<Enemy>();
    Vector3 ritualPosition;

    public bool IsDeathRitualActive => ritualRoutine != null;

    void Awake()
    {
        enemy = enemy != null ? enemy : GetComponent<Enemy>();
        animator = animator != null ? animator : GetComponentInChildren<Animator>();
        auraEmitter = auraEmitter != null ? auraEmitter : GetComponent<EnemyAuraStatusEmitter>();
    }

    void OnDisable()
    {
        if (ritualRoutine != null)
        {
            StopCoroutine(ritualRoutine);
        }

        ritualRoutine = null;
        if (animator != null)
        {
            animator.speed = 1f;
            EnemyAnimationLayers.ReleaseOwner(animator, DeathRitualLayerOwner);
        }
    }

    public bool TryStartDeathRitual()
    {
        if (!isActiveAndEnabled || ritualRoutine != null || enemy == null || !enemy.IsDead || animator == null)
        {
            return false;
        }

        int layerIndex = animator.GetLayerIndex(animationLayerName);
        if (layerIndex <= 0 || !HasState(layerIndex, getupStateName) || !HasState(layerIndex, boostStateName))
        {
            return false;
        }

        enemy.RestoreHealthForDeathRitual(ritualHealthPercent);
        ritualPosition = transform.position;
        ritualRoutine = StartCoroutine(DeathRitualRoutine(layerIndex));
        return true;
    }

    public void TakeRitualDamage(float damage)
    {
        if (ritualRoutine == null || enemy == null || !enemy.TakeDeathRitualDamage(damage))
        {
            return;
        }

        StopCoroutine(ritualRoutine);
        ritualRoutine = null;
        animator.speed = 1f;
        int layerIndex = animator.GetLayerIndex(animationLayerName);
        if (layerIndex > 0)
        {
            EnemyAnimationLayers.ReleaseOwner(animator, DeathRitualLayerOwner);
        }

        enemy.CompleteDeathAfterRitual();
    }

    IEnumerator DeathRitualRoutine(int layerIndex)
    {
        EnemyAnimationLayers.ReleaseLowerPriority(animator, AnimationLayerPriority.Death);
        if (!EnemyAnimationLayers.TryClaimLayer(
                animator,
                layerIndex,
                DeathRitualLayerOwner,
                AnimationLayerPriority.Death,
                true))
        {
            ritualRoutine = null;
            yield break;
        }

        animator.speed = 1f;
        PlayState(layerIndex, getupStateName, 0f);
        yield return new WaitForSeconds(GetCurrentStateDuration(layerIndex));

        PlayState(layerIndex, boostStateName, 0f);
        animator.speed = Mathf.Max(0.01f, boostPlaybackSpeed);
        yield return new WaitForSeconds(GetCurrentStateDuration(layerIndex) / animator.speed);

        animator.Play(GetStateHash(layerIndex, boostStateName), layerIndex, 1f);
        animator.Update(0f);
        animator.speed = 0f;
        ApplyFinalBuffs();
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, freezeAtBoostEndDuration));

        animator.speed = 1f;
        EnemyAnimationLayers.ReleaseOwner(animator, DeathRitualLayerOwner);
        ritualRoutine = null;
        enemy.ClearDeathRitualHealth();
        enemy.CompleteDeathAfterRitual();
    }

    void LateUpdate()
    {
        if (ritualRoutine != null)
        {
            transform.position = ritualPosition;
        }
    }

    void ApplyFinalBuffs()
    {
        auraEmitter?.PulseForDeathRitual();
        if (regenerationEffect == null)
        {
            return;
        }

        affectedEnemies.Clear();
        Collider[] hits = Physics.OverlapSphere(transform.position, Mathf.Max(0f, regenerationRadius), targetMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Enemy target = hits[i] != null ? hits[i].GetComponentInParent<Enemy>() : null;
            if (target == null || target.IsDead || (!includeSelf && target == enemy) || !affectedEnemies.Add(target))
            {
                continue;
            }

            EnemyStatusEffectController statusController = target.StatusController;
            if (statusController == null)
            {
                statusController = target.gameObject.AddComponent<EnemyStatusEffectController>();
                statusController.enemy = target;
            }

            statusController.AddEffect(regenerationEffect);
        }
    }

    bool HasState(int layerIndex, string stateName)
    {
        return animator.HasState(layerIndex, GetStateHash(layerIndex, stateName));
    }

    int GetStateHash(int layerIndex, string stateName)
    {
        return Animator.StringToHash($"{animator.GetLayerName(layerIndex)}.{stateName}");
    }

    void PlayState(int layerIndex, string stateName, float normalizedTime)
    {
        animator.Play(GetStateHash(layerIndex, stateName), layerIndex, normalizedTime);
        animator.Update(0f);
    }

    float GetCurrentStateDuration(int layerIndex)
    {
        return Mathf.Max(0.01f, animator.GetCurrentAnimatorStateInfo(layerIndex).length);
    }
}
