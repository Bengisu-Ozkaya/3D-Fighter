using System.Collections;
using UnityEngine;

/// <summary>
/// Düşman Adaptif Dövüş Beyni (Shared Adaptive Combat Brain).
/// Tüm düşmanlar PlayerCombatTracker üzerindeki TEK ORTAK ve KALICI öğrenme havuzunu paylaşır.
/// Hangi düşman doğarsa doğsun (Elvis, Ortiz, Grand Mom, Adem, Emine, Hüsnü, Kezban),
/// hepsi oyuncunun öğrenilmiş alışkanlıklarına karşı birebir aynı ortak stratejiyi uygular.
/// </summary>
[RequireComponent(typeof(EnemyController))]
public class AdaptiveCombatBrain : MonoBehaviour
{
    [Header("Referanslar")]
    [SerializeField] private EnemyController enemy;

    [Header("Adaptasyon Ayarları")]
    [Tooltip("Adaptif zeka aktif mi? Devre dışı bırakılırsa düşman standart davranışını sergiler.")]
    [SerializeField] private bool isAdaptiveAIEnabled = true;

    [Tooltip("İnsan refleksi gecikmesi (saniye). 0 olursa robotik/hile gibi hissettirir, 0.15 - 0.22 insan hissi verir.")]
    [SerializeField] private float humanReactionDelay = 0.18f;

    [Tooltip("Whiff sonrası oyuncuya doğru ani hamle yapabileceği maksimum mesafe")]
    [SerializeField] private float whiffPunishMaxDistance = 2.2f;

    // Tüm düşmanlar için standartlaştırılmış temel değerler (Prefab farklarını ortadan kaldırır)
    private float baseAttackRange = 1.1f;
    private float baseMoveSpeed = 1.2f;
    private float baseAttackCooldown = 1.8f;
    private int baseHitsToTriggerBlock = 2;
    private float baseBlockChance = 1.0f;
    private float baseBlockDuration = 1.2f;
    private float baseUppercutChance = 0.25f;

    private Coroutine counterAttackRoutine = null;
    private PlayerCombatArchetype currentAppliedArchetype = PlayerCombatArchetype.Balanced;

    void Awake()
    {
        if (enemy == null)
        {
            enemy = GetComponent<EnemyController>();
        }
        RefreshCanonicalBaseStats();
    }

    void Start()
    {
        if (enemy == null)
        {
            enemy = GetComponent<EnemyController>();
        }

        RefreshCanonicalBaseStats();
        SyncWithSharedTracker();
    }

    void OnEnable()
    {
        PlayerCombatTracker.OnArchetypeChanged += HandleArchetypeChanged;
        PlayerCombatTracker.OnProfileUpdated += HandleProfileUpdated;
        PlayerCombatTracker.OnPlayerWhiffed += HandlePlayerWhiffed;
    }

    void OnDisable()
    {
        PlayerCombatTracker.OnArchetypeChanged -= HandleArchetypeChanged;
        PlayerCombatTracker.OnProfileUpdated -= HandleProfileUpdated;
        PlayerCombatTracker.OnPlayerWhiffed -= HandlePlayerWhiffed;
    }

    /// <summary>
    /// Tüm düşmanların prefab farklılıklarından etkilenmemesi için temel değerleri
    /// ortak FighterCombatConfigSO ve aktif zorluk profilinden (GameBalanceSO) alır.
    /// </summary>
    public void RefreshCanonicalBaseStats()
    {
        FighterCombatConfigSO combatConfig = Resources.Load<FighterCombatConfigSO>("FighterCombatConfig");
        if (combatConfig != null)
        {
            baseMoveSpeed = combatConfig.enemyMoveSpeed;
            baseAttackRange = combatConfig.enemyAttackRange;
            baseAttackCooldown = combatConfig.enemyAttackCooldown;
        }
        else if (enemy != null)
        {
            baseMoveSpeed = 1.2f;
            baseAttackRange = 1.1f;
            baseAttackCooldown = 1.8f;
        }

        Difficulty diff = Difficulty.Easy;
        EnemySpawner spawner = FindObjectOfType<EnemySpawner>();
        if (spawner != null)
        {
            diff = spawner.CurrentDifficulty;
        }

        GameBalanceSO balanceConfig = Resources.Load<GameBalanceSO>("GameBalanceConfig");
        if (balanceConfig != null)
        {
            DifficultyWaveProfile profile = balanceConfig.GetProfile(diff);
            if (profile != null)
            {
                baseHitsToTriggerBlock = profile.hitsToTriggerBlock;
                baseBlockChance = profile.blockChance;
                baseBlockDuration = profile.blockDuration;
                baseUppercutChance = 0.25f;
                return;
            }
        }

        baseHitsToTriggerBlock = 2;
        baseBlockChance = 1.0f;
        baseUppercutChance = 0.25f;
        switch (diff)
        {
            case Difficulty.Easy:
                baseBlockDuration = 1.0f;
                break;
            case Difficulty.Medium:
                baseBlockDuration = 1.2f;
                break;
            case Difficulty.Hard:
                baseBlockDuration = 1.4f;
                break;
        }
    }

    /// <summary>
    /// EnemyController zorluk ayarlarını güncellediğinde çağrılır; ortak AI adaptasyonunu anında yeniden uygular.
    /// </summary>
    public void SyncWithSharedTracker()
    {
        if (PlayerCombatTracker.Instance != null)
        {
            ApplyStyleAdaptation(PlayerCombatTracker.Instance.CurrentArchetype);
        }
    }

    private void HandleArchetypeChanged(PlayerCombatArchetype oldStyle, PlayerCombatArchetype newStyle)
    {
        if (!isAdaptiveAIEnabled || enemy == null || enemy.IsDead) return;
        ApplyStyleAdaptation(newStyle);
    }

    private void HandleProfileUpdated(PlayerCombatArchetype style)
    {
        if (!isAdaptiveAIEnabled || enemy == null || enemy.IsDead) return;
        ApplyStyleAdaptation(style);
    }

    /// <summary>
    /// Oyuncunun ortak ve kalıcı öğrenme profiline göre tüm düşmanların dövüş parametrelerini
    /// birebir aynı ortak kurallarla kalibre eder.
    /// </summary>
    public void ApplyStyleAdaptation(PlayerCombatArchetype style)
    {
        if (enemy == null || !isAdaptiveAIEnabled) return;

        currentAppliedArchetype = style;

        PlayerCombatTracker tracker = PlayerCombatTracker.Instance;
        float learnedUpperRatio = tracker != null ? tracker.UppercutRatio : 0.25f;
        float learnedBlockRatio = tracker != null ? tracker.BlockRatio : 0.10f;
        float confidence = tracker != null ? tracker.ConfidenceScore : 0f;

        switch (style)
        {
            // 1. OYUNCU APARKAT SPAMLİYOR:
            // Tüm düşmanlar darbe eşiğini 1'e düşürür, gardını uzun tutar ve aparkat sonrası hızlı kontra yapar.
            case PlayerCombatArchetype.UppercutSpammer:
                enemy.HitsToTriggerBlock = 1;
                enemy.BlockChance = 1.0f;
                enemy.BlockDuration = baseBlockDuration * 1.3f;
                enemy.AttackCooldown = baseAttackCooldown * 0.85f;
                enemy.UppercutChance = 0.30f;
                enemy.MoveSpeed = baseMoveSpeed;
                enemy.AttackRange = baseAttackRange;
                break;

            // 2. OYUNCU DÜZ YUMRUK SPAMLİYOR:
            // Tüm düşmanlar gard alıp oyuncunun kombo akışını yüksek olasılıklı Aparkat ile böler.
            case PlayerCombatArchetype.PunchSpammer:
                enemy.HitsToTriggerBlock = 1;
                enemy.BlockChance = 0.95f;
                enemy.BlockDuration = baseBlockDuration;
                enemy.UppercutChance = 0.55f;
                enemy.AttackCooldown = baseAttackCooldown * 0.80f;
                enemy.MoveSpeed = baseMoveSpeed;
                enemy.AttackRange = baseAttackRange;
                break;

            // 3. OYUNCU BOŞA SALLIYOR (WHIFF PRONE):
            // Tüm düşmanlar tuzak mesafesinde bekler ve oyuncu ıskaladığı an hızla içeri girer.
            case PlayerCombatArchetype.WhiffProne:
                enemy.AttackRange = 1.25f;
                enemy.MoveSpeed = baseMoveSpeed * 1.15f;
                enemy.AttackCooldown = baseAttackCooldown * 0.88f;
                enemy.BlockChance = baseBlockChance;
                enemy.HitsToTriggerBlock = baseHitsToTriggerBlock;
                enemy.UppercutChance = Mathf.Clamp(baseUppercutChance + learnedUpperRatio * 0.25f, 0.25f, 0.55f);
                break;

            // 4. OYUNCU SÜREKLİ BLOKTA BEKLİYOR (TURTLE):
            // Tüm düşmanlar gard kırmak için aparkat oranını yükseltir ve baskıyı artırır.
            case PlayerCombatArchetype.TurtleDefensive:
                enemy.UppercutChance = 0.70f;
                enemy.AttackCooldown = baseAttackCooldown * 0.75f;
                enemy.MoveSpeed = baseMoveSpeed * 1.12f;
                enemy.HitsToTriggerBlock = baseHitsToTriggerBlock;
                enemy.BlockChance = baseBlockChance;
                enemy.AttackRange = baseAttackRange;
                break;

            // 5. OYUNCU SÜREKLİ GERİ KAÇIYOR (OUTBOXER / FLEEING):
            // Tüm düşmanlar hızlanarak mesafeyi kapatır.
            case PlayerCombatArchetype.OutboxerFleeing:
                enemy.MoveSpeed = baseMoveSpeed * 1.30f;
                enemy.AttackCooldown = baseAttackCooldown * 0.85f;
                enemy.AttackRange = baseAttackRange;
                enemy.HitsToTriggerBlock = baseHitsToTriggerBlock;
                enemy.BlockChance = baseBlockChance;
                enemy.UppercutChance = baseUppercutChance;
                break;

            // 6. OYUNCU SÜREKLİ BURNUNA GİRİYOR (BRAWLER / AGGRESSIVE):
            // Tüm düşmanlar ilk darbede gard alır ve sert karşılık verir.
            case PlayerCombatArchetype.BrawlerAggressive:
                enemy.HitsToTriggerBlock = 1;
                enemy.BlockChance = 0.95f;
                enemy.UppercutChance = 0.45f;
                enemy.AttackCooldown = baseAttackCooldown * 0.80f;
                enemy.MoveSpeed = baseMoveSpeed;
                enemy.AttackRange = baseAttackRange;
                break;

            // DENGELİ / VARSAYILAN (Yine de kalıcı öğrenilmiş oranlarla ortak ince ayar yapar):
            case PlayerCombatArchetype.Balanced:
            default:
                enemy.AttackRange = baseAttackRange;
                enemy.MoveSpeed = baseMoveSpeed;
                enemy.AttackCooldown = Mathf.Lerp(baseAttackCooldown, baseAttackCooldown * 0.88f, confidence * 0.5f);
                enemy.HitsToTriggerBlock = (confidence >= 0.6f && learnedUpperRatio >= 0.45f) ? 1 : baseHitsToTriggerBlock;
                enemy.BlockChance = baseBlockChance;
                enemy.BlockDuration = baseBlockDuration;
                enemy.UppercutChance = Mathf.Clamp(baseUppercutChance + (learnedBlockRatio * 0.35f), 0.25f, 0.65f);
                break;
        }
    }

    /// <summary>
    /// Oyuncu boşa yumruk salladığında (Whiff) tetiklenir:
    /// Bireysel rastgele zar atmak yerine ortak koordinasyonla en yakın/aktif düşman cezalandırma yapar.
    /// </summary>
    private void HandlePlayerWhiffed(CombatActionType actionType, Vector3 playerPos)
    {
        if (!isAdaptiveAIEnabled || enemy == null || enemy.IsDead) return;
        if (enemy.IsBlocking || enemy.IsTakingHit || enemy.IsAttacking || enemy.IsPendingUltiDeath) return;

        // Çoklu düşman varsa yalnızca saldırı hakkına sahip olan (veya en yakın) düşman kontra yapsın ki hepsi aynı kararlılıkla davransın
        if (EnemyGroupCoordinator.Instance != null && EnemyGroupCoordinator.Instance.HasMultipleEnemies())
        {
            if (!EnemyGroupCoordinator.Instance.ShouldApproachToAttack(enemy))
            {
                return;
            }
        }

        float distance = Vector3.Distance(transform.position, playerPos);
        if (distance > whiffPunishMaxDistance) return;

        if (counterAttackRoutine != null)
        {
            StopCoroutine(counterAttackRoutine);
        }
        counterAttackRoutine = StartCoroutine(WhiffPunishRoutine(actionType));
    }

    private IEnumerator WhiffPunishRoutine(CombatActionType whiffedAction)
    {
        yield return new WaitForSeconds(humanReactionDelay);

        if (enemy == null || enemy.IsDead || enemy.IsTakingHit || enemy.IsBlocking || enemy.IsAttacking)
        {
            yield break;
        }

        bool executed = enemy.TryTriggerCounterAttack();
        if (!executed)
        {
            enemy.MoveSpeed = baseMoveSpeed * 1.35f;
        }

        yield return new WaitForSeconds(0.4f);

        if (enemy != null && !enemy.IsDead)
        {
            SyncWithSharedTracker();
        }

        counterAttackRoutine = null;
    }
}
