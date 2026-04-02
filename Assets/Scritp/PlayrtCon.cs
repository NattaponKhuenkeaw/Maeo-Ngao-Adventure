using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.Tilemaps;

public class Player : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioClip walk;
    public AudioClip healSound;
    public AudioClip gameOverSound;
    public AudioClip gameWinSound;
    public AudioClip gameSaveSound;

    [Header("UI")]
    public Slider healthSlider;
    public GameObject die;
    public GameObject win;
    public Image damageFlashImage;
    public static Player instance;

    [Header("Movement Settings")]
    public float tileSize = 1f;
    public float moveSpeed = 4f;
    public bool snapToGridOnStart = true;

    [Header("Health Settings")]
    public int maxHP = 10;
    public int currentHP;
    public int damagePerStep = 1;

    [Header("Collision")]
    public Transform trapCheckPoint;
    public Tilemap movementTilemap;

    [Header("Trap Damage Flash")]
    public Color trapDamageFlashColor = new Color(1f, 0f, 0f, 0.35f);
    public float trapDamageFlashDuration = 0.2f;

    [Header("Spawn Save")]
    public bool autoSaveOnSceneStart = true;
    public float autoSaveDelay = 0f;

    [Header("Sprites")]
    public Sprite spriteUp;
    public Sprite spriteDown;
    public Sprite spriteLeft;
    public Sprite spriteRight;

    private SpriteRenderer spriteRenderer;
    private Collider2D playerCollider;
    private Vector2 moveInput;
    private bool isMoving = false;
    private Coroutine damageFlashCoroutine;
    private int floorLayer;

    void Awake()
    {
        instance = this;
        playerCollider = GetComponent<Collider2D>();
        floorLayer = LayerMask.NameToLayer("Floor");
    }

    void Start()
    {
        die.SetActive(false);
        win.SetActive(false);

        CacheMovementTilemap();

        if (movementTilemap != null)
        {
            AlignToMovementTilemap();
        }
        else if (snapToGridOnStart || NeedsGridAlignment(transform.position))
        {
            transform.position = GetSnappedGridPosition(transform.position);
        }

        spriteRenderer = GetComponent<SpriteRenderer>();
        currentHP = maxHP;

        if (Gamemanager.TryConsumePlayerHPForNextScene(out int carriedHP))
        {
            currentHP = Mathf.Clamp(carriedHP, 0, maxHP);
        }

        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHP;
            healthSlider.value = currentHP;
        }

        EnsureDamageFlashImage();
        if (AnalyticsManager.Instance != null)
        {
            AnalyticsManager.Instance.UpdateCurrentHP(currentHP);
        }

        if (autoSaveOnSceneStart)
        {
            StartCoroutine(AutoSaveOnSceneStartRoutine());
        }
    }

    void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    void Update()
    {
        if (isMoving)
        {
            return;
        }

        if (moveInput == Vector2.zero)
        {
            return;
        }

        
        Vector2 dir = Vector2.zero;
        if (Mathf.Abs(moveInput.x) > Mathf.Abs(moveInput.y))
        {
            dir = new Vector2(Mathf.Sign(moveInput.x), 0);
        }
        else
        {
            dir = new Vector2(0, Mathf.Sign(moveInput.y));
        }

        UpdateSprite(dir);

        
        if (TryGetMovementDestination(dir, out Vector3 destination))
        {
            StartCoroutine(MoveTo(destination));
        }
    }

    void UpdateSprite(Vector2 dir)
    {
        if (dir.x > 0)
        {
            spriteRenderer.sprite = spriteRight;
        }
        else if (dir.x < 0)
        {
            spriteRenderer.sprite = spriteLeft;
        }
        else if (dir.y > 0)
        {
            spriteRenderer.sprite = spriteUp;
        }
        else if (dir.y < 0)
        {
            spriteRenderer.sprite = spriteDown;
        }
    }

    IEnumerator MoveTo(Vector3 destination)
    {
        if (audioSource != null && walk != null)
        {
            audioSource.PlayOneShot(walk);
        }
        isMoving = true;

        while ((transform.position - destination).sqrMagnitude > 0.001f)
        {
            transform.position = Vector3.MoveTowards(transform.position, destination, moveSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = destination;
        isMoving = false;

        if (AnalyticsManager.Instance != null)
        {
            // Count a step only after the player successfully reaches the next tile.
            AnalyticsManager.Instance.RegisterSuccessfulStep();
        }

        TakeDamage(damagePerStep);
        FloorTrap.ApplyTrapDamage(GetTrapCheckPosition(), this);
    }

    public Vector3 GetTrapCheckPosition()
    {
        if (trapCheckPoint != null)
        {
            return trapCheckPoint.position;
        }

        if (playerCollider != null)
        {
            Bounds bounds = playerCollider.bounds;
            return new Vector3(bounds.center.x, bounds.min.y + 0.05f, transform.position.z);
        }

        return transform.position;
    }

    void CacheMovementTilemap()
    {
        if (movementTilemap != null)
        {
            return;
        }

        Tilemap[] tilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        foreach (Tilemap tilemap in tilemaps)
        {
            if (tilemap == null || !tilemap.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (floorLayer >= 0 && tilemap.gameObject.layer == floorLayer)
            {
                movementTilemap = tilemap;
                return;
            }
        }
    }

    void AlignToMovementTilemap()
    {
        if (movementTilemap == null)
        {
            return;
        }

        Vector3 referencePosition = GetMovementReferencePosition();
        Vector3Int currentCell = movementTilemap.WorldToCell(referencePosition);

        if (!movementTilemap.HasTile(currentCell))
        {
            return;
        }

        Vector3 cellCenter = movementTilemap.GetCellCenterWorld(currentCell);
        transform.position += cellCenter - referencePosition;
    }

    bool TryGetMovementDestination(Vector2 dir, out Vector3 destination)
    {
        if (movementTilemap != null)
        {
            Vector3 referencePosition = GetMovementReferencePosition();
            Vector3Int currentCell = movementTilemap.WorldToCell(referencePosition);
            Vector3Int targetCell = currentCell + Vector3Int.RoundToInt((Vector3)dir);

            if (!movementTilemap.HasTile(targetCell))
            {
                destination = transform.position;
                return false;
            }

            Vector3 currentCenter = movementTilemap.GetCellCenterWorld(currentCell);
            Vector3 targetCenter = movementTilemap.GetCellCenterWorld(targetCell);
            destination = transform.position + (targetCenter - currentCenter);
            return true;
        }

        destination = transform.position + (Vector3)dir * tileSize;
        Collider2D hit = Physics2D.OverlapCircle(destination, 0.2f, LayerMask.GetMask("Floor"));
        return hit != null;
    }

    Vector3 GetMovementReferencePosition()
    {
        return GetTrapCheckPosition();
    }

    bool NeedsGridAlignment(Vector3 position)
    {
        Vector3 snappedPosition = GetSnappedGridPosition(position);
        return Mathf.Abs(position.x - snappedPosition.x) > 0.01f
            || Mathf.Abs(position.y - snappedPosition.y) > 0.01f;
    }

    Vector3 GetSnappedGridPosition(Vector3 position)
    {
        float safeTileSize = Mathf.Max(0.01f, tileSize);
        position.x = Mathf.Round(position.x / safeTileSize) * safeTileSize;
        position.y = Mathf.Round(position.y / safeTileSize) * safeTileSize;
        return position;
    }

    void EnsureDamageFlashImage()
    {
        if (damageFlashImage == null)
        {
            Canvas canvas = null;

            if (healthSlider != null)
            {
                canvas = healthSlider.GetComponentInParent<Canvas>();
            }

            if (canvas == null)
            {
                canvas = FindFirstObjectByType<Canvas>();
            }

            if (canvas == null)
            {
                return;
            }

            Transform existingOverlay = canvas.transform.Find("DamageFlashOverlay");
            if (existingOverlay != null)
            {
                damageFlashImage = existingOverlay.GetComponent<Image>();
            }

            if (damageFlashImage == null)
            {
                GameObject overlayObject = new GameObject("DamageFlashOverlay", typeof(RectTransform), typeof(Image));
                overlayObject.transform.SetParent(canvas.transform, false);

                RectTransform rectTransform = overlayObject.GetComponent<RectTransform>();
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;

                damageFlashImage = overlayObject.GetComponent<Image>();
            }
        }

        if (damageFlashImage != null)
        {
            damageFlashImage.raycastTarget = false;
            damageFlashImage.transform.SetAsLastSibling();

            Color transparentColor = trapDamageFlashColor;
            transparentColor.a = 0f;
            damageFlashImage.color = transparentColor;
        }
    }

    void PlayTrapDamageFlash()
    {
        EnsureDamageFlashImage();
        if (damageFlashImage == null)
        {
            return;
        }

        if (damageFlashCoroutine != null)
        {
            StopCoroutine(damageFlashCoroutine);
        }

        damageFlashCoroutine = StartCoroutine(TrapDamageFlashRoutine());
    }

    IEnumerator TrapDamageFlashRoutine()
    {
        damageFlashImage.transform.SetAsLastSibling();

        Color visibleColor = trapDamageFlashColor;
        Color transparentColor = trapDamageFlashColor;
        transparentColor.a = 0f;

        damageFlashImage.color = visibleColor;

        float duration = Mathf.Max(0.01f, trapDamageFlashDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            damageFlashImage.color = Color.Lerp(visibleColor, transparentColor, t);
            yield return null;
        }

        damageFlashImage.color = transparentColor;
        damageFlashCoroutine = null;
    }

    IEnumerator AutoSaveOnSceneStartRoutine()
    {
        yield return null;

        if (autoSaveDelay > 0f)
        {
            yield return new WaitForSeconds(autoSaveDelay);
        }

        if (Gamemanager.instance == null || currentHP <= 0)
        {
            yield break;
        }

        Gamemanager.instance.SaveGame();
    }

    public void TakeDamage(int amount, bool showTrapFlash = false)
    {
        if (currentHP <= 0)
        {
            return;
        }

        currentHP -= amount;
        currentHP = Mathf.Max(currentHP, 0);

        if (healthSlider != null)
        {
            healthSlider.value = currentHP;
        }

        if (AnalyticsManager.Instance != null)
        {
            AnalyticsManager.Instance.UpdateCurrentHP(currentHP);
        }

        if (showTrapFlash)
        {
            PlayTrapDamageFlash();
        }

        if (currentHP <= 0)
        {
            if (audioSource != null && gameOverSound != null)
            {
                audioSource.PlayOneShot(gameOverSound);
            }

            if (AnalyticsManager.Instance != null)
            {
                string causeOfDeath = showTrapFlash ? "Trap" : "Insufficient HP";
                AnalyticsManager.Instance.HandlePlayerDeath(causeOfDeath, transform.position, currentHP);
            }

            die.SetActive(true);
            Debug.Log("Player is dead!");
            this.enabled = false;
        }
    }

    public void Heal(int amount)
    {
        
        if (currentHP <= 0)
        {
            return;
        }
        if (audioSource != null && healSound != null)
        {
            audioSource.PlayOneShot(healSound);
        }
        currentHP += amount;
        currentHP = Mathf.Min(currentHP, maxHP);

        if (healthSlider != null)
        {
            healthSlider.value = currentHP;
        }

        if (AnalyticsManager.Instance != null)
        {
            AnalyticsManager.Instance.UpdateCurrentHP(currentHP);
        }

        Debug.Log($"Player healed! HP = {currentHP}/{maxHP}");
    }

    public void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Save"))
        {
            if (audioSource != null && gameSaveSound != null)
            {
                audioSource.PlayOneShot(gameSaveSound);
            }
            Gamemanager.instance.SaveGame();
        }
        if (other.CompareTag("Win"))
        {
            if (audioSource != null && gameWinSound != null)
            {
                audioSource.PlayOneShot(gameWinSound);
            }

            if (AnalyticsManager.Instance != null)
            {
                AnalyticsManager.Instance.HandleLevelClear(currentHP);
            }

            win.SetActive(true);
           
        }
    }
}
