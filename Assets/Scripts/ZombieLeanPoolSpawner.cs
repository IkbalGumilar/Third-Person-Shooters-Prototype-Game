using System.Collections;
using Lean.Pool;
using UnityEngine;
using UnityEngine.AI;

public class ZombieLeanPoolSpawner : MonoBehaviour
{
    public GameObject zombiePrefab;
    public Transform[] spawnPoints;
    public Transform spawnCenter;
    public float spawnRadius = 12f;
    public int spawnCountPerWave = 5;
    public float spawnInterval = 10f;
    public int maxAliveZombies = 50;
    public bool spawnImmediately = true;
    public bool hideTemplateOnStart = true;
    public bool cleanupSceneDuplicatesOnStart = true;
    public bool removeTemplateAudioSources = true;
    public bool keepOneRootAudioSourceOnSpawn = true;
    public string zombieNamePrefix = "Zommbie Enemy";
    public float navMeshSampleDistance = 3f;

    void Start()
    {
        if (spawnCenter == null)
        {
            spawnCenter = transform;
        }

        if (zombiePrefab == null)
        {
            zombiePrefab = FindZombieTemplate();
        }

        if (cleanupSceneDuplicatesOnStart)
        {
            CleanupSceneDuplicates();
        }

        if (removeTemplateAudioSources && zombiePrefab != null)
        {
            CleanupAudioSources(zombiePrefab, false);
        }

        if (hideTemplateOnStart && zombiePrefab != null)
        {
            zombiePrefab.SetActive(false);
        }

        SetupPoolCapacity();
        StartCoroutine(SpawnLoop());
    }

    IEnumerator SpawnLoop()
    {
        yield return null;

        if (spawnImmediately)
        {
            SpawnWave();
        }

        WaitForSeconds wait = new WaitForSeconds(spawnInterval);

        while (true)
        {
            yield return wait;
            SpawnWave();
        }
    }

    void SpawnWave()
    {
        if (zombiePrefab == null || spawnCountPerWave <= 0 || maxAliveZombies <= 0)
        {
            return;
        }

        int aliveCount = CountAliveZombies();
        int spawnCount = Mathf.Min(spawnCountPerWave, maxAliveZombies - aliveCount);

        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 position = GetSpawnPosition();
            Quaternion rotation = GetSpawnRotation(position);
            GameObject zombie = LeanPool.Spawn(zombiePrefab, position, rotation);
            if (zombie == null)
            {
                continue;
            }

            zombie.name = zombieNamePrefix + " (Pooled)";
            zombie.SetActive(true);
            CleanupAudioSources(zombie, keepOneRootAudioSourceOnSpawn);

            Enemy enemy = zombie.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.despawnWithLeanPool = true;
                enemy.ResetHealth();
            }
        }
    }

    void SetupPoolCapacity()
    {
        if (zombiePrefab == null || maxAliveZombies <= 0)
        {
            return;
        }

        LeanGameObjectPool pool = null;
        if (!LeanGameObjectPool.TryFindPoolByPrefab(zombiePrefab, ref pool))
        {
            GameObject poolObject = new GameObject("LeanPool (" + zombiePrefab.name + ")");
            pool = poolObject.AddComponent<LeanGameObjectPool>();
            pool.Prefab = zombiePrefab;
        }

        pool.Capacity = maxAliveZombies;
        pool.Recycle = false;
    }

    void CleanupAudioSources(GameObject target, bool keepOneRootAudioSource)
    {
        if (target == null)
        {
            return;
        }

        AudioSource[] audioSources = target.GetComponentsInChildren<AudioSource>(true);
        bool keptRootAudioSource = false;

        foreach (AudioSource source in audioSources)
        {
            if (source == null)
            {
                continue;
            }

            if (keepOneRootAudioSource && !keptRootAudioSource && source.gameObject == target)
            {
                keptRootAudioSource = true;
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                continue;
            }

            Destroy(source);
        }
    }

    Vector3 GetSpawnPosition()
    {
        Vector3 position;

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
            position = point != null ? point.position : spawnCenter.position;
        }
        else
        {
            Vector2 circle = Random.insideUnitCircle * spawnRadius;
            position = spawnCenter.position + new Vector3(circle.x, 0f, circle.y);
        }

        if (NavMesh.SamplePosition(position, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
        {
            position = hit.position;
        }

        return position;
    }

    Quaternion GetSpawnRotation(Vector3 position)
    {
        if (spawnCenter == null)
        {
            return transform.rotation;
        }

        Vector3 direction = spawnCenter.position - position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.01f)
        {
            return spawnCenter.rotation;
        }

        return Quaternion.LookRotation(direction);
    }

    int CountAliveZombies()
    {
        int count = 0;
        var enemies = Enemy.ActiveEnemies;

        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy enemy = enemies[i];
            if (enemy == null || enemy.gameObject == zombiePrefab || enemy.IsDead)
            {
                continue;
            }

            if (IsZombie(enemy.gameObject))
            {
                count++;
            }
        }

        return count;
    }

    void CleanupSceneDuplicates()
    {
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsInactive.Include);

        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || enemy.gameObject == zombiePrefab || !IsZombie(enemy.gameObject))
            {
                continue;
            }

            Destroy(enemy.gameObject);
        }
    }

    GameObject FindZombieTemplate()
    {
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsInactive.Include);

        foreach (Enemy enemy in enemies)
        {
            if (enemy != null && IsZombie(enemy.gameObject))
            {
                return enemy.gameObject;
            }
        }

        return null;
    }

    bool IsZombie(GameObject candidate)
    {
        return candidate != null && candidate.name.StartsWith(zombieNamePrefix);
    }
}
