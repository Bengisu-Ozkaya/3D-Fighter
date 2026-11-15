using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Çoklu düşman dövüş koordinatörü (Attack Coordinator & Token System).
/// Çoklu düşman olduğunda hepsinin aynı anda oyuncuya vurup anında nakavt etmesini engeller.
/// Düşmanların tek tek (sırayla) saldırmasını, diğerlerinin halka formasyonunda gard alıp
/// sırasını beklemesini ve oyuncunun kombo yapabileceği nefes alma pencerelerine sahip olmasını sağlar.
/// </summary>
[DefaultExecutionOrder(-50)]
public class EnemyGroupCoordinator : MonoBehaviour
{
    private static EnemyGroupCoordinator _instance;
    public static EnemyGroupCoordinator Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<EnemyGroupCoordinator>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("EnemyGroupCoordinator");
                    _instance = go.AddComponent<EnemyGroupCoordinator>();
                }
            }
            return _instance;
        }
    }

    [Header("ScriptableObject Veritabanları")]
    [SerializeField] private GameBalanceSO gameBalanceConfig;
    [SerializeField] private FighterCombatConfigSO combatConfig;

    [Header("Saldırı Sırası & Token Ayarları")]
    [Tooltip("Aynı anda en fazla kaç düşman saldırabilir (1 = Her seferinde sadece 1 düşman saldırır)")]
    [SerializeField] private int maxConcurrentAttackers = 1;

    [Tooltip("Düşmanların saldırıları arasındaki minimum bekleme süresi (saniye)")]
    [SerializeField] private float minAttackInterval = 1.2f;

    [Tooltip("Düşmanların saldırıları arasındaki maksimum bekleme süresi (saniye)")]
    [SerializeField] private float maxAttackInterval = 1.7f;

    [Tooltip("Oyuncu bir düşmana vurduğunda diğer düşmanların saldırı başlatmasını engelleyen nefes alma süresi (saniye)")]
    [SerializeField] private float playerHitGracePeriod = 1.0f;

    [Tooltip("Bir düşmana saldırı hakkı verildikten sonra saldıramazsa token'ın geri alınma süresi (saniye)")]
    [SerializeField] private float tokenTimeout = 2.8f;

    [Header("Kuşatma & Halka (Ring) Formasyonu Ayarları")]
    [Tooltip("Sırasını bekleyen diğer düşmanların oyuncudan duracağı bekleme mesafesi (metre)")]
    [SerializeField] private float waitingRingDistance = 2.05f;

    [Tooltip("Sırasını bekleyen düşmanların oyuncu etrafında yavaşça dönme (orbit/pacing) hızı")]
    [SerializeField] private float circlePacingSpeed = 0.35f;

    public float WaitingRingDistance => waitingRingDistance;
    public float CirclePacingSpeed => circlePacingSpeed;

    // Sahnedeki aktif düşmanlar
    private readonly List<EnemyController> registeredEnemies = new List<EnemyController>();

    // Şu an saldırı hakkına (Attack Token) sahip olan düşman
    private EnemyController currentAttacker = null;
    private EnemyController lastAttacker = null;
    private float nextAllowedAttackTime = 0f;
    private float tokenAcquiredTime = 0f;

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

        LoadConfigsIfNeeded();
        FindPlayer();
    }

    private void LoadConfigsIfNeeded()
    {
        if (gameBalanceConfig == null)
        {
            gameBalanceConfig = Resources.Load<GameBalanceSO>("GameBalanceConfig");
        }
        if (combatConfig == null)
        {
            combatConfig = Resources.Load<FighterCombatConfigSO>("FighterCombatConfig");
        }

        if (combatConfig != null)
        {
            maxConcurrentAttackers = combatConfig.maxConcurrentAttackers;
            tokenTimeout = combatConfig.tokenTimeout;
            waitingRingDistance = combatConfig.waitingRingDistance;
            circlePacingSpeed = combatConfig.circlePacingSpeed;
        }
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

    /// <summary>
    /// Sahnede birden fazla canlı düşman olup olmadığını döner
    /// </summary>
    public bool HasMultipleEnemies()
    {
        int count = 0;
        for (int i = 0; i < registeredEnemies.Count; i++)
        {
            var e = registeredEnemies[i];
            if (e != null && !e.IsDead && !e.IsPendingUltiDeath)
            {
                count++;
                if (count > 1) return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Yeni bir düşmanı koordinatöre kaydeder
    /// </summary>
    public void RegisterEnemy(EnemyController enemy)
    {
        if (enemy == null) return;
        if (!registeredEnemies.Contains(enemy))
        {
            registeredEnemies.Add(enemy);
        }
    }

    /// <summary>
    /// Ölen veya yok olan düşmanı koordinatörden çıkarır
    /// </summary>
    public void UnregisterEnemy(EnemyController enemy)
    {
        if (enemy == null) return;

        if (currentAttacker == enemy)
        {
            currentAttacker = null;
            // Token boşa çıktığında diğer düşman hemen saldırmasın, kısa bir boşluk ver
            nextAllowedAttackTime = Mathf.Max(nextAllowedAttackTime, Time.time + 0.6f);
        }

        if (lastAttacker == enemy)
        {
            lastAttacker = null;
        }

        registeredEnemies.Remove(enemy);
    }

    /// <summary>
    /// Koordinatör durumunu tamamen sıfırlar (Dalga yeniden başlarken vb.)
    /// </summary>
    public void ResetCoordinator()
    {
        registeredEnemies.Clear();
        currentAttacker = null;
        lastAttacker = null;
        nextAllowedAttackTime = Time.time + 0.8f;
    }

    /// <summary>
    /// Aktif zorluk seviyesine göre saldırı aralıklarını kalibre eder (GameBalanceSO üzerinden)
    /// </summary>
    public void ApplyDifficultySettings(Difficulty difficulty)
    {
        LoadConfigsIfNeeded();
        if (gameBalanceConfig != null)
        {
            DifficultyWaveProfile profile = gameBalanceConfig.GetProfile(difficulty);
            if (profile != null)
            {
                minAttackInterval = profile.minAttackInterval;
                maxAttackInterval = profile.maxAttackInterval;
                playerHitGracePeriod = profile.playerHitGracePeriod;
                return;
            }
        }

        switch (difficulty)
        {
            case Difficulty.Easy:
                minAttackInterval = 1.6f;
                maxAttackInterval = 2.2f;
                playerHitGracePeriod = 1.2f;
                break;
            case Difficulty.Medium:
                minAttackInterval = 1.2f;
                maxAttackInterval = 1.7f;
                playerHitGracePeriod = 1.0f;
                break;
            case Difficulty.Hard:
                minAttackInterval = 0.9f;
                maxAttackInterval = 1.3f;
                playerHitGracePeriod = 0.7f;
                break;
        }
    }

    /// <summary>
    /// Düşmanın oyuncuya doğru yaklaşıp (1.1m) saldırma hakkı olup olmadığını belirler.
    /// Eğer saldırı hakkı yoksa düşman 2.05m bekleme mesafesinde kalır.
    /// </summary>
    public bool ShouldApproachToAttack(EnemyController enemy)
    {
        if (enemy == null || enemy.IsDead || enemy.IsPendingUltiDeath) return false;

        // Tek bir düşman varsa bekleme olmadan direkt oyuncuya yaklaşabilir (1v1 dövüş deneyimi)
        if (!HasMultipleEnemies()) return true;

        // Çoklu düşman varsa sadece token sahibi düşman yaklaşabilir
        return currentAttacker == enemy;
    }

    /// <summary>
    /// Düşmanın saldırıyı fiziksel olarak başlatmasına izin verilip verilmediğini kontrol eder
    /// </summary>
    public bool CanInitiateAttack(EnemyController enemy)
    {
        if (enemy == null || enemy.IsDead || enemy.IsPendingUltiDeath) return false;

        if (player == null) FindPlayer();
        if (player != null && (player.IsDead || player.IsCastingUlti)) return false;

        // Tek düşman varsa normal zamanlamasıyla vurabilir
        if (!HasMultipleEnemies()) return true;

        // Çoklu düşmanda sadece token sahibi ve bekleme süresi dolmuşsa vurabilir
        if (currentAttacker != enemy) return false;
        if (Time.time < nextAllowedAttackTime) return false;

        return true;
    }

    /// <summary>
    /// Düşman saldırıya başladığında çağrılır
    /// </summary>
    public void NotifyAttackStarted(EnemyController enemy)
    {
        currentAttacker = enemy;
    }

    /// <summary>
    /// Düşman saldırısını başarıyla bitirdiğinde çağrılır (Vuruş tamamlandı)
    /// </summary>
    public void NotifyAttackFinished(EnemyController enemy)
    {
        if (currentAttacker == enemy)
        {
            currentAttacker = null;
            lastAttacker = enemy;
            // Bir sonraki saldırıya kadar oyuncuya rahatça nefes alma ve karşılık verme penceresi ver
            nextAllowedAttackTime = Time.time + Random.Range(minAttackInterval, maxAttackInterval);
        }
    }

    /// <summary>
    /// Düşman saldırısı oyuncu tarafından darbe alarak kesildiğinde çağrılır
    /// </summary>
    public void NotifyAttackCancelled(EnemyController enemy)
    {
        if (currentAttacker == enemy)
        {
            currentAttacker = null;
            lastAttacker = enemy;
            // Saldırı kesildiğinde oyuncunun kombosunu tamamlayabilmesi için grace period başlat
            nextAllowedAttackTime = Mathf.Max(nextAllowedAttackTime, Time.time + playerHitGracePeriod);
        }
    }

    /// <summary>
    /// Oyuncu herhangi bir düşmana vurduğunda çağrılır.
    /// Diğer düşmanların oyuncuyu arkadan sıkıştırmasını önlemek için kısa bir serbestlik penceresi sağlar.
    /// </summary>
    public void NotifyPlayerHitEnemy(EnemyController hitEnemy)
    {
        // Oyuncunun kombo akışını bölmemek için bir sonraki düşman saldırısını ertele
        nextAllowedAttackTime = Mathf.Max(nextAllowedAttackTime, Time.time + playerHitGracePeriod);

        // Eğer darbe alan düşman o anki saldırgansa saldırganlığını düşür
        if (currentAttacker == hitEnemy)
        {
            currentAttacker = null;
            lastAttacker = hitEnemy;
        }
    }

    /// <summary>
    /// Sırasını bekleyen düşmanların oyuncu etrafında yavaş ve estetik bir daire çizerek (Orbiting)
    /// dövüş ringi atmosferi oluşturması için teğetsel kuvvet hesaplar
    /// </summary>
    public Vector3 CalculateCirclingForce(EnemyController enemy, Vector3 playerPos)
    {
        if (enemy == null) return Vector3.zero;

        Vector3 diff = enemy.transform.position - playerPos;
        diff.y = 0f;
        float dist = diff.magnitude;

        if (dist < 0.1f) return Vector3.zero;

        // Oyuncu - Düşman eksenine dik teğet vektörü (-z, x)
        Vector3 tangent = new Vector3(-diff.z, 0f, diff.x).normalized;

        // Düşman ID'sine göre saat yönü veya tersi sabit yön ver
        int dir = (Mathf.Abs(enemy.GetInstanceID()) % 2 == 0) ? 1 : -1;
        return tangent * (circlePacingSpeed * dir);
    }

    void Update()
    {
        // Sahneden yok olmuş referansları temizle
        registeredEnemies.RemoveAll(e => e == null);

        if (player == null) FindPlayer();
        if (player != null && (player.IsDead || player.IsCastingUlti))
        {
            currentAttacker = null;
            return;
        }

        // Tek düşman varsa grup koordinasyonuna gerek yok
        if (!HasMultipleEnemies())
        {
            currentAttacker = (registeredEnemies.Count == 1 && !registeredEnemies[0].IsDead)
                ? registeredEnemies[0]
                : null;
            return;
        }

        // Mevcut saldırganın durumunu kontrol et
        if (currentAttacker != null)
        {
            if (currentAttacker.IsDead || currentAttacker.IsPendingUltiDeath ||
                currentAttacker.IsTakingHit || currentAttacker.IsBlocking)
            {
                NotifyAttackCancelled(currentAttacker);
            }
            else if (!currentAttacker.IsAttacking && Time.time - tokenAcquiredTime > tokenTimeout)
            {
                // Saldırı hakkı verildi ama belirlenen sürede vuruş yapamadıysa hakkı devret
                currentAttacker = null;
                nextAllowedAttackTime = Time.time + 0.3f;
            }
        }

        // Eğer şu an saldıran kimse yoksa ve bekleme süresi dolduysa yeni saldırgan seç
        if (currentAttacker == null && Time.time >= nextAllowedAttackTime)
        {
            SelectNextAttacker();
        }
    }

    /// <summary>
    /// Düşmanlar arasından sıradaki saldırganı seçer (Adil ve akıcı dövüş rotasyonu)
    /// </summary>
    private void SelectNextAttacker()
    {
        if (player == null) return;

        List<EnemyController> eligibleEnemies = new List<EnemyController>();
        for (int i = 0; i < registeredEnemies.Count; i++)
        {
            var e = registeredEnemies[i];
            if (e != null && !e.IsDead && !e.IsPendingUltiDeath && !e.IsTakingHit && !e.IsBlocking)
            {
                eligibleEnemies.Add(e);
            }
        }

        if (eligibleEnemies.Count == 0) return;

        // Eğer birden fazla uygun düşman varsa, son saldıran hariç olanları öncelikle değerlendir (Rotasyon)
        EnemyController bestCandidate = null;
        float closestDistance = float.MaxValue;

        // 1. Tercih: Son saldıran HARİÇ en yakın düşman
        if (eligibleEnemies.Count > 1 && lastAttacker != null)
        {
            foreach (var e in eligibleEnemies)
            {
                if (e == lastAttacker) continue;
                float d = Vector3.Distance(e.transform.position, player.transform.position);
                if (d < closestDistance)
                {
                    closestDistance = d;
                    bestCandidate = e;
                }
            }
        }

        // 2. Fallback: Genel olarak en yakın düşman
        if (bestCandidate == null)
        {
            closestDistance = float.MaxValue;
            foreach (var e in eligibleEnemies)
            {
                float d = Vector3.Distance(e.transform.position, player.transform.position);
                if (d < closestDistance)
                {
                    closestDistance = d;
                    bestCandidate = e;
                }
            }
        }

        if (bestCandidate != null)
        {
            currentAttacker = bestCandidate;
            tokenAcquiredTime = Time.time;
        }
    }
}
