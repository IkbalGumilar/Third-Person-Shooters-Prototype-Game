using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Runtime controller for the Options canvas in MainScene.</summary>
public sealed class InGameOptionsMenu : MonoBehaviour
{
    private enum OptionTab
    {
        Graphics,
        Control,
        Audio
    }

    [Serializable]
    private sealed class OptionInformationEntry
    {
        public string targetName;
        public string title;
        [TextArea(2, 5)] public string information;
        public Sprite logo;
    }

    private static InGameOptionsMenu instance;
    private static bool inputBlocked;

    private const string MainSceneName = "MainScene";
    private const string MainMenuSceneName = "MainMenu";
    private const string ResolutionKey = "Graphics.Resolution";
    private const string QualityKey = "Graphics.Quality";
    private const string QualityPresetKey = "Graphics.QualityPreset";
    private const string ShadowKey = "Graphics.Shadows";
    private const string AntiAliasingKey = "Graphics.AntiAliasing";
    private const string TextureKey = "Graphics.TextureQuality";
    private const string VSyncKey = "Graphics.VSync";
    private const string FullscreenKey = "Graphics.Fullscreen";
    private const string CameraSensitivityKey = "Controls.CameraSensitivity";
    private const string AimSensitivityKey = "Controls.AimSensitivity";
    private const string InvertMouseKey = "Controls.InvertMouse";
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
    private const int QualityPresetLow = 0;
    private const int QualityPresetMedium = 1;
    private const int QualityPresetHigh = 2;
    private const int QualityPresetUltra = 3;
    private const int QualityPresetCustom = 4;

    private GameObject optionPanel;
    private RectTransform optionPanelRect;
    private TMP_Dropdown resolutionDropdown;
    private TMP_Dropdown qualityDropdown;
    private TMP_Dropdown shadowDropdown;
    private TMP_Dropdown antiAliasingDropdown;
    private TMP_Dropdown textureDropdown;
    private Toggle vSyncToggle;
    private Toggle fullscreenToggle;
    private Slider vSyncSlider;
    private Slider fullscreenSlider;
    private Slider cameraSensitivitySlider;
    private Slider aimSensitivitySlider;
    private Toggle invertMouseToggle;
    private Slider invertMouseSlider;
    private TMP_Text cameraSensitivityValueText;
    private TMP_Text aimSensitivityValueText;
    private Toggle bloomToggle;
    private Toggle motionBlurToggle;
    private Toggle depthOfFieldToggle;
    private Toggle chromaticToggle;
    private Toggle filmGrainToggle;
    private Slider bloomSlider;
    private Slider motionBlurSlider;
    private Slider depthOfFieldSlider;
    private Slider chromaticSlider;
    private Slider filmGrainSlider;
    private Slider masterVolumeSlider;
    private Slider musicVolumeSlider;
    private Slider sfxVolumeSlider;
    private Slider uiVolumeSlider;
    private TMP_Dropdown audioMixerDropdown;
    private TMP_Dropdown audioLanguageDropdown;
    private TMP_Dropdown audioSpeakerModeDropdown;
    private Toggle audioMuteToggle;
    private Slider audioMuteSlider;
    private TMP_Text masterVolumeValueText;
    private TMP_Text musicVolumeValueText;
    private TMP_Text sfxVolumeValueText;
    private TMP_Text uiVolumeValueText;
    private Button applyButton;
    private TMP_Text selectedOptionTitleText;
    private TMP_Text selectedOptionInfoText;
    [SerializeField] private Image selectedOptionLogoImage;
    [SerializeField] private OptionInformationEntry[] optionInformationEntries;
    private GameObject confirmationPanel;
    private GameObject mainMenuBlurOverlay;
    private Material mainMenuBlurMaterial;
    private Image mainMenuBlurImage;
    private Image applyImage;
    private GraphicsSettingsManager sharedGraphicsSettings;
    private PostProcessingSettings sharedPostProcessingSettings;
    private AudioSettingsManager sharedAudioSettings;
    private ResourceUsageDisplay resourceUsageDisplay;
    private Color applyDefaultColor;
    [Header("Option Tabs")]
    [SerializeField] private GameObject graphicsMenu;
    [SerializeField] private GameObject controlMenu;
    [SerializeField] private GameObject audioMenu;
    [SerializeField] private GameObject graphicsTabHighlight;
    [SerializeField] private GameObject controlTabHighlight;
    [SerializeField] private GameObject audioTabHighlight;
    [SerializeField] private Button graphicsTabButton;
    [SerializeField] private Button controlTabButton;
    [SerializeField] private Button audioTabButton;
    [SerializeField] private Button resetSensitivityButton;
    [SerializeField] private Button closeButton;
    [Header("Option Navigation")]
    [SerializeField] private Selectable firstSelectedOption;
    [SerializeField] private bool autoConfigureOptionNavigation = true;
    private OptionTab selectedTab = OptionTab.Graphics;
    private readonly List<Resolution> resolutions = new();
    private readonly List<Selectable> optionNavigationSelectables = new();
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
        if (instance != null || FindActiveSceneController() != null)
        {
            return;
        }

        var controllerObject = new GameObject(nameof(InGameOptionsMenu));
        DontDestroyOnLoad(controllerObject);
        controllerObject.AddComponent<InGameOptionsMenu>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            if (IsActiveSceneController(this) && !IsActiveSceneController(instance))
            {
                Destroy(instance.gameObject);
                instance = null;
            }
            else
            {
                enabled = false;
                return;
            }
        }

        if (instance != null && instance != this)
        {
            enabled = false;
            return;
        }

        instance = this;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        BindCurrentScene();
    }

    private static InGameOptionsMenu FindActiveSceneController()
    {
        foreach (InGameOptionsMenu controller in Resources.FindObjectsOfTypeAll<InGameOptionsMenu>())
        {
            if (IsActiveSceneController(controller))
            {
                return controller;
            }
        }

        return null;
    }

    private static bool IsActiveSceneController(InGameOptionsMenu controller)
    {
        return controller != null
            && controller.gameObject.scene.IsValid()
            && controller.gameObject.scene == SceneManager.GetActiveScene();
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

        if (optionsOpen && optionPanel != null && optionPanel.activeInHierarchy && (confirmationPanel == null || !confirmationPanel.activeSelf))
        {
            RestoreOptionSelectionIfNeeded();
        }

        bool togglePressed = WasOptionsTogglePressed();
        bool cancelPressed = optionsOpen && WasOptionsCancelPressed();
        if (optionPanel == null
            || isLoadingMainMenu
            || optionAnimationRoutine != null
            || (!togglePressed && !cancelPressed)
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
        else if (togglePressed)
        {
            OpenOptions();
        }
    }

    private static bool WasOptionsTogglePressed()
    {
        return (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
    }

    private static bool WasOptionsCancelPressed()
    {
        return Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
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

        CopySceneInspectorConfiguration();
        BindControls();
        ResolvePlayerControlReferences();
        SubscribePlayerHealth();
        ConfigureControls();
        AttachSharedSettingsControllers();
        RegisterListeners();
        RegisterOptionInformationTargets();
        CreateConfirmationDialog();
        CacheOptionAnimationState();
        SetOptionPanelImmediate(false);
    }

    private void BindControls()
    {
        resolutionDropdown = FindComponentOrChild<TMP_Dropdown>("Resolusi", "Resolution", "Resolution text");
        qualityDropdown = FindComponentOrChild<TMP_Dropdown>("Grafik Quality", "Quality", "Quality Preset", "Quality Preset text");
        shadowDropdown = FindComponentOrChild<TMP_Dropdown>("Shadow", "Shadow Quality", "Shadow Quality text");
        antiAliasingDropdown = FindComponentOrChild<TMP_Dropdown>("Anti-Aliasing", "Anti-Aliasing text");
        textureDropdown = FindComponentOrChild<TMP_Dropdown>("TextureQuality", "Texture Quality", "Texture Quality text");
        vSyncToggle = FindComponentOrChild<Toggle>("V-Sync", "VSync");
        fullscreenToggle = FindComponentOrChild<Toggle>("FullScreenMode", "Full screen", "Fullscreen");
        vSyncSlider = FindComponentOrChild<Slider>("V-Sync", "VSync");
        fullscreenSlider = FindComponentOrChild<Slider>("FullScreenMode", "Full screen", "Fullscreen");
        cameraSensitivitySlider = FindComponentOrChild<Slider>("Sensitivity", "Sensitivity text", "Camera Sensitivity", "Mouse Sensitivity");
        aimSensitivitySlider = FindComponentOrChild<Slider>("Sensitivity aim", "Sensitivity Aim text", "Aim Sensitivity");
        invertMouseToggle = FindComponentOrChild<Toggle>("Invert Mose", "Invert Mouse");
        invertMouseSlider = FindComponentOrChild<Slider>("Invert Mose", "Invert Mouse");
        cameraSensitivityValueText = FindValueText("Sensitivity text");
        aimSensitivityValueText = FindValueText("Sensitivity Aim text");
        bloomToggle = FindComponentOrChild<Toggle>("Bloom");
        motionBlurToggle = FindComponentOrChild<Toggle>("MotionBlur", "Motion Blur");
        depthOfFieldToggle = FindComponentOrChild<Toggle>("DepthOfField", "Depth Of Field", "DOF");
        chromaticToggle = FindComponentOrChild<Toggle>("Chromatic", "Chromatic Aberration");
        filmGrainToggle = FindComponentOrChild<Toggle>("FilmGrain", "Film Grain", "Film Gain");
        bloomSlider = FindComponentOrChild<Slider>("Bloom");
        motionBlurSlider = FindComponentOrChild<Slider>("MotionBlur", "Motion Blur");
        depthOfFieldSlider = FindComponentOrChild<Slider>("DepthOfField", "Depth Of Field", "DOF");
        chromaticSlider = FindComponentOrChild<Slider>("Chromatic", "Chromatic Aberration");
        filmGrainSlider = FindComponentOrChild<Slider>("FilmGrain", "Film Grain", "Film Gain");
        masterVolumeSlider = FindAudioComponentOrChild<Slider>("Master Volume text", "Master Volume", "Master");
        musicVolumeSlider = FindAudioComponentOrChild<Slider>("Music Volume text", "Music Volume", "Music");
        sfxVolumeSlider = FindAudioComponentOrChild<Slider>("Sound Effect Volume text", "Sound Volume text", "SFX Volume text", "Sound Tab", "SFX", "Sound");
        uiVolumeSlider = FindAudioComponentOrChild<Slider>("UI Volume text", "UI Volume", "UI");
        audioMixerDropdown = FindAudioComponentOrChild<TMP_Dropdown>("Mixer Text", "Mixer", "Audio Mixer");
        audioLanguageDropdown = FindAudioComponentOrChild<TMP_Dropdown>("Audio Language", "Language");
        audioSpeakerModeDropdown = FindAudioComponentOrChild<TMP_Dropdown>("Speaker Configuration", "Speaker", "Speaker Mode");
        audioMuteToggle = FindAudioComponentOrChild<Toggle>("Mute", "Mute When Unfocus", "Mute When Unfocused");
        audioMuteSlider = FindAudioComponentOrChild<Slider>("Mute", "Mute When Unfocus", "Mute When Unfocused");
        masterVolumeValueText = FindAudioValueText("Master Volume text", "Master Volume", "Master");
        musicVolumeValueText = FindAudioValueText("Music Volume text", "Music Volume", "Music");
        sfxVolumeValueText = FindAudioValueText("Sound Effect Volume text", "Sound Volume text", "SFX Volume text", "Sound Tab", "SFX", "Sound");
        uiVolumeValueText = FindAudioValueText("UI Volume text", "UI Volume", "UI");
        applyButton = FindComponentOrChild<Button>("Apply");
        selectedOptionTitleText = FindComponentOrChild<TMP_Text>("Select Text");
        selectedOptionInfoText = FindComponentOrChild<TMP_Text>("Information Text");
        if (selectedOptionLogoImage == null)
        {
            selectedOptionLogoImage = FindInformationLogoImage();
        }

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
        PopulateDropdown(qualityDropdown, GetQualityPresetLabels());
        PopulateDropdown(shadowDropdown, new List<string> { "Off", "Hard Shadows", "All Shadows" });
        PopulateDropdown(antiAliasingDropdown, AntiAliasingSettingsUtility.GetOptionLabels());
        PopulateDropdown(textureDropdown, new List<string> { "Full Resolution", "Half Resolution", "Quarter Resolution", "Eighth Resolution" });
        ConfigurePostProcessingSlider(vSyncSlider);
        ConfigurePostProcessingSlider(fullscreenSlider);
        ConfigureSensitivitySlider(cameraSensitivitySlider);
        ConfigureSensitivitySlider(aimSensitivitySlider);
        ConfigurePostProcessingSlider(invertMouseSlider);
        ConfigurePostProcessingSlider(bloomSlider);
        ConfigurePostProcessingSlider(motionBlurSlider);
        ConfigurePostProcessingSlider(depthOfFieldSlider);
        ConfigurePostProcessingSlider(chromaticSlider);
        ConfigurePostProcessingSlider(filmGrainSlider);
        LoadAppliedValues();
        SelectOptionTab(OptionTab.Graphics);
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

        sharedGraphicsSettings.ConfigureForOptions(resolutionDropdown, qualityDropdown, shadowDropdown, antiAliasingDropdown, textureDropdown,
            vSyncToggle, fullscreenToggle, null, vSyncSlider, fullscreenSlider);

        resourceUsageDisplay = optionPanel.GetComponent<ResourceUsageDisplay>();
        if (resourceUsageDisplay == null)
        {
            resourceUsageDisplay = optionPanel.AddComponent<ResourceUsageDisplay>();
        }

        resourceUsageDisplay.BindFrom(optionPanel.transform);

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

        sharedPostProcessingSettings.ConfigureForOptions(volume, bloomToggle, motionBlurToggle, depthOfFieldToggle, chromaticToggle, filmGrainToggle,
            bloomSlider, motionBlurSlider, depthOfFieldSlider, chromaticSlider, filmGrainSlider);

        sharedAudioSettings = optionPanel.GetComponent<AudioSettingsManager>();
        if (sharedAudioSettings == null)
        {
            sharedAudioSettings = optionPanel.AddComponent<AudioSettingsManager>();
        }

        sharedAudioSettings.ConfigureForOptions(audioMenu, masterVolumeSlider, musicVolumeSlider, sfxVolumeSlider, uiVolumeSlider,
            masterVolumeValueText, musicVolumeValueText, sfxVolumeValueText, uiVolumeValueText, audioMixerDropdown, audioLanguageDropdown, audioSpeakerModeDropdown, audioMuteToggle, audioMuteSlider);
    }

    private void RegisterListeners()
    {
        ConfigureMenuRootHitAreas();
        ConfigureTabHitAreas();
        RegisterTabButton(graphicsTabButton, OptionTab.Graphics);
        RegisterTabButton(controlTabButton, OptionTab.Control);
        RegisterTabButton(audioTabButton, OptionTab.Audio);

        RegisterChangeListener(resolutionDropdown);
        RegisterQualityPresetListener(qualityDropdown);
        RegisterGraphicsManualChangeListener(shadowDropdown);
        RegisterGraphicsManualChangeListener(antiAliasingDropdown);
        RegisterGraphicsManualChangeListener(textureDropdown);
        RegisterDropdownArrows(resolutionDropdown, "Resolusi", "Resolution", "Resolution text");
        RegisterDropdownArrows(qualityDropdown, "Grafik Quality", "Quality", "Quality Preset", "Quality Preset text");
        RegisterDropdownArrows(shadowDropdown, "Shadow", "Shadow Quality", "Shadow Quality text");
        RegisterDropdownArrows(antiAliasingDropdown, "Anti-Aliasing", "Anti-Aliasing text");
        RegisterDropdownArrows(textureDropdown, "TextureQuality", "Texture Quality", "Texture Quality text");
        RegisterGraphicsManualChangeListener(vSyncToggle);
        RegisterChangeListener(fullscreenToggle);
        RegisterGraphicsManualPostProcessingSlider(vSyncSlider);
        RegisterPostProcessingSlider(fullscreenSlider);
        RegisterInstantSensitivityListener(cameraSensitivitySlider);
        RegisterInstantSensitivityListener(aimSensitivitySlider);
        RegisterInstantInvertMouseListener(invertMouseToggle);
        RegisterInstantInvertMouseListener(invertMouseSlider);
        RegisterGraphicsManualChangeListener(bloomToggle);
        RegisterGraphicsManualChangeListener(motionBlurToggle);
        RegisterGraphicsManualChangeListener(depthOfFieldToggle);
        RegisterGraphicsManualChangeListener(chromaticToggle);
        RegisterGraphicsManualChangeListener(filmGrainToggle);
        RegisterGraphicsManualPostProcessingSlider(bloomSlider);
        RegisterGraphicsManualPostProcessingSlider(motionBlurSlider);
        RegisterGraphicsManualPostProcessingSlider(depthOfFieldSlider);
        RegisterGraphicsManualPostProcessingSlider(chromaticSlider);
        RegisterGraphicsManualPostProcessingSlider(filmGrainSlider);
        RegisterInstantAudioListener(masterVolumeSlider);
        RegisterInstantAudioListener(musicVolumeSlider);
        RegisterInstantAudioListener(sfxVolumeSlider);
        RegisterInstantAudioListener(uiVolumeSlider);
        RegisterInstantAudioListener(audioMixerDropdown);
        RegisterInstantAudioListener(audioLanguageDropdown);
        RegisterInstantAudioListener(audioSpeakerModeDropdown);
        RegisterInstantAudioListener(audioMuteToggle);
        RegisterInstantAudioListener(audioMuteSlider);
        RegisterAudioBinarySlider(audioMuteSlider);
        RegisterDropdownArrows(audioMixerDropdown, "Mixer Text", "Mixer", "Audio Mixer");
        RegisterDropdownArrows(audioLanguageDropdown, "Audio Language", "Language");
        RegisterDropdownArrows(audioSpeakerModeDropdown, "Speaker Configuration", "Speaker", "Speaker Mode");

        Button resumeButton = FindComponentOrChild<Button>("Resume", "Back");
        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveAllListeners();
            resumeButton.onClick.AddListener(ResumeGame);
        }

        foreach (Button apply in FindComponentsOrChild<Button>("Apply"))
        {
            apply.onClick.RemoveAllListeners();
            apply.onClick.AddListener(ApplySettings);
        }

        RegisterResetButton(resetSensitivityButton);
        foreach (Button defaultButton in FindButtonsByLabel("Default", "Reset", "Reset To Default", "Resret to Default"))
        {
            RegisterResetButton(defaultButton);
        }

        foreach (Button mainMenuButton in FindComponentsOrChild<Button>("Main Menu", "MainMenu"))
        {
            mainMenuButton.onClick.RemoveAllListeners();
            mainMenuButton.onClick.AddListener(RequestReturnToMainMenu);
        }

        RegisterCloseButton();
    }

    private void RegisterTabButton(Button button, OptionTab tab)
    {
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectOptionTab(tab));
        }
    }

    private void ConfigureTabHitAreas()
    {
        DisableHighlightRaycasts(graphicsTabHighlight);
        DisableHighlightRaycasts(controlTabHighlight);
        DisableHighlightRaycasts(audioTabHighlight);
    }

    private void ConfigureMenuRootHitAreas()
    {
        DisableRootRaycast(graphicsMenu);
        DisableRootRaycast(controlMenu);
        DisableRootRaycast(audioMenu);
    }

    private static void DisableRootRaycast(GameObject root)
    {
        if (root != null && root.TryGetComponent(out Graphic graphic))
        {
            graphic.raycastTarget = false;
        }
    }

    private static void DisableHighlightRaycasts(GameObject highlight)
    {
        if (highlight == null)
        {
            return;
        }

        foreach (Graphic graphic in highlight.GetComponentsInChildren<Graphic>(true))
        {
            graphic.raycastTarget = false;
        }

        foreach (Selectable selectable in highlight.GetComponentsInChildren<Selectable>(true))
        {
            selectable.interactable = false;
        }
    }

    private void RegisterCloseButton()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(ResumeGame);
        }
    }

    private void RegisterResetButton(Button button)
    {
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(ResetCurrentTabToDefault);
        }
    }

    private void RefreshOptionNavigation(bool selectDefault)
    {
        if (!autoConfigureOptionNavigation || optionPanel == null)
        {
            return;
        }

        optionNavigationSelectables.Clear();
        foreach (Selectable selectable in optionPanel.GetComponentsInChildren<Selectable>(true))
        {
            if (!IsSelectableUsable(selectable))
            {
                continue;
            }

            optionNavigationSelectables.Add(selectable);
        }

        for (int i = 0; i < optionNavigationSelectables.Count; i++)
        {
            Selectable selectable = optionNavigationSelectables[i];
            Navigation navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = FindNearestSelectable(selectable, Vector2.up),
                selectOnDown = FindNearestSelectable(selectable, Vector2.down),
                selectOnLeft = FindNearestSelectable(selectable, Vector2.left),
                selectOnRight = FindNearestSelectable(selectable, Vector2.right)
            };
            selectable.navigation = navigation;
        }

        if (selectDefault)
        {
            SelectDefaultOptionControl();
        }
    }

    private static bool IsSelectableUsable(Selectable selectable)
    {
        return selectable != null
            && selectable.gameObject.activeInHierarchy
            && selectable.IsInteractable()
            && selectable.TryGetComponent(out RectTransform _);
    }

    private Selectable FindNearestSelectable(Selectable from, Vector2 direction)
    {
        if (from == null || !from.TryGetComponent(out RectTransform fromRect))
        {
            return null;
        }

        Vector2 fromPosition = GetSelectablePosition(fromRect);
        Selectable nearest = null;
        float bestScore = float.PositiveInfinity;

        for (int i = 0; i < optionNavigationSelectables.Count; i++)
        {
            Selectable candidate = optionNavigationSelectables[i];
            if (candidate == from || candidate == null || !candidate.TryGetComponent(out RectTransform candidateRect))
            {
                continue;
            }

            Vector2 offset = GetSelectablePosition(candidateRect) - fromPosition;
            float directionalDistance = Vector2.Dot(offset, direction);
            if (directionalDistance <= 0.01f)
            {
                continue;
            }

            float perpendicularDistance = Mathf.Abs(Vector2.Dot(offset, new Vector2(-direction.y, direction.x)));
            float score = directionalDistance + perpendicularDistance * 2.5f;
            if (score < bestScore)
            {
                bestScore = score;
                nearest = candidate;
            }
        }

        return nearest;
    }

    private static Vector2 GetSelectablePosition(RectTransform rectTransform)
    {
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);
        return (corners[0] + corners[2]) * 0.5f;
    }

    private void SelectDefaultOptionControl()
    {
        if (EventSystem.current == null)
        {
            return;
        }

        Selectable selectable = GetDefaultOptionSelectable();
        if (selectable != null)
        {
            EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }
    }

    private static void SelectFirstUsableControl(GameObject root)
    {
        if (EventSystem.current == null || root == null)
        {
            return;
        }

        foreach (Selectable selectable in root.GetComponentsInChildren<Selectable>(true))
        {
            if (IsSelectableUsable(selectable))
            {
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
                return;
            }
        }
    }

    private Selectable GetDefaultOptionSelectable()
    {
        if (IsSelectableUsable(firstSelectedOption))
        {
            return firstSelectedOption;
        }

        if (selectedTab == OptionTab.Control && IsSelectableUsable(controlTabButton))
        {
            return controlTabButton;
        }

        if (selectedTab == OptionTab.Audio && IsSelectableUsable(audioTabButton))
        {
            return audioTabButton;
        }

        if (IsSelectableUsable(graphicsTabButton))
        {
            return graphicsTabButton;
        }

        return optionNavigationSelectables.Count > 0 ? optionNavigationSelectables[0] : null;
    }

    private void RestoreOptionSelectionIfNeeded()
    {
        if (!autoConfigureOptionNavigation || EventSystem.current == null)
        {
            return;
        }

        GameObject selectedObject = EventSystem.current.currentSelectedGameObject;
        if (selectedObject != null && selectedObject.activeInHierarchy)
        {
            return;
        }

        SelectDefaultOptionControl();
    }

    public void ShowGraphicsTab()
    {
        SelectOptionTab(OptionTab.Graphics);
    }

    public void ShowControlTab()
    {
        SelectOptionTab(OptionTab.Control);
    }

    public void ShowAudioTab()
    {
        SelectOptionTab(OptionTab.Audio);
    }

    public void CloseOptionsFromButton()
    {
        ResumeGame();
    }

    private void RegisterOptionInformationTargets()
    {
        SetSelectedOptionInfo("Graphics", "Select a graphics setting to see what it changes.", null);

        if (optionInformationEntries != null && optionInformationEntries.Length > 0)
        {
            for (int i = 0; i < optionInformationEntries.Length; i++)
            {
                OptionInformationEntry entry = optionInformationEntries[i];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.targetName))
                {
                    RegisterInfoTarget(entry.targetName, entry.title, entry.information, entry.logo);
                }
            }

            return;
        }

        RegisterDefaultInfoTargets();
    }

    private void SelectOptionTab(OptionTab tab)
    {
        if (tab == OptionTab.Audio && audioMenu == null)
        {
            SetSelectedOptionInfo("Audio", "Audio controls are not available yet. Create an Audio menu or Sound menu under Option to enable this tab.", null);
            return;
        }

        selectedTab = tab;

        if (graphicsMenu != null) graphicsMenu.SetActive(tab == OptionTab.Graphics);
        if (controlMenu != null) controlMenu.SetActive(tab == OptionTab.Control);
        if (audioMenu != null) audioMenu.SetActive(tab == OptionTab.Audio);

        if (graphicsTabHighlight != null) graphicsTabHighlight.SetActive(tab == OptionTab.Graphics);
        if (controlTabHighlight != null) controlTabHighlight.SetActive(tab == OptionTab.Control);
        if (audioTabHighlight != null) audioTabHighlight.SetActive(tab == OptionTab.Audio);

        if (tab == OptionTab.Graphics)
        {
            SetSelectedOptionInfo("Graphics", "Select a graphics setting to see what it changes.", null);
        }
        else if (tab == OptionTab.Control)
        {
            SetSelectedOptionInfo("Controls", "View and adjust camera sensitivity, aim sensitivity, mouse inversion, and gameplay input references.", null);
        }
        else
        {
            SetSelectedOptionInfo("Audio", "Adjust master, music, and sound effect volume when the audio controls are available.", null);
        }

        RefreshOptionNavigation(optionsOpen);
    }

    private void RegisterDefaultInfoTargets()
    {
        RegisterInfoTarget("Line Display", "Display", "Screen output settings such as resolution, fullscreen mode, and V-Sync.", null);
        RegisterInfoTarget("Resolution text", "Resolution", "Changes the game output resolution. Higher resolutions look sharper but cost more GPU performance.", null);
        RegisterInfoTarget("Full screen", "Full Screen", "Switches between fullscreen window mode and windowed mode. Slider value 0 is On, 1 is Off.", null);
        RegisterInfoTarget("V-Sync", "V-Sync", "Synchronizes rendering with the display refresh rate to reduce tearing. Slider value 0 is On, 1 is Off.", null);

        RegisterInfoTarget("Line Quality", "Quality", "Rendering quality settings that affect visual detail and performance.", null);
        RegisterInfoTarget("Quality Preset text", "Quality Preset", "Applies one of Unity's project quality presets.", null);
        RegisterInfoTarget("Texture Quality text", "Texture Quality", "Controls texture mipmap quality. Lower settings reduce memory usage and texture sharpness.", null);
        RegisterInfoTarget("Shadow Quality text", "Shadow Quality", "Controls shadow rendering. Higher shadow quality improves depth but costs GPU performance.", null);
        RegisterInfoTarget("Anti-Aliasing text", "Anti-Aliasing", "Reduces jagged edges. Unsupported methods will fall back to a supported option.", null);

        RegisterInfoTarget("Line Post Prosesing", "Post Processing", "Camera effects that change the final rendered image.", null);
        RegisterInfoTarget("Bloom", "Bloom", "Adds glow around bright areas. Slider value 0 is On, 1 is Off.", null);
        RegisterInfoTarget("Motion Blur", "Motion Blur", "Blends fast camera or object movement to create a smoother motion look. Slider value 0 is On, 1 is Off.", null);
        RegisterInfoTarget("DOF", "Depth Of Field", "Blurs areas outside the focus range for a camera lens effect. Slider value 0 is On, 1 is Off.", null);
        RegisterInfoTarget("Chromatic", "Chromatic Aberration", "Adds subtle color fringing near image edges. Slider value 0 is On, 1 is Off.", null);
        RegisterInfoTarget("Film Gain", "Film Grain", "Adds a grain/noise overlay for a cinematic texture. Slider value 0 is On, 1 is Off.", null);
    }

    private void RegisterInfoTarget(string objectName, string title, string info, Sprite logo)
    {
        Transform target = FindTransform(objectName);
        if (target == null)
        {
            return;
        }

        AddInfoClick(target, title, info, logo);
        foreach (Selectable selectable in target.GetComponentsInChildren<Selectable>(true))
        {
            AddInfoClick(selectable.transform, title, info, logo);
        }
    }

    private void CopySceneInspectorConfiguration()
    {
        foreach (InGameOptionsMenu controller in Resources.FindObjectsOfTypeAll<InGameOptionsMenu>())
        {
            if (controller == null || controller == this || !controller.gameObject.scene.IsValid() || controller.gameObject.scene != SceneManager.GetActiveScene())
            {
                continue;
            }

            if (selectedOptionLogoImage == null && controller.selectedOptionLogoImage != null)
            {
                selectedOptionLogoImage = controller.selectedOptionLogoImage;
            }

            if ((optionInformationEntries == null || optionInformationEntries.Length == 0) && controller.optionInformationEntries != null && controller.optionInformationEntries.Length > 0)
            {
                optionInformationEntries = controller.optionInformationEntries;
            }

            CopySerializedOptionReference(controller);
        }
    }

    private void CopySerializedOptionReference(InGameOptionsMenu source)
    {
        if (graphicsMenu == null) graphicsMenu = source.graphicsMenu;
        if (controlMenu == null) controlMenu = source.controlMenu;
        if (audioMenu == null) audioMenu = source.audioMenu;
        if (graphicsTabHighlight == null) graphicsTabHighlight = source.graphicsTabHighlight;
        if (controlTabHighlight == null) controlTabHighlight = source.controlTabHighlight;
        if (audioTabHighlight == null) audioTabHighlight = source.audioTabHighlight;
        if (graphicsTabButton == null) graphicsTabButton = source.graphicsTabButton;
        if (controlTabButton == null) controlTabButton = source.controlTabButton;
        if (audioTabButton == null) audioTabButton = source.audioTabButton;
        if (resetSensitivityButton == null) resetSensitivityButton = source.resetSensitivityButton;
        if (closeButton == null) closeButton = source.closeButton;
    }

    private void AddInfoClick(Transform target, string title, string info, Sprite logo)
    {
        AddPointerClick(target, () => SetSelectedOptionInfo(title, info, logo), false);
    }

    private void AddPointerClick(Transform target, UnityEngine.Events.UnityAction action, bool replaceExisting)
    {
        if (target == null || action == null)
        {
            return;
        }

        EventTrigger trigger = target.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = target.gameObject.AddComponent<EventTrigger>();
        }

        EventTrigger.Entry clickEntry = null;
        for (int i = 0; i < trigger.triggers.Count; i++)
        {
            if (trigger.triggers[i].eventID == EventTriggerType.PointerClick)
            {
                clickEntry = trigger.triggers[i];
                break;
            }
        }

        if (clickEntry == null)
        {
            clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            trigger.triggers.Add(clickEntry);
        }
        else if (replaceExisting)
        {
            clickEntry.callback.RemoveAllListeners();
        }

        clickEntry.callback.AddListener(_ => action());
    }

    private void SetSelectedOptionInfo(string title, string info, Sprite logo)
    {
        foreach (TMP_Text titleText in FindNamedComponents<TMP_Text>("Select Text"))
        {
            titleText.text = title;
        }

        foreach (TMP_Text infoText in FindNamedComponents<TMP_Text>("Information Text"))
        {
            infoText.text = info;
        }

        if (logo != null)
        {
            foreach (Image logoImage in FindInformationLogoImages())
            {
                logoImage.sprite = logo;
                logoImage.enabled = true;
            }
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
        RefreshOptionNavigation(true);
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
            RefreshOptionNavigation(true);
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
        ApplyAudioSettings();
        PlayerPrefs.Save();
        SetPendingChanges(false);
    }

    public void ResetSensitivityToDefault()
    {
        SetSensitivityValue(cameraSensitivitySlider, DefaultCameraSensitivity);
        SetSensitivityValue(aimSensitivitySlider, DefaultAimSensitivity);
        SetToggleValue(invertMouseToggle, false);
        SetPostProcessingSliderValue(invertMouseSlider, false);
        ApplySensitivitySettings();
        SetPendingChanges(false);
    }

    public void ResetCurrentTabToDefault()
    {
        if (selectedTab == OptionTab.Audio)
        {
            if (sharedAudioSettings != null)
            {
                sharedAudioSettings.ResetToDefault();
            }

            SetPendingChanges(false);
            return;
        }

        if (selectedTab == OptionTab.Control)
        {
            ResetSensitivityToDefault();
            return;
        }

        if (selectedTab == OptionTab.Graphics)
        {
            sharedGraphicsSettings?.ResetToDefault();
            sharedPostProcessingSettings?.ResetToDefault();
            suppressChangeDetection = true;
            LoadAppliedValues();
            suppressChangeDetection = false;
            SetPendingChanges(false);
        }
    }

    public void RequestReturnToMainMenu()
    {
        if (confirmationPanel != null)
        {
            confirmationPanel.SetActive(true);
            SelectFirstUsableControl(confirmationPanel);
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
            SelectDefaultOptionControl();
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
            FullScreenMode mode = GetPostProcessingControlValue(fullscreenToggle, fullscreenSlider) ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            Screen.SetResolution(resolution.width, resolution.height, mode, resolution.refreshRateRatio);
            PlayerPrefs.SetInt(ResolutionKey, index);
        }

        if (qualityDropdown != null)
        {
            int preset = Mathf.Clamp(qualityDropdown.value, QualityPresetLow, QualityPresetCustom);
            int qualityLevel = GetUnityQualityLevelForPreset(preset);
            QualitySettings.SetQualityLevel(qualityLevel, true);
            PlayerPrefs.SetInt(QualityPresetKey, preset);
            PlayerPrefs.SetInt(QualityKey, qualityLevel);
        }

        if (shadowDropdown != null)
        {
            QualitySettings.shadows = (ShadowQuality)Mathf.Clamp(shadowDropdown.value, 0, 2);
            PlayerPrefs.SetInt(ShadowKey, shadowDropdown.value);
        }

        if (antiAliasingDropdown != null)
        {
            int index = AntiAliasingSettingsUtility.GetEffectiveOptionIndex(antiAliasingDropdown.value);
            AntiAliasingSettingsUtility.Apply(index);
            antiAliasingDropdown.SetValueWithoutNotify(index);
            PlayerPrefs.SetInt(AntiAliasingKey, index);
        }

        if (textureDropdown != null)
        {
            QualitySettings.globalTextureMipmapLimit = Mathf.Clamp(textureDropdown.value, 0, 3);
            PlayerPrefs.SetInt(TextureKey, textureDropdown.value);
        }

        if (vSyncToggle != null || vSyncSlider != null)
        {
            bool vSyncActive = GetPostProcessingControlValue(vSyncToggle, vSyncSlider);
            QualitySettings.vSyncCount = vSyncActive ? 1 : 0;
            PlayerPrefs.SetInt(VSyncKey, vSyncActive ? 1 : 0);
        }

        if (fullscreenToggle != null || fullscreenSlider != null)
        {
            PlayerPrefs.SetInt(FullscreenKey, GetPostProcessingControlValue(fullscreenToggle, fullscreenSlider) ? 1 : 0);
        }

    }

    private void ApplySensitivitySettings()
    {
        float cameraSensitivity = GetSensitivityValue(cameraSensitivitySlider);
        float aimSensitivity = GetSensitivityValue(aimSensitivitySlider);
        bool invertMouse = GetPostProcessingControlValue(invertMouseToggle, invertMouseSlider);

        if (cameraControler != null)
        {
            cameraControler.SetPlayerSensitivityMultipliers(cameraSensitivity, aimSensitivity);
            cameraControler.invertMouseY = invertMouse;
        }

        PlayerPrefs.SetFloat(CameraSensitivityKey, cameraSensitivity);
        PlayerPrefs.SetFloat(AimSensitivityKey, aimSensitivity);
        PlayerPrefs.SetInt(InvertMouseKey, invertMouse ? 1 : 0);
        UpdateSensitivityValueTexts();
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

    private static void ConfigurePostProcessingSlider(Slider slider)
    {
        if (slider == null)
        {
            return;
        }

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = true;
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

    private void UpdateSensitivityValueTexts()
    {
        SetSensitivityValueText(cameraSensitivityValueText, GetSensitivityValue(cameraSensitivitySlider));
        SetSensitivityValueText(aimSensitivityValueText, GetSensitivityValue(aimSensitivitySlider));
    }

    private static void SetSensitivityValueText(TMP_Text text, float value)
    {
        if (text != null)
        {
            text.text = Mathf.RoundToInt(value * 100f).ToString();
        }
    }

    private static bool GetPostProcessingControlValue(Toggle toggle, Slider slider)
    {
        if (slider != null)
        {
            return slider.value < 0.5f;
        }

        return toggle != null && toggle.isOn;
    }

    private static void SetPostProcessingSliderValue(Slider slider, bool active)
    {
        if (slider != null)
        {
            slider.SetValueWithoutNotify(active ? 0f : 1f);
        }
    }

    private void ApplyPostProcessingSettings()
    {
        if (sharedPostProcessingSettings != null)
        {
            sharedPostProcessingSettings.ApplySettings();
            return;
        }
        ApplyEffect("Bloom", bloomToggle, bloomSlider, BloomKey);
        ApplyEffect("MotionBlur", motionBlurToggle, motionBlurSlider, MotionBlurKey);
        ApplyEffect("DepthOfField", depthOfFieldToggle, depthOfFieldSlider, DepthOfFieldKey);
        ApplyEffect("ChromaticAberration", chromaticToggle, chromaticSlider, ChromaticAberrationKey);
        ApplyEffect("FilmGrain", filmGrainToggle, filmGrainSlider, FilmGrainKey, "Grain");
    }

    private void ApplyAudioSettings()
    {
        if (sharedAudioSettings != null)
        {
            sharedAudioSettings.ApplySettings();
        }
    }

    private void ApplyEffect(string primaryName, Toggle toggle, Slider slider, string preferenceKey, string alternateName = null)
    {
        if (toggle == null && slider == null)
        {
            return;
        }

        bool active = GetPostProcessingControlValue(toggle, slider);
        PlayerPrefs.SetInt(preferenceKey, active ? 1 : 0);
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
                    SetMember(effect, "active", active);
                }
            }
        }
    }

    private void LoadAppliedValues()
    {
        SetDropdownValue(resolutionDropdown, PlayerPrefs.GetInt(ResolutionKey, FindCurrentResolutionIndex()));
        bool hasSavedPreset = PlayerPrefs.HasKey(QualityPresetKey);
        int preset = PlayerPrefs.GetInt(QualityPresetKey, DetectRecommendedQualityPreset());
        SetDropdownValue(qualityDropdown, preset);
        SetDropdownValue(shadowDropdown, PlayerPrefs.GetInt(ShadowKey, (int)QualitySettings.shadows));

        int aaDefault = AntiAliasingSettingsUtility.GetDefaultOptionIndex();
        SetDropdownValue(antiAliasingDropdown, AntiAliasingSettingsUtility.ClampOptionIndex(PlayerPrefs.GetInt(AntiAliasingKey, aaDefault)));
        SetDropdownValue(textureDropdown, PlayerPrefs.GetInt(TextureKey, QualitySettings.globalTextureMipmapLimit));
        SetToggleValue(vSyncToggle, PlayerPrefs.GetInt(VSyncKey, QualitySettings.vSyncCount > 0 ? 1 : 0) == 1);
        SetToggleValue(fullscreenToggle, PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1);
        SetPostProcessingSliderValue(vSyncSlider, PlayerPrefs.GetInt(VSyncKey, QualitySettings.vSyncCount > 0 ? 1 : 0) == 1);
        SetPostProcessingSliderValue(fullscreenSlider, PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1);

        SetSensitivityValue(cameraSensitivitySlider, PlayerPrefs.GetFloat(CameraSensitivityKey, DefaultCameraSensitivity));
        SetSensitivityValue(aimSensitivitySlider, PlayerPrefs.GetFloat(AimSensitivityKey, DefaultAimSensitivity));
        bool invertMouse = PlayerPrefs.GetInt(InvertMouseKey, 0) == 1;
        SetToggleValue(invertMouseToggle, invertMouse);
        SetPostProcessingSliderValue(invertMouseSlider, invertMouse);
        ApplySensitivitySettings();

        SetToggleValue(bloomToggle, PlayerPrefs.GetInt(BloomKey, 1) == 1);
        SetToggleValue(motionBlurToggle, PlayerPrefs.GetInt(MotionBlurKey, 1) == 1);
        SetToggleValue(depthOfFieldToggle, PlayerPrefs.GetInt(DepthOfFieldKey, 1) == 1);
        SetToggleValue(chromaticToggle, PlayerPrefs.GetInt(ChromaticAberrationKey, 1) == 1);
        SetToggleValue(filmGrainToggle, PlayerPrefs.GetInt(FilmGrainKey, 1) == 1);
        SetPostProcessingSliderValue(bloomSlider, PlayerPrefs.GetInt(BloomKey, 1) == 1);
        SetPostProcessingSliderValue(motionBlurSlider, PlayerPrefs.GetInt(MotionBlurKey, 1) == 1);
        SetPostProcessingSliderValue(depthOfFieldSlider, PlayerPrefs.GetInt(DepthOfFieldKey, 1) == 1);
        SetPostProcessingSliderValue(chromaticSlider, PlayerPrefs.GetInt(ChromaticAberrationKey, 1) == 1);
        SetPostProcessingSliderValue(filmGrainSlider, PlayerPrefs.GetInt(FilmGrainKey, 1) == 1);

        if (!hasSavedPreset && preset != QualityPresetCustom)
        {
            ApplyQualityPresetToControls(preset, false);
        }
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

    private void RegisterQualityPresetListener(TMP_Dropdown dropdown)
    {
        if (dropdown == null)
        {
            return;
        }

        dropdown.onValueChanged.RemoveAllListeners();
        dropdown.onValueChanged.AddListener(value =>
        {
            if (suppressChangeDetection)
            {
                return;
            }

            ApplyQualityPresetToControls(value, true);
            NotifyChanged();
        });
    }

    private void RegisterGraphicsManualChangeListener(TMP_Dropdown dropdown)
    {
        if (dropdown == null)
        {
            return;
        }

        dropdown.onValueChanged.RemoveAllListeners();
        dropdown.onValueChanged.AddListener(_ => NotifyGraphicsManualChanged());
    }

    private void RegisterGraphicsManualChangeListener(Toggle toggle)
    {
        if (toggle == null)
        {
            return;
        }

        toggle.onValueChanged.RemoveAllListeners();
        toggle.onValueChanged.AddListener(_ => NotifyGraphicsManualChanged());
    }

    private void RegisterChangeListener(Slider slider)
    {
        if (slider != null)
        {
            slider.onValueChanged.RemoveAllListeners();
            slider.onValueChanged.AddListener(_ => NotifyChanged());
        }
    }

    private void RegisterPostProcessingSlider(Slider slider)
    {
        RegisterPostProcessingSlider(slider, false);
    }

    private void RegisterGraphicsManualPostProcessingSlider(Slider slider)
    {
        RegisterPostProcessingSlider(slider, true);
    }

    private void RegisterPostProcessingSlider(Slider slider, bool marksQualityCustom)
    {
        if (slider == null)
        {
            return;
        }

        slider.onValueChanged.RemoveAllListeners();
        slider.onValueChanged.AddListener(_ =>
        {
            if (marksQualityCustom)
            {
                NotifyGraphicsManualChanged();
            }
            else
            {
                NotifyChanged();
            }
        });

        EventTrigger trigger = slider.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = slider.gameObject.AddComponent<EventTrigger>();
        }

        EventTrigger.Entry clickEntry = null;
        for (int i = 0; i < trigger.triggers.Count; i++)
        {
            if (trigger.triggers[i].eventID == EventTriggerType.PointerClick)
            {
                clickEntry = trigger.triggers[i];
                break;
            }
        }

        if (clickEntry == null)
        {
            clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            trigger.triggers.Add(clickEntry);
        }
        else
        {
            clickEntry.callback.RemoveAllListeners();
        }

        clickEntry.callback.AddListener(_ => TogglePostProcessingSlider(slider));
    }

    private void RegisterInstantInvertMouseListener(Toggle toggle)
    {
        if (toggle == null)
        {
            return;
        }

        toggle.onValueChanged.RemoveAllListeners();
        toggle.onValueChanged.AddListener(_ =>
        {
            if (!suppressChangeDetection)
            {
                ApplySensitivitySettings();
                SetPendingChanges(true);
            }
        });
    }

    private void RegisterInstantInvertMouseListener(Slider slider)
    {
        if (slider == null)
        {
            return;
        }

        slider.onValueChanged.RemoveAllListeners();
        slider.onValueChanged.AddListener(_ =>
        {
            if (!suppressChangeDetection)
            {
                ApplySensitivitySettings();
                SetPendingChanges(true);
            }
        });

        EventTrigger trigger = slider.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = slider.gameObject.AddComponent<EventTrigger>();
        }

        EventTrigger.Entry clickEntry = null;
        for (int i = 0; i < trigger.triggers.Count; i++)
        {
            if (trigger.triggers[i].eventID == EventTriggerType.PointerClick)
            {
                clickEntry = trigger.triggers[i];
                break;
            }
        }

        if (clickEntry == null)
        {
            clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            trigger.triggers.Add(clickEntry);
        }
        else
        {
            clickEntry.callback.RemoveAllListeners();
        }

        clickEntry.callback.AddListener(_ => TogglePostProcessingSlider(slider));
    }

    private void TogglePostProcessingSlider(Slider slider)
    {
        if (slider == null)
        {
            return;
        }

        slider.value = slider.value < 0.5f ? 1f : 0f;
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

    private void RegisterInstantAudioListener(Slider slider)
    {
        if (slider == null)
        {
            return;
        }

        slider.onValueChanged.RemoveAllListeners();
        slider.onValueChanged.AddListener(_ =>
        {
            if (!suppressChangeDetection)
            {
                ApplyAudioSettings();
            }
        });
    }

    private void RegisterInstantAudioListener(TMP_Dropdown dropdown)
    {
        if (dropdown == null)
        {
            return;
        }

        dropdown.onValueChanged.RemoveAllListeners();
        dropdown.onValueChanged.AddListener(_ =>
        {
            if (!suppressChangeDetection)
            {
                ApplyAudioSettings();
            }
        });
    }

    private void RegisterInstantAudioListener(Toggle toggle)
    {
        if (toggle == null)
        {
            return;
        }

        toggle.onValueChanged.RemoveAllListeners();
        toggle.onValueChanged.AddListener(_ =>
        {
            if (!suppressChangeDetection)
            {
                ApplyAudioSettings();
            }
        });
    }

    private void RegisterAudioBinarySlider(Slider slider)
    {
        if (slider == null)
        {
            return;
        }

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = true;

        EventTrigger trigger = slider.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = slider.gameObject.AddComponent<EventTrigger>();
        }

        bool wasActiveBeforeClick = slider.value < 0.5f;
        EventTrigger.Entry downEntry = null;
        EventTrigger.Entry clickEntry = null;
        for (int i = 0; i < trigger.triggers.Count; i++)
        {
            if (trigger.triggers[i].eventID == EventTriggerType.PointerDown)
            {
                downEntry = trigger.triggers[i];
            }

            if (trigger.triggers[i].eventID == EventTriggerType.PointerClick)
            {
                clickEntry = trigger.triggers[i];
            }
        }

        if (downEntry == null)
        {
            downEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            trigger.triggers.Add(downEntry);
        }
        else
        {
            downEntry.callback.RemoveAllListeners();
        }

        downEntry.callback.AddListener(_ => wasActiveBeforeClick = slider.value < 0.5f);

        if (clickEntry == null)
        {
            clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            trigger.triggers.Add(clickEntry);
        }
        else
        {
            clickEntry.callback.RemoveAllListeners();
        }

        clickEntry.callback.AddListener(_ =>
        {
            slider.value = wasActiveBeforeClick ? 1f : 0f;
            ApplyAudioSettings();
        });
    }

    private void RegisterDropdownArrows(TMP_Dropdown dropdown, params string[] containerNames)
    {
        if (dropdown == null || containerNames == null)
        {
            return;
        }

        foreach (Transform container in FindTransforms(containerNames))
        {
            foreach (Transform candidate in container.GetComponentsInChildren<Transform>(true))
            {
                int direction = GetDropdownArrowDirection(candidate, dropdown.transform);
                if (direction == 0)
                {
                    continue;
                }

                Button button = GetOrCreateArrowButton(candidate, container);
                if (button == null || button.GetComponent<TMP_Dropdown>() == dropdown)
                {
                    continue;
                }

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => StepDropdownValue(dropdown, direction));
            }
        }
    }

    private static Button GetOrCreateArrowButton(Transform arrow, Transform container)
    {
        if (arrow == null || container == null)
        {
            return null;
        }

        Button button = arrow.GetComponent<Button>();
        if (button == null)
        {
            button = arrow.GetComponentInParent<Button>(true);
            if (button != null && !button.transform.IsChildOf(container))
            {
                button = null;
            }
        }

        if (button == null)
        {
            button = arrow.gameObject.AddComponent<Button>();
            if (arrow.TryGetComponent(out Graphic graphic))
            {
                graphic.raycastTarget = true;
                button.targetGraphic = graphic;
            }
        }

        button.interactable = true;
        return button;
    }

    private void StepDropdownValue(TMP_Dropdown dropdown, int direction)
    {
        if (dropdown == null || dropdown.options.Count == 0 || direction == 0)
        {
            return;
        }

        int next = Mathf.Clamp(dropdown.value + Math.Sign(direction), 0, dropdown.options.Count - 1);
        if (next == dropdown.value)
        {
            return;
        }

        dropdown.value = next;
        if (dropdown == audioMixerDropdown || dropdown == audioLanguageDropdown || dropdown == audioSpeakerModeDropdown)
        {
            ApplyAudioSettings();
        }
        else
        {
            NotifyChanged();
        }
    }

    private static int GetDropdownArrowDirection(Transform arrow, Transform dropdown)
    {
        if (arrow == null)
        {
            return 0;
        }

        string arrowName = arrow.name;
        if (arrowName.IndexOf("arrow", StringComparison.OrdinalIgnoreCase) < 0
            && arrow.parent != null
            && arrow.parent.name.IndexOf("arrow", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            arrowName = arrow.parent.name;
        }

        if (arrowName.IndexOf("left", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return -1;
        }

        if (arrowName.IndexOf("right", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return 1;
        }

        return 0;
    }

    private void NotifyChanged()
    {
        if (!suppressChangeDetection)
        {
            SetPendingChanges(true);
        }
    }

    private void NotifyGraphicsManualChanged()
    {
        if (suppressChangeDetection)
        {
            return;
        }

        SetDropdownValue(qualityDropdown, QualityPresetCustom);
        SetPendingChanges(true);
    }

    private void ApplyQualityPresetToControls(int presetIndex, bool updateDropdown)
    {
        if (presetIndex == QualityPresetCustom)
        {
            if (updateDropdown)
            {
                SetDropdownValue(qualityDropdown, QualityPresetCustom);
            }

            return;
        }

        int preset = Mathf.Clamp(presetIndex, QualityPresetLow, QualityPresetUltra);
        if (updateDropdown)
        {
            SetDropdownValue(qualityDropdown, preset);
        }

        switch (preset)
        {
            case QualityPresetLow:
                SetDropdownValue(shadowDropdown, 0);
                SetDropdownValue(antiAliasingDropdown, FindAntiAliasingOption("Off", "None", "Disabled"));
                SetDropdownValue(textureDropdown, 2);
                SetToggleValue(vSyncToggle, false);
                SetPostProcessingSliderValue(vSyncSlider, false);
                SetPostProcessingValue(bloomToggle, bloomSlider, false);
                SetPostProcessingValue(motionBlurToggle, motionBlurSlider, false);
                SetPostProcessingValue(depthOfFieldToggle, depthOfFieldSlider, false);
                SetPostProcessingValue(chromaticToggle, chromaticSlider, false);
                SetPostProcessingValue(filmGrainToggle, filmGrainSlider, false);
                break;
            case QualityPresetMedium:
                SetDropdownValue(shadowDropdown, 1);
                SetDropdownValue(antiAliasingDropdown, FindAntiAliasingOption("FXAA", "MSAA 2x", "2x", "Off"));
                SetDropdownValue(textureDropdown, 1);
                SetToggleValue(vSyncToggle, false);
                SetPostProcessingSliderValue(vSyncSlider, false);
                SetPostProcessingValue(bloomToggle, bloomSlider, true);
                SetPostProcessingValue(motionBlurToggle, motionBlurSlider, false);
                SetPostProcessingValue(depthOfFieldToggle, depthOfFieldSlider, false);
                SetPostProcessingValue(chromaticToggle, chromaticSlider, false);
                SetPostProcessingValue(filmGrainToggle, filmGrainSlider, false);
                break;
            case QualityPresetHigh:
                SetDropdownValue(shadowDropdown, 2);
                SetDropdownValue(antiAliasingDropdown, FindAntiAliasingOption("TAA", "MSAA 4x", "4x", "FXAA"));
                SetDropdownValue(textureDropdown, 0);
                SetToggleValue(vSyncToggle, true);
                SetPostProcessingSliderValue(vSyncSlider, true);
                SetPostProcessingValue(bloomToggle, bloomSlider, true);
                SetPostProcessingValue(motionBlurToggle, motionBlurSlider, false);
                SetPostProcessingValue(depthOfFieldToggle, depthOfFieldSlider, true);
                SetPostProcessingValue(chromaticToggle, chromaticSlider, true);
                SetPostProcessingValue(filmGrainToggle, filmGrainSlider, false);
                break;
            case QualityPresetUltra:
                SetDropdownValue(shadowDropdown, 2);
                SetDropdownValue(antiAliasingDropdown, FindAntiAliasingOption("TAA", "MSAA 8x", "8x", "MSAA 4x", "4x"));
                SetDropdownValue(textureDropdown, 0);
                SetToggleValue(vSyncToggle, true);
                SetPostProcessingSliderValue(vSyncSlider, true);
                SetPostProcessingValue(bloomToggle, bloomSlider, true);
                SetPostProcessingValue(motionBlurToggle, motionBlurSlider, true);
                SetPostProcessingValue(depthOfFieldToggle, depthOfFieldSlider, true);
                SetPostProcessingValue(chromaticToggle, chromaticSlider, true);
                SetPostProcessingValue(filmGrainToggle, filmGrainSlider, true);
                break;
        }
    }

    private static List<string> GetQualityPresetLabels()
    {
        return new List<string> { "Low", "Medium", "High", "Ultra", "Custom" };
    }

    private static int DetectRecommendedQualityPreset()
    {
        if (SystemInfo.graphicsMemorySize < 1024 || SystemInfo.systemMemorySize < 4096 || SystemInfo.graphicsShaderLevel < 35)
        {
            return QualityPresetLow;
        }

        int score = 0;
        if (SystemInfo.systemMemorySize >= 16000) score += 2;
        else if (SystemInfo.systemMemorySize >= 8000) score += 1;

        if (SystemInfo.graphicsMemorySize >= 8000) score += 3;
        else if (SystemInfo.graphicsMemorySize >= 4000) score += 2;
        else if (SystemInfo.graphicsMemorySize >= 2000) score += 1;

        if (SystemInfo.processorCount >= 8) score += 2;
        else if (SystemInfo.processorCount >= 4) score += 1;

        if (SystemInfo.graphicsShaderLevel >= 50) score += 2;
        else if (SystemInfo.graphicsShaderLevel >= 45) score += 1;

        if (score >= 8) return QualityPresetUltra;
        if (score >= 5) return QualityPresetHigh;
        if (score >= 3) return QualityPresetMedium;
        return QualityPresetLow;
    }

    private static int GetUnityQualityLevelForPreset(int presetIndex)
    {
        if (QualitySettings.names == null || QualitySettings.names.Length == 0)
        {
            return 0;
        }

        return presetIndex switch
        {
            QualityPresetLow => FindUnityQualityLevel("Low", "Very Low", "Performant", "Performance"),
            QualityPresetMedium => FindUnityQualityLevel("Medium", "Balanced"),
            QualityPresetHigh => FindUnityQualityLevel("High", "Beautiful"),
            QualityPresetUltra => FindUnityQualityLevel("Ultra", "Very High", "Fantastic"),
            _ => Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1)
        };
    }

    private static int FindUnityQualityLevel(params string[] preferredNames)
    {
        for (int p = 0; p < preferredNames.Length; p++)
        {
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                if (QualitySettings.names[i].IndexOf(preferredNames[p], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return i;
                }
            }
        }

        if (preferredNames.Length > 0 && preferredNames[0].IndexOf("low", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return 0;
        }

        if (preferredNames.Length > 0 && preferredNames[0].IndexOf("medium", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return Mathf.Clamp(QualitySettings.names.Length / 2, 0, QualitySettings.names.Length - 1);
        }

        return QualitySettings.names.Length - 1;
    }

    private int FindAntiAliasingOption(params string[] preferredLabels)
    {
        if (antiAliasingDropdown == null || antiAliasingDropdown.options.Count == 0)
        {
            return 0;
        }

        for (int p = 0; p < preferredLabels.Length; p++)
        {
            for (int i = 0; i < antiAliasingDropdown.options.Count; i++)
            {
                if (antiAliasingDropdown.options[i].text.IndexOf(preferredLabels[p], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return i;
                }
            }
        }

        return AntiAliasingSettingsUtility.GetDefaultOptionIndex();
    }

    private static void SetPostProcessingValue(Toggle toggle, Slider slider, bool active)
    {
        SetToggleValue(toggle, active);
        SetPostProcessingSliderValue(slider, active);
    }

    private void SetPendingChanges(bool pending)
    {
        hasPendingChanges = pending;
        if (applyImage != null)
        {
            applyImage.color = pending ? new Color(0.18f, 0.78f, 0.32f, 1f) : applyDefaultColor;
        }

        foreach (Button apply in FindComponentsOrChild<Button>("Apply"))
        {
            if (apply.targetGraphic is Image image)
            {
                image.color = pending ? new Color(0.18f, 0.78f, 0.32f, 1f) : applyDefaultColor;
            }
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

    private T FindComponentOrChild<T>(params string[] objectNames) where T : Component
    {
        if (optionPanel == null || objectNames == null)
        {
            return null;
        }

        foreach (string objectName in objectNames)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                continue;
            }

            foreach (Transform transform in optionPanel.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name != objectName)
                {
                    continue;
                }

                T component = transform.GetComponent<T>();
                if (component != null)
                {
                    return component;
                }

                component = transform.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }
        }

        return null;
    }

    private IEnumerable<T> FindComponentsOrChild<T>(params string[] objectNames) where T : Component
    {
        if (optionPanel == null || objectNames == null)
        {
            yield break;
        }

        foreach (string objectName in objectNames)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                continue;
            }

            foreach (Transform transform in optionPanel.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name != objectName)
                {
                    continue;
                }

                T component = transform.GetComponent<T>();
                if (component != null)
                {
                    yield return component;
                }

                foreach (T childComponent in transform.GetComponentsInChildren<T>(true))
                {
                    if (childComponent != component)
                    {
                        yield return childComponent;
                    }
                }
            }
        }
    }

    private TMP_Text FindValueText(string containerName)
    {
        Transform container = FindTransform(containerName);
        if (container == null)
        {
            return null;
        }

        foreach (TMP_Text text in container.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.transform.name == "Value Text")
            {
                return text;
            }
        }

        return null;
    }

    private TMP_Text FindAudioValueText(params string[] containerNames)
    {
        foreach (Transform container in FindAudioTransforms(containerNames))
        {
            foreach (TMP_Text text in container.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.transform.name == "Value Text")
                {
                    return text;
                }
            }
        }

        return null;
    }

    private IEnumerable<T> FindNamedComponents<T>(string objectName) where T : Component
    {
        if (optionPanel == null || string.IsNullOrWhiteSpace(objectName))
        {
            yield break;
        }

        foreach (Transform transform in optionPanel.GetComponentsInChildren<Transform>(true))
        {
            if (transform.name != objectName)
            {
                continue;
            }

            T component = transform.GetComponent<T>();
            if (component != null)
            {
                yield return component;
            }
        }
    }

    private Transform FindTransform(string objectName)
    {
        if (optionPanel == null || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Transform lineMatch = null;
        Transform containsMatch = null;
        foreach (Transform transform in optionPanel.GetComponentsInChildren<Transform>(true))
        {
            if (transform.name == objectName)
            {
                return transform;
            }

            if (lineMatch == null && string.Equals(transform.name, "Line " + objectName, StringComparison.OrdinalIgnoreCase))
            {
                lineMatch = transform;
            }

            if (containsMatch == null && transform.name.IndexOf(objectName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                containsMatch = transform;
            }
        }

        return lineMatch != null ? lineMatch : containsMatch;
    }

    private IEnumerable<Transform> FindTransforms(params string[] objectNames)
    {
        if (optionPanel == null || objectNames == null)
        {
            yield break;
        }

        foreach (string objectName in objectNames)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                continue;
            }

            foreach (Transform transform in optionPanel.GetComponentsInChildren<Transform>(true))
            {
                if (string.Equals(transform.name, objectName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(transform.name, "Line " + objectName, StringComparison.OrdinalIgnoreCase))
                {
                    yield return transform;
                }
            }
        }
    }

    private IEnumerable<Transform> FindAudioTransforms(params string[] objectNames)
    {
        Transform root = audioMenu != null ? audioMenu.transform : optionPanel != null ? optionPanel.transform : null;
        if (root == null || objectNames == null)
        {
            yield break;
        }

        foreach (string objectName in objectNames)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                continue;
            }

            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (string.Equals(transform.name, objectName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(transform.name, "Line " + objectName, StringComparison.OrdinalIgnoreCase))
                {
                    yield return transform;
                }
            }
        }
    }

    private T FindAudioComponentOrChild<T>(params string[] objectNames) where T : Component
    {
        foreach (Transform transform in FindAudioTransforms(objectNames))
        {
            T component = transform.GetComponent<T>();
            if (component != null)
            {
                return component;
            }

            component = transform.GetComponentInChildren<T>(true);
            if (component != null)
            {
                return component;
            }
        }

        return null;
    }

    private Image FindInformationLogoImage()
    {
        Transform imageRoot = FindDirectChild(optionPanel != null ? optionPanel.transform : null, "Image");
        Transform icon = FindDirectChild(imageRoot, "Icon");
        if (icon != null && icon.TryGetComponent(out Image image))
        {
            return image;
        }

        return FindComponentOrChild<Image>("Icon");
    }

    private IEnumerable<Image> FindInformationLogoImages()
    {
        if (optionPanel == null)
        {
            yield break;
        }

        foreach (Transform transform in optionPanel.GetComponentsInChildren<Transform>(true))
        {
            if (transform.name != "Logo Select")
            {
                continue;
            }

            Transform icon = FindDirectChild(transform, "Icon");
            if (icon != null && icon.TryGetComponent(out Image image))
            {
                yield return image;
            }
        }

        if (selectedOptionLogoImage != null)
        {
            yield return selectedOptionLogoImage;
        }
    }

    private static Transform FindDirectChild(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
            {
                return child;
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

    private IEnumerable<Button> FindButtonsByLabel(params string[] labels)
    {
        if (optionPanel == null || labels == null)
        {
            yield break;
        }

        foreach (Button button in optionPanel.GetComponentsInChildren<Button>(true))
        {
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text == null)
            {
                continue;
            }

            for (int i = 0; i < labels.Length; i++)
            {
                if (string.Equals(text.text, labels[i], StringComparison.OrdinalIgnoreCase))
                {
                    yield return button;
                    break;
                }
            }
        }
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
