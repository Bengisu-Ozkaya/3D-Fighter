using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Oyuncunun gerçekleştirdiği dövüş eylemlerinin türü
/// </summary>
public enum CombatActionType
{
    NormalPunch,
    Uppercut,
    Block
}

/// <summary>
/// Oyuncunun canlı ve kalıcı dövüş verilerine göre sınıflandırılan dövüş stili / profili
/// </summary>
public enum PlayerCombatArchetype
{
    Balanced,           // Dengeli / Henüz belirginleşmemiş veya çok yönlü
    UppercutSpammer,    // Sürekli güçlü aparkat atan oyuncu
    PunchSpammer,       // Aralıksız hızlı düz yumruk spamleyen oyuncu
    WhiffProne,         // Mesafeyi ayarlayamayıp boşa yumruk sallayan oyuncu
    TurtleDefensive,    // Sürekli gard arkasına sığınan savunmacı oyuncu
    OutboxerFleeing,    // Sürekli geri kaçan ve mesafeli dövüşen oyuncu
    BrawlerAggressive   // Sürekli dibe girip aralıksız hücum eden agresif oyuncu
}

/// <summary>
/// Oyuncunun dövüş telemetrisini ve hareket alışkanlıklarını kaydeden veri yapısı
/// </summary>
public struct CombatTelemetryEvent
{
    public float timestamp;
    public CombatActionType actionType;
    public bool isHit;
    public float distanceToTarget;

    public CombatTelemetryEvent(float time, CombatActionType type, bool hit, float distance)
    {
        timestamp = time;
        actionType = type;
        isHit = hit;
        distanceToTarget = distance;
    }
}

/// <summary>
/// Oyun kapatılıp açılsa dahi silinmeyen, tüm düşmanların ortak kullandığı kalıcı öğrenme verisi
/// </summary>
[Serializable]
public class PersistentCombatProfileData
{
    public int totalPunches = 0;
    public int totalUppercuts = 0;
    public int totalHits = 0;
    public int totalWhiffs = 0;
    public float totalBlockTime = 0f;

    public float emaUppercutRatio = 0.25f;
    public float emaWhiffRatio = 0.20f;
    public float emaBlockRatio = 0.10f;
    public float emaAverageDistance = 1.50f;
    public float emaAttackFrequency = 0.40f;

    public int learnedArchetype = (int)PlayerCombatArchetype.Balanced;
    public float confidenceScore = 0f;
}

/// <summary>
/// Oyuncu Dövüş Takipçisi ve Kalıcı Ortak Yapay Zeka Öğrenme Sistemi.
/// Tüm düşmanlar için tek bir ortak öğrenme havuzu tutar ve öğrenilen oyuncu alışkanlıklarını
/// hem JSON dosyasına (Application.persistentDataPath) hem de PlayerPrefs'e kaydederek
/// oyun kapatılıp açıldığında bile kaldığı yerden devam etmesini sağlar.
/// </summary>
[DefaultExecutionOrder(-60)]
public class PlayerCombatTracker : MonoBehaviour
{
    private const string PREFS_KEY = "GlobalAdaptiveAILearnedProfile_v1";
    private const string FILE_NAME = "AdaptiveAILearnedProfile.json";

    private static PlayerCombatTracker _instance;
    public static PlayerCombatTracker Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<PlayerCombatTracker>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("PlayerCombatTracker");
                    _instance = go.AddComponent<PlayerCombatTracker>();
                }
            }
            return _instance;
        }
    }

    [Header("Analiz & Kayan Pencere (Rolling Window)")]
    [Tooltip("Canlı gözlem yapılan son zaman aralığı (saniye). Kalıcı profil ile harmanlanır.")]
    [SerializeField] private float analysisWindowDuration = 18.0f;

    [Tooltip("Bir stilin kararlaştırılması için gereken minimum aksiyon sayısı (Güven eşiği)")]
    [SerializeField] private int minActionsForClassification = 3;

    [Header("Eşik Değerleri (Archetype Thresholds)")]
    [Range(0.4f, 0.9f)]
    [SerializeField] private float uppercutSpamRatio = 0.55f;

    [Range(0.5f, 0.95f)]
    [SerializeField] private float punchSpamRatio = 0.75f;

    [Range(0.4f, 0.85f)]
    [SerializeField] private float whiffRateThreshold = 0.50f;

    [Range(0.3f, 0.8f)]
    [SerializeField] private float turtleBlockUptimeThreshold = 0.40f;

    [SerializeField] private float fleeingDistanceThreshold = 2.2f;
    [SerializeField] private float aggressiveDistanceThreshold = 1.15f;

    [Header("Kalıcı Öğrenme (Persistent Shared AI Memory)")]
    [Tooltip("Açıksa öğrenilen oyuncu profili diske kaydedilir ve oyun kapatılıp açıldığında korunur.")]
    [SerializeField] private bool enablePersistentLearning = true;

    [Tooltip("Yeni aksiyonların kalıcı hafızaya (EMA) etki hızı (0.15 = dengeli kalıcı öğrenme)")]
    [Range(0.05f, 0.50f)]
    [SerializeField] private float learningRate = 0.18f;

    [SerializeField] private PersistentCombatProfileData persistentProfile = new PersistentCombatProfileData();

    [Header("Debug Görselleştirme")]
    [Tooltip("Ekranda gerçek zamanlı AI analiz metriklerini göster")]
    [SerializeField] private bool showDebugHUD = false;

    // Canlı durum
    private PlayerCombatArchetype currentArchetype = PlayerCombatArchetype.Balanced;
    public PlayerCombatArchetype CurrentArchetype => currentArchetype;

    // Olay bildirimleri (Events)
    public static event Action<PlayerCombatArchetype, PlayerCombatArchetype> OnArchetypeChanged;
    public static event Action< PlayerCombatArchetype> OnProfileUpdated;
    public static event Action<CombatActionType, Vector3> OnPlayerWhiffed;
    public static event Action<CombatActionType> OnPlayerAttacked;

    // Aksiyon geçmişi (Sliding buffer)
    private readonly List<CombatTelemetryEvent> actionHistory = new List<CombatTelemetryEvent>();

    // Gard süresi takibi
    private bool isPlayerCurrentlyBlocking = false;
    private float blockStartTime = 0f;
    private float totalBlockTimeInWindow = 0f;

    // Canlı pencere istatistikleri
    private int punchCount = 0;
    private int uppercutCount = 0;
    private int hitCount = 0;
    private int whiffCount = 0;
    private float averageDistance = 1.5f;
    private float attackFrequency = 0f;
    private float confidenceScore = 0f;
    private float nextAutoSaveTime = 0f;
    private bool isDirty = false;

    public int LifetimeTotalActions => persistentProfile != null
        ? (persistentProfile.totalPunches + persistentProfile.totalUppercuts)
        : 0;

    public int TotalAttacksInWindow => punchCount + uppercutCount;

    /// <summary>
    /// Canlı pencere ile kalıcı öğrenilmiş aparkat oranının ortak harmanı (Tüm düşmanlar bunu kullanır)
    /// </summary>
    public float UppercutRatio
    {
        get
        {
            int windowAttacks = punchCount + uppercutCount;
            float liveRatio = windowAttacks > 0 ? (float)uppercutCount / windowAttacks : persistentProfile.emaUppercutRatio;
            if (LifetimeTotalActions <= 0) return liveRatio;
            float liveWeight = Mathf.Clamp01((float)windowAttacks / Mathf.Max(1, minActionsForClassification));
            return Mathf.Lerp(persistentProfile.emaUppercutRatio, liveRatio, liveWeight * 0.65f);
        }
    }

    /// <summary>
    /// Canlı pencere ile kalıcı öğrenilmiş ıska (whiff) oranının ortak harmanı
    /// </summary>
    public float WhiffRatio
    {
        get
        {
            int windowTotal = hitCount + whiffCount;
            float liveRatio = windowTotal > 0 ? (float)whiffCount / windowTotal : persistentProfile.emaWhiffRatio;
            if (LifetimeTotalActions <= 0) return liveRatio;
            float liveWeight = Mathf.Clamp01((float)windowTotal / Mathf.Max(1, minActionsForClassification));
            return Mathf.Lerp(persistentProfile.emaWhiffRatio, liveRatio, liveWeight * 0.65f);
        }
    }

    /// <summary>
    /// Canlı pencere ile kalıcı öğrenilmiş blok oranının ortak harmanı
    /// </summary>
    public float BlockRatio
    {
        get
        {
            float liveBlock = analysisWindowDuration > 0 ? Mathf.Clamp01(totalBlockTimeInWindow / analysisWindowDuration) : 0f;
            if (LifetimeTotalActions <= 0 && persistentProfile.totalBlockTime <= 0.1f) return liveBlock;
            return Mathf.Max(liveBlock, persistentProfile.emaBlockRatio * 0.85f);
        }
    }

    public float AverageDistance => LifetimeTotalActions > 0
        ? Mathf.Lerp(persistentProfile.emaAverageDistance, averageDistance, 0.6f)
        : averageDistance;

    public float AttackFrequency => LifetimeTotalActions > 0
        ? Mathf.Lerp(persistentProfile.emaAttackFrequency, attackFrequency, 0.6f)
        : attackFrequency;

    public float ConfidenceScore => confidenceScore;
    public PersistentCombatProfileData PersistentProfile => persistentProfile;

    private PlayerController player;

    private string SaveFilePath => Path.Combine(Application.persistentDataPath, FILE_NAME);

    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        LoadLearnedProfile();
        FindPlayer();
    }

    void Start()
    {
        FindPlayer();
        EvaluatePlayerArchetype(forceBroadcast: true);
    }

    void OnDisable()
    {
        if (_instance == this && isDirty)
        {
            SaveLearnedProfile();
        }
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            SaveLearnedProfile();
        }
    }

    void OnApplicationQuit()
    {
        SaveLearnedProfile();
    }

    private void FindPlayer()
    {
        if (player == null)
        {
            player = FindObjectOfType<PlayerController>();
        }
    }

    void Update()
    {
        CleanOldRecords();
        UpdateBlockTracking();
        UpdateSpatialTracking();
        EvaluatePlayerArchetype(forceBroadcast: false);

        if (isDirty && Time.unscaledTime >= nextAutoSaveTime)
        {
            SaveLearnedProfile();
        }
    }

    /// <summary>
    /// Oyuncu bir saldırı başlattığında çağrılır (Yumruk veya Aparkat)
    /// </summary>
    public void RecordAttackStart(CombatActionType actionType)
    {
        OnPlayerAttacked?.Invoke(actionType);
    }

    /// <summary>
    /// Oyuncunun vuruşu bir düşmana isabet ettiğinde çağrılır
    /// </summary>
    public void RecordAttackHit(CombatActionType actionType, EnemyController hitEnemy)
    {
        float dist = 1.0f;
        if (player != null && hitEnemy != null)
        {
            dist = Vector3.Distance(player.transform.position, hitEnemy.transform.position);
        }

        actionHistory.Add(new CombatTelemetryEvent(Time.time, actionType, true, dist));
        UpdatePersistentAttackMetrics(actionType, isHit: true, dist);
        EvaluatePlayerArchetype(forceBroadcast: true);
    }

    /// <summary>
    /// Oyuncunun vuruş penceresi bittiğinde hiçbir şeye isabet etmediyse (Iska / Whiff) çağrılır
    /// </summary>
    public void RecordAttackWhiff(CombatActionType actionType)
    {
        float dist = GetDistanceToNearestEnemy();
        actionHistory.Add(new CombatTelemetryEvent(Time.time, actionType, false, dist));
        UpdatePersistentAttackMetrics(actionType, isHit: false, dist);
        EvaluatePlayerArchetype(forceBroadcast: true);

        Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;
        OnPlayerWhiffed?.Invoke(actionType, playerPos);
    }

    private void UpdatePersistentAttackMetrics(CombatActionType actionType, bool isHit, float dist)
    {
        if (persistentProfile == null)
        {
            persistentProfile = new PersistentCombatProfileData();
        }

        bool isUpper = (actionType == CombatActionType.Uppercut);
        if (isUpper) persistentProfile.totalUppercuts++;
        else if (actionType == CombatActionType.NormalPunch) persistentProfile.totalPunches++;

        if (isHit) persistentProfile.totalHits++;
        else persistentProfile.totalWhiffs++;

        int totalLifetime = persistentProfile.totalPunches + persistentProfile.totalUppercuts;
        float alpha = totalLifetime <= 5 ? 0.35f : learningRate;

        persistentProfile.emaUppercutRatio = Mathf.Lerp(persistentProfile.emaUppercutRatio, isUpper ? 1f : 0f, alpha);
        persistentProfile.emaWhiffRatio = Mathf.Lerp(persistentProfile.emaWhiffRatio, isHit ? 0f : 1f, alpha);
        if (dist > 0.05f && dist < 8f)
        {
            persistentProfile.emaAverageDistance = Mathf.Lerp(persistentProfile.emaAverageDistance, dist, alpha);
        }

        MarkDirty();
    }

    /// <summary>
    /// Oyuncu gard aldığında veya gardı bıraktığında çağrılır
    /// </summary>
    public void RecordBlockState(bool isBlocking)
    {
        if (isPlayerCurrentlyBlocking == isBlocking) return;

        isPlayerCurrentlyBlocking = isBlocking;
        if (isBlocking)
        {
            blockStartTime = Time.time;
        }
        else
        {
            float duration = Mathf.Max(0f, Time.time - blockStartTime);
            totalBlockTimeInWindow += duration;
            if (persistentProfile != null)
            {
                persistentProfile.totalBlockTime += duration;
                float blockSample = Mathf.Clamp01(duration / 3.0f);
                persistentProfile.emaBlockRatio = Mathf.Lerp(persistentProfile.emaBlockRatio, blockSample, learningRate);
                MarkDirty();
            }
        }
    }

    private void MarkDirty()
    {
        isDirty = true;
        if (Time.unscaledTime >= nextAutoSaveTime)
        {
            nextAutoSaveTime = Time.unscaledTime + 2.0f;
        }
    }

    /// <summary>
    /// Kayan pencere süresinden (analysisWindowDuration) eski olan aksiyonları temizler ve sayaçları günceller
    /// </summary>
    private void CleanOldRecords()
    {
        float cutoffTime = Time.time - analysisWindowDuration;

        actionHistory.RemoveAll(e => e.timestamp < cutoffTime);

        punchCount = 0;
        uppercutCount = 0;
        hitCount = 0;
        whiffCount = 0;
        float totalDist = 0f;
        int distSampleCount = 0;

        foreach (var evt in actionHistory)
        {
            if (evt.actionType == CombatActionType.NormalPunch) punchCount++;
            else if (evt.actionType == CombatActionType.Uppercut) uppercutCount++;

            if (evt.isHit) hitCount++;
            else whiffCount++;

            if (evt.distanceToTarget > 0.05f)
            {
                totalDist += evt.distanceToTarget;
                distSampleCount++;
            }
        }

        if (distSampleCount > 0)
        {
            averageDistance = totalDist / distSampleCount;
        }
        else
        {
            averageDistance = GetDistanceToNearestEnemy();
        }

        int totalAttacks = punchCount + uppercutCount;
        attackFrequency = analysisWindowDuration > 0 ? (float)totalAttacks / analysisWindowDuration : 0f;

        if (totalAttacks >= 2 && persistentProfile != null)
        {
            persistentProfile.emaAttackFrequency = Mathf.Lerp(persistentProfile.emaAttackFrequency, attackFrequency, Time.deltaTime * 0.5f);
        }
    }

    /// <summary>
    /// Oyuncunun pencere içerisindeki aktif gard süresini günceller
    /// </summary>
    private void UpdateBlockTracking()
    {
        totalBlockTimeInWindow = Mathf.Max(0f, totalBlockTimeInWindow - (Time.deltaTime * 0.12f));

        if (isPlayerCurrentlyBlocking)
        {
            totalBlockTimeInWindow += Time.deltaTime;
            if (persistentProfile != null)
            {
                persistentProfile.totalBlockTime += Time.deltaTime;
                persistentProfile.emaBlockRatio = Mathf.Lerp(persistentProfile.emaBlockRatio, 1f, Time.deltaTime * 0.15f);
            }
        }
        else if (persistentProfile != null)
        {
            persistentProfile.emaBlockRatio = Mathf.Lerp(persistentProfile.emaBlockRatio, 0f, Time.deltaTime * 0.02f);
        }
    }

    /// <summary>
    /// Oyuncunun sahnedeki en yakın canlı düşmana olan mesafesini döner
    /// </summary>
    public float GetDistanceToNearestEnemy()
    {
        if (player == null) FindPlayer();
        if (player == null) return 2.0f;

        EnemyController[] enemies = FindObjectsOfType<EnemyController>();
        float minDist = float.MaxValue;

        foreach (var ec in enemies)
        {
            if (ec == null || ec.IsDead) continue;
            float d = Vector3.Distance(player.transform.position, ec.transform.position);
            if (d < minDist) minDist = d;
        }

        return minDist == float.MaxValue ? 2.0f : minDist;
    }

    private void UpdateSpatialTracking()
    {
        float liveDist = GetDistanceToNearestEnemy();
        averageDistance = Mathf.Lerp(averageDistance, liveDist, Time.deltaTime * 1.5f);
    }

    /// <summary>
    /// Toplanan canlı ve kalıcı metrikleri karar matrisinden geçirerek tüm düşmanlar için ortak oyuncu profilini belirler
    /// </summary>
    private void EvaluatePlayerArchetype(bool forceBroadcast)
    {
        int windowActions = punchCount + uppercutCount + (isPlayerCurrentlyBlocking ? 1 : 0);
        int effectiveActionCount = Mathf.Max(windowActions, LifetimeTotalActions);

        confidenceScore = Mathf.Clamp01((float)effectiveActionCount / Mathf.Max(1, minActionsForClassification));

        PlayerCombatArchetype newArchetype = (persistentProfile != null && LifetimeTotalActions >= minActionsForClassification)
            ? (PlayerCombatArchetype)persistentProfile.learnedArchetype
            : PlayerCombatArchetype.Balanced;

        if (effectiveActionCount >= minActionsForClassification)
        {
            float upperRatio = UppercutRatio;
            float wRatio = WhiffRatio;
            float bRatio = BlockRatio;
            float avgDist = AverageDistance;
            float atkFreq = AttackFrequency;

            int effectiveUppercuts = Mathf.Max(uppercutCount, persistentProfile != null ? persistentProfile.totalUppercuts : 0);
            int effectiveWhiffs = Mathf.Max(whiffCount, persistentProfile != null ? persistentProfile.totalWhiffs : 0);
            int effectivePunches = Mathf.Max(punchCount, persistentProfile != null ? persistentProfile.totalPunches : 0);

            // 1. Öncelik: Aparkat Spammer (Tehlikeli ağır darbe bağımlısı)
            if (effectiveUppercuts >= 2 && upperRatio >= uppercutSpamRatio)
            {
                newArchetype = PlayerCombatArchetype.UppercutSpammer;
            }
            // 2. Öncelik: Whiff Prone (Mesafe hissi zayıf, boşa savuran)
            else if (effectiveWhiffs >= 2 && wRatio >= whiffRateThreshold)
            {
                newArchetype = PlayerCombatArchetype.WhiffProne;
            }
            // 3. Öncelik: Turtle Defensive (Aşırı gardda bekleyen)
            else if (bRatio >= turtleBlockUptimeThreshold || (isPlayerCurrentlyBlocking && (Time.time - blockStartTime > 1.8f)))
            {
                newArchetype = PlayerCombatArchetype.TurtleDefensive;
            }
            // 4. Öncelik: Punch Spammer (Sürekli düz yumruk tuşlayan)
            else if (effectivePunches >= 4 && upperRatio < 0.28f && atkFreq >= 0.45f)
            {
                newArchetype = PlayerCombatArchetype.PunchSpammer;
            }
            // 5. Öncelik: Outboxer Fleeing (Kaçak dövüşen)
            else if (avgDist > fleeingDistanceThreshold && atkFreq < 0.35f)
            {
                newArchetype = PlayerCombatArchetype.OutboxerFleeing;
            }
            // 6. Öncelik: Brawler Aggressive (Yakın temas baskıcısı)
            else if (avgDist < aggressiveDistanceThreshold && atkFreq >= 0.45f)
            {
                newArchetype = PlayerCombatArchetype.BrawlerAggressive;
            }
            else
            {
                newArchetype = PlayerCombatArchetype.Balanced;
            }
        }

        bool archetypeChanged = (newArchetype != currentArchetype);
        PlayerCombatArchetype oldStyle = currentArchetype;
        currentArchetype = newArchetype;

        if (persistentProfile != null)
        {
            persistentProfile.learnedArchetype = (int)currentArchetype;
            persistentProfile.confidenceScore = confidenceScore;
        }

        if (archetypeChanged)
        {
            Debug.Log($"<color=lime>[ORTAK AI ADAPTASYON ANALİZİ]</color> Oyuncu Dövüş Stili Güncellendi: <b>{oldStyle}</b> ➔ <b><color=yellow>{newArchetype}</color></b> (Güven: %{(confidenceScore * 100):F0}, Aparkat: %{(UppercutRatio * 100):F0}, Iska: %{(WhiffRatio * 100):F0}, Toplam Kayıt: {LifetimeTotalActions})");
            SaveLearnedProfile();
            OnArchetypeChanged?.Invoke(oldStyle, newArchetype);
            OnProfileUpdated?.Invoke(newArchetype);
        }
        else if (forceBroadcast)
        {
            OnProfileUpdated?.Invoke(currentArchetype);
        }
    }

    /// <summary>
    /// Dalga geçişlerinde veya yeniden doğuşlarda öğrenilen kalıcı veriyi SİLMEDEN kaydeder
    /// ve sahnedeki tüm düşmanları ortak profille senkronize eder.
    /// </summary>
    public void OnMatchOrWaveTransition()
    {
        isPlayerCurrentlyBlocking = false;
        SaveLearnedProfile();
        OnProfileUpdated?.Invoke(currentArchetype);
    }

    /// <summary>
    /// Geriye dönük uyumluluk için korunur: Öğrenilen kalıcı veriyi silmez, yalnızca anlık blok bayrağını sıfırlayıp diske kaydeder.
    /// </summary>
    public void ResetTracker()
    {
        OnMatchOrWaveTransition();
    }

    /// <summary>
    /// Yalnızca geliştirici Inspector üzerinden sağ tıklayıp özellikle sıfırlamak isterse tüm kalıcı hafızayı temizler.
    /// </summary>
    [ContextMenu("Öğrenilmiş Kalıcı AI Verisini Sıfırla (Hard Reset)")]
    public void HardResetLearnedData()
    {
        actionHistory.Clear();
        punchCount = 0;
        uppercutCount = 0;
        hitCount = 0;
        whiffCount = 0;
        totalBlockTimeInWindow = 0f;
        isPlayerCurrentlyBlocking = false;
        persistentProfile = new PersistentCombatProfileData();
        currentArchetype = PlayerCombatArchetype.Balanced;
        confidenceScore = 0f;

        if (PlayerPrefs.HasKey(PREFS_KEY))
        {
            PlayerPrefs.DeleteKey(PREFS_KEY);
            PlayerPrefs.Save();
        }

        try
        {
            if (File.Exists(SaveFilePath))
            {
                File.Delete(SaveFilePath);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PlayerCombatTracker] Kalıcı profil dosyası silinirken uyarı: {e.Message}");
        }

        OnProfileUpdated?.Invoke(currentArchetype);
    }

    public void SaveLearnedProfile()
    {
        if (!enablePersistentLearning || persistentProfile == null) return;

        try
        {
            string json = JsonUtility.ToJson(persistentProfile, true);
            PlayerPrefs.SetString(PREFS_KEY, json);
            PlayerPrefs.Save();

            File.WriteAllText(SaveFilePath, json);
            isDirty = false;
            nextAutoSaveTime = Time.unscaledTime + 2.0f;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PlayerCombatTracker] Kalıcı AI öğrenme verisi kaydedilemedi: {e.Message}");
        }
    }

    public void LoadLearnedProfile()
    {
        if (!enablePersistentLearning) return;

        try
        {
            string json = null;
            if (File.Exists(SaveFilePath))
            {
                json = File.ReadAllText(SaveFilePath);
            }
            else if (PlayerPrefs.HasKey(PREFS_KEY))
            {
                json = PlayerPrefs.GetString(PREFS_KEY, "");
            }

            if (!string.IsNullOrEmpty(json))
            {
                PersistentCombatProfileData loaded = JsonUtility.FromJson<PersistentCombatProfileData>(json);
                if (loaded != null)
                {
                    persistentProfile = loaded;
                    currentArchetype = (PlayerCombatArchetype)Mathf.Clamp(
                        persistentProfile.learnedArchetype,
                        0,
                        Enum.GetValues(typeof(PlayerCombatArchetype)).Length - 1);
                    confidenceScore = Mathf.Clamp01(persistentProfile.confidenceScore);

                    Debug.Log($"<color=cyan>[KALICI AI HAFIZASI YÜKLENDİ]</color> Stil: <b>{currentArchetype}</b> | Toplam Hamle: {LifetimeTotalActions} (Yumruk: {persistentProfile.totalPunches}, Aparkat: {persistentProfile.totalUppercuts}, İsabet: {persistentProfile.totalHits}, Iska: {persistentProfile.totalWhiffs})");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PlayerCombatTracker] Kalıcı AI öğrenme verisi yüklenirken uyarı: {e.Message}");
        }
    }

    public string GetDebugSummary()
    {
        return $"[Ortak Kalıcı AI] Stil: {currentArchetype} | Güven: %{(confidenceScore * 100):F0}\n" +
               $"Toplam Hamle: {LifetimeTotalActions} | Aparkat Oranı: %{(UppercutRatio * 100):F0}\n" +
               $"Iska Oranı: %{(WhiffRatio * 100):F0} | Blok Oranı: %{(BlockRatio * 100):F0}\n" +
               $"Ortalama Mesafe: {AverageDistance:F2}m";
    }

    void OnGUI()
    {
        if (!showDebugHUD) return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 14;
        style.normal.textColor = Color.white;
        style.fontStyle = FontStyle.Bold;

        string summary = GetDebugSummary();
        GUI.Box(new Rect(10, 10, 340, 95), "Ortak Kalıcı AI Analizi");
        GUI.Label(new Rect(15, 30, 330, 75), summary, style);
    }
}
