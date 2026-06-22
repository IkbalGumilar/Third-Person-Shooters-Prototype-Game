using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyAuraStatusEmitter : MonoBehaviour
{
    public StatusEffectData[] effects;
    public float radius = 8f;
    public float interval = 1f;
    public LayerMask targetMask = ~0;
    public bool includeSelf;
    public bool addControllerIfMissing = true;
    public bool ignoreDeadEnemies = true;
    public bool playSupportBuffAnimation = true;
    public float applyEffectsDelay = 0.55f;

    float nextPulseTime;
    Coroutine pulseRoutine;
    EnemyAI cachedEnemyAI;
    Enemy sourceEnemy;
    readonly Collider[] hits = new Collider[64];
    readonly HashSet<Enemy> appliedEnemies = new HashSet<Enemy>();

    void Awake()
    {
        cachedEnemyAI = GetComponent<EnemyAI>();
        sourceEnemy = GetComponent<Enemy>();
    }

    void OnEnable()
    {
        nextPulseTime = Time.time;
    }

    void Update()
    {
        if (sourceEnemy != null && sourceEnemy.IsDead)
        {
            return;
        }

        if (Time.time < nextPulseTime || pulseRoutine != null)
        {
            return;
        }

        nextPulseTime = Time.time + Mathf.Max(0.05f, interval);
        pulseRoutine = StartCoroutine(PulseRoutine());
    }

    IEnumerator PulseRoutine()
    {
        if (sourceEnemy != null && sourceEnemy.IsDead)
        {
            pulseRoutine = null;
            yield break;
        }

        EnemyAI enemyAI = playSupportBuffAnimation ? cachedEnemyAI : null;
        bool animationStarted = enemyAI != null && enemyAI.TryPlaySupportBuffAnimation();

        if (animationStarted && applyEffectsDelay > 0f)
        {
            yield return new WaitForSeconds(applyEffectsDelay);
        }

        if (sourceEnemy != null && sourceEnemy.IsDead)
        {
            pulseRoutine = null;
            yield break;
        }

        Pulse();
        pulseRoutine = null;
    }

    public void Pulse()
    {
        PulseInternal(false);
    }

    public void PulseForDeathRitual()
    {
        PulseInternal(true);
    }

    void PulseInternal(bool allowDeadSource)
    {
        if (!allowDeadSource && sourceEnemy != null && sourceEnemy.IsDead)
        {
            return;
        }

        if (effects == null || effects.Length == 0 || radius <= 0f)
        {
            return;
        }

        appliedEnemies.Clear();
        int count = Physics.OverlapSphereNonAlloc(transform.position, radius, hits, targetMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            Enemy targetEnemy = hit.GetComponentInParent<Enemy>();
            if (targetEnemy == null)
            {
                continue;
            }

            if (!includeSelf && (targetEnemy.transform == transform || transform.IsChildOf(targetEnemy.transform)))
            {
                continue;
            }

            if (!appliedEnemies.Add(targetEnemy))
            {
                continue;
            }

            if (ignoreDeadEnemies && targetEnemy.IsDead)
            {
                continue;
            }

            EnemyStatusEffectController statusController = targetEnemy.StatusController;
            if (statusController == null && addControllerIfMissing)
            {
                statusController = targetEnemy.gameObject.AddComponent<EnemyStatusEffectController>();
                statusController.enemy = targetEnemy;
            }

            if (statusController == null)
            {
                continue;
            }

            for (int e = 0; e < effects.Length; e++)
            {
                statusController.AddEffect(effects[e]);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
