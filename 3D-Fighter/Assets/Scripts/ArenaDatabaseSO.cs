using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ArenaEntry
{
    [Tooltip("Arenanın benzersiz kimliği (Örn: BoxArena, ChineseArena, VikingArena)")]
    public string arenaId = "BoxArena";

    [Tooltip("UI ve loglarda görünecek arena adı")]
    public string displayName = "Box Arena";

    [Tooltip("Texture Arena nesnesinin altındaki Environment GameObject'inin tam adı")]
    public string hierarchyObjectName = "Box Arena Environment";

    [Tooltip("Arena seçim ekranında gösterilen RenderTexture (Assets/Arena Texture)")]
    public Texture previewTexture;

    [Tooltip("Saha sınır şekli: OctagonPosts (8 Köşe Direkli Kafes) veya Circle (Dairesel Saha)")]
    public ArenaShape arenaShape = ArenaShape.OctagonPosts;

    [Tooltip("Sahanın dünya koordinatlarındaki varsayılan merkezi")]
    public Vector3 center = new Vector3(-999.73f, 0.1f, -0.76f);

    [Tooltip("Sahanın iç dövüş yarıçapı (metre)")]
    public float radius = 2.65f;

    [Tooltip("Sahanın taban zemin Y yüksekliği")]
    public float groundY = 0.1f;

    [Tooltip("Oyuncunun bu arenada doğacağı dünya pozisyonu")]
    public Vector3 playerSpawnPosition = new Vector3(-1000f, 0.1f, 1.0f);

    [Tooltip("Oyuncunun başlangıç Y rotasyonu (derece)")]
    public float playerSpawnRotationY = 180f;

    [Tooltip("Düşmanların bu arenada doğacağı merkez dünya pozisyonu")]
    public Vector3 enemySpawnPosition = new Vector3(-1000f, 0.1f, -2.2f);

    [Tooltip("Düşmanların başlangıç Y rotasyonu (derece)")]
    public float enemySpawnRotationY = 0f;
}

/// <summary>
/// Texture Arena altındaki 3 dövüş arenasını (Box Arena, Ancient Chinese Arena, Viking Arena)
/// ve spawn/sınır koordinatlarını tutan ScriptableObject veritabanı.
/// </summary>
[CreateAssetMenu(fileName = "ArenaDatabase", menuName = "3D Fighter/Arena Database", order = 4)]
public class ArenaDatabaseSO : ScriptableObject
{
    [Tooltip("Texture Arena altındaki 3 arenanın yapılandırma listesi")]
    [SerializeField] private List<ArenaEntry> arenas = new List<ArenaEntry>();

    public IReadOnlyList<ArenaEntry> Arenas => arenas;
    public int Count => arenas != null ? arenas.Count : 0;

    public ArenaEntry GetArena(int index)
    {
        if (arenas == null || arenas.Count == 0) return null;
        int clamped = Mathf.Clamp(index, 0, arenas.Count - 1);
        return arenas[clamped];
    }

    public void SetArenas(List<ArenaEntry> newArenas)
    {
        arenas = newArenas ?? new List<ArenaEntry>();
    }

    public void EnsureInitialized()
    {
        if (arenas == null || arenas.Count < 3)
        {
            ResetToDefaults();
        }
    }

    public void ResetToDefaults()
    {
        arenas = new List<ArenaEntry>
        {
            // 0: Box Arena Environment (Texture Arena altında localPosition: -1000, 0, 0)
            new ArenaEntry
            {
                arenaId = "BoxArena",
                displayName = "Box Arena",
                hierarchyObjectName = "Box Arena Environment",
                arenaShape = ArenaShape.OctagonPosts,
                center = new Vector3(-999.73f, 0.1f, -0.76f),
                radius = 2.65f,
                groundY = 0.1f,
                playerSpawnPosition = new Vector3(-1000f, 0.1f, 1.0f),
                playerSpawnRotationY = 180f,
                enemySpawnPosition = new Vector3(-1000f, 0.1f, -2.2f),
                enemySpawnRotationY = 0f
            },
            // 1: Ancient Chinese Arena Environment (Texture Arena altında X ~ -1022.5, Z ~ -0.6)
            new ArenaEntry
            {
                arenaId = "ChineseArena",
                displayName = "Ancient Chinese Arena",
                hierarchyObjectName = "Ancient Chinese Arena Environment",
                arenaShape = ArenaShape.Circle,
                center = new Vector3(-1022.5f, 0.1f, -0.6f),
                radius = 2.85f,
                groundY = 0.1f,
                playerSpawnPosition = new Vector3(-1022.5f, 0.1f, 1.2f),
                playerSpawnRotationY = 180f,
                enemySpawnPosition = new Vector3(-1022.5f, 0.1f, -2.1f),
                enemySpawnRotationY = 0f
            },
            // 2: Viking Arena Environment (Texture Arena altında X = -1200, Floor Y = -0.27 -> groundY = 0.32)
            new ArenaEntry
            {
                arenaId = "VikingArena",
                displayName = "Viking Arena",
                hierarchyObjectName = "Viking Arena Environment",
                arenaShape = ArenaShape.Circle,
                center = new Vector3(-1200f, 0.32f, 0.6f),
                radius = 2.85f,
                groundY = 0.32f,
                playerSpawnPosition = new Vector3(-1200f, 0.32f, 2.3f),
                playerSpawnRotationY = 180f,
                enemySpawnPosition = new Vector3(-1200f, 0.32f, -1.0f),
                enemySpawnRotationY = 0f
            }
        };
    }
}
