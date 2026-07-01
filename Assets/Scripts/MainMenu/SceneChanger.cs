using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Handles main-menu actions and the asynchronous gameplay loading transition.</summary>
public sealed class SceneChanger : MonoBehaviour
{
    private static bool gameFinishedThisSession;
    private const string MainGameScenePath = "Assets/Scenes/MainScene.unity";

    [Header("Scene")]
    [SerializeField] private string mainGameSceneName = "MainScene";

    [Header("Loading Transition")]
    [SerializeField, Min(0f)] private float transitionFadeDuration = 0.25f;
    [SerializeField, Min(0f)] private float minimumLoadingScreenDuration = 0.75f;
    [SerializeField, Min(0.1f)] private float progressSmoothingSpeed = 2f;

    [Header("Finished Menu Layout")]
    [SerializeField] private bool gameFinished;
    [SerializeField] private RectTransform quitButtonRect;
    [SerializeField] private float defaultQuitButtonY = 100f;
    [SerializeField] private float finishedQuitButtonY = 0f;
    [SerializeField] private bool clampQuitButtonWidthToParent = true;
    [SerializeField, Range(0.1f, 1f)] private float quitButtonMaxParentWidthPercent = 0.85f;
    [SerializeField, Min(120f)] private float quitButtonMinimumWidth = 240f;

    [Header("Finished Credits")]
    [SerializeField] private GameObject creditPanel;
    [SerializeField] private Button creditButton;
    [SerializeField] private ScrollRect creditScrollRect;
    [SerializeField] private TMP_Text creditBackText;
    [SerializeField, Min(1f)] private float creditScrollDuration = 300f;
    [SerializeField, Min(1f)] private float creditFastForwardMultiplier = 8f;
    [SerializeField, Min(0f)] private float creditAutoCloseDelay = 5f;

    private GameObject selectMenuPanel;
    private GameObject loadingPanel;
    private CanvasGroup loadingCanvasGroup;
    private Slider loadingSlider;
    private TMP_Text loadingText;
    private Button[] menuButtons;
    private bool isLoading;
    private bool creditFastForward;
    private Coroutine creditRoutine;
    private string resolvedMainGameSceneIdentifier;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeGameFinishedFlag()
    {
        gameFinishedThisSession = false;
    }

    public static void MarkGameFinishedForCurrentSession()
    {
        gameFinishedThisSession = true;
    }

    public static void ClearGameFinishedForCurrentSession()
    {
        gameFinishedThisSession = false;
    }

    private void Awake()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        ResolveMenuPanels();
        if (loadingPanel == null)
        {
            // This scene contains duplicate legacy SceneChanger components on
            // UI panels. Only the controller beneath the loading UI is active.
            enabled = false;
            return;
        }

        menuButtons = GetComponentsInChildren<Button>(true);
        ResolveCreditMenu();
        ApplyFinishedMenuLayout();

        loadingPanel.SetActive(false);
        SetCreditPanelVisible(false);
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        SyncRuntimeFinishedFlagWithInspector();
        ApplyFinishedMenuLayout();
    }

    public void LoadMainGame()
    {
        if (isLoading)
        {
            return;
        }

        ApplyMenuSettingsBeforeSceneLoad();
        InGameOptionsMenu.SetInputBlocked(false);

        if (!TryResolveMainGameScene(out resolvedMainGameSceneIdentifier))
        {
            Debug.LogError($"Main game scene '{mainGameSceneName}' could not be loaded. Add '{MainGameScenePath}' to Build Settings or the active Build Profile scene list.", this);
            return;
        }

        StartCoroutine(LoadMainGameRoutine());
    }

    private void ApplyMenuSettingsBeforeSceneLoad()
    {
        Scene currentScene = gameObject.scene;

        GraphicsSettingsManager[] graphicsManagers = FindObjectsByType<GraphicsSettingsManager>(FindObjectsInactive.Include);
        for (int i = 0; i < graphicsManagers.Length; i++)
        {
            GraphicsSettingsManager manager = graphicsManagers[i];
            if (manager != null && manager.gameObject.scene == currentScene && manager.HasUsableBindings())
            {
                manager.ApplySettings();
            }
        }

        PostProcessingSettings[] postProcessingManagers = FindObjectsByType<PostProcessingSettings>(FindObjectsInactive.Include);
        for (int i = 0; i < postProcessingManagers.Length; i++)
        {
            PostProcessingSettings manager = postProcessingManagers[i];
            if (manager != null && manager.gameObject.scene == currentScene)
            {
                manager.ApplySettings();
            }
        }

        AudioSettingsManager[] audioManagers = FindObjectsByType<AudioSettingsManager>(FindObjectsInactive.Include);
        for (int i = 0; i < audioManagers.Length; i++)
        {
            AudioSettingsManager manager = audioManagers[i];
            if (manager != null && manager.gameObject.scene == currentScene)
            {
                manager.ApplySettings();
            }
        }

        PlayerPrefs.Save();
    }

    public void QuitGame()
    {
        if (isLoading)
        {
            return;
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OpenFinishedCredit()
    {
        if (isLoading || !IsGameFinishedForMenu() || creditPanel == null || creditScrollRect == null)
        {
            return;
        }

        if (creditRoutine != null)
        {
            StopCoroutine(creditRoutine);
        }

        creditRoutine = StartCoroutine(FinishedCreditRoutine());
    }

    public void CloseFinishedCredit()
    {
        if (creditRoutine != null)
        {
            StopCoroutine(creditRoutine);
            creditRoutine = null;
        }

        creditFastForward = false;
        SetCreditPanelVisible(false);
        SetMenuButtonsInteractable(true);
    }

    private IEnumerator LoadMainGameRoutine()
    {
        isLoading = true;
        SetMenuButtonsInteractable(false);
        if (selectMenuPanel != null)
        {
            selectMenuPanel.SetActive(false);
        }

        if (loadingCanvasGroup != null)
        {
            loadingCanvasGroup.alpha = 0f;
        }

        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
        }

        SetLoadingProgress(0f);
        yield return FadeLoadingPanel(0f, 1f);

        AsyncOperation operation = SceneManager.LoadSceneAsync(resolvedMainGameSceneIdentifier, LoadSceneMode.Single);
        operation.allowSceneActivation = false;

        float displayedProgress = 0f;
        float loadingScreenStartedAt = Time.unscaledTime;
        while (operation.progress < 0.9f)
        {
            float targetProgress = Mathf.Clamp01(operation.progress / 0.9f);
            displayedProgress = Mathf.MoveTowards(displayedProgress, targetProgress, progressSmoothingSpeed * Time.unscaledDeltaTime);
            SetLoadingProgress(displayedProgress);
            yield return null;
        }

        while (displayedProgress < 1f)
        {
            displayedProgress = Mathf.MoveTowards(displayedProgress, 1f, progressSmoothingSpeed * Time.unscaledDeltaTime);
            SetLoadingProgress(displayedProgress);
            yield return null;
        }

        float remainingTransitionTime = minimumLoadingScreenDuration - (Time.unscaledTime - loadingScreenStartedAt);
        if (remainingTransitionTime > 0f)
        {
            yield return new WaitForSecondsRealtime(remainingTransitionTime);
        }

        operation.allowSceneActivation = true;
    }

    private bool TryResolveMainGameScene(out string sceneIdentifier)
    {
        sceneIdentifier = mainGameSceneName;
        if (Application.CanStreamedLevelBeLoaded(sceneIdentifier))
        {
            return true;
        }

        sceneIdentifier = MainGameScenePath;
        return Application.CanStreamedLevelBeLoaded(sceneIdentifier);
    }

    private IEnumerator FinishedCreditRoutine()
    {
        creditFastForward = false;
        SetCreditPanelVisible(true);
        Canvas.ForceUpdateCanvases();
        creditScrollRect.verticalNormalizedPosition = 1f;
        yield return null;

        float elapsed = 0f;
        float duration = Mathf.Max(1f, creditScrollDuration);
        while (elapsed < duration)
        {
            creditFastForward = IsAnyButtonPressed();
            float speed = creditFastForward ? creditFastForwardMultiplier : 1f;
            elapsed += Time.unscaledDeltaTime * Mathf.Max(0.01f, speed);
            creditScrollRect.verticalNormalizedPosition = Mathf.Lerp(1f, 0f, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        creditScrollRect.verticalNormalizedPosition = 0f;

        float wait = 0f;
        while (wait < creditAutoCloseDelay)
        {
            if (WasAnyButtonPressedThisFrame())
            {
                break;
            }

            wait += Time.unscaledDeltaTime;
            yield return null;
        }

        CloseFinishedCredit();
    }

    private void ResolveMenuPanels()
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i].name == "Select Menu")
            {
                selectMenuPanel = transforms[i].gameObject;
            }
            else if (transforms[i].name == "Loading")
            {
                loadingPanel = transforms[i].gameObject;
            }
        }

        if (loadingPanel == null)
        {
            return;
        }

        loadingCanvasGroup = loadingPanel.GetComponent<CanvasGroup>();
        if (loadingCanvasGroup == null)
        {
            loadingCanvasGroup = loadingPanel.AddComponent<CanvasGroup>();
        }

        loadingSlider = loadingPanel.GetComponentInChildren<Slider>(true);
        loadingText = loadingPanel.GetComponentInChildren<TMP_Text>(true);
    }

    private void ResolveCreditMenu()
    {
        if (creditPanel == null)
        {
            creditPanel = FindCreditPanel();
        }

        if (creditPanel != null && creditScrollRect == null)
        {
            creditScrollRect = creditPanel.GetComponentInChildren<ScrollRect>(true);
        }

        if (creditPanel != null && creditBackText == null)
        {
            creditBackText = FindCreditBackText(creditPanel.transform);
        }

        if (creditBackText != null)
        {
            creditBackText.raycastTarget = true;
            Button backButton = creditBackText.GetComponent<Button>();
            if (backButton == null)
            {
                backButton = creditBackText.gameObject.AddComponent<Button>();
            }

            backButton.onClick.RemoveListener(CloseFinishedCredit);
            backButton.onClick.AddListener(CloseFinishedCredit);
        }

        if (creditButton == null)
        {
            creditButton = FindCreditMenuButton();
        }

        if (creditButton != null)
        {
            creditButton.onClick.RemoveListener(OpenFinishedCredit);
            creditButton.onClick.AddListener(OpenFinishedCredit);
        }

        ApplyCreditButtonVisibility();
    }

    private void ApplyFinishedMenuLayout()
    {
        if (quitButtonRect == null)
        {
            quitButtonRect = FindSceneRectTransform("Quit");
        }

        if (quitButtonRect == null)
        {
            return;
        }

        Vector2 position = quitButtonRect.anchoredPosition;
        position.y = IsGameFinishedForMenu()
            ? finishedQuitButtonY
            : defaultQuitButtonY;
        quitButtonRect.anchoredPosition = position;
        ClampQuitButtonWidth();
        ApplyCreditButtonVisibility();
    }

    private void ClampQuitButtonWidth()
    {
        RectTransform parentRect = quitButtonRect != null ? quitButtonRect.parent as RectTransform : null;
        if (!clampQuitButtonWidthToParent || quitButtonRect == null || parentRect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        float parentWidth = parentRect.rect.width;
        if (parentWidth <= 0f)
        {
            return;
        }

        Vector2 size = quitButtonRect.sizeDelta;
        float maxWidth = Mathf.Max(quitButtonMinimumWidth, parentWidth * quitButtonMaxParentWidthPercent);
        size.x = Mathf.Min(size.x, maxWidth);
        quitButtonRect.sizeDelta = size;
    }

    private void SyncRuntimeFinishedFlagWithInspector()
    {
        if (gameFinished)
        {
            gameFinishedThisSession = true;
        }
        else if (SceneManager.GetActiveScene().name == "MainMenu")
        {
            gameFinishedThisSession = false;
        }

        ApplyCreditButtonVisibility();
    }

    private bool IsGameFinishedForMenu()
    {
        return gameFinished || gameFinishedThisSession;
    }

    private void ApplyCreditButtonVisibility()
    {
        if (creditButton != null)
        {
            creditButton.gameObject.SetActive(IsGameFinishedForMenu());
        }
    }

    private void SetCreditPanelVisible(bool visible)
    {
        if (creditPanel != null)
        {
            creditPanel.SetActive(visible);
        }

        if (selectMenuPanel != null)
        {
            selectMenuPanel.SetActive(!visible);
        }
    }

    private static GameObject FindCreditPanel()
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        Scene activeScene = SceneManager.GetActiveScene();
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject candidate = objects[i];
            if (candidate == null
                || candidate.name != "Credit"
                || !candidate.scene.IsValid()
                || candidate.scene != activeScene
                || candidate.GetComponentInChildren<ScrollRect>(true) == null)
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    private static Button FindCreditMenuButton()
    {
        Button[] buttons = Resources.FindObjectsOfTypeAll<Button>();
        Scene activeScene = SceneManager.GetActiveScene();
        for (int i = 0; i < buttons.Length; i++)
        {
            Button candidate = buttons[i];
            if (candidate == null
                || !candidate.gameObject.scene.IsValid()
                || candidate.gameObject.scene != activeScene
                || candidate.GetComponentInChildren<ScrollRect>(true) != null)
            {
                continue;
            }

            TMP_Text label = candidate.GetComponentInChildren<TMP_Text>(true);
            string labelText = label != null ? label.text.Trim() : string.Empty;
            if (candidate.name == "Credit" || labelText == "Credit")
            {
                return candidate;
            }
        }

        return null;
    }

    private static TMP_Text FindCreditBackText(Transform root)
    {
        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text candidate = texts[i];
            string text = candidate.text.Trim();
            if (text == "Kembali"
                || text == "Back"
                || candidate.name == "Kembali"
                || candidate.name == "Back")
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool WasAnyButtonPressedThisFrame()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            return true;
        }

        if (Mouse.current != null
            && (Mouse.current.leftButton.wasPressedThisFrame
                || Mouse.current.rightButton.wasPressedThisFrame
                || Mouse.current.middleButton.wasPressedThisFrame))
        {
            return true;
        }

        return WasButtonPressed(Gamepad.current) || WasButtonPressed(Joystick.current);
    }

    private static bool IsAnyButtonPressed()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.isPressed)
        {
            return true;
        }

        if (Mouse.current != null
            && (Mouse.current.leftButton.isPressed
                || Mouse.current.rightButton.isPressed
                || Mouse.current.middleButton.isPressed))
        {
            return true;
        }

        return IsButtonPressed(Gamepad.current) || IsButtonPressed(Joystick.current);
    }

    private static bool WasButtonPressed(InputDevice device)
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

    private static bool IsButtonPressed(InputDevice device)
    {
        if (device == null)
        {
            return false;
        }

        foreach (InputControl control in device.allControls)
        {
            if (control is ButtonControl button && button.isPressed)
            {
                return true;
            }
        }

        return false;
    }

    private static RectTransform FindSceneRectTransform(string objectName)
    {
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        Scene activeScene = SceneManager.GetActiveScene();
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null
                || candidate.name != objectName
                || !candidate.gameObject.scene.IsValid()
                || candidate.gameObject.scene != activeScene)
            {
                continue;
            }

            return candidate.GetComponent<RectTransform>();
        }

        return null;
    }

    private IEnumerator FadeLoadingPanel(float from, float to)
    {
        if (loadingCanvasGroup == null)
        {
            yield break;
        }

        loadingCanvasGroup.alpha = from;
        loadingCanvasGroup.blocksRaycasts = true;

        if (transitionFadeDuration <= 0f)
        {
            loadingCanvasGroup.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < transitionFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            loadingCanvasGroup.alpha = Mathf.Lerp(from, to, elapsed / transitionFadeDuration);
            yield return null;
        }

        loadingCanvasGroup.alpha = to;
    }

    private void SetLoadingProgress(float progress)
    {
        float clampedProgress = Mathf.Clamp01(progress);
        if (loadingSlider != null)
        {
            loadingSlider.SetValueWithoutNotify(clampedProgress);
        }

        if (loadingText != null)
        {
            loadingText.text = $"LOADING {Mathf.RoundToInt(clampedProgress * 100f)}%";
        }
    }

    private void SetMenuButtonsInteractable(bool interactable)
    {
        for (int i = 0; i < menuButtons.Length; i++)
        {
            menuButtons[i].interactable = interactable;
        }
    }
}
