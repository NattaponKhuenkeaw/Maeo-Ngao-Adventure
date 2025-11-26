using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using UnityEngine.UI;

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
    public static Player instance;

    [Header("Movement Settings")]
    public float tileSize = 1f;
    public float moveSpeed = 4f;
    public bool snapToGridOnStart = true;

    [Header("Health Settings")]
    public int maxHP = 10;
    public int currentHP;
    public int damagePerStep = 1;

    [Header("Sprites")]
    public Sprite spriteUp;
    public Sprite spriteDown;
    public Sprite spriteLeft;
    public Sprite spriteRight;

    private SpriteRenderer spriteRenderer;
    private Vector2 moveInput;
    private bool isMoving = false;

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        die.SetActive(false);
        win.SetActive(false);

        if (snapToGridOnStart)
        {
            Vector3 p = transform.position;
            p.x = Mathf.Round(p.x);
            p.y = Mathf.Round(p.y);
            transform.position = p;
        }

        spriteRenderer = GetComponent<SpriteRenderer>();
        currentHP = maxHP;

        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHP;
            healthSlider.value = currentHP;
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

        
        Vector3 destination = transform.position + (Vector3)dir * tileSize;

        
        Collider2D hit = Physics2D.OverlapCircle(destination, 0.2f, LayerMask.GetMask("Floor"));

        if (hit != null)
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

        TakeDamage(damagePerStep);
    }

    void TakeDamage(int amount)
    {
        currentHP -= amount;
        currentHP = Mathf.Max(currentHP, 0);

        if (healthSlider != null)
        {
            healthSlider.value = currentHP;
        }

        if (currentHP <= 0)
        {
            if (audioSource != null && gameOverSound != null)
            {
                audioSource.PlayOneShot(gameOverSound);
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
            win.SetActive(true);
           
        }
    }
}
