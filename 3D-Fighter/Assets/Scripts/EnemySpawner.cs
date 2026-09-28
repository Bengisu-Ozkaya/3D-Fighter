using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public enum Difficulty
{
    Easy,
    Medium,
    Hard
}

public class EnemySpawner : MonoBehaviour
{
    [Header("Dalga ve Düşman Ayarları")]
    [SerializeField] GameObject enemyPrefab;
    [Tooltip("Önceki dalgadaki tüm düşmanlar yenildikten sonra yeni dalganın başlama gecikmesi (saniye)")]
    [SerializeField] float respawnDelay = 2.5f;
    [Tooltip("Maksimum dalga sayısı. Bu dalgadaki düşmanlar bittiğinde oyun durur (Örn: 3. dalgadan sonra)")]
    [SerializeField] int maxWaves = 3;
    [Tooltip("Birden fazla düşman doğduğunda aralarındaki yatay mesafe")]
    [SerializeField] float spawnSpacing = 1.4f;

    // Zorluk Modlarına Göre Dalga Düşman Sayıları
    private readonly int[] easyWaveCounts = new int[] { 1, 2, 3 };
    private readonly int[] midWaveCounts = new int[] { 3, 5, 7 };
    private readonly int[] hardWaveCounts = new int[] { 5, 7, 10 };

    private int[] currentWaveCounts = new int[] { 1, 2, 3 };
    private Difficulty currentDifficulty = Difficulty.Easy;

    private int wave = 1;
    private List<EnemyController> activeEnemies = new List<EnemyController>();
    private bool isGameStarted = false;
    private bool isSpawning = false;
    private bool isWavesCompleted = false;

    public int CurrentWave => wave;
    public bool IsWavesCompleted => isWavesCompleted;
    public bool IsGameStarted => isGameStarted;
    public Difficulty CurrentDifficulty => currentDifficulty;

    void Start()
    {
        // UI butonuna basılana kadar oyun beklemede kalır
    }

    /// <summary>
    /// Kolay Modu başlatır (1. Dalga: 1, 2. Dalga: 2, 3. Dalga: 3 Düşman)
    /// </summary>
    public void StartEasyMode()
    {
        StartGame(Difficulty.Easy);
    }

    /// <summary>
    /// Orta Modu başlatır (1. Dalga: 3, 2. Dalga: 5, 3. Dalga: 7 Düşman)
    /// </summary>
    public void StartMidMode()
    {
        StartGame(Difficulty.Medium);
    }

    /// <summary>
    /// Zor Modu başlatır (1. Dalga: 5, 2. Dalga: 7, 3. Dalga: 10 Düşman)
    /// </summary>
    public void StartHardMode()
    {
        StartGame(Difficulty.Hard);
    }

    /// <summary>
    /// Seçilen zorluk derecesiyle oyunu ve ilk dalgayı başlatır
    /// </summary>
    public void StartGame(Difficulty difficulty)
    {
        if (isGameStarted) return;

        currentDifficulty = difficulty;

        switch (difficulty)
        {
            case Difficulty.Easy:
                currentWaveCounts = easyWaveCounts;
                break;
            case Difficulty.Medium:
                currentWaveCounts = midWaveCounts;
                break;
            case Difficulty.Hard:
                currentWaveCounts = hardWaveCounts;
                break;
        }

        maxWaves = currentWaveCounts.Length;
        wave = 1;
        isWavesCompleted = false;
        isGameStarted = true;

        Debug.Log($"<color=cyan>[OYUN BAŞLADI]</color> Mod: {difficulty} | Toplam Dalga: {maxWaves}");

        // İlk dalgayı başlat
        StartCoroutine(SpawnEnemyRoutine(wave, true));
    }

    void Update()
    {
        // Oyun başlamadıysa veya dalgalar tamamlandıysa bekle
        if (!isGameStarted || isWavesCompleted) return;

        // Ölen veya Destroy edilen düşmanları listeden temizle
        activeEnemies.RemoveAll(e => e == null || e.IsDead);

        // Sahnede hiç canlı düşman kalmadıysa ve yeni dalga henüz başlatılmadıysa
        if (activeEnemies.Count == 0 && !isSpawning)
        {
            // Belirlenen maksimum dalgaya ulaşıldı ve o dalgadaki tüm düşmanlar yenildiyse
            if (wave >= maxWaves)
            {
                StartCoroutine(WavesCompletedRoutine());
                return;
            }

            // Bir sonraki dalgaya geç (1 -> 2, 2 -> 3 gibi)
            wave++;
            StartCoroutine(SpawnEnemyRoutine(wave, false));
        }
    }

    IEnumerator WavesCompletedRoutine()
    {
        isWavesCompleted = true;
        Debug.Log($"<color=green>[TEBRİKLER!]</color> {currentDifficulty} modunda tüm dalgalar tamamlandı!");

        // 1. Oyuncunun Show Pose zafer animasyonunun baştan sona oynayıp bitmesini bekle
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            yield return StartCoroutine(player.PlayVictoryShowPoseRoutine());
        }
        else
        {
            yield return new WaitForSeconds(6.3f);
        }

        // 2. Animasyon tamamen bittikten sonra Victory Panelini aç
        UIManager uiManager = FindObjectOfType<UIManager>();
        if (uiManager != null)
        {
            uiManager.ShowVictoryPanel();
        }
    }

    /// <summary>
    /// Aynı zorluk derecesiyle oyunu 1. dalgadan yeniden başlatır
    /// </summary>
    public void RestartGame()
    {
        StopAllCoroutines();
        ClearAllEnemies();

        isGameStarted = true;
        isWavesCompleted = false;
        isSpawning = false;
        wave = 1;

        Debug.Log($"<color=cyan>[YENİDEN BAŞLATILDI]</color> Mod: {currentDifficulty}");
        StartCoroutine(SpawnEnemyRoutine(wave, true));
    }

    /// <summary>
    /// Spawner'ı tamamen durdurur ve sıfırlar (Ana Menüye dönüş için)
    /// </summary>
    public void ResetSpawner()
    {
        StopAllCoroutines();
        ClearAllEnemies();

        isGameStarted = false;
        isWavesCompleted = false;
        isSpawning = false;
        wave = 1;
    }

    /// <summary>
    /// Sahnedeki tüm aktif veya ölü düşman nesnelerini temizler
    /// </summary>
    public void ClearAllEnemies()
    {
        foreach (var enemy in activeEnemies)
        {
            if (enemy != null)
            {
                Destroy(enemy.gameObject);
            }
        }
        activeEnemies.Clear();

        EnemyController[] extraEnemies = FindObjectsOfType<EnemyController>();
        foreach (var enemy in extraEnemies)
        {
            if (enemy != null)
            {
                Destroy(enemy.gameObject);
            }
        }
    }

    IEnumerator SpawnEnemyRoutine(int currentWave, bool isFirst = false)
    {
        isSpawning = true;

        int enemyCount = 1;
        if (currentWaveCounts != null && currentWave - 1 < currentWaveCounts.Length)
        {
            enemyCount = currentWaveCounts[currentWave - 1];
        }

        Debug.Log($"<color=yellow>[YENİ DALGA GELİYOR]</color> Dalga {currentWave}/{maxWaves} ({enemyCount} Düşman) - Zorluk: {currentDifficulty}");

        if (!isFirst)
        {
            yield return new WaitForSeconds(respawnDelay);
        }
        else
        {
            yield return new WaitForSeconds(0.4f);
        }

        SpawnEnemy(enemyCount);
        isSpawning = false;
    }

    void SpawnEnemy(int count)
    {
        if (enemyPrefab != null)
        {
            PlayerController player = FindObjectOfType<PlayerController>();

            for (int i = 0; i < count; i++)
            {
                // Düşmanların üst üste binmemesi için X ekseninde aralıklı yerleştir
                float xOffset = (count > 1) ? (i - (count - 1) * 0.5f) * spawnSpacing : 0f;
                Vector3 spawnPos = transform.position + new Vector3(xOffset, 0f, 0f);

                GameObject newEnemy = Instantiate(enemyPrefab, spawnPos, transform.rotation);
                EnemyController ec = newEnemy.GetComponent<EnemyController>();
                if (ec != null)
                {
                    activeEnemies.Add(ec);
                    if (player != null)
                    {
                        player.OnEnemySpawned(ec);
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning("EnemySpawner: Enemy Prefab atanmamış!");
        }
    }
}
