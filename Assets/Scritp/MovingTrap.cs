using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class MovingTrap : MonoBehaviour
{
    [Header("Path")]
    public Transform pointA;
    public Transform pointB;
    public Vector3 startOffset = Vector3.zero;
    public Vector3 endOffset = Vector3.right * 3f;
    public bool snapToStartPoint = true;
    public float moveSpeed = 2f;
    public float waitTimeAtEnds = 0.2f;
    public float reachDistance = 0.02f;

    [Header("Damage")]
    public int damage = 2;
    public float damageCooldown = 0.5f;

    private Rigidbody2D trapBody;
    private Collider2D trapCollider;
    private Vector3 fallbackPointA;
    private Vector3 fallbackPointB;
    private Vector3 runtimePointA;
    private Vector3 runtimePointB;
    private bool hasCachedRuntimePoints;
    private int targetIndex = 1;
    private float waitTimer;
    private float nextDamageTime;

    void Reset()
    {
        CacheComponents();
        ConfigurePhysics();
    }

    void Awake()
    {
        CacheComponents();
        ConfigurePhysics();

        Vector3 startPosition = transform.position;
        fallbackPointA = startPosition + startOffset;
        fallbackPointB = startPosition + endOffset;
    }

    void Start()
    {
        CacheRuntimePoints();

        if (snapToStartPoint)
        {
            Vector3 startPoint = GetPointPosition(0);
            transform.position = startPoint;
            trapBody.position = startPoint;
        }

        targetIndex = 1;
    }

    void OnValidate()
    {
        moveSpeed = Mathf.Max(0.1f, moveSpeed);
        waitTimeAtEnds = Mathf.Max(0f, waitTimeAtEnds);
        reachDistance = Mathf.Max(0.001f, reachDistance);
        damage = Mathf.Max(1, damage);
        damageCooldown = Mathf.Max(0f, damageCooldown);
    }

    void FixedUpdate()
    {
        if (waitTimer > 0f)
        {
            waitTimer -= Time.fixedDeltaTime;
            return;
        }

        Vector2 currentPosition = trapBody.position;
        Vector2 targetPosition = GetPointPosition(targetIndex);
        Vector2 nextPosition = Vector2.MoveTowards(currentPosition, targetPosition, moveSpeed * Time.fixedDeltaTime);

        trapBody.MovePosition(nextPosition);

        if (Vector2.Distance(nextPosition, targetPosition) <= reachDistance)
        {
            targetIndex = 1 - targetIndex;
            waitTimer = waitTimeAtEnds;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryDamagePlayer(other);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        TryDamagePlayer(collision.collider);
    }

    void CacheComponents()
    {
        trapBody = GetComponent<Rigidbody2D>();
        trapCollider = GetComponent<Collider2D>();
    }

    void ConfigurePhysics()
    {
        if (trapBody != null)
        {
            trapBody.bodyType = RigidbodyType2D.Kinematic;
            trapBody.gravityScale = 0f;
            trapBody.freezeRotation = true;
            trapBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        if (trapCollider != null)
        {
            trapCollider.isTrigger = true;
        }
    }

    Vector3 GetPointPosition(int index)
    {
        if (Application.isPlaying && hasCachedRuntimePoints)
        {
            return index == 0 ? runtimePointA : runtimePointB;
        }

        if (index == 0)
        {
            if (pointA != null)
            {
                return pointA.position;
            }

            return fallbackPointA;
        }

        if (pointB != null)
        {
            return pointB.position;
        }

        return fallbackPointB;
    }

    void CacheRuntimePoints()
    {
        runtimePointA = pointA != null ? pointA.position : fallbackPointA;
        runtimePointB = pointB != null ? pointB.position : fallbackPointB;
        hasCachedRuntimePoints = true;
    }

    void TryDamagePlayer(Collider2D other)
    {
        if (Time.time < nextDamageTime)
        {
            return;
        }

        Player player = other.GetComponent<Player>();
        if (player == null)
        {
            player = other.GetComponentInParent<Player>();
        }

        if (player == null)
        {
            return;
        }

        player.TakeDamage(damage, true);
        nextDamageTime = Time.time + damageCooldown;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 from = GetPointPosition(0);
        Vector3 to = GetPointPosition(1);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(from, to);
        Gizmos.DrawWireSphere(from, 0.12f);
        Gizmos.DrawWireSphere(to, 0.12f);
    }
}
