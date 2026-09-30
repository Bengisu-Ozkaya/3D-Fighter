using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum Difficulty
{
    Easy,
    Medium,
    Hard
}

[System.Serializable]
public class ArenaLightTier
{
    [Tooltip("Kademe İsmi (Örn: Dalga 1-5)")]
    public string tierName;
    [Tooltip("Bu kademedeki Point Light ışık rengi")]
    public Color lightColor;
    [Tooltip("Işık şiddeti / parlaklığı (Intensity)")]
    public float intensity;

    public ArenaLightTier(string name, Color color, float intensity = 10f)
    {
        this.tierName = name;
        this.lightColor = color;
        this.intensity = intensity;
    }
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

    [Header("Environment 1 Arena Işık Ayarları (Point Lights)")]
    [Tooltip("Kaç dövüş/dalga sonra ışık renginin daha yırtıcı renge değişeceği (İstenen: Her 5 dalgada bir)")]
    [SerializeField] private int wavesPerColorTier = 5;
    [Tooltip("Işık rengi geçiş yumuşatma süresi (saniye)")]
    [SerializeField] private float colorTransitionDuration = 1.5f;
    [Tooltip("Environment 1 altındaki Light objesinde bulunan Point Light'lar. Boş bırakılırsa hiyerarşiden otomatik bulunur.")]
    [SerializeField] private List<Light> arenaPointLights = new List<Light>();
    [Tooltip("Her 5 dalgada bir devreye girecek yüksek kontrastlı ve yırtıcı arena ışık kademeleri")]
    [SerializeField] private List<ArenaLightTier> arenaLightTiers = new List<ArenaLightTier>();

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
    private Coroutine lightTransitionCoroutine = null;

    public int CurrentWave => wave;
    public bool IsGameStarted => isGameStarted;
    public Difficulty CurrentDifficulty => currentDifficulty;
    public int CurrentEnvironmentIndex => currentEnvironmentIndex;

    void Awake()
    {
        EnsureDefaultLightTiers();
        FindArenaPointLightsIfNeeded();
    }

    void Start()
    {
        // Başlangıçta 1. sahayı (Environment 1) aktif kıl ve ışıkları başlangıç kademesine (Beyaz) ayarla
        UpdateArenaLighting(1, immediate: true);
    }

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
    /// Environment 2 devre dışı bırakıldığı için daima Environment 1 (Index 0) döner
    /// </summary>
    public int GetEnvironmentIndexForWave(int waveNumber)
    {
        return 0;
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

        // 1. Dalga arena ışık rengini (Beyaz) anında uygula
        UpdateArenaLighting(wave, immediate: true);

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

        // Environment 1 daima aktif
        if (RingBoundary.Instance != null)
        {
            RingBoundary.Instance.SwitchEnvironment(0);
        }
        currentEnvironmentIndex = 0;

        // Öldüğü dalganın ışık rengini anında uygula (Örn: Dalga 6-10 ise Mavi, 11-15 ise Kırmızı vb.)
        UpdateArenaLighting(wave, immediate: true);

        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            player.SetPunchDamage(GetCurrentDifficultyPlayerDamage());
            player.SetUppercutDamage(GetCurrentDifficultyPlayerUppercutDamage());
        }

        Debug.Log($"<color=cyan>[ÖLÜNEN DALGA YENİDEN BAŞLATILIYOR]</color> Dalga {wave} - Saha: #1 - Mod: {currentDifficulty}");
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

        // Başlangıç dalgası ışığını anında uygula (Beyaz)
        UpdateArenaLighting(1, immediate: true);

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

        // Işıkları başlangıç haline getir (Beyaz)
        UpdateArenaLighting(1, immediate: true);

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

        // Her 5 dalgada bir arena Point Light renklerini daha yırtıcı renge geçir (Örn: Dalga 6, 11, 16...)
        if ((currentWave - 1) % wavesPerColorTier == 0 && currentWave > 1)
        {
            ArenaLightTier tier = GetLightTierForWave(currentWave);
            Debug.Log($"<color=cyan>[ARENA IŞIKLARI]</color> {wavesPerColorTier} Dövüş tamamlandı! Point Light'lar yeni yırtıcı renge geçiyor: <color=yellow>{tier.tierName}</color> (Dalga {currentWave})");
            UpdateArenaLighting(currentWave, immediate: false);
        }
        else if (isFirst)
        {
            UpdateArenaLighting(currentWave, immediate: true);
        }

        int enemyCount = GetEnemyCountForWave(currentWave);
        Debug.Log($"<color=yellow>[YENİ DALGA GELİYOR]</color> Dalga {currentWave} ({enemyCount} Düşman) - Saha: #1 - Zorluk: {currentDifficulty}");

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
    /// Varsayılan yırtıcı arena ışık renk paletini kurar (Dalga 1-5: Beyaz, Dalga 6-10: Neon Mavi, Dalga 11-15: Kırmızı...)
    /// </summary>
    private void EnsureDefaultLightTiers()
    {
        if (arenaLightTiers == null)
        {
            arenaLightTiers = new List<ArenaLightTier>();
        }

        if (arenaLightTiers.Count == 0)
        {
            // 1. Kademe: Beyaz (Normal / Doğal Arena Işığı)
            arenaLightTiers.Add(new ArenaLightTier("Dalga 1-5 (Beyaz / Doğal Işık)", Color.white, 10f));
            // 2. Kademe: Neon Elektrik Mavisi (Yırtıcı Soğuk Ton)
            arenaLightTiers.Add(new ArenaLightTier("Dalga 6-10 (Yırtıcı Neon Mavi)", new Color(0f, 0.65f, 1f, 1f), 11f));
            // 3. Kademe: Şiddetli Kan Kırmızısı (Yırtıcı Saldırgan Ton)
            arenaLightTiers.Add(new ArenaLightTier("Dalga 11-15 (Kan Kırmızısı / Crimson)", new Color(1f, 0.08f, 0.08f, 1f), 12f));
            // 4. Kademe: Derin Yırtıcı Neon Mor / Violet
            arenaLightTiers.Add(new ArenaLightTier("Dalga 16-20 (Yırtıcı Neon Mor)", new Color(0.65f, 0f, 1f, 1f), 12f));
            // 5. Kademe: Cehennem Ateşi / Lav Turuncusu
            arenaLightTiers.Add(new ArenaLightTier("Dalga 21-25 (Alev Turuncusu)", new Color(1f, 0.35f, 0f, 1f), 12f));
            // 6. Kademe: Tehlikeli Zehir Yeşili / Toxic Green
            arenaLightTiers.Add(new ArenaLightTier("Dalga 26-30 (Zehirli Asit Yeşili)", new Color(0f, 1f, 0.35f, 1f), 11f));
        }
    }

    /// <summary>
    /// Environment 1 altındaki Light objesinin altındaki Point Light'ları otomatik bulur
    /// </summary>
    public void FindArenaPointLightsIfNeeded()
    {
        arenaPointLights.RemoveAll(l => l == null);
        if (arenaPointLights.Count > 0) return;

        // 1. Sahnedeki "Environment 1" veya "Environment1" GameObject'ini ara
        GameObject env1Go = null;
        foreach (var rootGo in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            string trimmed = rootGo.name.Trim();
            if (trimmed.Equals("Environment 1", System.StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("Environment1", System.StringComparison.OrdinalIgnoreCase))
            {
                env1Go = rootGo;
                break;
            }
        }

        Transform lightParent = null;
        if (env1Go != null)
        {
            lightParent = env1Go.transform.Find("Light");
        }

        if (lightParent == null)
        {
            GameObject lGo = GameObject.Find("Light");
            if (lGo != null) lightParent = lGo.transform;
        }

        if (lightParent != null)
        {
            // Sadece LightType.Point olanları al (Directional Light etkilenmez)
            Light[] found = lightParent.GetComponentsInChildren<Light>(true);
            foreach (var l in found)
            {
                if (l != null && l.type == LightType.Point)
                {
                    arenaPointLights.Add(l);
                }
            }
            Debug.Log($"<color=cyan>[EnemySpawner]</color> Environment 1/Light altındaki {arenaPointLights.Count} adet Point Light sisteme bağlandı.");
        }
        else
        {
            // Fallback: Sahnede adı "Point Light" olan veya tipi Point olan ışıkları al
            foreach (var l in FindObjectsOfType<Light>(true))
            {
                if (l != null && l.type == LightType.Point && l.name.IndexOf("Point Light", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    arenaPointLights.Add(l);
                }
            }
        }
    }

    /// <summary>
    /// Belirtilen dalgaya karşılık gelen ışık kademesini döner (5 dövüşte bir değişir, bitince yırtıcı renkler döner)
    /// </summary>
    public ArenaLightTier GetLightTierForWave(int waveNumber)
    {
        EnsureDefaultLightTiers();
        if (arenaLightTiers == null || arenaLightTiers.Count == 0)
        {
            return new ArenaLightTier("Beyaz", Color.white, 10f);
        }

        int tierIndex = (waveNumber - 1) / wavesPerColorTier;
        if (tierIndex < arenaLightTiers.Count)
        {
            return arenaLightTiers[tierIndex];
        }

        // Önceden tanımlı kademeler bittiğinde 0. indeksteki beyazı atlayıp yırtıcı renkler arasında döngüye devam et
        if (arenaLightTiers.Count > 1)
        {
            int predatoryCount = arenaLightTiers.Count - 1;
            int loopedIndex = 1 + ((tierIndex - 1) % predatoryCount);
            return arenaLightTiers[loopedIndex];
        }

        return arenaLightTiers[arenaLightTiers.Count - 1];
    }

    /// <summary>
    /// Environment 1 altındaki Point Light'ların rengini ve parlaklığını günceller
    /// </summary>
    public void UpdateArenaLighting(int waveNumber, bool immediate = false)
    {
        FindArenaPointLightsIfNeeded();
        if (arenaPointLights == null || arenaPointLights.Count == 0) return;

        ArenaLightTier targetTier = GetLightTierForWave(waveNumber);
        Color targetColor = targetTier.lightColor;
        float targetIntensity = targetTier.intensity;

        if (immediate || colorTransitionDuration <= 0f)
        {
            if (lightTransitionCoroutine != null)
            {
                StopCoroutine(lightTransitionCoroutine);
                lightTransitionCoroutine = null;
            }

            foreach (var l in arenaPointLights)
            {
                if (l != null)
                {
                    l.color = targetColor;
                    l.intensity = targetIntensity;
                }
            }
        }
        else
        {
            if (lightTransitionCoroutine != null)
            {
                StopCoroutine(lightTransitionCoroutine);
            }
            lightTransitionCoroutine = StartCoroutine(TransitionLightRoutine(targetColor, targetIntensity, colorTransitionDuration));
        }
    }

    /// <summary>
    /// Işık rengi ve yoğunluğunu yumuşak bir şekilde (SmoothStep) yeni renge geçirir
    /// </summary>
    private IEnumerator TransitionLightRoutine(Color targetColor, float targetIntensity, float duration)
    {
        Color startColor = Color.white;
        float startIntensity = 10f;

        foreach (var l in arenaPointLights)
        {
            if (l != null)
            {
                startColor = l.color;
                startIntensity = l.intensity;
                break;
            }
        }

        if (startColor == targetColor && Mathf.Approximately(startIntensity, targetIntensity))
        {
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            Color curColor = Color.Lerp(startColor, targetColor, smoothT);
            float curIntensity = Mathf.Lerp(startIntensity, targetIntensity, smoothT);

            foreach (var l in arenaPointLights)
            {
                if (l != null)
                {
                    l.color = curColor;
                    l.intensity = curIntensity;
                }
            }
            yield return null;
        }

        foreach (var l in arenaPointLights)
        {
            if (l != null)
            {
                l.color = targetColor;
                l.intensity = targetIntensity;
            }
        }

        lightTransitionCoroutine = null;
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
                    ec.ApplyDifficultyBlockSettings(currentDifficulty);
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
