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
    public string environmentName = "Box Arena";

    [Tooltip("Texture Arena altındaki Environment GameObject'i")]
    public GameObject environmentObject;

    [Tooltip("Bu arenanın altındaki Spectators (Seyirci) GameObject'i")]
    public GameObject spectatorsObject;

    [Tooltip("Bu arenanın önizleme (RenderTexture) Kamerası")]
    public Camera previewCamera;

    [Tooltip("Saha Sınır Şekli: OctagonPosts (Kafes & 8 Direk) veya Circle (Dairesel Alan)")]
    public ArenaShape arenaShape = ArenaShape.OctagonPosts;

    [Tooltip("Sahanın dünya koordinatlarındaki merkezi")]
    public Vector3 center = new Vector3(-999.73f, 0.1f, -0.76f);

    [Tooltip("Sahanın iç yarıçapı (Karakterlerin dışına çıkamayacağı sınır)")]
    public float radius = 2.65f;

    [Tooltip("Bu sahada oyuncunun doğacağı pozisyon")]
    public Vector3 playerSpawnPosition = new Vector3(-1000f, 0.1f, 1.0f);

    [Tooltip("Bu sahada oyuncunun başlangıç bakış açısı (Y rotasyonu)")]
    public float playerSpawnRotationY = 180f;

    [Tooltip("Bu sahada düşmanların doğacağı merkez pozisyon")]
    public Vector3 enemySpawnPosition = new Vector3(-1000f, 0.1f, -2.2f);

    [Tooltip("Bu sahada düşmanların başlangıç bakış açısı (Y rotasyonu)")]
    public float enemySpawnRotationY = 0f;

    [Tooltip("Sahanın taban zemin Y yüksekliği")]
    public float groundY = 0.1f;
}

/// <summary>
/// Çoklu Saha & Ring Sınırlandırma Sistemi.
/// Texture Arena altındaki 3 Environment'ı (0: Box Arena, 1: Ancient Chinese Arena, 2: Viking Arena) yönetir.
/// Arena seçim ekranında tüm seyircileri (Spectators) gizler, arena seçildikten sonra aktif arenanın seyircilerini görünür yapar.
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

    [Header("ScriptableObject Veritabanı")]
    [Tooltip("3 arenanın koordinat ve sınır bilgilerini tutan ScriptableObject (Resources/ArenaDatabase)")]
    [SerializeField] private ArenaDatabaseSO arenaDatabase;

    [Header("Environment & Saha Listesi (Texture Arena Altındaki 3 Ortam)")]
    [Tooltip("Texture Arena altındaki dövüş sahaları (0: Box Arena, 1: Ancient Chinese Arena, 2: Viking Arena)")]
    [SerializeField] private List<ArenaEnvironmentConfig> environments = new List<ArenaEnvironmentConfig>();

    [Tooltip("Şu anda aktif olan saha indeksi (0: Box Arena, 1: Ancient Chinese Arena, 2: Viking Arena)")]
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

    private GameObject textureArenaRoot;
    private bool isInGameplayMode = false;

    // Oktagon kafesi için çalışma zamanında hesaplanan geometri
    private List<Vector3> postPositions = new List<Vector3>();
    private List<Vector3> inwardNormals = new List<Vector3>();
    private bool isInitialized = false;

    public int ActiveEnvironmentIndex => activeEnvironmentIndex;
    public int EnvironmentCount => environments != null ? environments.Count : 0;
    public ArenaEnvironmentConfig CurrentArenaConfig => (environments != null && activeEnvironmentIndex >= 0 && activeEnvironmentIndex < environments.Count)
        ? environments[activeEnvironmentIndex]
        : null;

    public Vector3 ArenaCenter => CurrentArenaConfig != null ? CurrentArenaConfig.center : new Vector3(-999.73f, 0.1f, -0.76f);
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

        DisableLegacyRootDuplicateArenas();
        AutoSetupEnvironmentsIfNeeded();
        InitializeCurrentArena();

        // Başlangıçta (Menüde / Arena Seçiminde) tüm seyirciler görünmez olsun, 3 arena önizleme için açık kalsın
        PrepareForMenuPreview();
    }

    void Start()
    {
        if (!isInitialized)
        {
            InitializeCurrentArena();
        }

        if (!isInGameplayMode)
        {
            PrepareForMenuPreview();
        }

        if (generatePhysicalColliders)
        {
            RebuildPhysicalColliders();
        }
    }

    /// <summary>
    /// Sahnede Texture Arena dışında kök dizinde kalmış eski kopya arena ve seyirci objelerini kapatır.
    /// Böylece sadece Texture Arena altındaki 3 environment kullanılır.
    /// </summary>
    private void DisableLegacyRootDuplicateArenas()
    {
        foreach (var rootGo in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (rootGo == null) continue;
            string trimmed = rootGo.name.Trim();

            if (trimmed.Equals("Texture Arena", StringComparison.OrdinalIgnoreCase))
            {
                textureArenaRoot = rootGo;
                if (!textureArenaRoot.activeSelf)
                {
                    textureArenaRoot.SetActive(true);
                }
                continue;
            }

            // Kök dizindeki eski kopya arenaları ve bağımsız Spectators objesini kapat
            if (trimmed.Equals("Box Arena Environment", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("Ancient Chinese Arena Environment", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("Viking Arena Environment", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("Spectators", StringComparison.OrdinalIgnoreCase))
            {
                if (rootGo.activeSelf)
                {
                    rootGo.SetActive(false);
                }
            }
        }
    }

    /// <summary>
    /// Texture Arena altındaki 3 Environment'ı (Box Arena, Ancient Chinese Arena, Viking Arena)
    /// otomatik olarak bulur, seyirci (Spectators) ve önizleme kamerası referanslarını bağlar.
    /// </summary>
    public void AutoSetupEnvironmentsIfNeeded()
    {
        if (arenaDatabase == null)
        {
            arenaDatabase = Resources.Load<ArenaDatabaseSO>("ArenaDatabase");
        }
        if (arenaDatabase != null)
        {
            arenaDatabase.EnsureInitialized();
        }

        if (textureArenaRoot == null)
        {
            DisableLegacyRootDuplicateArenas();
            if (textureArenaRoot == null)
            {
                textureArenaRoot = GameObject.Find("Texture Arena");
            }
        }

        // Eğer mevcut liste 3 arenadan azsa veya Texture Arena altındaki objelere bağlı değilse baştan kur
        bool needsRebuild = (environments == null || environments.Count < 3);
        if (!needsRebuild && textureArenaRoot != null)
        {
            for (int i = 0; i < environments.Count; i++)
            {
                var env = environments[i];
                if (env == null || env.environmentObject == null || !env.environmentObject.transform.IsChildOf(textureArenaRoot.transform))
                {
                    needsRebuild = true;
                    break;
                }
            }
        }

        if (needsRebuild)
        {
            environments = new List<ArenaEnvironmentConfig>();

            GameObject boxEnvGo = FindTextureArenaChild("Box Arena");
            GameObject chineseEnvGo = FindTextureArenaChild("Chinese");
            GameObject vikingEnvGo = FindTextureArenaChild("Viking");

            // 0: Box Arena Environment
            ArenaEntry boxEntry = arenaDatabase != null ? arenaDatabase.GetArena(0) : null;
            ArenaEnvironmentConfig env0 = new ArenaEnvironmentConfig
            {
                environmentName = boxEntry != null ? boxEntry.displayName : "Box Arena",
                environmentObject = boxEnvGo,
                arenaShape = boxEntry != null ? boxEntry.arenaShape : ArenaShape.OctagonPosts,
                center = boxEntry != null ? boxEntry.center : new Vector3(-999.73f, 0.1f, -0.76f),
                radius = boxEntry != null ? boxEntry.radius : 2.65f,
                groundY = boxEntry != null ? boxEntry.groundY : 0.1f,
                playerSpawnPosition = boxEntry != null ? boxEntry.playerSpawnPosition : new Vector3(-1000f, 0.1f, 1.0f),
                playerSpawnRotationY = boxEntry != null ? boxEntry.playerSpawnRotationY : 180f,
                enemySpawnPosition = boxEntry != null ? boxEntry.enemySpawnPosition : new Vector3(-1000f, 0.1f, -2.2f),
                enemySpawnRotationY = boxEntry != null ? boxEntry.enemySpawnRotationY : 0f
            };
            BindSubObjectsAndCalibrate(env0);
            environments.Add(env0);

            // 1: Ancient Chinese Arena Environment
            ArenaEntry chineseEntry = arenaDatabase != null ? arenaDatabase.GetArena(1) : null;
            ArenaEnvironmentConfig env1 = new ArenaEnvironmentConfig
            {
                environmentName = chineseEntry != null ? chineseEntry.displayName : "Ancient Chinese Arena",
                environmentObject = chineseEnvGo,
                arenaShape = chineseEntry != null ? chineseEntry.arenaShape : ArenaShape.Circle,
                center = chineseEntry != null ? chineseEntry.center : new Vector3(-1022.5f, 0.1f, -0.6f),
                radius = chineseEntry != null ? chineseEntry.radius : 2.85f,
                groundY = chineseEntry != null ? chineseEntry.groundY : 0.1f,
                playerSpawnPosition = chineseEntry != null ? chineseEntry.playerSpawnPosition : new Vector3(-1022.5f, 0.1f, 1.2f),
                playerSpawnRotationY = chineseEntry != null ? chineseEntry.playerSpawnRotationY : 180f,
                enemySpawnPosition = chineseEntry != null ? chineseEntry.enemySpawnPosition : new Vector3(-1022.5f, 0.1f, -2.1f),
                enemySpawnRotationY = chineseEntry != null ? chineseEntry.enemySpawnRotationY : 0f
            };
            BindSubObjectsAndCalibrate(env1);
            environments.Add(env1);

            // 2: Viking Arena Environment
            ArenaEntry vikingEntry = arenaDatabase != null ? arenaDatabase.GetArena(2) : null;
            ArenaEnvironmentConfig env2 = new ArenaEnvironmentConfig
            {
                environmentName = vikingEntry != null ? vikingEntry.displayName : "Viking Arena",
                environmentObject = vikingEnvGo,
                arenaShape = vikingEntry != null ? vikingEntry.arenaShape : ArenaShape.Circle,
                center = vikingEntry != null ? vikingEntry.center : new Vector3(-1200f, 0.32f, 0.6f),
                radius = vikingEntry != null ? vikingEntry.radius : 2.85f,
                groundY = vikingEntry != null ? vikingEntry.groundY : 0.32f,
                playerSpawnPosition = vikingEntry != null ? vikingEntry.playerSpawnPosition : new Vector3(-1200f, 0.32f, 2.3f),
                playerSpawnRotationY = vikingEntry != null ? vikingEntry.playerSpawnRotationY : 180f,
                enemySpawnPosition = vikingEntry != null ? vikingEntry.enemySpawnPosition : new Vector3(-1200f, 0.32f, -1.0f),
                enemySpawnRotationY = vikingEntry != null ? vikingEntry.enemySpawnRotationY : 0f
            };
            BindSubObjectsAndCalibrate(env2);
            environments.Add(env2);
        }
        else
        {
            for (int i = 0; i < environments.Count; i++)
            {
                BindSubObjectsAndCalibrate(environments[i]);
            }
        }
    }

    private GameObject FindTextureArenaChild(string keyword)
    {
        if (textureArenaRoot != null)
        {
            foreach (Transform child in textureArenaRoot.transform)
            {
                if (child != null && child.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return child.gameObject;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Environment altındaki Spectators, Preview Camera, Light ve Floor nesnelerini bulup bağlar.
    /// Önizleme kameralarının MainCamera etiketini ve fazladan AudioListener'larını temizler.
    /// </summary>
    private void BindSubObjectsAndCalibrate(ArenaEnvironmentConfig config)
    {
        if (config == null || config.environmentObject == null) return;

        Transform envTransform = config.environmentObject.transform;

        // 1. Spectators nesnesini bul
        if (config.spectatorsObject == null)
        {
            foreach (Transform t in config.environmentObject.GetComponentsInChildren<Transform>(true))
            {
                if (t == envTransform) continue;
                if (t.name.StartsWith("Spectators", StringComparison.OrdinalIgnoreCase) ||
                    t.GetComponent<SpectatorManager>() != null)
                {
                    config.spectatorsObject = t.gameObject;
                    break;
                }
            }
        }

        // 2. Önizleme (RenderTexture) kamerasını bul ve MainCamera çakışmasını önle
        Camera[] cams = config.environmentObject.GetComponentsInChildren<Camera>(true);
        foreach (var cam in cams)
        {
            if (cam == null) continue;
            if (cam.GetComponent<FollowPlayer>() != null) continue;

            config.previewCamera = cam;

            // Camera.main'in oyuncu kamerası yerine önizleme kamerasını döndürmesini engelle!
            if (cam.CompareTag("MainCamera"))
            {
                cam.tag = "Untagged";
            }

            // Çoklu AudioListener uyarısını engelle
            AudioListener listener = cam.GetComponent<AudioListener>();
            if (listener != null)
            {
                listener.enabled = false;
            }
        }

        // 3. Box Arena altındaki Light objesi kapalıysa Point Light'ların çalışması için aktif et
        Transform lightChild = envTransform.Find("Light");
        if (lightChild != null)
        {
            if (!lightChild.gameObject.activeSelf)
            {
                lightChild.gameObject.SetActive(true);
            }
            // İçindeki ekstra Directional Light varsa sahne güneşiyle çakışmaması için sadece Point Light'ları açık tut
            foreach (var l in lightChild.GetComponentsInChildren<Light>(true))
            {
                if (l != null && l.type == LightType.Directional)
                {
                    l.gameObject.SetActive(false);
                }
                else if (l != null && l.type == LightType.Point)
                {
                    l.gameObject.SetActive(true);
                }
            }
        }

        // 4. Floor nesnesinden zemin Y yüksekliğini (groundY) hassas olarak doğrula.
        // ÖNEMLİ: Box Arena'da "Floor (1)" (y = 4.16, 180° ters çevrilmiş tavan) ve
        // Ancient Chinese Arena'da "Floor (4..7)" (duvarlar ve tavan) da "Floor" ile başladığı için
        // sadece yukarı bakan yatay zeminleri (t.up.y > 0.8f) filtreleyip EN ALTTAKİ gerçek tabanı seçiyoruz!
        Transform lowestFloor = null;
        float lowestFloorY = float.MaxValue;

        foreach (Transform t in config.environmentObject.GetComponentsInChildren<Transform>(true))
        {
            if (t == envTransform) continue;
            if (t.name.StartsWith("Floor", StringComparison.OrdinalIgnoreCase))
            {
                if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);

                // Tavanları (180° dönük -> t.up.y ≈ -1) ve yan duvarları (90° dönük -> t.up.y ≈ 0) ele
                if (Vector3.Dot(t.up, Vector3.up) < 0.8f)
                {
                    continue;
                }

                if (t.position.y < lowestFloorY)
                {
                    lowestFloorY = t.position.y;
                    lowestFloor = t;
                }
            }
        }

        if (lowestFloor != null)
        {
            // Box Arena ve Chinese Arena'da gerçek Floor.y = -0.49 iken karakterler groundY = 0.1'de durur (fark +0.59)
            float calculatedGroundY = Mathf.Round((lowestFloorY + 0.59f) * 100f) / 100f;
            config.groundY = calculatedGroundY;
            config.center.y = calculatedGroundY;
            config.playerSpawnPosition.y = calculatedGroundY;
            config.enemySpawnPosition.y = calculatedGroundY;
        }
    }

    /// <summary>
    /// Menüde ve Arena Seçimi ekranında çağrılır:
    /// Texture Arena altındaki 3 ortamı ve önizleme kameralarını açık tutar (RenderTexture'lar görünsün diye),
    /// ancak 3 ortamın altındaki tüm Spectators (seyirci) nesnelerini GÖRÜNMEZ (SetActive(false)) yapar.
    /// </summary>
    public void PrepareForMenuPreview()
    {
        isInGameplayMode = false;
        AutoSetupEnvironmentsIfNeeded();
        if (environments == null) return;

        if (textureArenaRoot != null && !textureArenaRoot.activeSelf)
        {
            textureArenaRoot.SetActive(true);
        }

        for (int i = 0; i < environments.Count; i++)
        {
            var env = environments[i];
            if (env == null) continue;

            if (env.environmentObject != null && !env.environmentObject.activeSelf)
            {
                env.environmentObject.SetActive(true);
            }

            if (env.previewCamera != null)
            {
                env.previewCamera.gameObject.SetActive(true);
                env.previewCamera.enabled = true;
            }

            // Arena seçiminde (ve menüde) seyirciler görünmez olsun
            if (env.spectatorsObject != null && env.spectatorsObject.activeSelf)
            {
                env.spectatorsObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Aktif sahanın geometrik verilerini (Oktagon direkleri veya daire sınırlarını) hazırlar
    /// </summary>
    public void InitializeArena() => InitializeCurrentArena();

    public void InitializeCurrentArena()
    {
        if (environments == null || environments.Count < 3)
        {
            AutoSetupEnvironmentsIfNeeded();
        }

        var config = CurrentArenaConfig;
        if (config == null) return;

        BindSubObjectsAndCalibrate(config);

        if (config.arenaShape == ArenaShape.OctagonPosts)
        {
            InitializeOctagonArena(config);
        }
        else
        {
            isInitialized = true;
        }

        Debug.Log($"<color=green>[RingBoundary]</color> Aktif Saha: {config.environmentName} | Merkez: {config.center} | Zemin Y: {config.groundY:F2} | Yarıçap: {config.radius:F2}m | Şekil: {config.arenaShape}");
    }

    /// <summary>
    /// Sadece ilgili ArenaEnvironmentConfig'in kendi environmentObject hiyerarşisi içindeki
    /// Oktagon kafesini ve köşe direklerini tespit edip geometrisini kurar.
    /// </summary>
    void InitializeOctagonArena(ArenaEnvironmentConfig config)
    {
        postPositions.Clear();
        inwardNormals.Clear();

        Transform cornersParent = null;

        if (config.environmentObject != null)
        {
            foreach (var t in config.environmentObject.GetComponentsInChildren<Transform>(true))
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

            if (cornersParent == null)
            {
                foreach (var t in config.environmentObject.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.Equals("Corners", StringComparison.OrdinalIgnoreCase) && t.childCount >= 4)
                    {
                        cornersParent = t;
                        break;
                    }
                }
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
                foreach (var t in config.environmentObject.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.StartsWith("EdgeCorner", StringComparison.OrdinalIgnoreCase))
                    {
                        postPositions.Add(t.position);
                    }
                }
            }
        }

        if (postPositions.Count >= 6)
        {
            Vector3 sum = Vector3.zero;
            foreach (var p in postPositions) sum += p;
            config.center = sum / postPositions.Count;
            config.center.y = config.groundY;

            // Spawn noktalarını sahanın gerçek merkezine göre hizala
            config.playerSpawnPosition = new Vector3(config.center.x - 0.27f, config.groundY, config.center.z + 1.76f);
            config.enemySpawnPosition = new Vector3(config.center.x - 0.27f, config.groundY, config.center.z - 1.44f);

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

                Vector2 pFlat = new Vector2(pCurrent.x, pCurrent.z);
                Vector2 cFlat = new Vector2(config.center.x, config.center.z);
                float dist = Vector2.Distance(pFlat, cFlat);
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
    /// Kullanıcı arenayı seçtikten sonra (veya maç başlarken) seçilen Texture Arena ortamını aktif eder,
    /// o arenanın altındaki Spectators (seyirci) nesnesini GÖRÜNÜR (SetActive(true)) yapar,
    /// diğer arenaların seyircilerini gizler ve fiziksel sınırları kurar.
    /// </summary>
    public void SwitchEnvironment(int targetIndex = 0)
    {
        AutoSetupEnvironmentsIfNeeded();
        if (environments == null || environments.Count == 0) return;

        isInGameplayMode = true;
        activeEnvironmentIndex = Mathf.Clamp(targetIndex, 0, environments.Count - 1);

        ApplyEnvironmentActiveStates();
        InitializeCurrentArena();

        var activeEnv = CurrentArenaConfig;
        if (activeEnv == null) return;

        if (generatePhysicalColliders)
        {
            RebuildPhysicalColliders();
        }

        Debug.Log($"<color=cyan>[RingBoundary]</color> Seçilen Arena Aktif ({activeEnvironmentIndex}): <color=yellow>{activeEnv.environmentName}</color> | Merkez: {activeEnv.center} | Seyirciler: AÇIK");
    }

    /// <summary>
    /// Oyun sırasında seçilen Environment'ı ve onun altındaki Spectators nesnesini aktif eder.
    /// </summary>
    private void ApplyEnvironmentActiveStates()
    {
        if (environments == null) return;

        if (textureArenaRoot != null && !textureArenaRoot.activeSelf)
        {
            textureArenaRoot.SetActive(true);
        }

        for (int i = 0; i < environments.Count; i++)
        {
            var env = environments[i];
            if (env == null) continue;

            bool isSelected = (i == activeEnvironmentIndex);

            if (env.environmentObject != null)
            {
                if (env.environmentObject.activeSelf != isSelected)
                {
                    env.environmentObject.SetActive(isSelected);
                }
            }

            // Seçilen arenanın seyircilerini (Spectators) görünür yap, diğerlerini gizle!
            if (env.spectatorsObject != null)
            {
                bool showSpectators = isInGameplayMode && isSelected;
                if (env.spectatorsObject.activeSelf != showSpectators)
                {
                    env.spectatorsObject.SetActive(showSpectators);
                }
            }

            // Oyun esnasında RenderTexture önizleme kameralarını kapatarak ekstra GPU performansı kazan
            if (env.previewCamera != null)
            {
                env.previewCamera.enabled = !isInGameplayMode;
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
                postColGo.transform.position = new Vector3(postPositions[i].x, activeEnv.groundY + 1.5f, postPositions[i].z);

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
                Vector3 posOnPerimeter = new Vector3(
                    activeEnv.center.x + Mathf.Sin(angle) * activeEnv.radius,
                    activeEnv.groundY + 1.5f,
                    activeEnv.center.z + Mathf.Cos(angle) * activeEnv.radius);

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

        // --- DAİRESEL SAHA SINIRLANDIRMASI (Ancient Chinese Arena & Viking Arena) ---
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

        // --- OKTAGON KAFES SINIRLANDIRMASI (Box Arena) ---
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

        // 3. Dairesel Saha Çizimi
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
        // 4. Oktagon Saha Çizimi
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
