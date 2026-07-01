using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Plays shared UI sounds for selectable controls in the active scene.</summary>
public sealed class UIAudioController : MonoBehaviour
{
    private static UIAudioController instance;
    private static float globalVolumeMultiplier = 1f;

    [Header("Clips")]
    [SerializeField] private AudioClip[] selectClips;
    [SerializeField] private AudioClip[] pressClips;
    [SerializeField] private AudioClip[] cancelClips;
    [SerializeField] private AudioClip[] changeClips;

    [Header("Playback")]
    [SerializeField, Range(0f, 1f)] private float selectVolume = 0.45f;
    [SerializeField, Range(0f, 1f)] private float pressVolume = 0.65f;
    [SerializeField, Range(0f, 1f)] private float cancelVolume = 0.65f;
    [SerializeField, Range(0f, 1f)] private float changeVolume = 0.5f;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.98f, 1.02f);
    [SerializeField] private float selectCooldown = 0.04f;
    [SerializeField] private float pressCooldown = 0.04f;
    [SerializeField] private float changeCooldown = 0.04f;
    [SerializeField] private float refreshInterval = 0.5f;
    [SerializeField] private bool playHoverSelect = true;
    [SerializeField] private bool playKeyboardSelect = true;
    [SerializeField] private bool playCancelInput = true;

    private readonly HashSet<Selectable> boundSelectables = new();
    private AudioSource audioSource;
    private GameObject lastSelectedObject;
    private float nextSelectTime;
    private float nextPressTime;
    private float nextChangeTime;
    private float nextRefreshTime;
    private float nextCancelTime;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
        EnsureAudioSource();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        RefreshBindings();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextRefreshTime)
        {
            nextRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);
            RefreshBindings();
        }

        if (playCancelInput && WasCancelPressed() && Time.unscaledTime >= nextCancelTime)
        {
            nextCancelTime = Time.unscaledTime + pressCooldown;
            PlayRandom(cancelClips, cancelVolume);
        }
    }

    public void RefreshBindings()
    {
        foreach (Selectable selectable in Resources.FindObjectsOfTypeAll<Selectable>())
        {
            if (!IsBindable(selectable))
            {
                continue;
            }

            if (!boundSelectables.Add(selectable))
            {
                continue;
            }

            BindSelectable(selectable);
        }
    }

    public void PlaySelect()
    {
        if (Time.unscaledTime < nextSelectTime)
        {
            return;
        }

        nextSelectTime = Time.unscaledTime + selectCooldown;
        PlayRandom(selectClips, selectVolume);
    }

    public void PlayPress()
    {
        if (Time.unscaledTime < nextPressTime)
        {
            return;
        }

        nextPressTime = Time.unscaledTime + pressCooldown;
        PlayRandom(pressClips, pressVolume);
    }

    public void PlayCancel()
    {
        PlayRandom(cancelClips, cancelVolume);
    }

    public void PlayChange()
    {
        if (Time.unscaledTime < nextChangeTime)
        {
            return;
        }

        nextChangeTime = Time.unscaledTime + changeCooldown;
        PlayRandom(changeClips, changeVolume);
    }

    public static void SetGlobalVolume(float volume)
    {
        globalVolumeMultiplier = Mathf.Clamp01(volume);
    }

    private void BindSelectable(Selectable selectable)
    {
        AddPointerEvents(selectable);
    }

    private void AddPointerEvents(Selectable selectable)
    {
        EventTrigger trigger = selectable.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = selectable.gameObject.AddComponent<EventTrigger>();
        }

        AddTrigger(trigger, EventTriggerType.Select, _ =>
        {
            if (!playKeyboardSelect || selectable.gameObject == lastSelectedObject)
            {
                return;
            }

            lastSelectedObject = selectable.gameObject;
            PlaySelect();
        });

        AddTrigger(trigger, EventTriggerType.PointerEnter, _ =>
        {
            if (!playHoverSelect || selectable.gameObject == lastSelectedObject)
            {
                return;
            }

            lastSelectedObject = selectable.gameObject;
            PlaySelect();
        });

        AddTrigger(trigger, EventTriggerType.PointerClick, _ => PlayPress());
        AddTrigger(trigger, EventTriggerType.Submit, _ => PlayPress());

        if (selectable is Slider || selectable is Dropdown || selectable is TMP_Dropdown)
        {
            AddTrigger(trigger, EventTriggerType.PointerUp, _ => PlayChange());
            AddTrigger(trigger, EventTriggerType.EndDrag, _ => PlayChange());
        }
    }

    private void PlayRandom(AudioClip[] clips, float volume)
    {
        if (clips == null || clips.Length == 0)
        {
            return;
        }

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null)
        {
            return;
        }

        EnsureAudioSource();
        if (audioSource == null)
        {
            return;
        }

        audioSource.pitch = Random.Range(Mathf.Min(pitchRange.x, pitchRange.y), Mathf.Max(pitchRange.x, pitchRange.y));
        audioSource.PlayOneShot(clip, volume * globalVolumeMultiplier);
    }

    private void EnsureAudioSource()
    {
        if (audioSource != null)
        {
            return;
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.ignoreListenerPause = true;
    }

    private static bool IsBindable(Selectable selectable)
    {
        return selectable != null
            && selectable.gameObject.scene.IsValid()
            && selectable.gameObject.scene == SceneManager.GetActiveScene();
    }

    private static bool WasCancelPressed()
    {
        return (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
    }

    private static void AddTrigger(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> callback)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(callback);
        trigger.triggers.Add(entry);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        boundSelectables.Clear();
        lastSelectedObject = null;
        RefreshBindings();
    }
}
