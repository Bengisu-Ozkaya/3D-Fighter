using System;
using System.Collections.Generic;
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
/// Oyuncunun canlı dövüş verilerine göre sınıflandırılan dövüş stili / profili
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
/// Oyuncu Dövüş Takipçisi ve Canlı Profilleme Sistemi (Player Combat Telemetry & Profiler).
/// Oyuncunun son X saniyedeki (Kayan Pencere / Rolling Window) tüm vuruş, ıska, gard ve mesafe
/// tercihlerini analiz ederek AI düşmanlara gerçek zamanlı stil bilgisi sunar.
/// </summary>
[DefaultExecutionOrder(-60)]
public class PlayerCombatTracker : MonoBehaviour
{
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
    [Tooltip("Gözlem yapılan son zaman aralığı (saniye). Eski veriler bu süreden sonra atılır.")]
    [SerializeField] private float analysisWindowDuration = 10.0f;

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

    [Header("Debug Görselleştirme")]
    [Tooltip("Ekranda gerçek zamanlı AI analiz metriklerini göster")]
    [SerializeField] private bool showDebugHUD = false;

    // Canlı durum
    private PlayerCombatArchetype currentArchetype = PlayerCombatArchetype.Balanced;
    public PlayerCombatArchetype CurrentArchetype => currentArchetype;

    // Olay bildirimleri (Events)
    public static event Action<PlayerCombatArchetype, PlayerCombatArchetype> OnArchetypeChanged;
    public static event Action<CombatActionType, Vector3> OnPlayerWhiffed;
    public static event Action<CombatActionType> OnPlayerAttacked;

    // Aksiyon geçmişi (Sliding buffer)
    private readonly List<CombatTelemetryEvent> actionHistory = new List<CombatTelemetryEvent>();

    // Gard süresi takibi
    private bool isPlayerCurrentlyBlocking = false;
    private float blockStartTime = 0f;
    private float totalBlockTimeInWindow = 0f;

    // Canlı istatistikler
    private int punchCount = 0;
    private int uppercutCount = 0;
    private int hitCount = 0;
    private int whiffCount = 0;
    private float averageDistance = 1.5f;
    private float attackFrequency = 0f;
    private float confidenceScore = 0f;

    public float UppercutRatio => (punchCount + uppercutCount) > 0 ? (float)uppercutCount / (punchCount + uppercutCount) : 0f;
    public float WhiffRatio => (hitCount + whiffCount) > 0 ? (float)whiffCount / (hitCount + whiffCount) : 0f;
    public float BlockRatio => analysisWindowDuration > 0 ? Mathf.Clamp01(totalBlockTimeInWindow / analysisWindowDuration) : 0f;
    public float AverageDistance => averageDistance;
    public float AttackFrequency => attackFrequency;
    public float ConfidenceScore => confidenceScore;
    public int TotalAttacksInWindow => punchCount + uppercutCount;

    private PlayerController player;

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

        FindPlayer();
    }

    void Start()
    {
        FindPlayer();
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
        EvaluatePlayerArchetype();
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
    }

    /// <summary>
    /// Oyuncunun vuruş penceresi bittiğinde hiçbir şeye isabet etmediyse (Iska / Whiff) çağrılır
    /// </summary>
    public void RecordAttackWhiff(CombatActionType actionType)
    {
        float dist = GetDistanceToNearestEnemy();
        actionHistory.Add(new CombatTelemetryEvent(Time.time, actionType, false, dist));

        Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;
        OnPlayerWhiffed?.Invoke(actionType, playerPos);
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
            float duration = Time.time - blockStartTime;
            totalBlockTimeInWindow += duration;
        }
    }

    /// <summary>
    /// Kayan pencere süresinden (analysisWindowDuration) eski olan aksiyonları temizler ve sayaçları günceller
    /// </summary>
    private void CleanOldRecords()
    {
        float cutoffTime = Time.time - analysisWindowDuration;

        actionHistory.RemoveAll(e => e.timestamp < cutoffTime);

        // Sayaçları sıfırlayıp baştan topla
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
    }

    /// <summary>
    /// Oyuncunun pencere içerisindeki aktif gard süresini günceller
    /// </summary>
    private void UpdateBlockTracking()
    {
        // Zaman geçtikçe eski blok süresini pencere oranında sönümle
        totalBlockTimeInWindow = Mathf.Max(0f, totalBlockTimeInWindow - (Time.deltaTime * 0.15f));

        if (isPlayerCurrentlyBlocking)
        {
            totalBlockTimeInWindow += Time.deltaTime;
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
        // En yakın düşmana olan mesafeyi periyodik olarak canlı tut
        float liveDist = GetDistanceToNearestEnemy();
        averageDistance = Mathf.Lerp(averageDistance, liveDist, Time.deltaTime * 1.5f);
    }

    /// <summary>
    /// Toplanan metrikleri karar matrisinden geçirerek oyuncunun güncel profilini belirler
    /// </summary>
    private void EvaluatePlayerArchetype()
    {
        int totalActions = punchCount + uppercutCount + (isPlayerCurrentlyBlocking ? 1 : 0);
        confidenceScore = Mathf.Clamp01((float)totalActions / minActionsForClassification);

        PlayerCombatArchetype newArchetype = PlayerCombatArchetype.Balanced;

        // Güven skoru yeterli değilse dengeli (Balanced) kal
        if (totalActions >= minActionsForClassification)
        {
            float upperRatio = UppercutRatio;
            float wRatio = WhiffRatio;
            float bRatio = BlockRatio;

            // 1. Öncelik: Aparkat Spammer (Tehlikeli ağır darbe bağımlısı)
            if (uppercutCount >= 2 && upperRatio >= uppercutSpamRatio)
            {
                newArchetype = PlayerCombatArchetype.UppercutSpammer;
            }
            // 2. Öncelik: Whiff Prone (Mesafe hissi zayıf, boşa savuran)
            else if (whiffCount >= 2 && wRatio >= whiffRateThreshold)
            {
                newArchetype = PlayerCombatArchetype.WhiffProne;
            }
            // 3. Öncelik: Turtle Defensive (Aşırı gardda bekleyen)
            else if (bRatio >= turtleBlockUptimeThreshold || (isPlayerCurrentlyBlocking && (Time.time - blockStartTime > 1.8f)))
            {
                newArchetype = PlayerCombatArchetype.TurtleDefensive;
            }
            // 4. Öncelik: Punch Spammer (Sürekli düz yumruk tuşlayan)
            else if (punchCount >= 4 && upperRatio < 0.25f && attackFrequency >= 0.5f)
            {
                newArchetype = PlayerCombatArchetype.PunchSpammer;
            }
            // 5. Öncelik: Outboxer Fleeing (Kaçak dövüşen)
            else if (averageDistance > fleeingDistanceThreshold && attackFrequency < 0.35f)
            {
                newArchetype = PlayerCombatArchetype.OutboxerFleeing;
            }
            // 6. Öncelik: Brawler Aggressive (Yakın temas baskıcısı)
            else if (averageDistance < aggressiveDistanceThreshold && attackFrequency >= 0.45f)
            {
                newArchetype = PlayerCombatArchetype.BrawlerAggressive;
            }
        }

        if (newArchetype != currentArchetype)
        {
            PlayerCombatArchetype oldStyle = currentArchetype;
            currentArchetype = newArchetype;
            Debug.Log($"<color=lime>[AI ADAPTASYON ANALİZİ]</color> Oyuncu Dövüş Stili Değişti: <b>{oldStyle}</b> ➔ <b><color=yellow>{newArchetype}</color></b> (Güven: %{(confidenceScore * 100):F0}, Aparkat: %{(UppercutRatio * 100):F0}, Iska: %{(WhiffRatio * 100):F0})");
            OnArchetypeChanged?.Invoke(oldStyle, newArchetype);
        }
    }

    /// <summary>
    /// Yeni dalga, raunt veya maç başında analiz havuzunu sıfırlar
    /// </summary>
    public void ResetTracker()
    {
        actionHistory.Clear();
        punchCount = 0;
        uppercutCount = 0;
        hitCount = 0;
        whiffCount = 0;
        totalBlockTimeInWindow = 0f;
        isPlayerCurrentlyBlocking = false;
        currentArchetype = PlayerCombatArchetype.Balanced;
        confidenceScore = 0f;
    }

    public string GetDebugSummary()
    {
        return $"[AI Tracker] Profil: {currentArchetype} | Güven: %{(confidenceScore * 100):F0}\n" +
               $"Yumruk: {punchCount} | Aparkat: {uppercutCount} (Oran: %{(UppercutRatio * 100):F0})\n" +
               $"İsabet: {hitCount} | Iska: {whiffCount} (Iska Oranı: %{(WhiffRatio * 100):F0})\n" +
               $"Ortalama Mesafe: {averageDistance:F2}m | Blok Oranı: %{(BlockRatio * 100):F0}";
    }

    void OnGUI()
    {
        if (!showDebugHUD) return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 14;
        style.normal.textColor = Color.white;
        style.fontStyle = FontStyle.Bold;

        string summary = GetDebugSummary();
        GUI.Box(new Rect(10, 10, 320, 95), "AI Canlı Oyuncu Analizi");
        GUI.Label(new Rect(15, 30, 310, 75), summary, style);
    }
}
