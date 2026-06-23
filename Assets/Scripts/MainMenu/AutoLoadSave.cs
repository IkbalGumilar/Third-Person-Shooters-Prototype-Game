using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Optional menu entry point for loading a configured gameplay scene.</summary>
public sealed class AutoLoadSave : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "MainScene";

    public void LoadGame()
    {
        if (Application.CanStreamedLevelBeLoaded(gameSceneName))
        {
            SceneManager.LoadScene(gameSceneName);
        }
        else
        {
            Debug.LogError($"Game scene '{gameSceneName}' is not included in Build Settings.", this);
        }
    }
}
