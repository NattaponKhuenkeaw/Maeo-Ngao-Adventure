using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Analytics;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-1000)]
public class AnalyticsManager : MonoBehaviour
{
    public static AnalyticsManager Instance { get; private set; }

    [Header("Analytics Settings")]
    [Tooltip("For a small prototype you can auto-grant required opt-in consent during testing. Leave this off if you want to handle consent with your own UI.")]
    public bool autoProvideRequiredConsentForPrototype = false;

    [Header("Runtime Analytics Data")]
    [SerializeField] private string currentLevel;
    [SerializeField] private int stepCount;
    [SerializeField] private int potionCollected;
    [SerializeField] private int currentHP;

    private bool isInitialized;
    private bool isInitializing;
    private bool hasSentLevelEndAnalytics;

    // Keep the original schema requested for this student project.
    // If Unity Event Manager rejects "Level", rename only this key in both the dashboard and code.
    const string LevelParameterName = "Level";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void CreateSingletonBeforeSceneLoad()
    {
        if (FindFirstObjectByType<AnalyticsManager>() != null)
        {
            return;
        }

        GameObject analyticsObject = new GameObject("AnalyticsManager");
        analyticsObject.AddComponent<AnalyticsManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    async void Start()
    {
        if (string.IsNullOrEmpty(currentLevel))
        {
            ResetLevelAnalytics(SceneManager.GetActiveScene().name);
        }

        await InitializeAnalyticsAsync();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResetLevelAnalytics(scene.name);

        if (Player.instance != null)
        {
            UpdateCurrentHP(Player.instance.currentHP);
        }
    }

    public async Task InitializeAnalyticsAsync()
    {
        if (isInitialized || isInitializing)
        {
            return;
        }

        isInitializing = true;

        try
        {
            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            List<string> requiredConsents = await AnalyticsService.Instance.CheckForRequiredConsents();
            if (requiredConsents.Count > 0)
            {
                if (autoProvideRequiredConsentForPrototype)
                {
                    foreach (string consentIdentifier in requiredConsents)
                    {
                        AnalyticsService.Instance.ProvideOptInConsent(consentIdentifier, true);
                        Debug.Log($"[Analytics] Auto consent granted for: {consentIdentifier}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[Analytics] Consent is required before sending events: {string.Join(", ", requiredConsents)}");
                }
            }

            isInitialized = true;
            Debug.Log("[Analytics] Unity Services and Analytics initialized.");
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Analytics] Initialize failed: {exception.Message}");
        }
        finally
        {
            isInitializing = false;
        }
    }

    public void ResetLevelAnalytics(string levelName)
    {
        currentLevel = levelName;
        stepCount = 0;
        potionCollected = 0;
        currentHP = 0;
        hasSentLevelEndAnalytics = false;

        Debug.Log($"[Analytics] Reset counters for level: {currentLevel}");
    }

    public void RegisterSuccessfulStep()
    {
        stepCount++;

        if (Player.instance != null)
        {
            UpdateCurrentHP(Player.instance.currentHP);
        }
    }

    public void RegisterPotionCollected(int hpAfterPickup)
    {
        potionCollected++;
        UpdateCurrentHP(hpAfterPickup);
    }

    public void UpdateCurrentHP(int hp)
    {
        currentHP = Mathf.Max(0, hp);
    }

    public void HandlePlayerDeath(string causeOfDeath, Vector3 deathPosition, int hpRemaining)
    {
        if (hasSentLevelEndAnalytics)
        {
            return;
        }

        UpdateCurrentHP(hpRemaining);

        bool sentAnyEvent = false;
        sentAnyEvent |= SendPlayerDeath(currentLevel, causeOfDeath, deathPosition.x, deathPosition.y);
        sentAnyEvent |= SendPlayerMoveLevel(currentLevel, stepCount, "Dead", currentHP);
        sentAnyEvent |= SendPotionCollected(currentLevel, "Dead", potionCollected);

        hasSentLevelEndAnalytics = true;

        if (sentAnyEvent)
        {
            FlushAnalytics();
        }
    }

    public void HandleLevelClear(int hpRemaining)
    {
        if (hasSentLevelEndAnalytics)
        {
            return;
        }

        UpdateCurrentHP(hpRemaining);

        bool sentAnyEvent = false;
        sentAnyEvent |= SendPlayerMoveLevel(currentLevel, stepCount, "Clear", currentHP);
        sentAnyEvent |= SendPotionCollected(currentLevel, "Clear", potionCollected);

        hasSentLevelEndAnalytics = true;

        if (sentAnyEvent)
        {
            FlushAnalytics();
        }
    }

    public bool SendPlayerDeath(string level, string playerCauseOfDeath, float deathPositionX, float deathPositionY)
    {
        if (!CanSendEvent())
        {
            return false;
        }

        Debug.Log($"[Analytics] Sending Player_Death | Level={level}, Player_CauseOfdeath={playerCauseOfDeath}, Death_Position_X={deathPositionX}, Death_Position_Y={deathPositionY}");

        try
        {
            Dictionary<string, object> analyticsEvent = new Dictionary<string, object>
            {
                { LevelParameterName, level },
                { "Player_CauseOfdeath", playerCauseOfDeath },
                { "Death_Position_X", deathPositionX },
                { "Death_Position_Y", deathPositionY }
            };

            AnalyticsService.Instance.CustomData("Player_Death", analyticsEvent);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Analytics] Failed to send Player_Death: {exception.Message}");
            return false;
        }
    }

    public bool SendPlayerMoveLevel(string level, int totalStepCount, string playerStatus, int hpRemaining)
    {
        if (!CanSendEvent())
        {
            return false;
        }

        Debug.Log($"[Analytics] Sending Player_Move_Level | Level={level}, Step_Count={totalStepCount}, Player_Status={playerStatus}, HP_Remaining={hpRemaining}");

        try
        {
            Dictionary<string, object> analyticsEvent = new Dictionary<string, object>
            {
                { LevelParameterName, level },
                { "Step_Count", totalStepCount },
                { "Player_Status", playerStatus },
                { "HP_Remaining", hpRemaining }
            };

            AnalyticsService.Instance.CustomData("Player_Move_Level", analyticsEvent);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Analytics] Failed to send Player_Move_Level: {exception.Message}");
            return false;
        }
    }

    public bool SendPotionCollected(string level, string playerStatus, int collectedCount)
    {
        if (!CanSendEvent())
        {
            return false;
        }

        Debug.Log($"[Analytics] Sending Potion_Collected | Level={level}, Player_Status={playerStatus}, Potion_Collected={collectedCount}");

        try
        {
            Dictionary<string, object> analyticsEvent = new Dictionary<string, object>
            {
                { LevelParameterName, level },
                { "Player_Status", playerStatus },
                { "Potion_Collected", collectedCount }
            };

            AnalyticsService.Instance.CustomData("Potion_Collected", analyticsEvent);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Analytics] Failed to send Potion_Collected: {exception.Message}");
            return false;
        }
    }

    bool CanSendEvent()
    {
        if (isInitialized)
        {
            return true;
        }

        if (!isInitializing)
        {
            _ = InitializeAnalyticsAsync();
        }

        Debug.LogWarning("[Analytics] Unity Services is not ready yet, so this event was skipped.");
        return false;
    }

    void FlushAnalytics()
    {
        try
        {
            AnalyticsService.Instance.Flush();
            Debug.Log("[Analytics] Flush requested.");
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Analytics] Flush failed: {exception.Message}");
        }
    }
}
