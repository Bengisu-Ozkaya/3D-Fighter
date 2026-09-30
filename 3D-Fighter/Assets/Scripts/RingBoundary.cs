using System;
using System.Collections.Generic;
using UnityEngine;

public enum ArenaShape
{
    OctagonPosts,
    Circle
}

[System.Serializable]
public class ArenaEnvironmentConfig
{
    [Tooltip("Saha / Ortam Adı")]
    public string environmentName = "Environment 1";

    [Tooltip("Hierarchy'deki Environment GameObject'i (Örn: Environment 1, Environment 2)")]
    public GameObject environmentObject;

    [Tooltip("Saha Sınır Şekli: OctagonPosts (Kafes & 8 Direk) veya Circle (Dairesel Alan)")]
    public ArenaShape arenaShape = ArenaShape.OctagonPosts;

    [Tooltip("Sahanın dünya koordinatlarındaki merkezi")]
    public Vector3 center = new Vector3(0.27f, 0f, 2.67f);

    [Tooltip("Sahanın iç yarıçapı (Karakterlerin dışına çıkamayacağı sınır)")]
    public float radius = 2.65f;

    [Tooltip("Bu sahada oyuncunun doğacağı pozisyon")]
    public Vector3 playerSpawnPosition = new Vector3(0f, 0f, 4.7f);

    [Tooltip("Bu sahada oyuncunun başlangıç bakış açısı (Y rotasyonu)")]
    public float playerSpawnRotationY = 180f;

    [Tooltip("Bu sahada düşmanların doğacağı merkez pozisyon")]
    public Vector3 enemySpawnPosition = new Vector3(0f, 0f, 0.5f);

    [Tooltip("Bu sahada düşmanların başlangıç bakış açısı (Y rotasyonu)")]
    public float enemySpawnRotationY = 0f;

    [Tooltip("Sahanın taban zemin Y yüksekliği")]
    public float groundY = 0f;
}

/// <summary>
/// Çoklu Saha & Ring Sınırlandırma Sistemi.
/// Her 3 dalgada bir çevre (Environment) değiştiğinde sahanın geometrik sınırını,
/// köşe direklerini ve dairesel duvarlarını dinamik olarak yönetir.
/// Karakterlerin ringden ve evlerle çevrili dairesel köy alanından dışarı çıkmasını engeller.
/// </summary>
[DefaultExecutionOrder(-100)]
public class RingBoundary : MonoBehaviour
{
    private static RingBoundary _instance;
    public static RingBoundary Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<RingBoundary>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("RingBoundary");
                    _instance = go.AddComponent<RingBoundary>();
                }
            }
            return _instance;
        }
    }

    [Header("Environment & Saha Listesi")]
    [Tooltip("Oyundaki tüm dövüş sahaları. Her 3 dalgada bir bu listedeki sahalar sırasıyla aktifleşir.")]
    [SerializeField] private List<ArenaEnvironmentConfig> environments = new List<ArenaEnvironmentConfig>();

    [Tooltip("Şu anda aktif olan saha indeksi (0: Environment 1, 1: Environment 2)")]
    [SerializeField] private int activeEnvironmentIndex = 0;

    [Header("Köşe Direği & Duvar Ayarları")]
    [Tooltip("Köşe direklerinin fiziksel yarıçapı (Oktagon için)")]
    [SerializeField] private float postRadius = 0.32f;

    [Tooltip("Duvarlardan karakter gövdesine bırakılacak ekstra güvenlik mesafesi")]
    [SerializeField] private float wallMargin = 0.05f;

    [Tooltip("Otomatik olarak direklere ve tel duvarlara 3D fizik collider'ları ekler")]
    [SerializeField] private bool generatePhysicalColliders = true;

    [Header("Gizmo / Editör Görselleştirme")]
    [SerializeField] private bool showGizmos = true;

    // Oktagon kafesi için çalışma zamanında hesaplanan geometri
    private List<Vector3> postPositions = new List<Vector3>();
    private List<Vector3> inwardNormals = new List<Vector3>();
    private bool isInitialized = false;

    public int ActiveEnvironmentIndex => activeEnvironmentIndex;
    public int EnvironmentCount => environments != null ? environments.Count : 0;
    public ArenaEnvironmentConfig CurrentArenaConfig => (environments != null && activeEnvironmentIndex >= 0 && activeEnvironmentIndex < environments.Count)
        ? environments[activeEnvironmentIndex]
        : null;

    public Vector3 ArenaCenter => CurrentArenaConfig != null ? CurrentArenaConfig.center : new Vector3(0.27f, 0f, 2.67f);
    public float PostRadius => postRadius;

    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        AutoSetupEnvironmentsIfNeeded();
        InitializeCurrentArena();
    }

    void Start()
    {
        if (!isInitialized)
        {
            InitializeCurrentArena();
        }

        // Başlangıçta 0. sahayı (Environment 1) aktif kıl
        ApplyEnvironmentActiveStates();

        if (generatePhysicalColliders)
        {
            RebuildPhysicalColliders();
        }
    }

    /// <summary>
    /// Eğer Inspector'dan environments listesi girilmemişse sahnedeki Environment 1 ve 2'yi otomatik bulup kurar
    /// </summary>
    public void AutoSetupEnvironmentsIfNeeded()
    {
        if (environments == null)
        {
            environments = new List<ArenaEnvironmentConfig>();
        }

        if (environments.Count == 0)
        {
            // Sahnedeki Environment objelerini bul
            GameObject env1Go = null;
            GameObject env2Go = null;

            foreach (var rootGo in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                string trimmed = rootGo.name.Trim();
                if (trimmed.Equals("Environment 1", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("Environment1", StringComparison.OrdinalIgnoreCase))
                {
                    env1Go = rootGo;
                }
                else if (trimmed.Equals("Environment 2", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("Environment2", StringComparison.OrdinalIgnoreCase))
                {
                    env2Go = rootGo;
                }
            }

            // 1. Environment 1 (Oktagon Kafesi)
            ArenaEnvironmentConfig env1 = new ArenaEnvironmentConfig
            {
                environmentName = "Environment 1 (Kafes Ring)",
                environmentObject = env1Go,
                arenaShape = ArenaShape.OctagonPosts,
                center = new Vector3(0.27f, 0f, 2.67f),
                radius = 2.65f,
                playerSpawnPosition = new Vector3(0f, 0f, 4.7f),
                playerSpawnRotationY = 180f,
                enemySpawnPosition = new Vector3(0f, 0f, 0.5f),
                enemySpawnRotationY = 0f,
                groundY = 0f
            };
            environments.Add(env1);

            // Environment 2 geçiş sistemi devre dışı bırakıldı, Environment 2 sahnede varsa kapatılır
            if (env2Go != null)
            {
                env2Go.SetActive(false);
            }
        }
        else
        {
            // Listede obje referansları boş kalmışsa sahneden otomatik eşle
            foreach (var env in environments)
            {
                if (env.environmentObject == null)
                {
                    foreach (var rootGo in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                    {
                        if (rootGo.name.Trim().IndexOf(env.environmentName.Split(' ')[0], StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            env.environmentObject = rootGo;
                            break;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Aktif sahanın geometrik verilerini (Oktagon direkleri veya daire sınırlarını) hazırlar
    /// </summary>
    public void InitializeArena() => InitializeCurrentArena();

    public void InitializeCurrentArena()
    {
        if (environments == null || environments.Count == 0)
        {
            AutoSetupEnvironmentsIfNeeded();
        }

        var config = CurrentArenaConfig;
        if (config == null) return;

        if (config.arenaShape == ArenaShape.OctagonPosts)
        {
            InitializeOctagonArena(config);
        }
        else
        {
            isInitialized = true;
        }

        Debug.Log($"<color=green>[RingBoundary]</color> Aktif Saha: {config.environmentName} | Merkez: {config.center} | Yarıçap: {config.radius:F2}m | Şekil: {config.arenaShape}");
    }

    /// <summary>
    /// Sahnedeki Oktagon kafesini ve köşe direklerini otomatik tespit edip geometrisini kurar
    /// </summary>
    void InitializeOctagonArena(ArenaEnvironmentConfig config)
    {
        postPositions.Clear();
        inwardNormals.Clear();

        Transform cornersParent = null;
        GameObject octogonGo = GameObject.Find("Octogon (1)");
        if (octogonGo == null)
        {
            foreach (var t in FindObjectsOfType<Transform>(true))
            {
                if (t.name.IndexOf("Octogon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.name.IndexOf("Octagon", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Transform c = t.Find("Corners");
                    if (c != null && c.childCount >= 4)
                    {
                        cornersParent = c;
                        break;
                    }
                }
            }
        }
        else
        {
            cornersParent = octogonGo.transform.Find("Corners");
        }

        if (cornersParent == null)
        {
            GameObject cGo = GameObject.Find("Corners");
            if (cGo != null) cornersParent = cGo.transform;
        }

        if (cornersParent != null)
        {
            foreach (Transform child in cornersParent)
            {
                postPositions.Add(child.position);
            }
        }
        else
        {
            foreach (var t in FindObjectsOfType<Transform>(true))
            {
                if (t.name.StartsWith("EdgeCorner", StringComparison.OrdinalIgnoreCase))
                {
                    postPositions.Add(t.position);
                }
            }
        }

        if (postPositions.Count >= 6)
        {
            Vector3 sum = Vector3.zero;
            foreach (var p in postPositions) sum += p;
            config.center = sum / postPositions.Count;
            config.center.y = config.groundY;

            postPositions.Sort((a, b) =>
            {
                float angleA = Mathf.Atan2(a.z - config.center.z, a.x - config.center.x);
                float angleB = Mathf.Atan2(b.z - config.center.z, b.x - config.center.x);
                return angleA.CompareTo(angleB);
            });

            float minRadius = float.MaxValue;
            for (int i = 0; i < postPositions.Count; i++)
            {
                Vector3 pCurrent = postPositions[i];
                Vector3 pNext = postPositions[(i + 1) % postPositions.Count];
                Vector3 edge = pNext - pCurrent;
                edge.y = 0f;

                Vector3 n = new Vector3(-edge.z, 0f, edge.x).normalized;
                Vector3 midPoint = (pCurrent + pNext) * 0.5f;
                Vector3 toCenter = config.center - midPoint;
                toCenter.y = 0f;

                if (Vector3.Dot(n, toCenter) < 0f) n = -n;
                inwardNormals.Add(n);

                float dist = Vector3.Distance(new Vector3(pCurrent.x, 0f, pCurrent.z), config.center);
                if (dist < minRadius) minRadius = dist;
            }

            config.radius = minRadius;
            isInitialized = true;
        }
        else
        {
            BuildFallbackOctagon(config);
        }
    }

    void BuildFallbackOctagon(ArenaEnvironmentConfig config)
    {
        config.center = new Vector3(0.27f, config.groundY, 2.67f);
        float radius = 2.78f;
        postPositions.Clear();
        inwardNormals.Clear();

        for (int i = 0; i < 8; i++)
        {
            float angle = (i * 45f + 22.5f) * Mathf.Deg2Rad;
            Vector3 pos = config.center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            postPositions.Add(pos);
        }

        for (int i = 0; i < 8; i++)
        {
            Vector3 pCurrent = postPositions[i];
            Vector3 pNext = postPositions[(i + 1) % 8];
            Vector3 edge = pNext - pCurrent;
            edge.y = 0f;

            Vector3 n = new Vector3(-edge.z, 0f, edge.x).normalized;
            Vector3 midPoint = (pCurrent + pNext) * 0.5f;
            Vector3 toCenter = config.center - midPoint;
            toCenter.y = 0f;

            if (Vector3.Dot(n, toCenter) < 0f) n = -n;
            inwardNormals.Add(n);
        }

        config.radius = radius * Mathf.Cos(22.5f * Mathf.Deg2Rad);
        isInitialized = true;
    }

    /// <summary>
    /// Ortamlar arasında geçiş yapar (Örn: Her 3 dalgada bir 0 -> 1 -> 0 döngüsü).
    /// Objeleri açıp kapatır, oyuncuyu ve spawner'ı yeni sahadaki yerlerine taşır.
    /// </summary>
    public void SwitchEnvironment(int targetIndex = 0)
    {
        if (environments == null || environments.Count == 0) return;

        // Environment 2 geçişi tamamen devre dışı bırakıldı; daima Environment 1 (Index 0) kullanılır
        activeEnvironmentIndex = 0;

        ApplyEnvironmentActiveStates();
        InitializeCurrentArena();

        var activeEnv = CurrentArenaConfig;
        if (activeEnv == null) return;

        if (generatePhysicalColliders)
        {
            RebuildPhysicalColliders();
        }

        Debug.Log($"<color=cyan>[RingBoundary]</color> Aktif Saha: <color=yellow>{activeEnv.environmentName}</color> | Merkez: {activeEnv.center} | Yarıçap: {activeEnv.radius:F2}m");
    }

    /// <summary>
    /// Hierarchy'deki Environment GameObject'lerinin aktiflik durumlarını günceller (Environment 1 aktif, Environment 2 daima pasif)
    /// </summary>
    private void ApplyEnvironmentActiveStates()
    {
        if (environments == null) return;

        for (int i = 0; i < environments.Count; i++)
        {
            if (environments[i].environmentObject != null)
            {
                bool shouldBeActive = (i == 0);
                if (environments[i].environmentObject.activeSelf != shouldBeActive)
                {
                    environments[i].environmentObject.SetActive(shouldBeActive);
                }
            }
        }

        // Sahnedeki olası Environment 2 objesini de tamamen pasif tut
        foreach (var rootGo in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            string trimmed = rootGo.name.Trim();
            if (trimmed.Equals("Environment 2", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("Environment2", StringComparison.OrdinalIgnoreCase))
            {
                if (rootGo.activeSelf)
                {
                    rootGo.SetActive(false);
                }
            }
        }
    }

    /// <summary>
    /// Fiziksel duvar collider'larını aktif sahaya göre yeniden inşa eder
    /// </summary>
    public void RebuildPhysicalColliders()
    {
        Transform wallsContainer = transform.Find("PhysicalWalls");
        if (wallsContainer == null)
        {
            GameObject containerGo = new GameObject("PhysicalWalls");
            containerGo.transform.SetParent(transform);
            containerGo.transform.localPosition = Vector3.zero;
            containerGo.transform.localRotation = Quaternion.identity;
            containerGo.transform.localScale = Vector3.one;
            wallsContainer = containerGo.transform;
        }

        // Eski collider çocuk objelerini temizle
        List<GameObject> childrenToDestroy = new List<GameObject>();
        foreach (Transform child in wallsContainer)
        {
            childrenToDestroy.Add(child.gameObject);
        }
        foreach (var go in childrenToDestroy)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        var activeEnv = CurrentArenaConfig;
        if (activeEnv == null) return;

        if (activeEnv.arenaShape == ArenaShape.OctagonPosts && postPositions.Count >= 6)
        {
            // Oktagon direk ve kenar collider'ları
            for (int i = 0; i < postPositions.Count; i++)
            {
                GameObject postColGo = new GameObject($"PostCollider_{i}");
                postColGo.transform.SetParent(wallsContainer);
                postColGo.transform.position = postPositions[i] + Vector3.up * 1.5f;

                CapsuleCollider cap = postColGo.AddComponent<CapsuleCollider>();
                cap.radius = postRadius;
                cap.height = 3.0f;
            }

            for (int i = 0; i < postPositions.Count; i++)
            {
                Vector3 p1 = postPositions[i];
                Vector3 p2 = postPositions[(i + 1) % postPositions.Count];

                GameObject wall = new GameObject($"FenceWall_{i}");
                wall.transform.SetParent(wallsContainer);

                Vector3 midPoint = (p1 + p2) * 0.5f;
                midPoint.y = activeEnv.groundY + 1.25f;
                wall.transform.position = midPoint;

                Vector3 dir = (p2 - p1);
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                {
                    wall.transform.rotation = Quaternion.LookRotation(dir);
                }

                BoxCollider box = wall.AddComponent<BoxCollider>();
                box.size = new Vector3(0.2f, 2.5f, dir.magnitude);
            }
        }
        else if (activeEnv.arenaShape == ArenaShape.Circle)
        {
            // Dairesel alan için çevreye 24 teğet kutu duvar collider'ı yerleştir
            int segments = 24;
            float angleStep = 360f / segments;
            float segLength = 2f * activeEnv.radius * Mathf.Tan(Mathf.PI / segments) * 1.05f;

            for (int i = 0; i < segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 posOnPerimeter = activeEnv.center + new Vector3(Mathf.Sin(angle) * activeEnv.radius, activeEnv.groundY + 1.5f, Mathf.Cos(angle) * activeEnv.radius);

                GameObject wall = new GameObject($"CircleWall_{i}");
                wall.transform.SetParent(wallsContainer);
                wall.transform.position = posOnPerimeter;

                Vector3 tangent = new Vector3(Mathf.Cos(angle), 0f, -Mathf.Sin(angle));
                wall.transform.rotation = Quaternion.LookRotation(tangent);

                BoxCollider box = wall.AddComponent<BoxCollider>();
                box.size = new Vector3(0.5f, 3.5f, segLength);
            }
        }
    }

    /// <summary>
    /// Karakterin pozisyonunu o an aktif olan sahanın içinde tutar.
    /// Hem oyuncu (PlayerController) hem düşmanlar (EnemyController) tarafından çağrılır.
    /// </summary>
    public static Vector3 ClampToArena(Vector3 position, float bodyRadius)
    {
        if (_instance == null)
        {
            _instance = Instance;
        }

        if (_instance == null || !_instance.isInitialized)
        {
            return position;
        }

        return _instance.InternalClamp(position, bodyRadius);
    }

    /// <summary>
    /// Aktif sahanın şekline (Oktagon veya Daire) göre sınırlandırma ve çarpışma çözümü
    /// </summary>
    public Vector3 InternalClamp(Vector3 pos, float bodyRadius)
    {
        var config = CurrentArenaConfig;
        if (config == null) return pos;

        float originalY = pos.y;

        // --- DAİRESEL SAHA SINIRLANDIRMASI (Environment 2 - Köy Alanı) ---
        if (config.arenaShape == ArenaShape.Circle)
        {
            Vector3 diff = pos - config.center;
            diff.y = 0f;

            float maxAllowedRadius = Mathf.Max(0.5f, config.radius - bodyRadius);
            float sqrDist = diff.sqrMagnitude;

            if (sqrDist > maxAllowedRadius * maxAllowedRadius)
            {
                float dist = Mathf.Sqrt(sqrDist);
                if (dist > 0.0001f)
                {
                    // Dairesel sınırın dışına taşmayı engelle, çembere teğet sınırla
                    pos = config.center + (diff / dist) * maxAllowedRadius;
                }
            }

            pos.y = originalY;
            return pos;
        }

        // --- OKTAGON KAFES SINIRLANDIRMASI (Environment 1 - Octagon Ring) ---
        pos.y = config.center.y;
        int count = postPositions.Count;
        if (count < 3) return new Vector3(pos.x, originalY, pos.z);

        // 1. Köşe Direkleri Çarpışması (Pass 1)
        float minPostDist = postRadius + bodyRadius;
        float minPostDistSq = minPostDist * minPostDist;

        for (int i = 0; i < count; i++)
        {
            Vector3 diff = pos - postPositions[i];
            diff.y = 0f;
            float sqrDist = diff.sqrMagnitude;

            if (sqrDist < minPostDistSq)
            {
                float dist = Mathf.Sqrt(sqrDist);
                if (dist > 0.0001f)
                {
                    pos = postPositions[i] + (diff / dist) * minPostDist;
                }
                else
                {
                    Vector3 toCenter = config.center - postPositions[i];
                    toCenter.y = 0f;
                    pos = postPositions[i] + toCenter.normalized * minPostDist;
                }
            }
        }

        // 2. Oktagon Kenar Duvarları (Kafes Telleri)
        float requiredMargin = bodyRadius + wallMargin;
        for (int i = 0; i < count; i++)
        {
            Vector3 edgeStart = postPositions[i];
            Vector3 normal = inwardNormals[i];

            Vector3 toPos = pos - edgeStart;
            toPos.y = 0f;
            float signedDist = Vector3.Dot(toPos, normal);

            if (signedDist < requiredMargin)
            {
                float penetration = requiredMargin - signedDist;
                pos += normal * penetration;
            }
        }

        // 3. Köşe Direkleri Çarpışması (Pass 2)
        for (int i = 0; i < count; i++)
        {
            Vector3 diff = pos - postPositions[i];
            diff.y = 0f;
            float sqrDist = diff.sqrMagnitude;

            if (sqrDist < minPostDistSq)
            {
                float dist = Mathf.Sqrt(sqrDist);
                if (dist > 0.0001f)
                {
                    pos = postPositions[i] + (diff / dist) * minPostDist;
                }
            }
        }

        // 4. Global Yarıçap Emniyet Kilidi
        Vector3 fromCenter = pos - config.center;
        fromCenter.y = 0f;
        float maxAllowed = Mathf.Max(0.5f, config.radius - bodyRadius);
        if (fromCenter.sqrMagnitude > maxAllowed * maxAllowed)
        {
            pos = config.center + fromCenter.normalized * maxAllowed;
        }

        pos.y = originalY;
        return pos;
    }

    void OnDrawGizmos()
    {
        if (!showGizmos) return;

        var config = CurrentArenaConfig;
        if (config == null) return;

        // 1. Merkez İşareti
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(config.center + Vector3.up * 0.1f, 0.35f);

        // 2. Spawn Noktaları Görselleştirmesi
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(config.playerSpawnPosition + Vector3.up * 0.5f, 0.45f);
        Vector3 playerForward = Quaternion.Euler(0f, config.playerSpawnRotationY, 0f) * Vector3.forward;
        Gizmos.DrawRay(config.playerSpawnPosition + Vector3.up * 0.5f, playerForward * 1.2f);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(config.enemySpawnPosition + Vector3.up * 0.5f, 0.45f);
        Vector3 enemyForward = Quaternion.Euler(0f, config.enemySpawnRotationY, 0f) * Vector3.forward;
        Gizmos.DrawRay(config.enemySpawnPosition + Vector3.up * 0.5f, enemyForward * 1.2f);

        // 3. Dairesel Saha Çizimi (Environment 2)
        if (config.arenaShape == ArenaShape.Circle)
        {
            Gizmos.color = Color.cyan;
            int segments = 48;
            float angleStep = 360f / segments;
            for (int i = 0; i < segments; i++)
            {
                float a1 = i * angleStep * Mathf.Deg2Rad;
                float a2 = (i + 1) * angleStep * Mathf.Deg2Rad;

                Vector3 p1 = config.center + new Vector3(Mathf.Sin(a1) * config.radius, 0.1f, Mathf.Cos(a1) * config.radius);
                Vector3 p2 = config.center + new Vector3(Mathf.Sin(a2) * config.radius, 0.1f, Mathf.Cos(a2) * config.radius);
                Gizmos.DrawLine(p1, p2);

                Vector3 p1Top = p1 + Vector3.up * 2f;
                Vector3 p2Top = p2 + Vector3.up * 2f;
                Gizmos.DrawLine(p1Top, p2Top);
                if (i % 4 == 0) Gizmos.DrawLine(p1, p1Top);
            }
        }
        // 4. Oktagon Saha Çizimi (Environment 1)
        else if (postPositions != null && postPositions.Count >= 3)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < postPositions.Count; i++)
            {
                Vector3 p1 = postPositions[i];
                Vector3 p2 = postPositions[(i + 1) % postPositions.Count];
                Gizmos.DrawLine(p1 + Vector3.up * 0.1f, p2 + Vector3.up * 0.1f);
                Gizmos.DrawLine(p1 + Vector3.up * 1.5f, p2 + Vector3.up * 1.5f);
            }

            for (int i = 0; i < postPositions.Count; i++)
            {
                Vector3 postPos = postPositions[i];
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(postPos + Vector3.up * 0.5f, postRadius);
                Gizmos.DrawWireSphere(postPos + Vector3.up * 1.5f, postRadius);
                Gizmos.DrawLine(postPos, postPos + Vector3.up * 2.5f);
            }
        }
    }
}
