using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Runtime controller for the Options canvas in MainScene.</summary>
public sealed class InGameOptionsMenu : MonoBehaviour
{
    private static InGameOptionsMenu instance;
    private static bool inputBlocked;

    private const string MainSceneName = "MainScene";
    private const string MainMenuSceneName = "MainMenu";
    private const string ResolutionKey = "Graphics.Resolution";
    private const string QualityKey = "Graphics.Quality";
    private const string ShadowKey = "Graphics.Shadows";
    private const string AntiAliasingKey = "Graphics.AntiAliasing";
    private const string TextureKey = "Graphics.TextureQuality";
    private const string VSyncKey = "Graphics.VSync";
    private const string FullscreenKey = "Graphics.Fullscreen";
    private const string CameraSensitivityKey = "Controls.CameraSensitivity";
    private const string AimSensitivityKey = "Controls.AimSensitivity";
    private const float DefaultCameraSensitivity = 1.2f;
    private const float DefaultAimSensitivity = 0.9f;
    private const string BloomKey = "PostProcessing.Bloom";
    private const string MotionBlurKey = "PostProcessing.MotionBlur";
    private const string DepthOfFieldKey = "PostProcessing.DepthOfField";
    private const string ChromaticAberrationKey = "PostProcessing.ChromaticAberration";
    private const string FilmGrainKey = "PostProcessing.FilmGrain";
    private const float MainMenuBlurDuration = 3f;
    private const float MainMenuBlurRampDuration = 0.2f;
    private const float MainMenuDarkenDuration = 2f;
    private const float OptionsHiddenOffsetY = -1200f;
    private const float OptionsClosedChildScale = 0.2f;
    private const float OptionsOpenDuration = 0.28f;
    private const float OptionsCloseDuration = 0.22f;

    private GameObject optionPanel;
    private RectTransform optionPanelRect;
    private TMP_Dropdown resolutionDropdown;
    private TMP_Dropdown qualityDropdown;
    private TMP_Dropdown shadowDropdown;
    private TMP_Dropdown antiAliasingDropdown;
    private TMP_Dropdown textureDropdown;
    private Toggle vSyncToggle;
    private Toggle fullscreenToggle;
    private Slider cameraSensitivitySlider;
    private Slider aimSensitivitySlider;
    private Toggle bloomToggle;
    private Toggle motionBlurToggle;
    private Toggle depthOfFieldToggle;
    private Toggle chromaticToggle;
    private Toggle filmGrainToggle;
    private Button applyButton;
    private GameObject confirmationPanel;
    private GameObject mainMenuBlurOverlay;
    private Material mainMenuBlurMaterial;
    private Image mainMenuBlurImage;
    private Image applyImage;
    private GraphicsSettingsManager sharedGraphicsSettings;
    private PostProcessingSettings sharedPostProcessingSettings;
    private Color applyDefaultColor;
    private readonly List<Resolution> resolutions = new();
    private readonly Dictionary<RectTransform, Vector3> optionChildOpenScales = new();
    private Vector2 optionPanelOpenPosition;
    private Coroutine optionAnimationRoutine;
    private bool suppressChangeDetection;
    private bool hasPendingChanges;
    private bool isLoadingMainMenu;
    private bool optionAnimationStateCached;
    private bool optionsOpen;
    private PlayerHealth playerHealth;
    private PlayerMovement playerMovement;
    private PlayerShoot playerShoot;
    private PlayerWeaponEquip weaponEquip;
    private PlayerWeaponAnimator weaponAnimator;
    private CameraControler cameraControler;
    private PlayerScopeController scopeController;
    private CursorController cursorController;
    private bool controlsFrozen;
    private bool previousMovementInput;
    private bool previousShootEnabled;
    private bool previousWeaponEquipInput;
    private bool previousWeaponAimInput;
    private bool previousCameraEnabled;
    private bool previousScopeEnabled;
    private bool previousCursorEnabled;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockState;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeInputBlock()
    {
        inputBlocked = false;
    }

    public static void SetInputBlocked(bool blocked)
    {
        inputBlocked = blocked;
        if (blocked && instance != null)
        {
            instance.CloseForExternalInputBlock();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateRuntimeController()
    {
        var controllerObject = new GameObject(nameof(InGameOptionsMenu));
        DontDestroyOnLoad(controllerObject);
        controllerObject.AddComponent<InGameOptionsMenu>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            enabled = false;
            return;
        }

        instance = this;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        BindCurrentScene();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnsubscribePlayerHealth();
        SetControlsFrozen(false);
    }

    private void Update()
    {
        if (inputBlocked)
        {
            CloseForExternalInputBlock();
            return;
        }

        if (playerHealth != null && playerHealth.IsDead)
        {
            CloseForPlayerDeath();
            return;
        }

        if (optionPanel == null
            || isLoadingMainMenu
            || optionAnimationRoutine != null
            || Keyboard.current == null
            || !Keyboard.current.escapeKey.wasPressedThisFrame
            || InventoryGridUI.IsAnyInventoryOpen
            || InventoryGridUI.LastClosedByCancelFrame == Time.frameCount)
        {
            return;
        }

        if (confirmationPanel != null && confirmationPanel.activeSelf)
        {
            HideConfirmation();
        }
        else if (optionsOpen)
        {
            ResumeGame();
        }
        else
        {
            OpenOptions();
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindCurrentScene();
    }

    private void BindCurrentScene()
    {
        UnsubscribePlayerHealth();
        SetControlsFrozen(false);
        optionPanel = null;
        optionPanelRect = null;
        confirmationPanel = null;
        optionAnimationStateCached = false;
        optionsOpen = false;
        optionChildOpenScales.Clear();
        if (optionAnimationRoutine != null)
        {
            StopCoroutine(optionAnimationRoutine);
            optionAnimationRoutine = null;
        }

        if (SceneManager.GetActiveScene().name != MainSceneName)
        {
            return;
        }

        optionPanel = FindSceneObject("Option");
        if (optionPanel == null)
        {
            Debug.LogWarning("MainScene Options UI named 'Option' was not found.");
            return;
        }

        BindControls();
        ResolvePlayerControlReferences();
        SubscribePlayerHealth();
        ConfigureControls();
        AttachSharedSettingsControllers();
        RegisterListeners();
        CreateConfirmationDialog();
        CacheOptionAnimationState();
        SetOptionPanelImmediate(false);
    }

    private void BindControls()
    {
        resolutionDropdown = FindComponent<TMP_Dropdown>("Resolusi");
        qualityDropdown = FindComponent<TMP_Dropdown>("Grafik Quality");
        shadowDropdown = FindComponent<TMP_Dropdown>("Shadow");
        antiAliasingDropdown = FindComponent<TMP_Dropdown>("Anti-Aliasing");
        textureDropdown = FindComponent<TMP_Dropdown>("TextureQuality");
        vSyncToggle = FindComponent<Toggle>("V-Sync");
        fullscreenToggle = FindComponent<Toggle>("FullScreenMode");
        cameraSensitivitySlider = FindComponentUnder<Slider>("Sensitivity");
        aimSensitivitySlider = FindComponentUnder<Slider>("Sensitivity aim");
        bloomToggle = FindComponent<Toggle>("Bloom");
        motionBlurToggle = FindComponent<Toggle>("MotionBlur");
        depthOfFieldToggle = FindComponent<Toggle>("DepthOfField");
        chromaticToggle = FindComponent<Toggle>("Chromatic");
        filmGrainToggle = FindComponent<Toggle>("FilmGrain");
        applyButton = FindComponent<Button>("Apply");

        if (applyButton != null)
        {
            applyImage = applyButton.targetGraphic as Image;
            if (applyImage != null)
            {
                applyDefaultColor = applyImage.color;
            }
        }
    }

    private void ConfigureControls()
    {
        suppressChangeDetection = true;
        PopulateResolutions();
        PopulateDropdown(qualityDropdown, new List<string>(QualitySettings.names));
        PopulateDropdown(shadowDropdown, new List<string> { "Off", "Hard Shadows", "All Shadows" });
        PopulateDropdown(antiAliasingDropdown, new List<string> { "Off", "MSAA 2x", "MSAA 4x", "MSAA 8x" });
        PopulateDropdown(textureDropdown, new List<string> { "Full Resolution", "Half Resolution", "Quarter Resolution", "Eighth Resolution" });
        ConfigureSensitivitySlider(cameraSensitivitySlider);
        ConfigureSensitivitySlider(aimSensitivitySlider);
        LoadAppliedValues();
        suppressChangeDetection = false;
        SetPendingChanges(false);
    }

    private void AttachSharedSettingsControllers()
    {
        sharedGraphicsSettings = optionPanel.GetComponent<GraphicsSettingsManager>();
        if (sharedGraphicsSettings == null)
        {
            sharedGraphicsSettings = optionPanel.AddComponent<GraphicsSettingsManager>();
        }

        sharedGraphicsSettings.ConfigureForOptions(resolutionDropdown, qualityDropdown, shadowDropdown, antiAliasingDropdown, textureDropdown, vSyncToggle, fullscreenToggle, null);

        sharedPostProcessingSettings = optionPanel.GetComponent<PostProcessingSettings>();
        if (sharedPostProcessingSettings == null)
        {
            sharedPostProcessingSettings = optionPanel.AddComponent<PostProcessingSettings>();
        }

        Component volume = null;
        foreach (Component candidate in FindVolumeComponents())
        {
            volume = candidate;
            break;
        }

        sharedPostProcessingSettings.ConfigureForOptions(volume, bloomToggle, motionBlurToggle, depthOfFieldToggle, chromaticToggle, filmGrainToggle);
    }

    private void RegisterListeners()
    {
        RegisterChangeListener(resolutionDropdown);
        RegisterChangeListener(qualityDropdown);
        RegisterChangeListener(shadowDropdown);
        RegisterChangeListener(antiAliasingDropdown);
        RegisterChangeListener(textureDropdown);
        RegisterChangeListener(vSyncToggle);
        RegisterChangeListener(fullscreenToggle);
        RegisterInstantSensitivityListener(cameraSensitivitySlider);
        RegisterInstantSensitivityListener(aimSensitivitySlider);
        RegisterChangeListener(bloomToggle);
        RegisterChangeListener(motionBlurToggle);
        RegisterChangeListener(depthOfFieldToggle);
        RegisterChangeListener(chromaticToggle);
        RegisterChangeListener(filmGrainToggle);

        Button resumeButton = FindComponent<Button>("Resume");
        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveAllListeners();
            resumeButton.onClick.AddListener(ResumeGame);
        }

        if (applyButton != null)
        {
            applyButton.onClick.RemoveAllListeners();
            applyButton.onClick.AddListener(ApplySettings);
        }

        Button defaultButton = FindButtonByLabel("Default");
        if (defaultButton != null)
        {
            defaultButton.onClick.RemoveAllListeners();
            defaultButton.onClick.AddListener(ResetSensitivityToDefault);
        }

        Button mainMenuButton = FindComponent<Button>("Main Menu");
        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveAllListeners();
            mainMenuButton.onClick.AddListener(RequestReturnToMainMenu);
        }
    }

    private void OpenOptions()
    {
        if (playerHealth != null && playerHealth.IsDead)
        {
            return;
        }

        SetControlsFrozen(true);

        suppressChangeDetection = true;
        LoadAppliedValues();
        suppressChangeDetection = false;
        SetPendingChanges(false);
        optionsOpen = true;
        PlayOptionOpenAnimation();
    }

    public void ResumeGame()
    {
        if (playerHealth != null && playerHealth.IsDead)
        {
            CloseForPlayerDeath();
            return;
        }

        if (optionPanel == null)
        {
            return;
        }

        HideConfirmation();
        optionsOpen = false;
        PlayOptionCloseAnimation(true);
    }

    private void PlayOptionOpenAnimation()
    {
        if (optionPanel == null)
        {
            return;
        }

        CacheOptionAnimationState();
        if (optionAnimationRoutine != null)
        {
            StopCoroutine(optionAnimationRoutine);
            optionAnimationRoutine = null;
        }

        optionPanel.SetActive(true);
        SetOptionPanelClosedPose();
        optionAnimationRoutine = StartCoroutine(AnimateOptionPanel(true, false));
    }

    private void PlayOptionCloseAnimation(bool unfreezeControlsWhenDone)
    {
        if (optionPanel == null)
        {
            if (unfreezeControlsWhenDone)
            {
                SetControlsFrozen(false);
            }

            return;
        }

        CacheOptionAnimationState();
        if (optionAnimationRoutine != null)
        {
            StopCoroutine(optionAnimationRoutine);
            optionAnimationRoutine = null;
        }

        if (!optionPanel.activeSelf)
        {
            SetOptionPanelImmediate(false);
            if (unfreezeControlsWhenDone)
            {
                SetControlsFrozen(false);
            }

            return;
        }

        optionAnimationRoutine = StartCoroutine(AnimateOptionPanel(false, unfreezeControlsWhenDone));
    }

    private IEnumerator AnimateOptionPanel(bool opening, bool unfreezeControlsWhenDone)
    {
        float duration = Mathf.Max(0.01f, opening ? OptionsOpenDuration : OptionsCloseDuration);
        float elapsed = 0f;
        Vector2 fromPosition = optionPanelRect != null ? optionPanelRect.anchoredPosition : Vector2.zero;
        Vector2 toPosition = opening
            ? optionPanelOpenPosition
            : optionPanelOpenPosition + new Vector2(0f, OptionsHiddenOffsetY);

        Dictionary<RectTransform, Vector3> fromScales = new();
        Dictionary<RectTransform, Vector3> toScales = new();
        foreach (KeyValuePair<RectTransform, Vector3> pair in optionChildOpenScales)
        {
            if (pair.Key == null)
            {
                continue;
            }

            fromScales[pair.Key] = pair.Key.localScale;
            toScales[pair.Key] = opening ? pair.Value : pair.Value * OptionsClosedChildScale;
        }

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = opening ? EaseOutCubic(t) : EaseInCubic(t);

            if (optionPanelRect != null)
            {
                optionPanelRect.anchoredPosition = Vector2.LerpUnclamped(fromPosition, toPosition, eased);
            }

            foreach (KeyValuePair<RectTransform, Vector3> pair in toScales)
            {
                if (pair.Key != null && fromScales.TryGetValue(pair.Key, out Vector3 startScale))
                {
                    pair.Key.localScale = Vector3.LerpUnclamped(startScale, pair.Value, eased);
                }
            }

            yield return null;
        }

        if (opening)
        {
            SetOptionPanelOpenPose();
        }
        else
        {
            SetOptionPanelClosedPose();
            optionPanel.SetActive(false);
        }

        optionAnimationRoutine = null;
        if (unfreezeControlsWhenDone)
        {
            SetControlsFrozen(false);
        }
    }

    private void CacheOptionAnimationState()
    {
        if (optionPanel == null)
        {
            return;
        }

        optionPanelRect = optionPanel.GetComponent<RectTransform>();
        if (optionPanelRect != null && !optionAnimationStateCached)
        {
            optionPanelOpenPosition = Vector2.zero;
        }

        if (!optionAnimationStateCached)
        {
            optionChildOpenScales.Clear();
            for (int i = 0; i < optionPanel.transform.childCount; i++)
            {
                RectTransform child = optionPanel.transform.GetChild(i) as RectTransform;
                if (child != null)
                {
                    optionChildOpenScales[child] = Vector3.one;
                }
            }

            optionAnimationStateCached = true;
        }
    }

    private void SetOptionPanelImmediate(bool open)
    {
        if (optionAnimationRoutine != null)
        {
            StopCoroutine(optionAnimationRoutine);
            optionAnimationRoutine = null;
        }

        CacheOptionAnimationState();
        optionsOpen = open;
        if (open)
        {
            optionPanel?.SetActive(true);
            SetOptionPanelOpenPose();
        }
        else
        {
            SetOptionPanelClosedPose();
            optionPanel?.SetActive(false);
        }
    }

    private void SetOptionPanelOpenPose()
    {
        if (optionPanelRect != null)
        {
            optionPanelRect.anchoredPosition = optionPanelOpenPosition;
        }

        foreach (KeyValuePair<RectTransform, Vector3> pair in optionChildOpenScales)
        {
            if (pair.Key != null)
            {
                pair.Key.localScale = pair.Value;
            }
        }
    }

    private void SetOptionPanelClosedPose()
    {
        if (optionPanelRect != null)
        {
            optionPanelRect.anchoredPosition = optionPanelOpenPosition + new Vector2(0f, OptionsHiddenOffsetY);
        }

        foreach (KeyValuePair<RectTransform, Vector3> pair in optionChildOpenScales)
        {
            if (pair.Key != null)
            {
                pair.Key.localScale = pair.Value * OptionsClosedChildScale;
            }
        }
    }

    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private static float EaseInCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t;
    }

    public void ApplySettings()
    {
        ApplyEngineSettings();
        ApplySensitivitySettings();
        ApplyPostProcessingSettings();
        PlayerPrefs.Save();
        SetPendingChanges(false);
    }

    private void ResetSensitivityToDefault()
    {
        SetSensitivityValue(cameraSensitivitySlider, DefaultCameraSensitivity);
        SetSensitivityValue(aimSensitivitySlider, DefaultAimSensitivity);
        ApplySensitivitySettings();
    }

    public void RequestReturnToMainMenu()
    {
        if (confirmationPanel != null)
        {
            confirmationPanel.SetActive(true);
        }
        else
        {
            ReturnToMainMenu();
        }
    }

    public void ReturnToMainMenu()
    {
        if (!isLoadingMainMenu)
        {
            StartCoroutine(ReturnToMainMenuRoutine());
        }
    }

    private void HideConfirmation()
    {
        if (confirmationPanel != null)
        {
            confirmationPanel.SetActive(false);
        }
    }

    private void CreateConfirmationDialog()
    {
        if (optionPanel == null || confirmationPanel != null)
        {
            return;
        }

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        confirmationPanel = CreateUiObject("Main Menu Confirmation", optionPanel.transform);
        RectTransform overlayRect = confirmationPanel.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        Image overlayImage = confirmationPanel.AddComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.82f);

        GameObject content = CreateUiObject("Confirmation Content", confirmationPanel.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 0.5f);
        contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.sizeDelta = new Vector2(520f, 220f);
        contentRect.anchoredPosition = Vector2.zero;
        Image contentImage = content.AddComponent<Image>();
        contentImage.color = new Color(0.08f, 0.1f, 0.13f, 0.98f);

        CreateText("Back to Main Menu?", content.transform, font, new Vector2(0f, 44f), 28, Color.white);
        CreateText("Current gameplay will be left.", content.transform, font, new Vector2(0f, 8f), 18, new Color(0.78f, 0.82f, 0.88f));
        CreateButton("Back to Main Menu", content.transform, font, new Vector2(-120f, -62f), new Color(0.72f, 0.2f, 0.2f, 1f), ReturnToMainMenu);
        CreateButton("Stay", content.transform, font, new Vector2(120f, -62f), new Color(0.22f, 0.48f, 0.78f, 1f), HideConfirmation);
        confirmationPanel.SetActive(false);
    }

    private static GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject uiObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer));
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }

    private static void CreateText(string value, Transform parent, Font font, Vector2 position, int fontSize, Color color)
    {
        GameObject textObject = CreateUiObject("Text", parent);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(460f, 42f);
        rect.anchoredPosition = position;
        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
    }

    private static void CreateButton(string label, Transform parent, Font font, Vector2 position, Color color, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = CreateUiObject(label, parent);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(210f, 48f);
        rect.anchoredPosition = position;
        Image image = buttonObject.AddComponent<Image>();
        image.color = color;
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        CreateText(label, buttonObject.transform, font, Vector2.zero, 18, Color.white);
    }

    private IEnumerator ReturnToMainMenuRoutine()
    {
        isLoadingMainMenu = true;
        ApplySettings();

        if (optionPanel != null)
        {
            SetOptionPanelImmediate(false);
        }

        CreateMainMenuBlurOverlay();
        float elapsed = 0f;
        while (elapsed < MainMenuBlurDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (mainMenuBlurMaterial != null)
            {
                mainMenuBlurMaterial.SetFloat("_BlurSize", Mathf.Lerp(0.5f, 3f, Mathf.Clamp01(elapsed / MainMenuBlurRampDuration)));
                mainMenuBlurMaterial.SetFloat("_Darkness", Mathf.Clamp01(elapsed / MainMenuDarkenDuration));
            }
            else if (mainMenuBlurImage != null)
            {
                Color color = mainMenuBlurImage.color;
                color.a = 0.7f * Mathf.Clamp01(elapsed / MainMenuDarkenDuration);
                mainMenuBlurImage.color = color;
            }

            yield return null;
        }
        DestroyMainMenuBlurOverlay();
        SetControlsFrozen(false);
        Time.timeScale = 1f;
        SceneManager.LoadScene(MainMenuSceneName);
    }

    private void CreateMainMenuBlurOverlay()
    {
        Canvas canvas = optionPanel != null ? optionPanel.GetComponentInParent<Canvas>() : null;
        if (canvas == null) return;
        mainMenuBlurOverlay = new GameObject("Main Menu Blur Transition", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        mainMenuBlurOverlay.transform.SetParent(canvas.transform, false);
        mainMenuBlurOverlay.transform.SetAsLastSibling();
        RectTransform rect = mainMenuBlurOverlay.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = mainMenuBlurOverlay.GetComponent<Image>();
        mainMenuBlurImage = image;
        image.raycastTarget = true;
        Shader blurShader = Shader.Find("Hidden/MainMenuTransitionBlur");
        if (blurShader != null)
        {
            mainMenuBlurMaterial = new Material(blurShader);
            image.material = mainMenuBlurMaterial;
            image.color = Color.white;
        }
        else
        {
            image.color = new Color(0f, 0f, 0f, 0.7f);
        }
    }

    private void DestroyMainMenuBlurOverlay()
    {
        if (mainMenuBlurOverlay != null)
        {
            Destroy(mainMenuBlurOverlay);
            mainMenuBlurOverlay = null;
            mainMenuBlurImage = null;
        }
        if (mainMenuBlurMaterial != null)
        {
            Destroy(mainMenuBlurMaterial);
            mainMenuBlurMaterial = null;
        }
    }

    private void ResolvePlayerControlReferences()
    {
        playerHealth = FindAnyObjectByType<PlayerHealth>();
        playerMovement = FindAnyObjectByType<PlayerMovement>();
        playerShoot = FindAnyObjectByType<PlayerShoot>();
        weaponEquip = FindAnyObjectByType<PlayerWeaponEquip>();
        weaponAnimator = FindAnyObjectByType<PlayerWeaponAnimator>();
        cameraControler = FindAnyObjectByType<CameraControler>();
        scopeController = FindAnyObjectByType<PlayerScopeController>();
        cursorController = FindAnyObjectByType<CursorController>();
    }

    private void SubscribePlayerHealth()
    {
        if (playerHealth != null)
        {
            playerHealth.Damaged += HandlePlayerDamaged;
            playerHealth.Died += HandlePlayerDied;
        }
    }

    private void UnsubscribePlayerHealth()
    {
        if (playerHealth != null)
        {
            playerHealth.Damaged -= HandlePlayerDamaged;
            playerHealth.Died -= HandlePlayerDied;
        }
    }

    private void HandlePlayerDamaged()
    {
        if (optionPanel != null && optionsOpen)
        {
            ResumeGame();
        }
    }

    private void HandlePlayerDied()
    {
        CloseForPlayerDeath();
    }

    private void CloseForPlayerDeath()
    {
        if (optionPanel != null)
        {
            SetOptionPanelImmediate(false);
        }

        if (confirmationPanel != null)
        {
            confirmationPanel.SetActive(false);
        }

        // PlayerHealth has disabled gameplay input. Do not restore the
        // previous options-menu state while the death sequence is active.
        controlsFrozen = false;
    }

    private void CloseForExternalInputBlock()
    {
        if (optionAnimationRoutine != null)
        {
            StopCoroutine(optionAnimationRoutine);
            optionAnimationRoutine = null;
        }

        HideConfirmation();
        optionsOpen = false;
        if (optionPanel != null)
        {
            SetOptionPanelImmediate(false);
        }

        SetControlsFrozen(false);
    }

    private void SetControlsFrozen(bool frozen)
    {
        if (frozen == controlsFrozen)
        {
            return;
        }

        if (frozen)
        {
            previousMovementInput = playerMovement == null || playerMovement.allowInput;
            previousShootEnabled = playerShoot != null && playerShoot.enabled;
            previousWeaponEquipInput = weaponEquip == null || weaponEquip.allowInput;
            previousWeaponAimInput = weaponAnimator == null || weaponAnimator.allowAimInput;
            previousCameraEnabled = cameraControler != null && cameraControler.enabled;
            previousScopeEnabled = scopeController != null && scopeController.enabled;
            previousCursorEnabled = cursorController != null && cursorController.enabled;
            previousCursorVisible = Cursor.visible;
            previousCursorLockState = Cursor.lockState;

            if (playerMovement != null)
            {
                playerMovement.StopLocomotionForInputFreeze();
                playerMovement.allowInput = false;
            }

            if (playerShoot != null) playerShoot.enabled = false;
            if (weaponEquip != null) weaponEquip.allowInput = false;
            if (weaponAnimator != null) weaponAnimator.SetAimInputEnabled(false, false, true);
            if (cameraControler != null) cameraControler.enabled = false;
            if (scopeController != null) scopeController.enabled = false;
            if (cursorController != null) cursorController.enabled = false;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            if (playerMovement != null) playerMovement.allowInput = previousMovementInput;
            if (playerShoot != null) playerShoot.enabled = previousShootEnabled;
            if (weaponEquip != null) weaponEquip.allowInput = previousWeaponEquipInput;
            if (weaponAnimator != null) weaponAnimator.SetAimInputEnabled(previousWeaponAimInput, false);
            if (cameraControler != null) cameraControler.enabled = previousCameraEnabled;
            if (scopeController != null) scopeController.enabled = previousScopeEnabled;
            if (cursorController != null) cursorController.enabled = previousCursorEnabled;
            Cursor.visible = previousCursorVisible;
            Cursor.lockState = previousCursorLockState;
        }

        controlsFrozen = frozen;
    }

    private void ApplyEngineSettings()
    {
        if (sharedGraphicsSettings != null)
        {
            sharedGraphicsSettings.ApplySettings();
            return;
        }
        if (resolutionDropdown != null && resolutions.Count > 0)
        {
            int index = Mathf.Clamp(resolutionDropdown.value, 0, resolutions.Count - 1);
            Resolution resolution = resolutions[index];
            FullScreenMode mode = fullscreenToggle != null && fullscreenToggle.isOn ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            Screen.SetResolution(resolution.width, resolution.height, mode, resolution.refreshRateRatio);
            PlayerPrefs.SetInt(ResolutionKey, index);
        }

        if (qualityDropdown != null)
        {
            QualitySettings.SetQualityLevel(Mathf.Clamp(qualityDropdown.value, 0, Mathf.Max(0, QualitySettings.names.Length - 1)), true);
            PlayerPrefs.SetInt(QualityKey, qualityDropdown.value);
        }

        if (shadowDropdown != null)
        {
            QualitySettings.shadows = (ShadowQuality)Mathf.Clamp(shadowDropdown.value, 0, 2);
            PlayerPrefs.SetInt(ShadowKey, shadowDropdown.value);
        }

        if (antiAliasingDropdown != null)
        {
            int[] options = { 0, 2, 4, 8 };
            int index = Mathf.Clamp(antiAliasingDropdown.value, 0, options.Length - 1);
            QualitySettings.antiAliasing = options[index];
            PlayerPrefs.SetInt(AntiAliasingKey, index);
        }

        if (textureDropdown != null)
        {
            QualitySettings.globalTextureMipmapLimit = Mathf.Clamp(textureDropdown.value, 0, 3);
            PlayerPrefs.SetInt(TextureKey, textureDropdown.value);
        }

        if (vSyncToggle != null)
        {
            QualitySettings.vSyncCount = vSyncToggle.isOn ? 1 : 0;
            PlayerPrefs.SetInt(VSyncKey, vSyncToggle.isOn ? 1 : 0);
        }

        if (fullscreenToggle != null)
        {
            PlayerPrefs.SetInt(FullscreenKey, fullscreenToggle.isOn ? 1 : 0);
        }

    }

    private void ApplySensitivitySettings()
    {
        float cameraSensitivity = GetSensitivityValue(cameraSensitivitySlider);
        float aimSensitivity = GetSensitivityValue(aimSensitivitySlider);

        if (cameraControler != null)
        {
            cameraControler.SetPlayerSensitivityMultipliers(cameraSensitivity, aimSensitivity);
        }

        PlayerPrefs.SetFloat(CameraSensitivityKey, cameraSensitivity);
        PlayerPrefs.SetFloat(AimSensitivityKey, aimSensitivity);
        PlayerPrefs.Save();
    }

    private static void ConfigureSensitivitySlider(Slider slider)
    {
        if (slider == null)
        {
            return;
        }

        slider.minValue = 0.1f;
        slider.maxValue = 5f;
        slider.wholeNumbers = false;
    }

    private static float GetSensitivityValue(Slider slider)
    {
        return slider == null ? 1f : Mathf.Clamp(slider.value, 0.1f, 5f);
    }

    private static void SetSensitivityValue(Slider slider, float value)
    {
        if (slider != null)
        {
            slider.SetValueWithoutNotify(Mathf.Clamp(value, 0.1f, 5f));
        }
    }

    private void ApplyPostProcessingSettings()
    {
        if (sharedPostProcessingSettings != null)
        {
            sharedPostProcessingSettings.ApplySettings();
            return;
        }
        ApplyEffect("Bloom", bloomToggle, BloomKey);
        ApplyEffect("MotionBlur", motionBlurToggle, MotionBlurKey);
        ApplyEffect("DepthOfField", depthOfFieldToggle, DepthOfFieldKey);
        ApplyEffect("ChromaticAberration", chromaticToggle, ChromaticAberrationKey);
        ApplyEffect("FilmGrain", filmGrainToggle, FilmGrainKey, "Grain");
    }

    private void ApplyEffect(string primaryName, Toggle toggle, string preferenceKey, string alternateName = null)
    {
        if (toggle == null)
        {
            return;
        }

        PlayerPrefs.SetInt(preferenceKey, toggle.isOn ? 1 : 0);
        foreach (Component volume in FindVolumeComponents())
        {
            object profile = ReadMember(volume, "profile") ?? ReadMember(volume, "sharedProfile");
            object componentList = profile == null ? null : ReadMember(profile, "components") ?? ReadMember(profile, "settings");
            if (!(componentList is IEnumerable effects))
            {
                continue;
            }

            foreach (object effect in effects)
            {
                string typeName = effect.GetType().Name;
                if (typeName == primaryName || typeName == alternateName)
                {
                    SetMember(effect, "active", toggle.isOn);
                }
            }
        }
    }

    private void LoadAppliedValues()
    {
        SetDropdownValue(resolutionDropdown, PlayerPrefs.GetInt(ResolutionKey, FindCurrentResolutionIndex()));
        SetDropdownValue(qualityDropdown, PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel()));
        SetDropdownValue(shadowDropdown, PlayerPrefs.GetInt(ShadowKey, (int)QualitySettings.shadows));

        int aaDefault = QualitySettings.antiAliasing == 8 ? 3 : QualitySettings.antiAliasing == 4 ? 2 : QualitySettings.antiAliasing == 2 ? 1 : 0;
        SetDropdownValue(antiAliasingDropdown, PlayerPrefs.GetInt(AntiAliasingKey, aaDefault));
        SetDropdownValue(textureDropdown, PlayerPrefs.GetInt(TextureKey, QualitySettings.globalTextureMipmapLimit));
        SetToggleValue(vSyncToggle, PlayerPrefs.GetInt(VSyncKey, QualitySettings.vSyncCount > 0 ? 1 : 0) == 1);
        SetToggleValue(fullscreenToggle, PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1);

        SetSensitivityValue(cameraSensitivitySlider, PlayerPrefs.GetFloat(CameraSensitivityKey, DefaultCameraSensitivity));
        SetSensitivityValue(aimSensitivitySlider, PlayerPrefs.GetFloat(AimSensitivityKey, DefaultAimSensitivity));
        ApplySensitivitySettings();

        SetToggleValue(bloomToggle, PlayerPrefs.GetInt(BloomKey, 1) == 1);
        SetToggleValue(motionBlurToggle, PlayerPrefs.GetInt(MotionBlurKey, 1) == 1);
        SetToggleValue(depthOfFieldToggle, PlayerPrefs.GetInt(DepthOfFieldKey, 1) == 1);
        SetToggleValue(chromaticToggle, PlayerPrefs.GetInt(ChromaticAberrationKey, 1) == 1);
        SetToggleValue(filmGrainToggle, PlayerPrefs.GetInt(FilmGrainKey, 1) == 1);
    }

    private void PopulateResolutions()
    {
        resolutions.Clear();
        if (resolutionDropdown == null)
        {
            return;
        }

        var options = new List<string>();
        foreach (Resolution candidate in Screen.resolutions)
        {
            bool alreadyAdded = false;
            foreach (Resolution existing in resolutions)
            {
                if (existing.width == candidate.width && existing.height == candidate.height && existing.refreshRateRatio.value == candidate.refreshRateRatio.value)
                {
                    alreadyAdded = true;
                    break;
                }
            }

            if (!alreadyAdded)
            {
                resolutions.Add(candidate);
                options.Add($"{candidate.width} x {candidate.height} ({Mathf.RoundToInt((float)candidate.refreshRateRatio.value)} Hz)");
            }
        }

        PopulateDropdown(resolutionDropdown, options);
    }

    private void PopulateDropdown(TMP_Dropdown dropdown, List<string> options)
    {
        if (dropdown == null)
        {
            return;
        }

        dropdown.ClearOptions();
        dropdown.AddOptions(options);
    }

    private void RegisterChangeListener(TMP_Dropdown dropdown)
    {
        if (dropdown != null)
        {
            dropdown.onValueChanged.RemoveAllListeners();
            dropdown.onValueChanged.AddListener(_ => NotifyChanged());
        }
    }

    private void RegisterChangeListener(Toggle toggle)
    {
        if (toggle != null)
        {
            toggle.onValueChanged.RemoveAllListeners();
            toggle.onValueChanged.AddListener(_ => NotifyChanged());
        }
    }

    private void RegisterChangeListener(Slider slider)
    {
        if (slider != null)
        {
            slider.onValueChanged.RemoveAllListeners();
            slider.onValueChanged.AddListener(_ => NotifyChanged());
        }
    }

    private void RegisterInstantSensitivityListener(Slider slider)
    {
        if (slider != null)
        {
            slider.onValueChanged.RemoveAllListeners();
            slider.onValueChanged.AddListener(_ =>
            {
                if (!suppressChangeDetection)
                {
                    ApplySensitivitySettings();
                }
            });
        }
    }

    private void NotifyChanged()
    {
        if (!suppressChangeDetection)
        {
            SetPendingChanges(true);
        }
    }

    private void SetPendingChanges(bool pending)
    {
        hasPendingChanges = pending;
        if (applyImage != null)
        {
            applyImage.color = pending ? new Color(0.18f, 0.78f, 0.32f, 1f) : applyDefaultColor;
        }
    }

    private int FindCurrentResolutionIndex()
    {
        for (int i = 0; i < resolutions.Count; i++)
        {
            if (resolutions[i].width == Screen.currentResolution.width && resolutions[i].height == Screen.currentResolution.height)
            {
                return i;
            }
        }

        return Mathf.Max(0, resolutions.Count - 1);
    }

    private static void SetDropdownValue(TMP_Dropdown dropdown, int value)
    {
        if (dropdown != null && dropdown.options.Count > 0)
        {
            dropdown.SetValueWithoutNotify(Mathf.Clamp(value, 0, dropdown.options.Count - 1));
        }
    }

    private static void SetToggleValue(Toggle toggle, bool value)
    {
        if (toggle != null)
        {
            toggle.SetIsOnWithoutNotify(value);
        }
    }

    private GameObject FindSceneObject(string objectName)
    {
        foreach (Transform transform in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (transform.name == objectName && transform.gameObject.scene.IsValid() && transform.gameObject.scene == SceneManager.GetActiveScene())
            {
                return transform.gameObject;
            }
        }

        return null;
    }

    private T FindComponent<T>(string objectName) where T : Component
    {
        if (optionPanel == null)
        {
            return null;
        }

        foreach (Transform transform in optionPanel.GetComponentsInChildren<Transform>(true))
        {
            if (transform.name == objectName)
            {
                T component = transform.GetComponent<T>();
                if (component != null)
                {
                    return component;
                }
            }
        }

        return null;
    }

    private T FindComponentUnder<T>(string containerName) where T : Component
    {
        if (optionPanel == null)
        {
            return null;
        }

        foreach (Transform transform in optionPanel.GetComponentsInChildren<Transform>(true))
        {
            if (transform.name == containerName)
            {
                T component = transform.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }
        }

        return null;
    }

    private Button FindButtonByLabel(string label)
    {
        if (optionPanel == null)
        {
            return null;
        }

        foreach (Button button in optionPanel.GetComponentsInChildren<Button>(true))
        {
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null && text.text == label)
            {
                return button;
            }
        }

        return null;
    }

    private static IEnumerable<Component> FindVolumeComponents()
    {
        foreach (Component component in Resources.FindObjectsOfTypeAll<Component>())
        {
            if (component != null && component.GetType().Name == "Volume" && component.gameObject.scene.IsValid())
            {
                yield return component;
            }
        }
    }

    private static object ReadMember(object target, string memberName)
    {
        Type type = target.GetType();
        PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
        if (property != null && property.CanRead)
        {
            return property.GetValue(target);
        }

        FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public);
        return field == null ? null : field.GetValue(target);
    }

    private static void SetMember(object target, string memberName, bool value)
    {
        Type type = target.GetType();
        PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
        if (property != null && property.CanWrite)
        {
            property.SetValue(target, value);
            return;
        }

        FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public);
        if (field != null)
        {
            field.SetValue(target, value);
        }
    }
}
