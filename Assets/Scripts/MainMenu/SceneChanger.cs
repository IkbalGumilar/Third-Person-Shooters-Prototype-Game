using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Handles main-menu actions and the asynchronous gameplay loading transition.</summary>
public sealed class SceneChanger : MonoBehaviour
{
    private static bool gameFinishedThisSession;

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

    private GameObject selectMenuPanel;
    private GameObject loadingPanel;
    private CanvasGroup loadingCanvasGroup;
    private Slider loadingSlider;
    private TMP_Text loadingText;
    private Button[] menuButtons;
    private bool isLoading;

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
        ApplyFinishedMenuLayout();

        loadingPanel.SetActive(false);
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

        InGameOptionsMenu.SetInputBlocked(false);

        if (!Application.CanStreamedLevelBeLoaded(mainGameSceneName))
        {
            Debug.LogError($"Main game scene {mainGameSceneName} is not included in Build Settings.", this);
            return;
        }

        StartCoroutine(LoadMainGameRoutine());
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

        AsyncOperation operation = SceneManager.LoadSceneAsync(mainGameSceneName, LoadSceneMode.Single);
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
        position.y = (gameFinished || gameFinishedThisSession)
            ? finishedQuitButtonY
            : defaultQuitButtonY;
        quitButtonRect.anchoredPosition = position;
        ClampQuitButtonWidth();
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
