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
    [Tooltip("Önceki dalgadaki tüm düşmanlar yok olduktan (Destroy edildikten) sonra yeni dalganın başlama gecikmesi (saniye)")]
    [SerializeField] float respawnDelay = 1.0f;
    [Tooltip("Birden fazla düşman doğduğunda aralarındaki yatay mesafe")]
    [SerializeField] float spawnSpacing = 1.4f;
    [Tooltip("Düşmanların doğarken sahip olacağı rotasyon açısı (Y ekseni derece, Varsayılan 0)")]
    [SerializeField] float spawnRotationY = 0f;
    [Tooltip("Sonsuz dalgalarda bir dalgada aynı anda doğabilecek maksimum düşman sayısı")]
    [SerializeField] int maxEnemiesPerWave = 8;
    [Tooltip("Environment (Saha) değiştirilirken araya giren bekleme süresi (saniye)")]
    [SerializeField] float environmentTransitionDelay = 1.2f;

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

    // Zorluk Modlarına Göre Temel Dalga Düşman Sayıları
    private readonly int[] easyWaveCounts = new int[] { 1, 2, 3, 4, 5, 6 };
    private readonly int[] midWaveCounts = new int[] { 1, 2, 3, 4, 5 };
    private readonly int[] hardWaveCounts = new int[] { 1, 2, 3, 4, 5 };

    private int[] currentWaveCounts = new int[] { 1, 2, 3 };
    private Difficulty currentDifficulty = Difficulty.Easy;

    private int wave = 1;
    private int currentEnvironmentIndex = 0;
    private List<EnemyController> activeEnemies = new List<EnemyController>();
    private bool isGameStarted = false;
    private bool isSpawning = false;
    private bool isChangingEnvironment = false;

    public int CurrentWave => wave;
    public bool IsGameStarted => isGameStarted;
    public Difficulty CurrentDifficulty => currentDifficulty;
    public int CurrentEnvironmentIndex => currentEnvironmentIndex;

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
                return easyEnemyHealth;
            case Difficulty.Medium:
                return midEnemyHealth;
            case Difficulty.Hard:
                return hardEnemyHealth;
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
                return easyPlayerDamage;
            case Difficulty.Medium:
                return midPlayerDamage;
            case Difficulty.Hard:
                return hardPlayerDamage;
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
                return easyPlayerUppercutDamage;
            case Difficulty.Medium:
                return midPlayerUppercutDamage;
            case Difficulty.Hard:
                return hardPlayerUppercutDamage;
            default:
                return 20f;
        }
    }

    /// <summary>
    /// Her 3 dalgada bir ortamın (Environment) değişmesi için hangi indeksin aktif olacağını hesaplar
    /// 1, 2, 3 -> Environment 1 (Index 0)
    /// 4, 5, 6 -> Environment 2 (Index 1)
    /// 7, 8, 9 -> Environment 1 (veya ileride 3)
    /// </summary>
    public int GetEnvironmentIndexForWave(int waveNumber)
    {
        int envCount = RingBoundary.Instance != null ? RingBoundary.Instance.EnvironmentCount : 2;
        if (envCount <= 0) envCount = 2;
        return ((waveNumber - 1) / 3) % envCount;
    }

    /// <summary>
    /// Mevcut dalga numarasına göre sahada doğacak düşman sayısını hesaplar
    /// </summary>
    public int GetEnemyCountForWave(int waveNumber)
    {
        if (currentWaveCounts != null && waveNumber - 1 < currentWaveCounts.Length)
        {
            return currentWaveCounts[waveNumber - 1];
        }

        int baseCount = (currentWaveCounts != null && currentWaveCounts.Length > 0)
            ? currentWaveCounts[currentWaveCounts.Length - 1]
            : 3;
        int extra = (waveNumber - (currentWaveCounts != null ? currentWaveCounts.Length : 3));
        return Mathf.Clamp(baseCount + extra, 1, maxEnemiesPerWave);
    }

    public void SetSpawnRotation(float rotY)
    {
        spawnRotationY = rotY;
    }

    public void StartEasyMode()
    {
        StartGame(Difficulty.Easy);
    }

    public void StartMidMode()
    {
        StartGame(Difficulty.Medium);
    }

    public void StartHardMode()
    {
        StartGame(Difficulty.Hard);
    }

    /// <summary>
    /// Seçilen zorluk derecesiyle oyunu ve ilk dalgayı başlatır (Sonsuz mod)
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

        wave = 1;
        currentEnvironmentIndex = 0;
        isGameStarted = true;
        isChangingEnvironment = false;

        // Başlangıçta 1. sahayı (Environment 1) aktif et
        if (RingBoundary.Instance != null)
        {
            RingBoundary.Instance.SwitchEnvironment(0);
        }

        float dmg = GetCurrentDifficultyDamage();
        float hp = GetCurrentDifficultyEnemyHealth();
        float playerDmg = GetCurrentDifficultyPlayerDamage();
        float playerUppercutDmg = GetCurrentDifficultyPlayerUppercutDamage();
        Debug.Log($"<color=cyan>[SONSUZ DALGA OYUNU BAŞLADI]</color> Mod: {difficulty} | Düşman Canı: {hp} | Düşman Hasarı: {dmg} | Oyuncu Yumruk: {playerDmg} | Aparkat: {playerUppercutDmg}");

        // Sahnedeki mevcut düşmanların hasarını ve canını güncelle
        foreach (var ec in FindObjectsOfType<EnemyController>())
        {
            if (ec != null)
            {
                ec.SetAttackDamage(dmg);
                ec.SetHealth(hp);
            }
        }

        // Oyuncunun yumruk ve aparkat hasarını güncelle
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
        // Oyun başlamadıysa veya ortam değişiyorsa bekle
        if (!isGameStarted || isChangingEnvironment) return;

        // Sahneden yok olan düşmanları listeden temizle
        activeEnemies.RemoveAll(e => e == null);

        // Sahnedeki tüm düşmanları kontrol et
        bool anyEnemyInScene = false;
        EnemyController[] allEnemiesInScene = FindObjectsOfType<EnemyController>();
        if (allEnemiesInScene != null && allEnemiesInScene.Length > 0)
        {
            anyEnemyInScene = true;
        }

        // Sahnede önceki dalgadan HİÇBİR düşman kalmadıysa ve yeni dalga henüz başlatılmadıysa
        if (activeEnemies.Count == 0 && !anyEnemyInScene && !isSpawning)
        {
            // Dalgalar kullanıcı ölene kadar sonsuz olarak artar (1, 2, 3, 4, 5...)
            wave++;
            StartCoroutine(SpawnEnemyRoutine(wave, false));
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
        isSpawning = false;
        isChangingEnvironment = false;

        // Öldüğü dalganın ortamına uygun sahayı aktif et
        int targetEnvIndex = GetEnvironmentIndexForWave(wave);
        if (RingBoundary.Instance != null)
        {
            RingBoundary.Instance.SwitchEnvironment(targetEnvIndex);
        }
        currentEnvironmentIndex = targetEnvIndex;

        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            player.SetPunchDamage(GetCurrentDifficultyPlayerDamage());
            player.SetUppercutDamage(GetCurrentDifficultyPlayerUppercutDamage());
        }

        Debug.Log($"<color=cyan>[ÖLÜNEN DALGA YENİDEN BAŞLATILIYOR]</color> Dalga {wave} - Saha: #{currentEnvironmentIndex + 1} - Mod: {currentDifficulty}");
        StartCoroutine(SpawnEnemyRoutine(wave, true));
    }

    /// <summary>
    /// Oyunu 1. dalgadan ve Environment 1'den yeniden başlatır
    /// </summary>
    public void RestartGame()
    {
        StopAllCoroutines();
        ClearAllEnemies();

        isGameStarted = true;
        isSpawning = false;
        isChangingEnvironment = false;
        wave = 1;
        currentEnvironmentIndex = 0;

        if (RingBoundary.Instance != null)
        {
            RingBoundary.Instance.SwitchEnvironment(0);
        }

        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            player.SetPunchDamage(GetCurrentDifficultyPlayerDamage());
            player.SetUppercutDamage(GetCurrentDifficultyPlayerUppercutDamage());
        }

        Debug.Log($"<color=cyan>[OYUN YENİDEN BAŞLATILDI]</color> Mod: {currentDifficulty} - Saha: #1");
        StartCoroutine(SpawnEnemyRoutine(wave, true));
    }

    /// <summary>
    /// Spawner'ı durdurur ve ana menü durumuna sıfırlar
    /// </summary>
    public void ResetSpawner()
    {
        StopAllCoroutines();
        ClearAllEnemies();

        isGameStarted = false;
        isSpawning = false;
        isChangingEnvironment = false;
        wave = 1;
        currentEnvironmentIndex = 0;

        if (RingBoundary.Instance != null)
        {
            RingBoundary.Instance.SwitchEnvironment(0);
        }

        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            player.SetPunchDamage(10f);
            player.SetUppercutDamage(20f);
        }
    }

    /// <summary>
    /// Sahnedeki tüm düşman nesnelerini temizler
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

        // Her 3 dalga tamamlandığında (Örn: Dalga 4, 7, 10...) Environment değiştir
        int targetEnvIndex = GetEnvironmentIndexForWave(currentWave);
        if (targetEnvIndex != currentEnvironmentIndex)
        {
            yield return StartCoroutine(ChangeEnvironmentRoutine(targetEnvIndex, currentWave));
        }

        int enemyCount = GetEnemyCountForWave(currentWave);
        Debug.Log($"<color=yellow>[YENİ DALGA GELİYOR]</color> Dalga {currentWave} ({enemyCount} Düşman) - Saha: #{currentEnvironmentIndex + 1} - Zorluk: {currentDifficulty}");

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

    /// <summary>
    /// Her 3 dalgada bir çağrılır: Sahayı, collider'ları, oyuncuyu ve spawner'ı yeni çevreye taşır
    /// </summary>
    IEnumerator ChangeEnvironmentRoutine(int targetEnvIndex, int targetWave)
    {
        isChangingEnvironment = true;
        Debug.Log($"<color=cyan>[ENVIRONMENT DEĞİŞİYOR]</color> 3 Dalga atlatıldı! Yeni Sahaya Geçiliyor: Saha #{targetEnvIndex + 1} (Dalga {targetWave})");

        ClearAllEnemies();

        if (RingBoundary.Instance != null)
        {
            RingBoundary.Instance.SwitchEnvironment(targetEnvIndex);
        }
        currentEnvironmentIndex = targetEnvIndex;

        // Yeni ortama geçiş anında oyuncunun hazır olması için kısa bir geçiş beklemesi
        yield return new WaitForSeconds(environmentTransitionDelay);
        isChangingEnvironment = false;
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
                spawnPos = RingBoundary.ClampToArena(spawnPos, 0.45f);

                Quaternion spawnRot = Quaternion.Euler(0f, spawnRotationY, 0f);
                GameObject newEnemy = Instantiate(enemyPrefab, spawnPos, spawnRot);
                EnemyController ec = newEnemy.GetComponent<EnemyController>();
                if (ec != null)
                {
                    ec.transform.rotation = spawnRot;
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
