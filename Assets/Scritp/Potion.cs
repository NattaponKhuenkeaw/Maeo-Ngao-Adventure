using UnityEngine;

public class Potion : MonoBehaviour
{
    public string potionID;   
    public int healAmount = 3;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            Player.instance.Heal(healAmount);

            if (AnalyticsManager.Instance != null)
            {
                AnalyticsManager.Instance.RegisterPotionCollected(Player.instance.currentHP);
            }

            Destroy(gameObject);
        }
    }
}
