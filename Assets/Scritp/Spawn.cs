using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PotionSpawner : MonoBehaviour
{
    public static PotionSpawner instance;

    private void Awake()
    {
        if (instance == null) instance = this;

        
    }

    public Tilemap tilemap;
    public GameObject itemPrefab;
    public List<SpawnZone> zones = new List<SpawnZone>();
    public float minDistance = 1.2f;

    [Header("Zone Gizmos")]
    public bool showZoneGizmos = true;
    public bool alwaysShowZoneGizmos = false;
    public bool fillZoneGizmos = true;
    [Range(0f, 1f)] public float zoneFillAlpha = 0.12f;
    public float zoneGizmoDepth = 0.05f;

    private List<Vector3> usedPositions = new List<Vector3>();

    void Start()
    {
        // ถ้าไม่มี Save -> เริ่มเกมใหม่ (สุ่ม)
        if (!PlayerPrefs.HasKey("save"))
        {
            SpawnRandom();
        }
        else
        {
            // ถ้ามี Save -> ให้ GameManager จัดการ Load เอง
            Gamemanager.instance.LoadGame();
        }
    }

    // ฟังก์ชันสำหรับเริ่มเกมใหม่ (New Game)
    public void SpawnRandom()
    {
        ClearPotions(); // ล้างของเก่าก่อน (เผื่อมี)
        usedPositions.Clear();

        foreach (var zone in zones)
        {
            int attempts = 0;
            int spawned = 0;

            while (spawned < zone.spawnCount && attempts < zone.spawnCount * 30)
            {
                attempts++;
                int x = Random.Range(zone.minCell.x, zone.maxCell.x + 1);
                int y = Random.Range(zone.minCell.y, zone.maxCell.y + 1);

                Vector3Int cell = new Vector3Int(x, y, 0);
                if (!tilemap.HasTile(cell)) continue;

                Vector3 worldPos = tilemap.GetCellCenterWorld(cell);
                if (IsTooClose(worldPos)) continue;

                InstantiatePotion(worldPos);

                usedPositions.Add(worldPos);
                spawned++;
            }
        }
    }

    
    public void LoadPotionsFromSave(List<Vector3> savedPositions)
    {
        ClearPotions(); 
        usedPositions.Clear();

        if (savedPositions == null) return;

        foreach (Vector3 pos in savedPositions)
        {
            InstantiatePotion(pos);
            usedPositions.Add(pos); 
        }
    }

    void InstantiatePotion(Vector3 pos)
    {
        GameObject obj = Instantiate(itemPrefab, pos, Quaternion.identity);
        obj.tag = "Potion"; 
        Potion p = obj.GetComponent<Potion>();
        if (p != null) p.potionID = $"{pos.x}_{pos.y}";
    }

    void ClearPotions()
    {
        foreach (var obj in GameObject.FindGameObjectsWithTag("Potion"))
        {
            Destroy(obj);
        }
    }

    bool IsTooClose(Vector3 pos)
    {
        foreach (var p in usedPositions)
            if (Vector3.Distance(p, pos) < minDistance) return true;
        return false;
    }

    void OnDrawGizmos()
    {
        if (alwaysShowZoneGizmos)
        {
            DrawZoneGizmos();
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!alwaysShowZoneGizmos)
        {
            DrawZoneGizmos();
        }
    }

    void DrawZoneGizmos()
    {
        if (!showZoneGizmos || tilemap == null || zones == null)
        {
            return;
        }

        foreach (SpawnZone zone in zones)
        {
            Bounds bounds = GetZoneBounds(zone);

            if (fillZoneGizmos)
            {
                Color fillColor = zone.gizmoColor;
                fillColor.a = zoneFillAlpha;
                Gizmos.color = fillColor;
                Gizmos.DrawCube(bounds.center, bounds.size);
            }

            Gizmos.color = zone.gizmoColor;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }

    Bounds GetZoneBounds(SpawnZone zone)
    {
        int minX = Mathf.Min(zone.minCell.x, zone.maxCell.x);
        int maxX = Mathf.Max(zone.minCell.x, zone.maxCell.x);
        int minY = Mathf.Min(zone.minCell.y, zone.maxCell.y);
        int maxY = Mathf.Max(zone.minCell.y, zone.maxCell.y);

        Vector3Int minCell = new Vector3Int(minX, minY, 0);
        Vector3Int maxCellExclusive = new Vector3Int(maxX + 1, maxY + 1, 0);

        Vector3 minWorld = tilemap.CellToWorld(minCell);
        Vector3 maxWorld = tilemap.CellToWorld(maxCellExclusive);

        Vector3 center = (minWorld + maxWorld) * 0.5f;
        Vector3 size = maxWorld - minWorld;
        size.z = Mathf.Max(zoneGizmoDepth, 0.01f);

        return new Bounds(center, size);
    }

    [System.Serializable]
    public class SpawnZone
    {
        public Vector2Int minCell;
        public Vector2Int maxCell;
        public int spawnCount = 3;
        public Color gizmoColor = new Color(0.2f, 1f, 0.35f, 1f);
    }
}
