using UnityEngine;
using UnityEngine.SceneManagement;

public class NextSceneTrigger : MonoBehaviour
{
    [Header("Scene Target")]
    public string targetSceneName;
    public bool useNextBuildIndex = true;

    [Header("Trigger Settings")]
    public bool playerTagOnly = true;
    public float loadDelay = 0f;

    private bool isLoading;

    void OnTriggerEnter2D(Collider2D other)
    {
        TryLoadScene(other.gameObject);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        TryLoadScene(collision.gameObject);
    }

    void TryLoadScene(GameObject other)
    {
        if (isLoading)
        {
            return;
        }

        if (playerTagOnly && !other.CompareTag("Player"))
        {
            return;
        }

        string sceneToLoad = GetSceneToLoad();
        if (string.IsNullOrEmpty(sceneToLoad))
        {
            Debug.LogWarning("NextSceneTrigger could not find a scene to load.");
            return;
        }

        isLoading = true;
        if (Player.instance != null)
        {
            if (AnalyticsManager.Instance != null)
            {
                AnalyticsManager.Instance.HandleLevelClear(Player.instance.currentHP);
            }

            Gamemanager.SavePlayerHPForNextScene(Player.instance.currentHP);
        }

        if (loadDelay > 0f)
        {
            Invoke(nameof(LoadResolvedScene), loadDelay);
            cachedSceneName = sceneToLoad;
            return;
        }

        SceneManager.LoadScene(sceneToLoad);
    }

    private string cachedSceneName;

    void LoadResolvedScene()
    {
        if (!string.IsNullOrEmpty(cachedSceneName))
        {
            SceneManager.LoadScene(cachedSceneName);
        }
    }

    string GetSceneToLoad()
    {
        if (!string.IsNullOrWhiteSpace(targetSceneName))
        {
            return targetSceneName;
        }

        if (!useNextBuildIndex)
        {
            return string.Empty;
        }

        int nextBuildIndex = SceneManager.GetActiveScene().buildIndex + 1;
        if (nextBuildIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogWarning("NextSceneTrigger: there is no next scene in Build Settings.");
            return string.Empty;
        }

        return SceneUtility.GetScenePathByBuildIndex(nextBuildIndex);
    }
}
