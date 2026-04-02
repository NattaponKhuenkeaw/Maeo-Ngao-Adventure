using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class Gamemanager : MonoBehaviour
{
    public static Gamemanager instance;
    private void Awake()
    {
        if (instance == null) instance = this;
    }

    public static class SessionSave
    {
        public static bool hasSave = false;
        public static SaveData data;
    }

    public static class SceneTransition
    {
        public static bool hasPlayerHP = false;
        public static int playerHP;
    }


    [System.Serializable]
    public class SaveData
    {
        public float playerX;
        public float playerY;
        public int currentHP;       
        public List<Vector3> activePotionPositions;
    }

    public void SaveGame()
    {
        SaveData data = new SaveData();

        data.playerX = Player.instance.transform.position.x;
        data.playerY = Player.instance.transform.position.y;
        data.currentHP = Player.instance.currentHP;

        // เก็บ potion ที่ยังอยู่
        data.activePotionPositions = new List<Vector3>();
        GameObject[] potions = GameObject.FindGameObjectsWithTag("Potion");
        foreach (var p in potions)
        {
            data.activePotionPositions.Add(p.transform.position);
        }

        // เซฟเข้า RAM (Session)
        SessionSave.data = data;
        SessionSave.hasSave = true;

        Debug.Log("Session Saved (no disk save)");
    }


    public void LoadGame()
    {
        Player.instance.die.SetActive(false);

        if (!SessionSave.hasSave)
        {
            Debug.Log("No Session Save");
            return;
        }
        Player.instance.enabled = true;

        SaveData data = SessionSave.data;

        Player.instance.transform.position = new Vector2(data.playerX, data.playerY);
        Player.instance.currentHP = data.currentHP;

        PotionSpawner.instance.LoadPotionsFromSave(data.activePotionPositions);

        Debug.Log("Session Loaded!");
    }


    public void RestartGame()
    {
        SessionSave.hasSave = false;
        SessionSave.data = null;
        SceneTransition.hasPlayerHP = false;
        SceneTransition.playerHP = 0;

        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
        );
    }

    public static void SavePlayerHPForNextScene(int hp)
    {
        SceneTransition.playerHP = hp;
        SceneTransition.hasPlayerHP = true;
    }

    public static bool TryConsumePlayerHPForNextScene(out int hp)
    {
        if (!SceneTransition.hasPlayerHP)
        {
            hp = 0;
            return false;
        }

        hp = SceneTransition.playerHP;
        SceneTransition.playerHP = 0;
        SceneTransition.hasPlayerHP = false;
        return true;
    }

}
