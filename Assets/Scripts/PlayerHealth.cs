using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;

public class PlayerHealth : MonoBehaviour
{
    public event System.Action Damaged;

    public float maxHealth = 100f;
    public float currentHealth = 100f;
    public float regenHealth = 100f;
    [Range(0f, 1f)] public float regenDamageLossPercent = 0.4f;
    public float healthCatchUpSpeed = 25f;
    public bool usePercentHealthCatchUpSpeed = true;
    public float healthCatchUpSpeedPercent = 25f;
    [Header("Shield")]
    [Tooltip("Legacy/reference value. Shield UI now uses maxHealth as its visual max; currentShield can exceed it.")]
    public float maxShield = 0f;
    public float currentShield = 0f;
    public bool useShield = true;
    [SerializeField] private List<float> shieldStacks = new List<float>();
    [Range(0f, 1f)] public float heavyShieldHealthPiercePercent = 1f;
    [Range(0f, 1f)] public float criticalShieldHealthPiercePercent = 1f;
    [Range(0f, 1f)] public float knockbackShieldHealthPiercePercent = 0.2f;
    [Range(0f, 2f)] public float knockbackExtraShieldDamagePercent = 1f;
    public Animator animator;
    public string deadTriggerName = "Dead";
    public string deadStateName = "Dead";
    public string deadLayerName = "Base Layer";
    [Header("Death Animation States")]
    public string unarmedDeathLayerName = "Unarmed-Death-Revive";
    public string unarmedDeathStateName = "Unarmed-Death1";
    public string oneHandDeathLayerName = "Armed-Death-Revive";
    public string oneHandDeathStateName = "Armed-Death1";
    public string twoHandDeathLayerName = "2Hand-Shooting-Death-Revive";
    public string twoHandDeathStateName = "Shooting-Death1";
    public float deadFadeDuration = 0.1f;
    public float deadStopDelay = 12f;
    [Tooltip("Scene loaded after the death UI timeout or confirmation input.")]
    public string deathReturnSceneName = "MainMenu";
    public bool disableControlOnDeath = true;
    public Transform deathCameraTransform;
    public Vector3 deathCameraLookOffset = new Vector3(0f, 1.2f, 0f);
    public float deathCameraHeight = 4f;
    public float deathCameraStartDistance = 3f;
    public float deathCameraEndDistance = 7f;
    public float deathCameraMoveSmooth = 4f;
    public bool lockPositionOnDeath = true;
    public bool disableEventSystemOnDeath = true;
    public bool lockCursorOnDeath = true;
    public GameObject deathUiRoot;
    public TMP_Text deathTitleText;
    public TMP_Text deathPromptText;
    public bool showDeathUiOnDeath = true;
    public bool allowAnyKeyExitOnDeath = true;
    public float deathUiDelay = 1f;
    public float deathUiFadeDuration = 0.75f;
    public float deathTitleStartScale = 0.85f;
    public float deathPromptDelay = 1.2f;
    public float deathPromptFadeDuration = 0.6f;
    public float deathPromptBlinkSpeed = 3f;
    [Range(0, 255)] public int deathPromptBlinkMinVertexAlpha = 0;
    [Range(0, 255)] public int deathPromptBlinkMaxVertexAlpha = 255;
    [Header("Hit Audio")]
    public AudioClip[] hitSounds;
    public float hitSoundVolume = 0.8f;
    public Vector2 hitSoundPitchRange = new Vector2(0.95f, 1.05f);
    public GameObject hitEffect;
    public GameObject criticalHitEffect;
    public float hitEffectSurfaceOffset = 0.02f;
    [Header("Shield Audio")]
    public AudioClip[] shieldHitSounds;
    public float shieldHitSoundVolume = 0.8f;
    public Vector2 shieldHitSoundPitchRange = new Vector2(0.95f, 1.05f);
    public AudioClip[] shieldBreakSounds;
    public float shieldBreakSoundVolume = 1f;
    public Vector2 shieldBreakSoundPitchRange = new Vector2(0.95f, 1.05f);
    [Header("Death Audio")]
    public AudioClip[] deathSounds;
    public float deathSoundVolume = 1f;
    public Vector2 deathSoundPitchRange = new Vector2(0.95f, 1.05f);

    public float CurrentHealth => currentHealth;
    public float RegenHealth => regenHealth;
    public float MaxHealth => maxHealth;
    public float CurrentShield => currentShield;
    public float MaxShield => Mathf.Max(1f, maxHealth);
    public int ShieldStackCount => shieldStacks.Count;
    public bool HasShield => useShield && currentShield > 0f;
    public bool IsDead => isDead;
    public bool LastDamageWasBlocked { get; private set; }

    private bool isDead;
    private Coroutine deathRoutine;
    private Coroutine deathCameraRoutine;
    private Coroutine deathUiRoutine;
    private Vector3 deathPosition;
    private EventSystem cachedEventSystem;
    private CanvasGroup deathUiCanvasGroup;
    private Color32 deathPromptOriginalVertexColor = new Color32(255, 255, 255, 255);
    private PlayerStatusEffectController statusController;
    private PlayerBlockController blockController;
    private bool deathPromptOriginalColorCached;
    private RectTransform deathTitleRect;
    private Vector3 deathTitleOriginalScale = Vector3.one;
    private bool deathTitleOriginalScaleCached;
    private AudioSource audioSource;
    private KontrolPemain kontrolPemain;

    void Awake()
    {
        kontrolPemain = new KontrolPemain();
        animator = animator != null ? animator : GetComponent<Animator>();
        AnimationEventReceiver.EnsureOn(animator);
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        if (deathCameraTransform == null && Camera.main != null)
        {
            deathCameraTransform = Camera.main.transform;
        }

        cachedEventSystem = EventSystem.current;
        if (deathUiRoot != null)
        {
            SetupDeathUiReferences();
            deathUiRoot.SetActive(false);
        }

        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        regenHealth = Mathf.Clamp(regenHealth, currentHealth, maxHealth);
        InitializeShieldStacks();
        isDead = currentHealth <= 0f;
    }

    void OnEnable()
    {
        kontrolPemain?.Pemain.Enable();
    }

    void OnDisable()
    {
        kontrolPemain?.Pemain.Disable();
    }

    void OnDestroy()
    {
        kontrolPemain?.Dispose();
    }

    void Update()
    {
        if (!isDead && currentHealth < regenHealth)
        {
            float catchUpSpeed = GetHealthCatchUpSpeed();
            currentHealth = Mathf.MoveTowards(currentHealth, regenHealth, catchUpSpeed * Time.deltaTime);
        }
    }

    float GetHealthCatchUpSpeed()
    {
        statusController = statusController != null ? statusController : GetComponent<PlayerStatusEffectController>();
        float percent = statusController != null
            ? statusController.ModifyStat(StatusEffectStat.HealthCatchUpSpeedPercent, healthCatchUpSpeedPercent)
            : healthCatchUpSpeedPercent;

        float percentSpeed = maxHealth * percent / 100f;
        if (usePercentHealthCatchUpSpeed && percent > 0f)
        {
            return Mathf.Max(0f, percentSpeed);
        }

        return Mathf.Max(0f, healthCatchUpSpeed + percentSpeed);
    }

    void LateUpdate()
    {
        if (isDead && lockPositionOnDeath)
        {
            transform.position = deathPosition;
        }
    }

    public void TakeDamage(float damage)
    {
        TakeDamage(damage, transform.position, -transform.forward, false);
    }

    public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitNormal, bool isCritical = false)
    {
        TakeDamage(damage, hitPoint, hitNormal, false, isCritical, false);
    }

    public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitNormal, bool isHeavy, bool isCritical, bool isKnockback)
    {
        LastDamageWasBlocked = false;
        if (damage <= 0f || IsDead)
        {
            return;
        }

        damage = ApplyDamageReduction(damage);
        if (damage <= 0f)
        {
            return;
        }

        blockController = blockController != null ? blockController : GetComponent<PlayerBlockController>();
        if (blockController != null && blockController.TryBlockDamage(damage, hitPoint, hitNormal, isHeavy, isCritical, isKnockback, out float blockedDamage))
        {
            LastDamageWasBlocked = true;
            damage = blockedDamage;
            if (damage <= 0f)
            {
                Damaged?.Invoke();
                return;
            }
        }

        SpawnHitEffect(hitPoint, hitNormal, isCritical);
        damage = ApplyShieldDamage(damage, isHeavy, isCritical, isKnockback);
        if (damage <= 0f)
        {
            Damaged?.Invoke();
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        regenHealth = Mathf.Max(currentHealth, regenHealth - damage * regenDamageLossPercent);
        Damaged?.Invoke();

        if (currentHealth <= 0f)
        {
            Die();
            return;
        }

        PlayHitSound();
    }

    float ApplyDamageReduction(float damage)
    {
        statusController = statusController != null ? statusController : GetComponent<PlayerStatusEffectController>();
        if (statusController == null)
        {
            return damage;
        }

        float reductionPercent = Mathf.Clamp(statusController.ModifyStat(StatusEffectStat.DamageReductionPercent, 0f), 0f, 90f);
        return damage * (1f - reductionPercent / 100f);
    }

    float ApplyShieldDamage(float damage, bool isHeavy, bool isCritical, bool isKnockback)
    {
        if (!HasShield)
        {
            return damage;
        }

        float shieldDamage = damage;
        float healthDamage = 0f;

        if (isKnockback)
        {
            shieldDamage = damage * (1f + knockbackExtraShieldDamagePercent);
            healthDamage = damage * knockbackShieldHealthPiercePercent;
        }
        else if (isHeavy)
        {
            healthDamage = damage * heavyShieldHealthPiercePercent;
        }
        else if (isCritical)
        {
            healthDamage = damage * criticalShieldHealthPiercePercent;
        }

        bool allShieldBroken = ConsumeShieldDamage(shieldDamage);
        if (allShieldBroken)
        {
            PlayShieldBreakSound();
        }
        else
        {
            PlayShieldHitSound();
        }

        return healthDamage;
    }

    public void AddShield(float amount)
    {
        AddShieldStack(amount);
    }

    public void AddShieldStack(float amount)
    {
        if (amount <= 0f || IsDead)
        {
            return;
        }

        shieldStacks.Add(amount);
        RecalculateCurrentShield();
    }

    void InitializeShieldStacks()
    {
        for (int i = shieldStacks.Count - 1; i >= 0; i--)
        {
            if (shieldStacks[i] <= 0f)
            {
                shieldStacks.RemoveAt(i);
            }
        }

        if (shieldStacks.Count == 0 && currentShield > 0f)
        {
            shieldStacks.Add(currentShield);
        }

        RecalculateCurrentShield();
    }

    bool ConsumeShieldDamage(float damage)
    {
        if (damage <= 0f || shieldStacks.Count == 0)
        {
            return currentShield <= 0f;
        }

        while (damage > 0f && shieldStacks.Count > 0)
        {
            float firstStack = shieldStacks[0];
            float used = Mathf.Min(firstStack, damage);
            firstStack -= used;
            damage -= used;

            if (firstStack <= 0f)
            {
                shieldStacks.RemoveAt(0);
            }
            else
            {
                shieldStacks[0] = firstStack;
            }
        }

        RecalculateCurrentShield();
        return currentShield <= 0f;
    }

    void RecalculateCurrentShield()
    {
        float total = 0f;
        for (int i = 0; i < shieldStacks.Count; i++)
        {
            total += Mathf.Max(0f, shieldStacks[i]);
        }

        currentShield = total;
    }

    public void Heal(float amount)
    {
        if (amount <= 0f || IsDead)
        {
            return;
        }

        regenHealth = Mathf.Min(maxHealth, regenHealth + amount);
    }

    public void ApplyDirectHealthDelta(float amount)
    {
        ApplyDirectHealthDelta(amount, true, true, regenDamageLossPercent, true);
    }

    public void ApplyDirectHealthDelta(float amount, bool canKill, bool invokeDamagedEvent, float regenLossPercent)
    {
        ApplyDirectHealthDelta(amount, canKill, invokeDamagedEvent, regenLossPercent, true);
    }

    public void ApplyDirectHealthDelta(float amount, bool canKill, bool invokeDamagedEvent, float regenLossPercent, bool clampRegenToCurrentHealth)
    {
        if (Mathf.Approximately(amount, 0f) || IsDead)
        {
            return;
        }

        float minimumHealth = canKill ? 0f : 1f;
        currentHealth = Mathf.Clamp(currentHealth + amount, minimumHealth, maxHealth);
        if (amount < 0f)
        {
            float nextRegenHealth = regenHealth + amount * Mathf.Clamp01(regenLossPercent);
            regenHealth = clampRegenToCurrentHealth
                ? Mathf.Max(currentHealth, nextRegenHealth)
                : Mathf.Max(0f, nextRegenHealth);

            if (invokeDamagedEvent)
            {
                Damaged?.Invoke();
            }
        }
        else
        {
            regenHealth = Mathf.Max(regenHealth, currentHealth);
        }

        if (canKill && currentHealth <= 0f)
        {
            Die();
        }
    }

    void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        deathPosition = transform.position;
        currentHealth = 0f;
        regenHealth = Mathf.Max(0f, regenHealth);

        NotifyEnemiesPlayerDied();
        PlayDeathSound();
        PlayDeathAnimation();
        StartDeathUiSequence();

        if (disableControlOnDeath)
        {
            DisableControlComponents();
        }

        DisableGlobalInput();

        if (deathCameraRoutine == null)
        {
            deathCameraRoutine = StartCoroutine(DeathCameraRoutine());
        }

        if (deathRoutine == null)
        {
            deathRoutine = StartCoroutine(StopAfterDeathDelay());
        }
    }

    void PlayDeathAnimation()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator == null)
        {
            return;
        }

        if (TryCrossFadeConfiguredDeathState())
        {
            return;
        }

        if (HasAnimatorTrigger(deadTriggerName))
        {
            animator.SetTrigger(deadTriggerName);
            animator.Update(0f);
            return;
        }

        CrossFadeDeathState();
    }

    void PlayDeathSound()
    {
        PlayRandomSound(deathSounds, deathSoundVolume, deathSoundPitchRange);
    }

    void PlayHitSound()
    {
        PlayRandomSound(hitSounds, hitSoundVolume, hitSoundPitchRange);
    }

    void PlayShieldHitSound()
    {
        PlayRandomSound(shieldHitSounds, shieldHitSoundVolume, shieldHitSoundPitchRange);
    }

    void PlayShieldBreakSound()
    {
        PlayRandomSound(shieldBreakSounds, shieldBreakSoundVolume, shieldBreakSoundPitchRange);
    }

    void PlayRandomSound(AudioClip[] clips, float volume, Vector2 pitchRange)
    {
        if (audioSource == null || clips == null || clips.Length == 0)
        {
            return;
        }

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null)
        {
            return;
        }

        audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
        audioSource.PlayOneShot(clip, volume);
    }

    void SpawnHitEffect(Vector3 hitPoint, Vector3 hitNormal, bool isCritical)
    {
        GameObject effect = isCritical && criticalHitEffect != null ? criticalHitEffect : hitEffect;
        if (effect == null)
        {
            return;
        }

        Vector3 normal = hitNormal.sqrMagnitude > 0.0001f ? hitNormal.normalized : -transform.forward;
        Vector3 spawnPosition = hitPoint + normal * hitEffectSurfaceOffset;
        Quaternion spawnRotation = Quaternion.LookRotation(normal);
        Instantiate(effect, spawnPosition, spawnRotation);
    }

    IEnumerator StopAfterDeathDelay()
    {
        float elapsed = 0f;
        float inputDelay = Mathf.Max(0f, deathUiDelay + deathPromptDelay);
        float maxDelay = Mathf.Max(inputDelay, deadStopDelay);

        while (elapsed < maxDelay)
        {
            if (allowAnyKeyExitOnDeath && elapsed >= inputDelay && IsDeathConfirmPressedThisFrame())
            {
                ReturnToMainMenu();
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        ReturnToMainMenu();
    }

    bool IsDeathConfirmPressedThisFrame()
    {
        return kontrolPemain != null && kontrolPemain.Pemain.DeathConfirm.WasPressedThisFrame();
    }

    void ReturnToMainMenu()
    {
        if (Application.CanStreamedLevelBeLoaded(deathReturnSceneName))
        {
            SceneManager.LoadScene(deathReturnSceneName);
            return;
        }

        Debug.LogError($"Death return scene {deathReturnSceneName} is not included in Build Settings.", this);
    }

    IEnumerator DeathCameraRoutine()
    {
        if (deathCameraTransform == null)
        {
            yield break;
        }

        Vector3 lookPoint = transform.position + deathCameraLookOffset;
        Vector3 awayDirection = deathCameraTransform.position - lookPoint;
        awayDirection.y = 0f;

        if (awayDirection.sqrMagnitude < 0.01f)
        {
            awayDirection = -transform.forward;
        }

        awayDirection.Normalize();

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, deadStopDelay);

        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            float distance = Mathf.Lerp(deathCameraStartDistance, deathCameraEndDistance, smoothT);
            lookPoint = transform.position + deathCameraLookOffset;

            Vector3 targetPosition = lookPoint + Vector3.up * deathCameraHeight + awayDirection * distance;
            deathCameraTransform.position = Vector3.Lerp(
                deathCameraTransform.position,
                targetPosition,
                deathCameraMoveSmooth * Time.deltaTime
            );

            Vector3 lookDirection = lookPoint - deathCameraTransform.position;
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
                deathCameraTransform.rotation = Quaternion.Slerp(
                    deathCameraTransform.rotation,
                    targetRotation,
                    deathCameraMoveSmooth * Time.deltaTime
                );
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    void StartDeathUiSequence()
    {
        if (!showDeathUiOnDeath || deathUiRoot == null || deathUiRoutine != null)
        {
            return;
        }

        deathUiRoutine = StartCoroutine(DeathUiRoutine());
    }

    IEnumerator DeathUiRoutine()
    {
        SetupDeathUiReferences();

        yield return new WaitForSeconds(Mathf.Max(0f, deathUiDelay));

        deathUiRoot.SetActive(true);

        if (deathUiCanvasGroup != null)
        {
            deathUiCanvasGroup.alpha = 0f;
        }

        if (deathPromptText != null)
        {
            SetDeathPromptAlpha01(0f);
        }

        if (deathTitleRect != null)
        {
            deathTitleRect.localScale = deathTitleOriginalScale * deathTitleStartScale;
        }

        float elapsed = 0f;
        float fadeDuration = Mathf.Max(0.01f, deathUiFadeDuration);
        while (elapsed < fadeDuration)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / fadeDuration);

            if (deathUiCanvasGroup != null)
            {
                deathUiCanvasGroup.alpha = t;
            }

            if (deathTitleRect != null)
            {
                deathTitleRect.localScale = Vector3.Lerp(
                    deathTitleOriginalScale * deathTitleStartScale,
                    deathTitleOriginalScale,
                    t
                );
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (deathUiCanvasGroup != null)
        {
            deathUiCanvasGroup.alpha = 1f;
        }

        if (deathTitleRect != null)
        {
            deathTitleRect.localScale = deathTitleOriginalScale;
        }

        yield return new WaitForSeconds(Mathf.Max(0f, deathPromptDelay));

        elapsed = 0f;
        float promptFadeDuration = Mathf.Max(0.01f, deathPromptFadeDuration);
        while (elapsed < promptFadeDuration)
        {
            if (deathPromptText != null)
            {
                SetDeathPromptAlpha01(Mathf.SmoothStep(0f, 1f, elapsed / promptFadeDuration));
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        while (isDead)
        {
            if (deathPromptText != null)
            {
                int minAlpha = Mathf.Clamp(deathPromptBlinkMinVertexAlpha, 0, 255);
                int maxAlpha = Mathf.Clamp(deathPromptBlinkMaxVertexAlpha, minAlpha, 255);
                int alpha = Mathf.RoundToInt(Mathf.Lerp(
                    minAlpha,
                    maxAlpha,
                    Mathf.PingPong(Time.time * deathPromptBlinkSpeed, 1f)
                ));

                SetDeathPromptVertexAlpha(alpha);
            }

            yield return null;
        }
    }

    void SetupDeathUiReferences()
    {
        if (deathUiRoot == null)
        {
            return;
        }

        deathUiCanvasGroup = deathUiRoot.GetComponent<CanvasGroup>();
        if (deathUiCanvasGroup == null)
        {
            deathUiCanvasGroup = deathUiRoot.AddComponent<CanvasGroup>();
        }

        deathUiCanvasGroup.interactable = false;
        deathUiCanvasGroup.blocksRaycasts = false;

        if (deathPromptText == null || deathTitleText == null)
        {
            TMP_Text[] texts = deathUiRoot.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text == null)
                {
                    continue;
                }

                string normalizedText = text.text.ToLowerInvariant();
                string normalizedName = text.gameObject.name.ToLowerInvariant();
                if (deathPromptText == null
                    && (normalizedText.Contains("press any key") || normalizedName.Contains("press any key")))
                {
                    deathPromptText = text;
                }
                else if (deathTitleText == null
                    && (normalizedText.Contains("you are death") || normalizedName.Contains("death")))
                {
                    deathTitleText = text;
                }
            }
        }

        if (deathPromptText != null && !deathPromptOriginalColorCached)
        {
            deathPromptOriginalVertexColor = deathPromptText.color;
            deathPromptOriginalVertexColor.a = 255;
            deathPromptOriginalColorCached = true;
        }

        if (deathTitleText != null)
        {
            deathTitleRect = deathTitleText.GetComponent<RectTransform>();
            if (deathTitleRect != null && !deathTitleOriginalScaleCached)
            {
                deathTitleOriginalScale = deathTitleRect.localScale;
                deathTitleOriginalScaleCached = true;
            }
        }

        SetDeathPromptVertexAlpha(0);
    }

    void SetDeathPromptAlpha01(float alpha)
    {
        SetDeathPromptVertexAlpha(Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
    }

    void SetDeathPromptVertexAlpha(int alpha)
    {
        if (deathPromptText == null)
        {
            return;
        }

        Color32 color = deathPromptOriginalVertexColor;
        color.a = (byte)Mathf.Clamp(alpha, 0, 255);
        deathPromptText.color = color;
    }

    void DisableControlComponents()
    {
        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
        {
            movement.enabled = false;
        }

        PlayerShoot shoot = GetComponent<PlayerShoot>();
        if (shoot != null)
        {
            shoot.enabled = false;
        }

        PlayerMeleeController meleeController = GetComponent<PlayerMeleeController>();
        if (meleeController != null)
        {
            meleeController.enabled = false;
        }

        PlayerBlockController blockController = GetComponent<PlayerBlockController>();
        if (blockController != null)
        {
            blockController.enabled = false;
        }

        PlayerWeaponEquip weaponEquip = GetComponent<PlayerWeaponEquip>();
        if (weaponEquip != null)
        {
            weaponEquip.enabled = false;
        }

        CameraControler cameraControler = GetComponent<CameraControler>();
        if (cameraControler != null)
        {
            cameraControler.enabled = false;
        }

        Camera deathCamera = deathCameraTransform != null
            ? deathCameraTransform.GetComponent<Camera>()
            : Camera.main;
        CinemachineBrain cinemachineBrain = deathCamera != null ? deathCamera.GetComponent<CinemachineBrain>() : null;
        if (cinemachineBrain != null)
        {
            cinemachineBrain.enabled = false;
        }

        PlayerWeaponAnimator weaponAnimator = GetComponent<PlayerWeaponAnimator>();
        if (weaponAnimator != null)
        {
            weaponAnimator.enabled = false;
        }

        PlayerAimIK aimIK = GetComponent<PlayerAimIK>();
        if (aimIK != null)
        {
            aimIK.enabled = false;
        }

        PlayerScopeController scopeController = GetComponent<PlayerScopeController>();
        if (scopeController != null)
        {
            scopeController.enabled = false;
        }

        CursorController cursorController = GetComponent<CursorController>();
        if (cursorController != null)
        {
            cursorController.enabled = false;
        }
    }

    void DisableGlobalInput()
    {
        if (lockCursorOnDeath)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        if (!disableEventSystemOnDeath)
        {
            return;
        }

        if (cachedEventSystem == null)
        {
            cachedEventSystem = EventSystem.current;
        }

        if (cachedEventSystem != null)
        {
            cachedEventSystem.enabled = false;
        }
    }

    void NotifyEnemiesPlayerDied()
    {
        IReadOnlyList<EnemyAI> enemies = EnemyAI.ActiveEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyAI enemyAI = enemies[i];
            if (enemyAI != null && enemyAI.gameObject.activeInHierarchy)
            {
                enemyAI.OnPlayerDied(transform);
            }
        }
    }

    bool HasAnimatorTrigger(string parameterName)
    {
        if (animator == null || string.IsNullOrEmpty(parameterName))
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == parameterName)
            {
                return true;
            }
        }

        return false;
    }

    void CrossFadeDeathState()
    {
        if (animator == null || string.IsNullOrEmpty(deadStateName))
        {
            return;
        }

        int layerIndex = animator.GetLayerIndex(deadLayerName);
        if (layerIndex < 0)
        {
            layerIndex = 0;
        }

        int fullPathHash = Animator.StringToHash($"{deadLayerName}.{deadStateName}");
        int stateHash = animator.HasState(layerIndex, fullPathHash)
            ? fullPathHash
            : Animator.StringToHash(deadStateName);

        if (animator.HasState(layerIndex, stateHash))
        {
            ActivateDeathLayer(layerIndex);
            animator.CrossFadeInFixedTime(stateHash, deadFadeDuration, layerIndex, 0f);
            animator.Update(0f);
        }
    }

    bool TryCrossFadeConfiguredDeathState()
    {
        Weapon weapon = GetComponent<PlayerShoot>() != null ? GetComponent<PlayerShoot>().currentWeapon : null;
        if (weapon == null)
        {
            return TryCrossFadeDeathState(unarmedDeathLayerName, unarmedDeathStateName);
        }

        if (weapon.holdType == WeaponHoldType.OneHand)
        {
            return TryCrossFadeDeathState(oneHandDeathLayerName, oneHandDeathStateName)
                || TryCrossFadeDeathState(unarmedDeathLayerName, unarmedDeathStateName);
        }

        return TryCrossFadeDeathState(twoHandDeathLayerName, twoHandDeathStateName)
            || TryCrossFadeDeathState(unarmedDeathLayerName, unarmedDeathStateName);
    }

    bool TryCrossFadeDeathState(string layerName, string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(layerName) || string.IsNullOrEmpty(stateName))
        {
            return false;
        }

        int layerIndex = animator.GetLayerIndex(layerName);
        if (layerIndex < 0)
        {
            return false;
        }

        int stateHash = Animator.StringToHash($"{layerName}.{stateName}");
        if (!animator.HasState(layerIndex, stateHash))
        {
            stateHash = Animator.StringToHash(stateName);
            if (!animator.HasState(layerIndex, stateHash))
            {
                return false;
            }
        }

        ActivateDeathLayer(layerIndex);
        animator.CrossFadeInFixedTime(stateHash, Mathf.Max(0f, deadFadeDuration), layerIndex, 0f);
        animator.Update(0f);
        return true;
    }

    void ActivateDeathLayer(int deathLayerIndex)
    {
        if (animator == null)
        {
            return;
        }

        for (int i = 1; i < animator.layerCount; i++)
        {
            animator.SetLayerWeight(i, i == deathLayerIndex ? 1f : 0f);
        }

        animator.SetLayerWeight(0, deathLayerIndex == 0 ? 1f : 0f);
    }
}
