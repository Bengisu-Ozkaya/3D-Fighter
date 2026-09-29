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

    [Header("Zorluk Hasar Ayarları (Enemy Attack Damage)")]
    [Tooltip("Kolay modda düşmanların oyuncuya vereceği hasar")]
    [SerializeField] float easyDamage = 5f;
    [Tooltip("Orta modda düşmanların oyuncuya vereceği hasar")]
    [SerializeField] float midDamage = 10f;
    [Tooltip("Zor modda düşmanların oyuncuya vereceği hasar")]
    [SerializeField] float hardDamage = 15f;

    [Header("Zorluk Düşman Can Ayarları (Enemy Health)")]
    [Tooltip("Kolay modda düşmanların canı")]
    [SerializeField] float easyEnemyHealth = 30f;
    [Tooltip("Orta modda düşmanların canı (İstenen: 40)")]
    [SerializeField] float midEnemyHealth = 40f;
    [Tooltip("Zor modda düşmanların canı (İstenen: 50)")]
    [SerializeField] float hardEnemyHealth = 50f;

    [Header("Zorluk Oyuncu Yumruk Hasarı (Player Punch Damage)")]
    [Tooltip("Kolay modda oyuncunun yumruk hasarı (İstenen: 10)")]
    [SerializeField] float easyPlayerDamage = 10f;
    [Tooltip("Orta modda oyuncunun yumruk hasarı (İstenen: 10)")]
    [SerializeField] float midPlayerDamage = 10f;
    [Tooltip("Zor modda oyuncunun yumruk hasarı (İstenen: 20)")]
    [SerializeField] float hardPlayerDamage = 20f;

    [Header("Zorluk Oyuncu Aparkat Hasarı (Player Uppercut Damage - 'E' Tuşu)")]
    [Tooltip("Kolay modda oyuncunun aparkat hasarı (İstenen: 20)")]
    [SerializeField] float easyPlayerUppercutDamage = 20f;
    [Tooltip("Orta modda oyuncunun aparkat hasarı (İstenen: 20)")]
    [SerializeField] float midPlayerUppercutDamage = 20f;
    [Tooltip("Zor modda oyuncunun aparkat hasarı (İstenen: 30)")]
    [SerializeField] float hardPlayerUppercutDamage = 30f;

    // Zorluk Modlarına Göre Dalga Düşman Sayıları
    private readonly int[] easyWaveCounts = new int[] { 1, 2, 3 };
    private readonly int[] midWaveCounts = new int[] { 1, 3, 5 };
    private readonly int[] hardWaveCounts = new int[] { 1, 3, 5 };

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

    /// <summary>
    /// Aktif zorluk derecesine göre düşmanın vereceği hasar miktarını döner
    /// </summary>
    public float GetCurrentDifficultyDamage()
    {
        switch (currentDifficulty)
        {
            case Difficulty.Easy:
                return easyDamage;
            case Difficulty.Medium:
                return midDamage;
            case Difficulty.Hard:
                return hardDamage;
            default:
                return 10f;
        }
    }

    /// <summary>
    /// Aktif zorluk derecesine göre düşmanın can miktarını döner
    /// </summary>
    public float GetCurrentDifficultyEnemyHealth()
    {
        switch (currentDifficulty)
        {
            case Difficulty.Easy:
                return 30f; // Kolay mod: Düşman Canı 30
            case Difficulty.Medium:
                return 40f; // Orta mod: Düşman Canı 40
            case Difficulty.Hard:
                return 50f; // Zor mod: Düşman Canı 50
            default:
                return 30f;
        }
    }

    /// <summary>
    /// Aktif zorluk derecesine göre oyuncunun vereceği yumruk hasarını döner
    /// </summary>
    public float GetCurrentDifficultyPlayerDamage()
    {
        switch (currentDifficulty)
        {
            case Difficulty.Easy:
                return 10f; // Kolay mod: Normal yumruk 10 hasar (30'dan 20'ye iner)
            case Difficulty.Medium:
                return 10f; // Orta mod: Normal yumruk 10 hasar (40'tan 30'a iner)
            case Difficulty.Hard:
                return 20f; // Zor mod: Normal yumruk 20 hasar (50'den 20 götürür)
            default:
                return 10f;
        }
    }

    /// <summary>
    /// Aktif zorluk derecesine göre oyuncunun vereceği aparkat hasarını döner
    /// </summary>
    public float GetCurrentDifficultyPlayerUppercutDamage()
    {
        switch (currentDifficulty)
        {
            case Difficulty.Easy:
                return 20f; // Kolay mod: Aparkat 20 hasar (30'dan 10'a iner)
            case Difficulty.Medium:
                return 20f; // Orta mod: Aparkat 20 hasar (40'tan 20'ye iner)
            case Difficulty.Hard:
                return 30f; // Zor mod: Aparkat 30 hasar (50'den 30 götürür)
            default:
                return 20f;
        }
    }

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

        float dmg = GetCurrentDifficultyDamage();
        float hp = GetCurrentDifficultyEnemyHealth();
        float playerDmg = GetCurrentDifficultyPlayerDamage();
        float playerUppercutDmg = GetCurrentDifficultyPlayerUppercutDamage();
        Debug.Log($"<color=cyan>[OYUN BAŞLADI]</color> Mod: {difficulty} | Toplam Dalga: {maxWaves} | Düşman Canı: {hp} | Düşman Hasarı: {dmg} | Oyuncu Yumruk Hasarı: {playerDmg} | Aparkat Hasarı: {playerUppercutDmg}");

        // Sahnedeki mevcut düşmanların hasarını ve canını güncelle
        foreach (var ec in FindObjectsOfType<EnemyController>())
        {
            if (ec != null)
            {
                ec.SetAttackDamage(dmg);
                ec.SetHealth(hp);
            }
        }

        // Oyuncunun yumruk ve aparkat hasarını zorluk moduna göre güncelle
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            player.SetPunchDamage(playerDmg);
            player.SetUppercutDamage(playerUppercutDmg);
        }

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
    /// Oyuncunun öldüğü mevcut dalgayı yeniden başlatır (Relive butonu için)
    /// </summary>
    public void RestartCurrentWave()
    {
        StopAllCoroutines();
        ClearAllEnemies();

        isGameStarted = true;
        isWavesCompleted = false;
        isSpawning = false;

        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            player.SetPunchDamage(GetCurrentDifficultyPlayerDamage());
            player.SetUppercutDamage(GetCurrentDifficultyPlayerUppercutDamage());
        }

        Debug.Log($"<color=cyan>[ÖLÜNEN DALGA YENİDEN BAŞLATILIYOR]</color> Dalga {wave}/{maxWaves} - Mod: {currentDifficulty}");
        StartCoroutine(SpawnEnemyRoutine(wave, true));
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

        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            player.SetPunchDamage(GetCurrentDifficultyPlayerDamage());
            player.SetUppercutDamage(GetCurrentDifficultyPlayerUppercutDamage());
        }

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

        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            player.SetPunchDamage(10f);
            player.SetUppercutDamage(20f);
        }
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
                    ec.SetAttackDamage(GetCurrentDifficultyDamage());
                    ec.SetHealth(GetCurrentDifficultyEnemyHealth());
                    activeEnemies.Add(ec);
                    if (player != null)
                    {
                        player.OnEnemySpawned(ec);
                        player.SetPunchDamage(GetCurrentDifficultyPlayerDamage());
                        player.SetUppercutDamage(GetCurrentDifficultyPlayerUppercutDamage());
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
