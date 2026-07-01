using System.Collections;
using System.Collections.Generic;
using Lean.Pool;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BossEncounterDirector : MonoBehaviour
{
    [Header("Boss")]
    public GameObject bossPrefab;
    public bool spawnWithLeanPool = true;
    public bool triggerOnlyOnce = true;
    public float spawnDelay = 3f;
    public float spawnRadiusFromPlayer = 15f;
    [Range(0f, 1f)] public float minSpawnDistanceFactor = 0.9f;
    public bool spawnBehindPlayer = true;
    [Range(0f, 180f)] public float behindSpawnSearchAngle = 70f;
    public float navMeshSampleDistance = 6f;

    [Header("Enemy Check")]
    public float checkInterval = 0.5f;
    public bool ignoreDeadEnemies = true;
    public bool ignoreBossEnemies = true;
    public MonoBehaviour[] spawnersToDisableOnBossWarning;

    [Header("Player")]
    public Transform player;

    [Header("Warning UI")]
    public bool autoCreateWarningUI = true;
    public GameObject warningRoot;
    public TMP_Text warningText;
    public string warningMessage = "WARNING: BOSS INCOMING";
    public Color warningBackgroundColor = new Color(0f, 0f, 0f, 0.65f);
    public Color warningTextColor = new Color(1f, 0.08f, 0.04f, 1f);
    public float warningPanelHeight = 140f;
    public float warningFontSize = 42f;
    public bool animateWarningUI = true;
    public float warningPanelSlideDuration = 0.22f;
    public float warningScaleDuration = 0.28f;
    public float warningTextSlideDuration = 0.28f;
    public float warningPanelOffscreenX = -2200f;
    public float warningTextOffscreenX = 2200f;

    [Header("Game Clear Sequence")]
    public bool playGameClearSequenceOnBossDeath = true;
    public float missionClearDelay = 3f;
    public float missionClearHoldDuration = 5f;
    public float missionClearIntroDuration = 0.35f;
    public float missionClearOutroDuration = 0.25f;
    public string missionClearMessage = "MISSION CLEAR";
    public GameObject missionClearRoot;
    public TMP_Text missionClearText;
    public GameObject[] deactivateOnMissionClear;
    public Color missionClearBackgroundColor = new Color(0f, 0f, 0f, 0.65f);
    public Color missionClearTextColor = new Color(0.1f, 0.85f, 1f, 1f);

    [Header("Game Clear Camera")]
    public Camera clearCamera;
    public float clearCameraRiseDuration = 3f;
    public float clearCameraTargetY = 20f;
    public float clearCameraTargetPitch = 30f;
    public bool disableCameraBehavioursDuringClear = true;

    [Header("Credit")]
    public GameObject creditRoot;
    public ScrollRect creditScrollRect;
    public RectTransform creditContent;
    public Image creditFadeImage;
    public TMP_Text pressAnyButtonText;
    public float creditScrollDuration = 25f;
    public float creditIntroFadeDuration = 0.8f;
    [Range(0f, 255f)] public float creditScrollOverlayAlpha = 150f;
    public float creditFadeToBlackDuration = 2f;
    public float creditBlackHoldDuration = 3f;
    public string pressAnyButtonMessage = "PRESS ANY BUTTON TO BACK MAIN MENU";
    public string mainMenuSceneName = "MainMenu";
    public bool autoApplyResponsiveCreditLayout = true;
    [Range(0f, 0.25f)] public float creditHorizontalSafeMargin = 0.08f;
    [Range(0f, 0.25f)] public float creditVerticalSafeMargin = 0.1f;
    [Range(0f, 0.5f)] public float pressAnyButtonAnchorY = 0.12f;

    [Header("Credit Music")]
    public AudioClip creditMusicClip;
    public AudioSource creditMusicSource;
    [Range(0f, 1f)] public float creditMusicVolume = 0.8f;
    public bool loopCreditMusic;

    private bool bossSequenceStarted;
    private bool bossSpawned;
    private bool gameClearStarted;
    private Enemy spawnedBossEnemy;
    private Coroutine bossRoutine;
    private Coroutine bossDeathRoutine;
    private Coroutine gameClearRoutine;
    private Coroutine warningAnimationRoutine;
    private RectTransform warningRootRect;
    private RectTransform warningTextRect;
    private CanvasGroup warningCanvasGroup;
    private RectTransform missionClearRootRect;
    private RectTransform missionClearTextRect;
    private CanvasGroup missionClearCanvasGroup;
    private CanvasGroup creditCanvasGroup;
    private readonly List<Behaviour> disabledClearBehaviours = new List<Behaviour>();

    void Awake()
    {
        ResolvePlayer();
        EnsureWarningUI();
        SetWarningVisible(false);
        EnsureMissionClearUI();
        SetMissionClearVisible(false);
        EnsureCreditUI();
        SetCreditVisible(false);
    }

    void OnEnable()
    {
        bossSequenceStarted = false;
        bossSpawned = false;
        gameClearStarted = false;
        spawnedBossEnemy = null;
        if (bossRoutine != null)
        {
            StopCoroutine(bossRoutine);
            bossRoutine = null;
        }

        if (bossDeathRoutine != null)
        {
            StopCoroutine(bossDeathRoutine);
            bossDeathRoutine = null;
        }

        if (gameClearRoutine != null)
        {
            StopCoroutine(gameClearRoutine);
            gameClearRoutine = null;
        }

        bossRoutine = StartCoroutine(MonitorEnemiesRoutine());
    }

    void OnDisable()
    {
        if (bossRoutine != null)
        {
            StopCoroutine(bossRoutine);
            bossRoutine = null;
        }

        SetWarningVisible(false);
        SetMissionClearVisible(false);
        StopCreditMusic();
    }

    IEnumerator MonitorEnemiesRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.05f, checkInterval));
        yield return wait;

        while (!bossSpawned || !triggerOnlyOnce)
        {
            if (!bossSequenceStarted && CountAliveEnemiesForBossTrigger() <= 0)
            {
                bossSequenceStarted = true;
                yield return StartCoroutine(BossSpawnSequence());

                if (triggerOnlyOnce)
                {
                    yield break;
                }

                bossSequenceStarted = false;
            }

            yield return wait;
        }
    }

    IEnumerator BossSpawnSequence()
    {
        DisableConfiguredSpawners();
        ShowWarning();
        yield return new WaitForSeconds(Mathf.Max(0f, spawnDelay));
        yield return StartCoroutine(HideWarningOutroRoutine());
        SpawnBoss();
    }

    int CountAliveEnemiesForBossTrigger()
    {
        int count = 0;
        IReadOnlyList<Enemy> enemies = Enemy.ActiveEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy enemy = enemies[i];
            if (enemy == null)
            {
                continue;
            }

            if (ignoreDeadEnemies && enemy.IsDead)
            {
                continue;
            }

            if (ignoreBossEnemies && enemy.enemyData != null && enemy.enemyData.enemyType == EnemyType.Boss)
            {
                continue;
            }

            count++;
        }

        return count;
    }

    void SpawnBoss()
    {
        if (bossPrefab == null)
        {
            Debug.LogWarning("[BossEncounterDirector] Boss prefab is not assigned.", this);
            return;
        }

        ResolvePlayer();
        Vector3 spawnPosition = GetBossSpawnPosition();
        Quaternion spawnRotation = GetBossSpawnRotation(spawnPosition);
        GameObject boss = spawnWithLeanPool
            ? LeanPool.Spawn(bossPrefab, spawnPosition, spawnRotation)
            : Instantiate(bossPrefab, spawnPosition, spawnRotation);

        if (boss == null)
        {
            return;
        }

        boss.SetActive(true);

        Enemy enemy = boss.GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.despawnWithLeanPool = spawnWithLeanPool;
            enemy.ResetHealth();
            spawnedBossEnemy = enemy;
        }

        EnemyAI ai = boss.GetComponent<EnemyAI>();
        if (ai != null && player != null)
        {
            ai.SetPersistentChaseTarget(player);
        }

        bossSpawned = true;
        StartBossDeathMonitor();
    }

    void StartBossDeathMonitor()
    {
        if (!playGameClearSequenceOnBossDeath || spawnedBossEnemy == null)
        {
            return;
        }

        if (bossDeathRoutine != null)
        {
            StopCoroutine(bossDeathRoutine);
        }

        bossDeathRoutine = StartCoroutine(MonitorBossDeathRoutine(spawnedBossEnemy));
    }

    IEnumerator MonitorBossDeathRoutine(Enemy bossEnemy)
    {
        WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.05f, checkInterval));
        while (bossEnemy != null && !bossEnemy.IsDead)
        {
            yield return wait;
        }

        bossDeathRoutine = null;
        if (gameClearStarted)
        {
            yield break;
        }

        gameClearStarted = true;
        gameClearRoutine = StartCoroutine(GameClearSequenceRoutine());
    }

    IEnumerator GameClearSequenceRoutine()
    {
        DisableGameplayForGameClear();
        yield return new WaitForSeconds(Mathf.Max(0f, missionClearDelay));

        yield return StartCoroutine(ShowMissionClearRoutine());
        yield return new WaitForSeconds(Mathf.Max(0f, missionClearHoldDuration));
        yield return StartCoroutine(HideMissionClearRoutine());

        yield return StartCoroutine(PlayClearCameraRoutine());
        yield return StartCoroutine(PlayCreditRoutine());
        gameClearRoutine = null;
    }

    void DisableGameplayForGameClear()
    {
        InGameOptionsMenu.SetInputBlocked(true);
        DeactivateMissionClearObjects();

        InventoryGridUI inventory = FindAnyObjectByType<InventoryGridUI>();
        if (inventory != null)
        {
            inventory.SetOpen(false);
            DisableBehaviourForClear(inventory);
        }

        InGameOptionsMenu inGameOptions = FindAnyObjectByType<InGameOptionsMenu>();
        if (inGameOptions != null)
        {
            inGameOptions.ResumeGame();
            DisableBehaviourForClear(inGameOptions);
        }

        PlayerMovement movement = FindAnyObjectByType<PlayerMovement>();
        if (movement != null)
        {
            movement.allowInput = false;
            movement.statusBlocksMovement = true;
            movement.statusBlocksRun = true;
            movement.statusBlocksJump = true;
        }

        CameraControler cameraControler = FindAnyObjectByType<CameraControler>();
        if (cameraControler != null)
        {
            cameraControler.allowLookInput = false;
        }

        DisableBehaviourForClear(FindAnyObjectByType<PlayerShoot>());
        DisableBehaviourForClear(FindAnyObjectByType<PlayerMeleeController>());
        DisableBehaviourForClear(FindAnyObjectByType<PlayerBlockController>());
        DisableBehaviourForClear(FindAnyObjectByType<PlayerWeaponEquip>());
        DisableBehaviourForClear(FindAnyObjectByType<PlayerScopeController>());
        DisableBehaviourForClear(FindAnyObjectByType<PlayerAimIK>());
        DisableBehaviourForClear(FindAnyObjectByType<CursorController>());

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    void DeactivateMissionClearObjects()
    {
        if (deactivateOnMissionClear == null)
        {
            return;
        }

        for (int i = 0; i < deactivateOnMissionClear.Length; i++)
        {
            GameObject target = deactivateOnMissionClear[i];
            if (target == null || target == missionClearRoot || target == creditRoot)
            {
                continue;
            }

            target.SetActive(false);
        }
    }

    void DisableBehaviourForClear(Behaviour behaviour)
    {
        if (behaviour == null || !behaviour.enabled)
        {
            return;
        }

        behaviour.enabled = false;
        disabledClearBehaviours.Add(behaviour);
    }

    IEnumerator ShowMissionClearRoutine()
    {
        EnsureMissionClearUI();
        if (missionClearRoot == null)
        {
            yield break;
        }

        if (missionClearText != null)
        {
            missionClearText.text = LocalizationManager.GetText(missionClearMessage);
        }

        SetMissionClearVisible(true);
        CacheMissionClearReferences();

        float duration = Mathf.Max(0.01f, missionClearIntroDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = EaseOutBack(Mathf.Clamp01(elapsed / duration));
            if (missionClearCanvasGroup != null)
            {
                missionClearCanvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
            }

            if (missionClearRootRect != null)
            {
                missionClearRootRect.localScale = Vector3.LerpUnclamped(new Vector3(0.7f, 0f, 1f), Vector3.one, t);
            }

            yield return null;
        }

        if (missionClearCanvasGroup != null)
        {
            missionClearCanvasGroup.alpha = 1f;
        }

        if (missionClearRootRect != null)
        {
            missionClearRootRect.localScale = Vector3.one;
        }
    }

    IEnumerator HideMissionClearRoutine()
    {
        if (missionClearRoot == null)
        {
            yield break;
        }

        CacheMissionClearReferences();
        float duration = Mathf.Max(0.01f, missionClearOutroDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = EaseInCubic(Mathf.Clamp01(elapsed / duration));
            if (missionClearCanvasGroup != null)
            {
                missionClearCanvasGroup.alpha = 1f - t;
            }

            if (missionClearRootRect != null)
            {
                missionClearRootRect.localScale = Vector3.LerpUnclamped(Vector3.one, new Vector3(0.7f, 0f, 1f), t);
            }

            yield return null;
        }

        SetMissionClearVisible(false);
    }

    IEnumerator PlayClearCameraRoutine()
    {
        Camera targetCamera = ResolveClearCamera();
        if (targetCamera == null)
        {
            yield break;
        }

        DisableClearCameraDrivers(targetCamera);

        Transform cameraTransform = targetCamera.transform;
        Vector3 startPosition = cameraTransform.position;
        Vector3 targetPosition = startPosition;
        targetPosition.y = Mathf.Max(startPosition.y, clearCameraTargetY);

        Vector3 startEuler = cameraTransform.eulerAngles;
        Vector3 targetEuler = startEuler;
        targetEuler.x = clearCameraTargetPitch;

        float duration = Mathf.Max(0.01f, clearCameraRiseDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = EaseInOutCubic(Mathf.Clamp01(elapsed / duration));
            cameraTransform.position = Vector3.LerpUnclamped(startPosition, targetPosition, t);
            cameraTransform.rotation = Quaternion.SlerpUnclamped(
                Quaternion.Euler(startEuler),
                Quaternion.Euler(targetEuler),
                t);
            yield return null;
        }

        cameraTransform.position = targetPosition;
        cameraTransform.rotation = Quaternion.Euler(targetEuler);
    }

    IEnumerator PlayCreditRoutine()
    {
        EnsureCreditUI();
        if (creditRoot == null)
        {
            yield break;
        }

        SetCreditVisible(true);
        ResetCreditScroll();
        SetCreditFadeAlpha(0f);
        SetPressAnyButtonVisible(false, 0f);

        if (creditCanvasGroup != null)
        {
            creditCanvasGroup.alpha = 1f;
        }

        yield return StartCoroutine(FadeCreditOverlayRoutine(0f, GetCreditScrollOverlayAlpha01(), creditIntroFadeDuration));
        yield return StartCoroutine(ScrollCreditRoutine());
        yield return StartCoroutine(FadeCreditOverlayRoutine(GetCreditScrollOverlayAlpha01(), 1f, creditFadeToBlackDuration));
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, creditBlackHoldDuration));
        yield return StartCoroutine(ShowPressAnyButtonRoutine());

        while (!WasAnyButtonPressedThisFrame())
        {
            yield return null;
        }

        SceneChanger.MarkGameFinishedForCurrentSession();

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    IEnumerator ScrollCreditRoutine()
    {
        if (creditScrollRect == null)
        {
            yield break;
        }

        PlayCreditMusic();
        creditScrollRect.verticalNormalizedPosition = 1f;
        float duration = Mathf.Max(0.01f, creditScrollDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            creditScrollRect.verticalNormalizedPosition = Mathf.Lerp(1f, 0f, t);
            yield return null;
        }

        creditScrollRect.verticalNormalizedPosition = 0f;
        StopCreditMusic();
    }

    void PlayCreditMusic()
    {
        if (creditMusicClip == null)
        {
            return;
        }

        EnsureCreditMusicSource();
        if (creditMusicSource == null)
        {
            return;
        }

        creditMusicSource.clip = creditMusicClip;
        creditMusicSource.volume = Mathf.Clamp01(creditMusicVolume);
        creditMusicSource.loop = loopCreditMusic;
        creditMusicSource.playOnAwake = false;
        creditMusicSource.spatialBlend = 0f;
        creditMusicSource.ignoreListenerPause = true;
        creditMusicSource.Play();
    }

    void StopCreditMusic()
    {
        if (creditMusicSource != null && creditMusicSource.isPlaying)
        {
            creditMusicSource.Stop();
        }
    }

    void EnsureCreditMusicSource()
    {
        if (creditMusicSource != null)
        {
            return;
        }

        creditMusicSource = GetComponent<AudioSource>();
        if (creditMusicSource == null)
        {
            creditMusicSource = gameObject.AddComponent<AudioSource>();
        }
    }

    IEnumerator FadeCreditOverlayRoutine(float fromAlpha, float toAlpha, float durationSeconds)
    {
        float duration = Mathf.Max(0.01f, durationSeconds);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = EaseInOutCubic(Mathf.Clamp01(elapsed / duration));
            SetCreditFadeAlpha(Mathf.Lerp(fromAlpha, toAlpha, t));
            yield return null;
        }

        SetCreditFadeAlpha(toAlpha);
    }

    IEnumerator ShowPressAnyButtonRoutine()
    {
        SetPressAnyButtonVisible(true, 0f);
        float duration = 0.4f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetPressAnyButtonVisible(true, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        SetPressAnyButtonVisible(true, 1f);
    }

    Vector3 GetBossSpawnPosition()
    {
        ResolvePlayer();
        Vector3 center = player != null ? player.position : transform.position;
        float radius = Mathf.Max(0f, spawnRadiusFromPlayer);
        float minDistance = radius * Mathf.Clamp01(minSpawnDistanceFactor);
        float minDistanceSqr = minDistance * minDistance;

        if (spawnBehindPlayer && player != null)
        {
            Vector3 behindDirection = GetFlatBehindDirection();
            if (TryGetValidBossSpawnPosition(center + behindDirection * radius, center, minDistanceSqr, out Vector3 validBehindPosition))
            {
                return validBehindPosition;
            }

            for (int i = 0; i < 32; i++)
            {
                float angle = GetAlternatingSearchAngle(i, behindSpawnSearchAngle);
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * behindDirection;
                Vector3 candidate = center + direction * radius;
                if (TryGetValidBossSpawnPosition(candidate, center, minDistanceSqr, out Vector3 validPosition))
                {
                    return validPosition;
                }
            }
        }

        for (int i = 0; i < 32; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector2 circle = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            Vector3 candidate = center + new Vector3(circle.x, 0f, circle.y);
            if (TryGetValidBossSpawnPosition(candidate, center, minDistanceSqr, out Vector3 validPosition))
            {
                return validPosition;
            }
        }

        for (int i = 0; i < 32; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float fallbackRadius = Random.Range(minDistance, radius + navMeshSampleDistance);
            Vector2 circle = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * fallbackRadius;
            Vector3 candidate = center + new Vector3(circle.x, 0f, circle.y);
            if (TryGetValidBossSpawnPosition(candidate, center, minDistanceSqr, out Vector3 validPosition))
            {
                return validPosition;
            }
        }

        Vector3 fallbackDirection = spawnBehindPlayer && player != null ? GetFlatBehindDirection() : transform.forward;
        Vector3 fallbackCandidate = center + fallbackDirection * Mathf.Max(minDistance, radius);
        float fallbackSampleDistance = Mathf.Max(navMeshSampleDistance, radius, 1f);
        if (NavMesh.SamplePosition(fallbackCandidate, out NavMeshHit fallbackHit, fallbackSampleDistance, NavMesh.AllAreas))
        {
            return fallbackHit.position;
        }

        Debug.LogWarning("[BossEncounterDirector] Could not find a NavMesh spawn position near the player. Falling back to director position.", this);
        return transform.position;
    }

    Vector3 GetFlatBehindDirection()
    {
        Vector3 direction = player != null ? -player.forward : -transform.forward;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
        {
            direction = -transform.forward;
            direction.y = 0f;
        }

        return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.back;
    }

    float GetAlternatingSearchAngle(int index, float maxAngle)
    {
        int step = index / 2 + 1;
        float sign = index % 2 == 0 ? 1f : -1f;
        float normalizedStep = step / 16f;
        return sign * Mathf.Min(maxAngle, maxAngle * normalizedStep);
    }

    bool TryGetValidBossSpawnPosition(Vector3 candidate, Vector3 center, float minDistanceSqr, out Vector3 position)
    {
        position = candidate;
        if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
        {
            return false;
        }

        Vector3 offset = hit.position - center;
        offset.y = 0f;
        if (offset.sqrMagnitude < minDistanceSqr)
        {
            return false;
        }

        position = hit.position;
        return true;
    }

    Quaternion GetBossSpawnRotation(Vector3 spawnPosition)
    {
        ResolvePlayer();
        if (player == null)
        {
            return transform.rotation;
        }

        Vector3 direction = player.position - spawnPosition;
        direction.y = 0f;
        return direction.sqrMagnitude > 0.001f ? Quaternion.LookRotation(direction.normalized) : transform.rotation;
    }

    void DisableConfiguredSpawners()
    {
        if (spawnersToDisableOnBossWarning == null)
        {
            return;
        }

        for (int i = 0; i < spawnersToDisableOnBossWarning.Length; i++)
        {
            if (spawnersToDisableOnBossWarning[i] != null)
            {
                spawnersToDisableOnBossWarning[i].enabled = false;
            }
        }
    }

    void EnsureMissionClearUI()
    {
        if (missionClearRoot == null)
        {
            missionClearRoot = FindSceneGameObject("Mission Clear");
        }

        if (missionClearText == null && missionClearRoot != null)
        {
            missionClearText = missionClearRoot.GetComponentInChildren<TMP_Text>(true);
        }

        if (missionClearRoot == null)
        {
            Canvas canvas = GetOrCreateCanvas("Game Clear Canvas");
            GameObject root = new GameObject("Mission Clear", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            root.transform.SetParent(canvas.transform, false);
            missionClearRoot = root;

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, 150f);

            Image background = root.GetComponent<Image>();
            background.color = missionClearBackgroundColor;
            background.raycastTarget = false;

            GameObject textObject = new GameObject("Mission Clear Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(root.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(24f, 0f);
            textRect.offsetMax = new Vector2(-24f, 0f);

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = LocalizationManager.GetText(missionClearMessage);
            text.color = missionClearTextColor;
            text.fontStyle = FontStyles.Bold;
            text.fontSize = 64f;
            text.enableAutoSizing = true;
            text.fontSizeMin = 22f;
            text.fontSizeMax = 64f;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            missionClearText = text;
        }

        CacheMissionClearReferences();
        ApplyMissionClearResponsiveLayout();
    }

    void CacheMissionClearReferences()
    {
        if (missionClearRoot != null && missionClearRootRect == null)
        {
            missionClearRootRect = missionClearRoot.GetComponent<RectTransform>();
        }

        if (missionClearRoot != null && missionClearCanvasGroup == null)
        {
            missionClearCanvasGroup = missionClearRoot.GetComponent<CanvasGroup>();
            if (missionClearCanvasGroup == null)
            {
                missionClearCanvasGroup = missionClearRoot.AddComponent<CanvasGroup>();
            }
        }

        if (missionClearText != null && missionClearTextRect == null)
        {
            missionClearTextRect = missionClearText.GetComponent<RectTransform>();
        }
    }

    void ApplyMissionClearResponsiveLayout()
    {
        if (missionClearRootRect != null)
        {
            missionClearRootRect.anchorMin = new Vector2(0f, 0.5f);
            missionClearRootRect.anchorMax = new Vector2(1f, 0.5f);
            missionClearRootRect.pivot = new Vector2(0.5f, 0.5f);
            missionClearRootRect.anchoredPosition = Vector2.zero;
            missionClearRootRect.sizeDelta = new Vector2(0f, Mathf.Max(96f, 150f));
        }

        if (missionClearTextRect != null)
        {
            missionClearTextRect.anchorMin = Vector2.zero;
            missionClearTextRect.anchorMax = Vector2.one;
            missionClearTextRect.offsetMin = new Vector2(24f, 0f);
            missionClearTextRect.offsetMax = new Vector2(-24f, 0f);
        }

        if (missionClearText != null)
        {
            missionClearText.enableAutoSizing = true;
            missionClearText.fontSizeMin = Mathf.Min(missionClearText.fontSizeMin <= 0f ? 22f : missionClearText.fontSizeMin, 22f);
            missionClearText.fontSizeMax = Mathf.Max(missionClearText.fontSizeMax, 64f);
            missionClearText.alignment = TextAlignmentOptions.Center;
        }
    }

    void SetMissionClearVisible(bool visible)
    {
        if (visible)
        {
            EnsureMissionClearUI();
        }

        if (missionClearRoot != null)
        {
            missionClearRoot.SetActive(visible);
        }

        CacheMissionClearReferences();
        if (missionClearCanvasGroup != null)
        {
            missionClearCanvasGroup.alpha = visible ? 1f : 0f;
        }

        if (missionClearRootRect != null)
        {
            missionClearRootRect.localScale = visible ? Vector3.one : new Vector3(0.7f, 0f, 1f);
        }
    }

    Camera ResolveClearCamera()
    {
        if (clearCamera != null)
        {
            return clearCamera;
        }

        clearCamera = Camera.main;
        if (clearCamera == null)
        {
            clearCamera = FindAnyObjectByType<Camera>();
        }

        return clearCamera;
    }

    void DisableClearCameraDrivers(Camera targetCamera)
    {
        if (!disableCameraBehavioursDuringClear || targetCamera == null)
        {
            return;
        }

        Behaviour[] behaviours = targetCamera.GetComponents<Behaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour behaviour = behaviours[i];
            if (behaviour == null || !behaviour.enabled)
            {
                continue;
            }

            string typeName = behaviour.GetType().Name;
            if (typeName == nameof(Camera) || typeName == nameof(AudioListener))
            {
                continue;
            }

            if (typeName.Contains("CameraControler") || typeName.Contains("CinemachineBrain"))
            {
                DisableBehaviourForClear(behaviour);
            }
        }
    }

    void EnsureCreditUI()
    {
        if (creditRoot == null)
        {
            creditRoot = FindSceneGameObject("Credit");
        }

        if (creditRoot == null)
        {
            CreateDefaultCreditUI();
        }

        if (creditRoot == null)
        {
            return;
        }

        creditCanvasGroup = creditCanvasGroup != null ? creditCanvasGroup : creditRoot.GetComponent<CanvasGroup>();
        if (creditCanvasGroup == null)
        {
            creditCanvasGroup = creditRoot.AddComponent<CanvasGroup>();
        }

        if (creditScrollRect == null)
        {
            creditScrollRect = creditRoot.GetComponentInChildren<ScrollRect>(true);
        }

        if (creditContent == null && creditScrollRect != null)
        {
            creditContent = creditScrollRect.content;
        }

        if (creditFadeImage == null)
        {
            Transform fadeTransform = creditRoot.transform.Find("Credit Fade");
            creditFadeImage = fadeTransform != null ? fadeTransform.GetComponent<Image>() : null;
        }

        if (creditFadeImage == null)
        {
            GameObject fadeObject = new GameObject("Credit Fade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fadeObject.transform.SetParent(creditRoot.transform, false);
            RectTransform fadeRect = fadeObject.GetComponent<RectTransform>();
            fadeRect.anchorMin = Vector2.zero;
            fadeRect.anchorMax = Vector2.one;
            fadeRect.offsetMin = Vector2.zero;
            fadeRect.offsetMax = Vector2.zero;

            creditFadeImage = fadeObject.GetComponent<Image>();
            creditFadeImage.color = Color.clear;
            creditFadeImage.raycastTarget = false;
        }

        creditFadeImage.transform.SetAsLastSibling();

        if (pressAnyButtonText == null)
        {
            TMP_Text[] texts = creditRoot.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].name.ToLowerInvariant().Contains("press"))
                {
                    pressAnyButtonText = texts[i];
                    break;
                }
            }
        }

        if (pressAnyButtonText == null)
        {
            GameObject textObject = new GameObject("Press Any Button Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(creditRoot.transform, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, pressAnyButtonAnchorY);
            rect.anchorMax = new Vector2(1f, pressAnyButtonAnchorY);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(-80f, 60f);

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = LocalizationManager.GetText(pressAnyButtonMessage);
            text.color = Color.white;
            text.fontStyle = FontStyles.Bold;
            text.fontSize = 28f;
            text.enableAutoSizing = true;
            text.fontSizeMin = 14f;
            text.fontSizeMax = 32f;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            pressAnyButtonText = text;
        }

        if (pressAnyButtonText != null)
        {
            pressAnyButtonText.transform.SetAsLastSibling();
        }

        ApplyCreditResponsiveLayout();
    }

    void CreateDefaultCreditUI()
    {
        Canvas canvas = GetOrCreateCanvas("Credit Canvas");
        GameObject root = new GameObject("Credit", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        root.transform.SetParent(canvas.transform, false);
        creditRoot = root;

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image background = root.GetComponent<Image>();
        background.color = Color.black;
        background.raycastTarget = false;

        GameObject scrollObject = new GameObject("Credit Scroll View", typeof(RectTransform), typeof(ScrollRect));
        scrollObject.transform.SetParent(root.transform, false);
        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = new Vector2(0.12f, 0.12f);
        scrollRectTransform.anchorMax = new Vector2(0.88f, 0.88f);
        scrollRectTransform.offsetMin = Vector2.zero;
        scrollRectTransform.offsetMax = Vector2.zero;

        GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportObject.transform.SetParent(scrollObject.transform, false);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        viewportObject.GetComponent<Image>().color = Color.clear;
        viewportObject.GetComponent<Mask>().showMaskGraphic = false;

        GameObject contentObject = new GameObject("Content", typeof(RectTransform));
        contentObject.transform.SetParent(viewportObject.transform, false);
        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 1500f);

        GameObject creditTextObject = new GameObject("Credit Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        creditTextObject.transform.SetParent(contentObject.transform, false);
        RectTransform textRect = creditTextObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = creditTextObject.GetComponent<TextMeshProUGUI>();
        text.text = LocalizationManager.GetText("THIRD-PERSON SHOOTERS PROTOTYPE GAME\n\nCreated by Ikbal Gumilar\n\nThanks for playing");
        text.color = Color.white;
        text.fontSize = 34f;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;

        ScrollRect scrollRect = scrollObject.GetComponent<ScrollRect>();
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = false;

        creditScrollRect = scrollRect;
        creditContent = contentRect;
    }

    void ApplyCreditResponsiveLayout()
    {
        if (!autoApplyResponsiveCreditLayout || creditRoot == null)
        {
            return;
        }

        RectTransform rootRect = creditRoot.GetComponent<RectTransform>();
        if (rootRect != null)
        {
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            rootRect.pivot = new Vector2(0.5f, 0.5f);
        }

        if (creditScrollRect != null)
        {
            RectTransform scrollRectTransform = creditScrollRect.GetComponent<RectTransform>();
            if (scrollRectTransform != null)
            {
                float xMargin = Mathf.Clamp01(creditHorizontalSafeMargin);
                float yMargin = Mathf.Clamp01(creditVerticalSafeMargin);
                scrollRectTransform.anchorMin = new Vector2(xMargin, yMargin);
                scrollRectTransform.anchorMax = new Vector2(1f - xMargin, 1f - yMargin);
                scrollRectTransform.offsetMin = Vector2.zero;
                scrollRectTransform.offsetMax = Vector2.zero;
            }

            if (creditScrollRect.viewport != null)
            {
                RectTransform viewportRect = creditScrollRect.viewport;
                viewportRect.anchorMin = Vector2.zero;
                viewportRect.anchorMax = Vector2.one;
                viewportRect.offsetMin = Vector2.zero;
                viewportRect.offsetMax = Vector2.zero;
            }

            if (creditScrollRect.content != null)
            {
                creditContent = creditScrollRect.content;
            }
        }

        if (creditContent != null)
        {
            creditContent.anchorMin = new Vector2(0f, 1f);
            creditContent.anchorMax = new Vector2(1f, 1f);
            creditContent.pivot = new Vector2(0.5f, 1f);
            creditContent.anchoredPosition = Vector2.zero;
        }

        if (creditFadeImage != null)
        {
            RectTransform fadeRect = creditFadeImage.GetComponent<RectTransform>();
            if (fadeRect != null)
            {
                fadeRect.anchorMin = Vector2.zero;
                fadeRect.anchorMax = Vector2.one;
                fadeRect.offsetMin = Vector2.zero;
                fadeRect.offsetMax = Vector2.zero;
            }
        }

        if (pressAnyButtonText != null)
        {
            RectTransform pressRect = pressAnyButtonText.GetComponent<RectTransform>();
            if (pressRect != null)
            {
                pressRect.anchorMin = new Vector2(0f, pressAnyButtonAnchorY);
                pressRect.anchorMax = new Vector2(1f, pressAnyButtonAnchorY);
                pressRect.pivot = new Vector2(0.5f, 0.5f);
                pressRect.anchoredPosition = Vector2.zero;
                pressRect.sizeDelta = new Vector2(-80f, 60f);
            }

            pressAnyButtonText.enableAutoSizing = true;
            pressAnyButtonText.fontSizeMin = Mathf.Min(pressAnyButtonText.fontSizeMin <= 0f ? 14f : pressAnyButtonText.fontSizeMin, 14f);
            pressAnyButtonText.fontSizeMax = Mathf.Max(pressAnyButtonText.fontSizeMax, 32f);
            pressAnyButtonText.alignment = TextAlignmentOptions.Center;
        }
    }

    void SetCreditVisible(bool visible)
    {
        if (visible)
        {
            EnsureCreditUI();
        }

        if (creditRoot != null)
        {
            creditRoot.SetActive(visible);
        }
    }

    void ResetCreditScroll()
    {
        if (creditScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            creditScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    void SetCreditFadeAlpha(float alpha)
    {
        if (creditFadeImage == null)
        {
            return;
        }

        Color color = Color.black;
        color.a = Mathf.Clamp01(alpha);
        creditFadeImage.color = color;
    }

    float GetCreditScrollOverlayAlpha01()
    {
        return Mathf.Clamp01(creditScrollOverlayAlpha / 255f);
    }

    void SetPressAnyButtonVisible(bool visible, float alpha)
    {
        if (pressAnyButtonText == null)
        {
            return;
        }

        pressAnyButtonText.gameObject.SetActive(visible);
        pressAnyButtonText.text = LocalizationManager.GetText(pressAnyButtonMessage);
        Color color = pressAnyButtonText.color;
        color.a = Mathf.Clamp01(alpha);
        pressAnyButtonText.color = color;
    }

    Canvas GetOrCreateCanvas(string canvasName)
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas != null)
        {
            return canvas;
        }

        GameObject canvasObject = new GameObject(canvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    GameObject FindSceneGameObject(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        GameObject direct = GameObject.Find(objectName);
        if (direct != null)
        {
            return direct;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject candidate = objects[i];
            if (candidate == null || candidate.name != objectName || !candidate.scene.IsValid() || candidate.scene != activeScene)
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    bool WasAnyButtonPressedThisFrame()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            return true;
        }

        if (Mouse.current != null
            && (Mouse.current.leftButton.wasPressedThisFrame
                || Mouse.current.rightButton.wasPressedThisFrame
                || Mouse.current.middleButton.wasPressedThisFrame
                || Mouse.current.forwardButton.wasPressedThisFrame
                || Mouse.current.backButton.wasPressedThisFrame))
        {
            return true;
        }

        return WasButtonPressed(Gamepad.current) || WasButtonPressed(Joystick.current);
    }

    bool WasButtonPressed(InputDevice device)
    {
        if (device == null)
        {
            return false;
        }

        foreach (InputControl control in device.allControls)
        {
            if (control is ButtonControl button && button.wasPressedThisFrame)
            {
                return true;
            }
        }

        return false;
    }

    void ShowWarning()
    {
        EnsureWarningUI();
        if (warningText != null)
        {
            warningText.text = LocalizationManager.GetText(warningMessage);
        }

        PlayWarningIntro();
    }

    void SetWarningVisible(bool visible)
    {
        if (visible)
        {
            EnsureWarningUI();
        }

        if (warningRoot != null)
        {
            warningRoot.SetActive(visible);
        }
        else if (warningText != null)
        {
            warningText.gameObject.SetActive(visible);
        }

        if (!visible)
        {
            StopWarningAnimation();
            ResetWarningTransform(false);
        }
    }

    void EnsureWarningUI()
    {
        if (!autoCreateWarningUI || warningRoot != null || warningText != null)
        {
            return;
        }

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("Boss Warning Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        GameObject root = new GameObject("Boss Warning", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        root.transform.SetParent(canvas.transform, false);
        warningRoot = root;

        warningRootRect = root.GetComponent<RectTransform>();
        warningCanvasGroup = root.GetComponent<CanvasGroup>();
        warningRootRect.anchorMin = new Vector2(0f, 0.5f);
        warningRootRect.anchorMax = new Vector2(1f, 0.5f);
        warningRootRect.pivot = new Vector2(0.5f, 0.5f);
        warningRootRect.anchoredPosition = Vector2.zero;
        warningRootRect.sizeDelta = new Vector2(0f, warningPanelHeight);

        Image background = root.GetComponent<Image>();
        background.color = warningBackgroundColor;
        background.raycastTarget = false;

        GameObject textObject = new GameObject("Warning Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(root.transform, false);
        warningTextRect = textObject.GetComponent<RectTransform>();
        warningTextRect.anchorMin = Vector2.zero;
        warningTextRect.anchorMax = Vector2.one;
        warningTextRect.offsetMin = new Vector2(24f, 0f);
        warningTextRect.offsetMax = new Vector2(-24f, 0f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.raycastTarget = false;
        text.text = LocalizationManager.GetText(warningMessage);
        text.color = warningTextColor;
        text.fontSize = warningFontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 18f;
        text.fontSizeMax = warningFontSize;
        warningText = text;
    }

    void CacheWarningUIReferences()
    {
        if (warningRoot != null && warningRootRect == null)
        {
            warningRootRect = warningRoot.GetComponent<RectTransform>();
        }

        if (warningRoot != null && warningCanvasGroup == null)
        {
            warningCanvasGroup = warningRoot.GetComponent<CanvasGroup>();
            if (warningCanvasGroup == null)
            {
                warningCanvasGroup = warningRoot.AddComponent<CanvasGroup>();
            }
        }

        if (warningText != null && warningTextRect == null)
        {
            warningTextRect = warningText.GetComponent<RectTransform>();
        }
    }

    void PlayWarningIntro()
    {
        EnsureWarningUI();
        CacheWarningUIReferences();
        SetWarningVisible(true);

        if (!animateWarningUI || warningRootRect == null)
        {
            ResetWarningTransform(true);
            return;
        }

        StopWarningAnimation();
        warningAnimationRoutine = StartCoroutine(WarningIntroRoutine());
    }

    IEnumerator WarningIntroRoutine()
    {
        CacheWarningUIReferences();
        Vector2 rootCenter = Vector2.zero;
        Vector2 textCenter = Vector2.zero;
        Vector2 rootStart = new Vector2(warningPanelOffscreenX, 0f);
        Vector2 textStart = new Vector2(warningTextOffscreenX, 0f);

        if (warningCanvasGroup != null)
        {
            warningCanvasGroup.alpha = 1f;
        }

        warningRootRect.anchoredPosition = rootStart;
        warningRootRect.localScale = new Vector3(1f, 0f, 1f);
        if (warningTextRect != null)
        {
            warningTextRect.anchoredPosition = textStart;
            warningTextRect.localScale = Vector3.one;
        }

        float slideDuration = Mathf.Max(0.01f, warningPanelSlideDuration);
        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = EaseOutCubic(Mathf.Clamp01(elapsed / slideDuration));
            warningRootRect.anchoredPosition = Vector2.LerpUnclamped(rootStart, rootCenter, t);
            yield return null;
        }

        warningRootRect.anchoredPosition = rootCenter;

        float revealDuration = Mathf.Max(0.01f, Mathf.Max(warningScaleDuration, warningTextSlideDuration));
        elapsed = 0f;
        while (elapsed < revealDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float scaleT = EaseOutBack(Mathf.Clamp01(elapsed / Mathf.Max(0.01f, warningScaleDuration)));
            float textT = EaseOutCubic(Mathf.Clamp01(elapsed / Mathf.Max(0.01f, warningTextSlideDuration)));

            warningRootRect.localScale = new Vector3(1f, scaleT, 1f);
            if (warningTextRect != null)
            {
                warningTextRect.anchoredPosition = Vector2.LerpUnclamped(textStart, textCenter, textT);
            }

            yield return null;
        }

        ResetWarningTransform(true);
        warningAnimationRoutine = null;
    }

    IEnumerator HideWarningOutroRoutine()
    {
        EnsureWarningUI();
        CacheWarningUIReferences();
        if (warningRoot == null)
        {
            yield break;
        }

        if (!animateWarningUI || warningRootRect == null)
        {
            SetWarningVisible(false);
            yield break;
        }

        StopWarningAnimation();
        warningRoot.SetActive(true);
        if (warningCanvasGroup != null)
        {
            warningCanvasGroup.alpha = 1f;
        }

        Vector2 rootCenter = Vector2.zero;
        Vector2 textCenter = Vector2.zero;
        Vector2 rootEnd = new Vector2(warningPanelOffscreenX, 0f);
        Vector2 textEnd = new Vector2(warningTextOffscreenX, 0f);

        warningRootRect.anchoredPosition = rootCenter;
        warningRootRect.localScale = Vector3.one;
        if (warningTextRect != null)
        {
            warningTextRect.anchoredPosition = textCenter;
            warningTextRect.localScale = Vector3.one;
        }

        float collapseDuration = Mathf.Max(0.01f, Mathf.Max(warningScaleDuration, warningTextSlideDuration));
        float elapsed = 0f;
        while (elapsed < collapseDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float scaleT = EaseInCubic(Mathf.Clamp01(elapsed / Mathf.Max(0.01f, warningScaleDuration)));
            float textT = EaseInCubic(Mathf.Clamp01(elapsed / Mathf.Max(0.01f, warningTextSlideDuration)));

            warningRootRect.localScale = new Vector3(1f, Mathf.LerpUnclamped(1f, 0f, scaleT), 1f);
            if (warningTextRect != null)
            {
                warningTextRect.anchoredPosition = Vector2.LerpUnclamped(textCenter, textEnd, textT);
            }

            yield return null;
        }

        warningRootRect.localScale = new Vector3(1f, 0f, 1f);
        if (warningTextRect != null)
        {
            warningTextRect.anchoredPosition = textEnd;
        }

        float slideDuration = Mathf.Max(0.01f, warningPanelSlideDuration);
        elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = EaseInCubic(Mathf.Clamp01(elapsed / slideDuration));
            warningRootRect.anchoredPosition = Vector2.LerpUnclamped(rootCenter, rootEnd, t);
            yield return null;
        }

        SetWarningVisible(false);
    }

    void StopWarningAnimation()
    {
        if (warningAnimationRoutine == null)
        {
            return;
        }

        StopCoroutine(warningAnimationRoutine);
        warningAnimationRoutine = null;
    }

    void ResetWarningTransform(bool visibleState)
    {
        CacheWarningUIReferences();
        if (warningCanvasGroup != null)
        {
            warningCanvasGroup.alpha = visibleState ? 1f : 0f;
        }

        if (warningRootRect != null)
        {
            warningRootRect.anchoredPosition = Vector2.zero;
            warningRootRect.localScale = visibleState ? Vector3.one : new Vector3(1f, 0f, 1f);
        }

        if (warningTextRect != null)
        {
            warningTextRect.anchoredPosition = Vector2.zero;
            warningTextRect.localScale = Vector3.one;
        }
    }

    float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    float EaseInCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t;
    }

    float EaseInOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t < 0.5f
            ? 4f * t * t * t
            : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
    }

    float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    void ResolvePlayer()
    {
        if (player != null)
        {
            return;
        }

        PlayerHealth playerHealth = FindAnyObjectByType<PlayerHealth>();
        if (playerHealth != null)
        {
            player = playerHealth.transform;
        }
    }
}
