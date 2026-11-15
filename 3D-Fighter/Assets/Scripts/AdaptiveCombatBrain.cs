using System.Collections;
using UnityEngine;

/// <summary>
/// Düşman Adaptif Dövüş Beyni (Adaptive Combat Brain).
/// PlayerCombatTracker'dan gelen canlı oyuncu stilini ve telemetriyi dinler;
/// düşmanın saldırı, savunma, menzil ve kontra reflekslerini oyuncunun
/// zaaflarına göre dinamik olarak şekillendirir.
/// </summary>
[RequireComponent(typeof(EnemyController))]
public class AdaptiveCombatBrain : MonoBehaviour
{
    [Header("Referanslar")]
    [SerializeField] private EnemyController enemy;

    [Header("Adaptasyon Ayarları")]
    [Tooltip("Adaptif zeka aktif mi? Devre dışı bırakılırsa düşman standart davranışını sergiler.")]
    [SerializeField] private bool isAdaptiveAIEnabled = true;

    [Tooltip("Oyuncu boşa vurduğunda (whiff) cezalandırıcı kontra atak yapma şansı")]
    [Range(0f, 1f)]
    [SerializeField] private float whiffPunishChance = 0.85f;

    [Tooltip("İnsan refleksi gecikmesi (saniye). 0 olursa robotik/hile gibi hissettirir, 0.15 - 0.22 insan hissi verir.")]
    [SerializeField] private float humanReactionDelay = 0.18f;

    [Tooltip("Whiff sonrası oyuncuya doğru ani hamle yapabileceği maksimum mesafe")]
    [SerializeField] private float whiffPunishMaxDistance = 2.2f;

    // Düşmanın başlangıç temel değerleri
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
    }

    void Start()
    {
        if (enemy == null)
        {
            enemy = GetComponent<EnemyController>();
        }

        // Temel değerleri kaydet
        if (enemy != null)
        {
            baseAttackRange = enemy.AttackRange;
            baseMoveSpeed = enemy.MoveSpeed;
            baseAttackCooldown = enemy.AttackCooldown;
            baseHitsToTriggerBlock = enemy.HitsToTriggerBlock;
            baseBlockChance = enemy.BlockChance;
            baseBlockDuration = enemy.BlockDuration;
            baseUppercutChance = enemy.UppercutChance;
        }

        // Oyuncunun güncel stilini hemen uygula
        if (PlayerCombatTracker.Instance != null)
        {
            ApplyStyleAdaptation(PlayerCombatTracker.Instance.CurrentArchetype);
        }
    }

    void OnEnable()
    {
        PlayerCombatTracker.OnArchetypeChanged += HandleArchetypeChanged;
        PlayerCombatTracker.OnPlayerWhiffed += HandlePlayerWhiffed;
    }

    void OnDisable()
    {
        PlayerCombatTracker.OnArchetypeChanged -= HandleArchetypeChanged;
        PlayerCombatTracker.OnPlayerWhiffed -= HandlePlayerWhiffed;
    }

    private void HandleArchetypeChanged(PlayerCombatArchetype oldStyle, PlayerCombatArchetype newStyle)
    {
        if (!isAdaptiveAIEnabled || enemy == null || enemy.IsDead) return;
        ApplyStyleAdaptation(newStyle);
    }

    /// <summary>
    /// Oyuncunun tespit edilen yeni stiline göre düşmanın tüm dövüş parametrelerini yeniden kalibre eder
    /// </summary>
    public void ApplyStyleAdaptation(PlayerCombatArchetype style)
    {
        if (enemy == null || !isAdaptiveAIEnabled) return;

        currentAppliedArchetype = style;

        switch (style)
        {
            // 1. OYUNCU APARKAT SPAMLİYOR:
            // Aparkatın hasarı yüksek ama toparlanması yavaş.
            // Düşman gardını hemen kaldırır, darbe eşiğini 1'e düşürür ve aparkat sonrası kontra için hazır bekler.
            case PlayerCombatArchetype.UppercutSpammer:
                enemy.HitsToTriggerBlock = 1;
                enemy.BlockChance = 0.95f;
                enemy.BlockDuration = baseBlockDuration * 1.3f;
                enemy.AttackCooldown = baseAttackCooldown * 0.85f;
                enemy.UppercutChance = 0.20f;
                enemy.MoveSpeed = baseMoveSpeed;
                enemy.AttackRange = baseAttackRange;
                Debug.Log($"<color=cyan>[ADAPTİF ZEKA]</color> {gameObject.name} <color=yellow>APARKAT SPAM'e</color> karşı gardını güçlendirdi (1 Darbede Blok, Hızlı Kontra Hazır)!");
                break;

            // 2. OYUNCU DÜZ YUMRUK SPAMLİYOR:
            // Oyuncu aralıksız sol/sağ basıyor. Düşman gard alıp oyuncunun kombo akışını sert bir Aparkat ile böler.
            case PlayerCombatArchetype.PunchSpammer:
                enemy.HitsToTriggerBlock = 2;
                enemy.BlockChance = 0.90f;
                enemy.BlockDuration = baseBlockDuration;
                enemy.UppercutChance = 0.55f; // Kombo kesici ağır darbe
                enemy.AttackCooldown = baseAttackCooldown * 0.80f;
                enemy.MoveSpeed = baseMoveSpeed;
                enemy.AttackRange = baseAttackRange;
                Debug.Log($"<color=cyan>[ADAPTİF ZEKA]</color> {gameObject.name} <color=yellow>YUMRUK SPAM'e</color> karşı aparkat şansını artırdı (%55)! Seriyi bozacak.");
                break;

            // 3. OYUNCU BOŞA SALLIYOR (WHIFF PRONE):
            // Düşman oyuncunun menzilinin hemen kıyısında (tuzak mesafesi) bekler, oyuncu ıskaladığı an içeri dalar.
            case PlayerCombatArchetype.WhiffProne:
                enemy.AttackRange = 1.30f; // Hafif açıkta durup ıskalatma tuzağı
                enemy.MoveSpeed = baseMoveSpeed * 1.15f;
                enemy.AttackCooldown = baseAttackCooldown * 0.90f;
                enemy.BlockChance = baseBlockChance;
                enemy.HitsToTriggerBlock = baseHitsToTriggerBlock;
                enemy.UppercutChance = baseUppercutChance;
                Debug.Log($"<color=cyan>[ADAPTİF ZEKA]</color> {gameObject.name} oyuncunun <color=yellow>ISKA EĞİLİMİNİ</color> sezdi. Tuzak mesafesine geçti (1.30m)!");
                break;

            // 4. OYUNCU SÜREKLİ BLOKTA BEKLİYOR (TURTLE):
            // Düşman gard kırmak için aparkat şansını tavan yapar (Aparkat gardı kırar).
            case PlayerCombatArchetype.TurtleDefensive:
                enemy.UppercutChance = 0.70f; // Gard kırma (Guard Break) için yüksek aparkat
                enemy.AttackCooldown = baseAttackCooldown * 0.75f; // Kesintisiz baskı
                enemy.MoveSpeed = baseMoveSpeed * 1.10f;
                enemy.HitsToTriggerBlock = baseHitsToTriggerBlock;
                enemy.AttackRange = baseAttackRange;
                Debug.Log($"<color=cyan>[ADAPTİF ZEKA]</color> {gameObject.name} oyuncunun <color=yellow>GARDA YATTIĞINI</color> gördü. Gard kırmak için Aparkat moduna geçti (%70)!");
                break;

            // 5. OYUNCU SÜREKLİ GERİ KAÇIYOR (OUTBOXER / FLEEING):
            // Düşman daha hızlı yaklaşır, ringi keser ve oyuncuyu köşeye sıkıştırır.
            case PlayerCombatArchetype.OutboxerFleeing:
                enemy.MoveSpeed = baseMoveSpeed * 1.30f; // Kaçanı yakalamak için hız artışı
                enemy.AttackCooldown = baseAttackCooldown * 0.85f;
                enemy.AttackRange = baseAttackRange;
                enemy.HitsToTriggerBlock = baseHitsToTriggerBlock;
                enemy.UppercutChance = baseUppercutChance;
                Debug.Log($"<color=cyan>[ADAPTİF ZEKA]</color> {gameObject.name} <color=yellow>KAÇAK DÖVÜŞEN</color> oyuncunun üzerine hızla hücum ediyor (Hız: {enemy.MoveSpeed:F1})!");
                break;

            // 6. OYUNCU SÜREKLİ BURNUNA GİRİYOR (BRAWLER / AGGRESSIVE):
            // Düşman yakın temasta darbeyi hemen emer, gard alır ve sert karşılık verir.
            case PlayerCombatArchetype.BrawlerAggressive:
                enemy.HitsToTriggerBlock = 1;
                enemy.BlockChance = 0.85f;
                enemy.UppercutChance = 0.40f;
                enemy.AttackCooldown = baseAttackCooldown * 0.80f;
                enemy.MoveSpeed = baseMoveSpeed * 0.95f;
                Debug.Log($"<color=cyan>[ADAPTİF ZEKA]</color> {gameObject.name} <color=yellow>AGRESİF OYUNCUYA</color> karşı yakın dövüş savunmasını sıkılaştırdı!");
                break;

            // DENGELİ / VARSAYILAN:
            case PlayerCombatArchetype.Balanced:
            default:
                enemy.AttackRange = baseAttackRange;
                enemy.MoveSpeed = baseMoveSpeed;
                enemy.AttackCooldown = baseAttackCooldown;
                enemy.HitsToTriggerBlock = baseHitsToTriggerBlock;
                enemy.BlockChance = baseBlockChance;
                enemy.BlockDuration = baseBlockDuration;
                enemy.UppercutChance = baseUppercutChance;
                break;
        }
    }

    /// <summary>
    /// Oyuncu boşa yumruk salladığında (Whiff) tetiklenir:
    /// Düşman insani bir refleks süresi bekleyip, oyuncunun gard toparlanma açığından faydalanarak içeri dalar.
    /// </summary>
    private void HandlePlayerWhiffed(CombatActionType actionType, Vector3 playerPos)
    {
        if (!isAdaptiveAIEnabled || enemy == null || enemy.IsDead) return;
        if (enemy.IsBlocking || enemy.IsTakingHit || enemy.IsAttacking || enemy.IsPendingUltiDeath) return;

        // Kontra şansı kontrolü
        if (Random.value > whiffPunishChance) return;

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
        // 1. İnsani reaksiyon payı (İçeri hile hissi vermemek için)
        yield return new WaitForSeconds(humanReactionDelay);

        if (enemy == null || enemy.IsDead || enemy.IsTakingHit || enemy.IsBlocking || enemy.IsAttacking)
        {
            yield break;
        }

        // Oyuncu özellikle toparlanması uzun olan Aparkat'ı boşa salladıysa cezalandırma çok daha etkilidir
        bool isHighValuePunish = (whiffedAction == CombatActionType.Uppercut);

        Debug.Log($"<color=yellow>[AI CEZALANDIRMA / WHIFF PUNISH]</color> {gameObject.name} oyuncunun boşa salladığı <b>{whiffedAction}</b> açığını yakaladı! Kontra saldırı başlatılıyor!");

        bool executed = enemy.TryTriggerCounterAttack();
        if (!executed)
        {
            // Eğer mesafesi azıcık uzaktaysa hızlıca yaklaşmayı dener
            enemy.MoveSpeed = baseMoveSpeed * 1.35f;
        }

        yield return new WaitForSeconds(0.4f);

        // Hızı normale döndür
        if (enemy != null && !enemy.IsDead)
        {
            enemy.MoveSpeed = (currentAppliedArchetype == PlayerCombatArchetype.OutboxerFleeing) ? baseMoveSpeed * 1.30f : baseMoveSpeed;
        }

        counterAttackRoutine = null;
    }
}
