using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class FloorTrap : MonoBehaviour
{
    private static readonly List<FloorTrap> ActiveTraps = new List<FloorTrap>();

    [Header("Trap Timing")]
    public bool startActive = false;
    public float activeDuration = 1.5f;
    public float inactiveDuration = 1.5f;

    [Header("Trap Damage")]
    public int damage = 2;

    [Header("Trap Visual")]
    public Color inactiveColor = Color.white;
    public Color activeColor = new Color(1f, 0.3f, 0.3f, 1f);

    private SpriteRenderer spriteRenderer;
    private Tilemap tilemap;
    private bool isActive;

    public static void ApplyTrapDamage(Vector3 worldPosition, Player player)
    {
        for (int i = 0; i < ActiveTraps.Count; i++)
        {
            FloorTrap trap = ActiveTraps[i];
            if (trap == null)
            {
                continue;
            }

            if (trap.IsTrapActiveAt(worldPosition))
            {
                player.TakeDamage(trap.damage, true);
            }
        }
    }

    void Reset()
    {
        CacheComponents();
    }

    void Awake()
    {
        CacheComponents();
    }

    void OnEnable()
    {
        if (!ActiveTraps.Contains(this))
        {
            ActiveTraps.Add(this);
        }
    }

    void OnDisable()
    {
        ActiveTraps.Remove(this);
    }

    void Start()
    {
        SetTrapState(startActive);
        StartCoroutine(ToggleRoutine());
    }

    void OnValidate()
    {
        activeDuration = Mathf.Max(0.1f, activeDuration);
        inactiveDuration = Mathf.Max(0.1f, inactiveDuration);
        damage = Mathf.Max(1, damage);

        if (!Application.isPlaying)
        {
            CacheComponents();
            UpdateVisualState(startActive);
        }
    }

    IEnumerator ToggleRoutine()
    {
        while (true)
        {
            float waitTime = isActive ? activeDuration : inactiveDuration;
            yield return new WaitForSeconds(waitTime);
            SetTrapState(!isActive);
        }
    }

    void CacheComponents()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        tilemap = GetComponent<Tilemap>();
    }

    bool IsTrapActiveAt(Vector3 worldPosition)
    {
        if (!isActive)
        {
            return false;
        }

        if (tilemap != null)
        {
            Vector3Int cellPosition = tilemap.WorldToCell(worldPosition);
            return tilemap.HasTile(cellPosition);
        }

        if (spriteRenderer != null)
        {
            return spriteRenderer.bounds.Contains(worldPosition);
        }

        return false;
    }

    void SetTrapState(bool active)
    {
        isActive = active;
        UpdateVisualState(active);

        if (active && Player.instance != null)
        {
            Vector3 playerPosition = Player.instance.GetTrapCheckPosition();
            if (IsTrapActiveAt(playerPosition))
            {
                Player.instance.TakeDamage(damage, true);
            }
        }
    }

    void UpdateVisualState(bool active)
    {
        Color targetColor = active ? activeColor : inactiveColor;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = targetColor;
        }

        if (tilemap != null)
        {
            tilemap.color = targetColor;
        }
    }
}
