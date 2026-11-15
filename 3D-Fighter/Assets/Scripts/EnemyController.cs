using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [Header("ScriptableObject Veritabanları")]
    [SerializeField] private FighterCombatConfigSO combatConfig;
    [SerializeField] private GameBalanceSO gameBalanceConfig;

    [Header("Sağlık Ayarları")]
    [SerializeField] float health = 30f;
    [SerializeField] float maxHealth = 30f;

    [Header("Darbe / Geri Tepme Ayarları")]
    [SerializeField] float knockbackDistance = 0.2f;

    public float CurrentHealth => health;
    public float MaxHealth => maxHealth;

    /// <summary>
    /// Spawner veya zorluk moduna göre düşmanın maksimum canını ve mevcut canını ayarlar
    /// </summary>
    public void SetHealth(float newMaxHealth)
    {
        maxHealth = newMaxHealth;
        health = newMaxHealth;
    }

    [SerializeField] Animator enemyAnimator;

    [Header("Saldırı & Takip Ayarları")]
    [Tooltip("Düşmanın saldırıya geçeceği yaklaşma mesafesi")]
    [SerializeField] float attackRange = 1.1f;       // Yumruk yaklaşma mesafesi
    [Tooltip("Yumruğun temas edebileceği maksimum mesafe (Oyuncu geri kaçtıysa ıskalar)")]
    [SerializeField] float maxHitDistance = 1.25f;
    [SerializeField] float attackCooldown = 1.8f;    // Kaç saniyede bir saldıracak
    [SerializeField] float moveSpeed = 1.2f;        // Oyuncuya yaklaşma hızı (0 yapılırsa yerinde durur)
    [Tooltip("Düşmanın normal yumruk vuruşunun vereceği hasar")]
    [SerializeField] float attackDamage = 10f;      // Player'a vereceği hasar
    [Tooltip("Düşmanın aparkat vuruşunun vereceği hasar")]
    [SerializeField] float uppercutDamage = 10f;    // Player'a vereceği aparkat hasarı
    [Tooltip("Düşmanın aparkat atma olasılığı (0.25 = %25, Adaptif Yapay Zeka tarafından dinamik yönetilir)")]
    [Range(0.05f, 0.95f)]
    [SerializeField] float uppercutChance = 0.25f;

    public float AttackRange { get => attackRange; set => attackRange = value; }
    public float MoveSpeed { get => moveSpeed; set => moveSpeed = value; }
    public float AttackCooldown { get => attackCooldown; set => attackCooldown = value; }
    public int HitsToTriggerBlock { get => hitsToTriggerBlock; set => hitsToTriggerBlock = value; }
    public float BlockChance { get => blockChance; set => blockChance = value; }
    public float BlockDuration { get => blockDuration; set => blockDuration = value; }
    public float UppercutChance { get => uppercutChance; set => uppercutChance = value; }
    private Transform playerTransform;
    private PlayerController playerController;
    private UIManager uiManager;
    private float nextAttackTime = 0f;

    [Header("Blok & Gard Ayarları (Senaryo 1: Ardışık Darbe Savunması)")]
    [Tooltip("Düşmanın gard pozisyonuna geçmesi için arka arkaya yemesi gereken darbe sayısı (Normal veya Aparkat)")]
    [SerializeField] int hitsToTriggerBlock = 2;
    [Tooltip("Gereken darbe sayısına ulaşıldığında blok yapma olasılığı (1 = %100 kesin blok)")]
    [Range(0.1f, 1f)]
    [SerializeField] float blockChance = 1.0f;
    [Tooltip("Düşmanın gardını havada tutacağı süre (saniye)")]
    [SerializeField] float blockDuration = 1.2f;
    [Tooltip("Blok bittikten sonra tekrar blok yapabilmesi için bekleme süresi")]
    [SerializeField] float blockCooldown = 0.5f;
    [Tooltip("Oyuncu vurmayı bırakırsa ardışık vuruş sayacının sıfırlanma süresi (saniye)")]
    [SerializeField] float comboResetThreshold = 1.8f;

    private bool isBlocking = false;
    public bool IsBlocking => isBlocking;

    private bool isPendingUltiDeath = false;
    public bool IsPendingUltiDeath => isPendingUltiDeath;

    private bool isAttacking = false;
    public bool IsAttacking => isAttacking;

    private bool isTakingHit = false;
    public bool IsTakingHit => isTakingHit;

    private Coroutine currentAttackCoroutine = null;
    private Coroutine hitStunCoroutine = null;

    private int consecutiveHitsTaken = 0;
    private float lastHitTakenTime = 0f;
    private float nextBlockAvailableTime = 0f;
    private Coroutine blockCoroutine = null;

    void Awake()
    {
        if (combatConfig == null)
        {
            combatConfig = Resources.Load<FighterCombatConfigSO>("FighterCombatConfig");
        }
        if (gameBalanceConfig == null)
        {
            gameBalanceConfig = Resources.Load<GameBalanceSO>("GameBalanceConfig");
        }

        if (combatConfig != null)
        {
            moveSpeed = combatConfig.enemyMoveSpeed;
            attackRange = combatConfig.enemyAttackRange;
            maxHitDistance = combatConfig.enemyMaxHitDistance;
            attackCooldown = combatConfig.enemyAttackCooldown;
            bodyRadius = combatConfig.enemyBodyRadius;
            separationRadius = combatConfig.enemySeparationRadius;
            separationStrength = combatConfig.enemySeparationStrength;
            comboResetThreshold = combatConfig.comboResetThreshold;
        }
    }

    void Start()
    {
        if (RingBoundary.Instance != null && RingBoundary.Instance.CurrentArenaConfig != null)
        {
            SetArenaGroundY(RingBoundary.Instance.CurrentArenaConfig.groundY);
        }

        // Aktif zorluk moduna ve dalgaya göre saldırı hasarını, aparkat hasarını ve canını belirle
        EnemySpawner spawner = FindObjectOfType<EnemySpawner>();
        if (spawner != null && spawner.IsGameStarted)
        {
            var waveStats = spawner.GetWaveStatsForWave(spawner.CurrentWave);
            attackDamage = waveStats.enemyPunchDamage;
            uppercutDamage = waveStats.enemyUppercutDamage;
            SetHealth(waveStats.enemyHealth);
            ApplyDifficultyBlockSettings(spawner.CurrentDifficulty);
        }

        //Enemy
        if (maxHealth <= 0) maxHealth = 30f;
        if (health <= 0) health = maxHealth;
        if (enemyAnimator == null || enemyAnimator.gameObject != gameObject)
        {
            enemyAnimator = GetComponent<Animator>();
            if (enemyAnimator == null)
            {
                enemyAnimator = GetComponentInChildren<Animator>();
            }
        }
        if (enemyAnimator != null) enemyAnimator.applyRootMotion = false;

        // Kararlı yakın dövüş mesafesi ayarı
        if (attackRange > 1.25f) attackRange = 1.1f;
        if (maxHitDistance <= 0f || maxHitDistance > 1.4f) maxHitDistance = 1.25f;

        //Player
        playerController = FindObjectOfType<PlayerController>();
        if (playerController != null)
        {
            playerTransform = playerController.transform;
        }

        // Çoklu düşman grup koordinatörüne bu düşmanı kaydet
        if (EnemyGroupCoordinator.Instance != null)
        {
            EnemyGroupCoordinator.Instance.RegisterEnemy(this);
        }

        // Adaptif Dövüş Beyni (AdaptiveCombatBrain) kontrolü ve ortak kalıcı hafızayla senkronizasyonu
        AdaptiveCombatBrain brain = GetComponent<AdaptiveCombatBrain>();
        if (brain == null)
        {
            brain = gameObject.AddComponent<AdaptiveCombatBrain>();
        }
        brain.RefreshCanonicalBaseStats();
        brain.SyncWithSharedTracker();
    }

    void OnDestroy()
    {
        if (EnemyGroupCoordinator.Instance != null)
        {
            EnemyGroupCoordinator.Instance.UnregisterEnemy(this);
        }
    }

    /// <summary>
    /// Spawner tarafından zorluk moduna ve dalgaya uygun normal yumruk hasarını atar
    /// </summary>
    public void SetAttackDamage(float damage)
    {
        attackDamage = damage;
    }

    /// <summary>
    /// Spawner tarafından zorluk moduna ve dalgaya uygun aparkat hasarını atar
    /// </summary>
    public void SetUppercutDamage(float damage)
    {
        uppercutDamage = damage;
    }

    public float GetUppercutDamage() => uppercutDamage;

    [Header("Pozisyon & Yükseklik (Y) Ayarları (Unity Inspector'dan Düzenlenebilir)")]
    [Tooltip("Normal ayaktayken ve yürürken Y pozisyonu")]
    [SerializeField] float standingYPosition = 0.1f;

    [Tooltip("Düşman nakavt olup yere düştüğündeki Y pozisyonu")]
    [SerializeField] float fallenYPosition = 0.45f;

    /// <summary>
    /// Aktif Texture Arena ortamının zemin Y yüksekliğine göre düşmanın ayakta ve yerde yatma Y yüksekliğini kalibre eder
    /// </summary>
    public void SetArenaGroundY(float groundY)
    {
        standingYPosition = groundY;
        fallenYPosition = groundY;
        Vector3 pos = transform.position;
        pos.y = isDead ? fallenYPosition : standingYPosition;
        transform.position = pos;
    }

    void LateUpdate()
    {
        // Düşman ayaktayken standingYPosition, nakavt olup yerde yatarken fallenYPosition
        Vector3 pos = transform.position;
        float targetY = isDead ? fallenYPosition : standingYPosition;
        if (pos.y != targetY)
        {
            pos.y = targetY;
            transform.position = pos;
        }
        if (!isDead)
        {
            pos = RingBoundary.ClampToArena(pos, bodyRadius);
        }
        transform.position = pos;
    }

    private bool isDead = false;
    public bool IsDead => isDead;

    public void SetPlayerDead(bool dead)
    {
        if (dead)
        {
            StopAttack();
            StopBlocking();
            if (hitStunCoroutine != null)
            {
                StopCoroutine(hitStunCoroutine);
                hitStunCoroutine = null;
            }
            isTakingHit = false;
        }

        if (enemyAnimator != null)
        {
            enemyAnimator.SetBool("rightMove", false);
            enemyAnimator.SetBool("leftMove", false);
            enemyAnimator.SetBool("isDeadEnemy", dead);
            if (!dead)
            {
                enemyAnimator.CrossFadeInFixedTime("Idle", 0.15f);
            }
        }
    }

    void Update()
    {
        if (health <= 0 && !isDead)
        {
            if (isPendingUltiDeath) return;
            Die();
            return;
        }

        if (isDead) return;

        if (playerTransform != null)
        {
            // Eğer oyuncu öldüyse saldırmayı ve hareketi durdur, Show Pose'da bekle
            if (playerController != null && playerController.IsDead)
            {
                if (enemyAnimator != null)
                {
                    enemyAnimator.SetBool("isDeadEnemy", true);
                }
                return;
            }
            else
            {
                if (enemyAnimator != null && enemyAnimator.GetBool("isDeadEnemy"))
                {
                    enemyAnimator.SetBool("isDeadEnemy", false);
                    enemyAnimator.CrossFadeInFixedTime("Idle", 0.2f);
                }
            }

            CombatPlayer();
        }
    }

    [Header("Çarpışma & Ayrışma (Avoidance) Ayarları")]
    [Tooltip("Düşmanın gövde yarıçapı. İç içe geçmeleri engeller.")]
    [SerializeField] float bodyRadius = 0.45f;
    [Tooltip("Düşmanların birbirini hissettiği ve ayrışmaya başladığı mesafe")]
    [SerializeField] float separationRadius = 0.40f;
    [Tooltip("Düşmanların birbirini itme ve oyuncunun etrafına yayılma gücü")]
    [SerializeField] float separationStrength = 0.40f;

    void CombatPlayer()
    {
        float distance = Vector3.Distance(transform.position, playerTransform.position);

        // 1. Oyuncuya doğru yüzünü dön (Y ekseninde)
        Vector3 lookDirection = (playerTransform.position - transform.position).normalized;
        lookDirection.y = 0;
        if (lookDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDirection), Time.deltaTime * 6f);
        }

        // Eğer 3-2-1 geri sayımı aktifse veya düşman şu an blok yapıyorsa, darbe alıyorsa, saldırıdaysa ya da Ulti sonrası ölüm gecikmesindeyse: hareket edip saldırmasın
        if (UIManager.IsCountingDown || isBlocking || isTakingHit || isPendingUltiDeath || isAttacking)
        {
            if (enemyAnimator != null)
            {
                enemyAnimator.SetBool("rightMove", false);
                enemyAnimator.SetBool("leftMove", false);
            }
            ResolveOverlaps();
            return;
        }

        // 2. Diğer düşmanlardan kaçınma (Separation) vektörünü hesapla
        Vector3 separationForce = CalculateSeparationForce();

        // 3. Çoklu düşman grup koordinatör kontrolü (Saldırı Token'ı)
        bool isCoordinatorActive = EnemyGroupCoordinator.Instance != null && EnemyGroupCoordinator.Instance.HasMultipleEnemies();
        bool shouldApproachToAttack = !isCoordinatorActive || EnemyGroupCoordinator.Instance.ShouldApproachToAttack(this);

        if (shouldApproachToAttack)
        {
            // === SALDIRI HAKKINA SAHİP DÜŞMAN (Veya Tek Düşman) ===
            // Oyuncuya yumruk mesafesine (attackRange = 1.1m) kadar yaklaşır ve vurur
            if (distance > attackRange)
            {
                if (enemyAnimator != null)
                {
                    enemyAnimator.SetBool("rightMove", true);
                }

                if (moveSpeed > 0)
                {
                    Vector3 toPlayer = (playerTransform.position - transform.position).normalized;
                    toPlayer.y = 0f;

                    // Oyuncuya gidiş yönü ile diğer düşmanlardan kaçınma yönünü harmanla
                    Vector3 combinedDir = (toPlayer + separationForce).normalized;
                    Vector3 newPos = transform.position + combinedDir * moveSpeed * Time.deltaTime;
                    newPos.y = standingYPosition;
                    newPos = RingBoundary.ClampToArena(newPos, bodyRadius);
                    transform.position = newPos;
                }
            }
            else
            {
                if (enemyAnimator != null)
                {
                    enemyAnimator.SetBool("rightMove", false);
                }

                // Düşmanlar saldırı mesafesinde olsa bile üst üste binmesinler, hafifçe yana açılsınlar
                if (separationForce.sqrMagnitude > 0.01f)
                {
                    Vector3 sidePush = separationForce * (moveSpeed * 0.7f) * Time.deltaTime;
                    sidePush.y = 0f;
                    Vector3 sidePos = RingBoundary.ClampToArena(transform.position + sidePush, bodyRadius);
                    transform.position = sidePos;
                }

                if (Time.time >= nextAttackTime && !isAttacking && !isTakingHit && !isBlocking)
                {
                    if (!isCoordinatorActive || EnemyGroupCoordinator.Instance.CanInitiateAttack(this))
                    {
                        AttackPlayer();
                    }
                }
            }
        }
        else
        {
            // === SIRASINI BEKLEYEN DÜŞMANLAR (Halka Formasyonu) ===
            // Oyuncunun etrafında bekleme mesafesinde (2.05m) kalarak gard alır ve sırasını bekler
            float ringDistance = EnemyGroupCoordinator.Instance != null ? EnemyGroupCoordinator.Instance.WaitingRingDistance : 2.05f;

            if (distance > ringDistance + 0.15f)
            {
                // Halkaya doğru ilerle
                if (enemyAnimator != null)
                {
                    enemyAnimator.SetBool("rightMove", true);
                }

                if (moveSpeed > 0)
                {
                    Vector3 toPlayer = (playerTransform.position - transform.position).normalized;
                    toPlayer.y = 0f;

                    Vector3 combinedDir = (toPlayer + separationForce).normalized;
                    Vector3 newPos = transform.position + combinedDir * (moveSpeed * 0.95f) * Time.deltaTime;
                    newPos.y = standingYPosition;
                    newPos = RingBoundary.ClampToArena(newPos, bodyRadius);
                    transform.position = newPos;
                }
            }
            else if (distance < ringDistance - 0.25f)
            {
                // Çok yaklaştıysa (örn. önceki saldırıdan kalma veya oyuncu üzerine yürüdüyse) geriye çekilip halkayı koru
                if (enemyAnimator != null)
                {
                    enemyAnimator.SetBool("rightMove", false);
                }

                Vector3 awayFromPlayer = (transform.position - playerTransform.position).normalized;
                awayFromPlayer.y = 0f;

                Vector3 retreatDir = (awayFromPlayer + separationForce).normalized;
                Vector3 newPos = transform.position + retreatDir * (moveSpeed * 0.7f) * Time.deltaTime;
                newPos.y = standingYPosition;
                newPos = RingBoundary.ClampToArena(newPos, bodyRadius);
                transform.position = newPos;
            }
            else
            {
                // Tam halka mesafesinde: dur, gardını koru ve oyuncunun etrafında estetik hafif daire çiz
                if (enemyAnimator != null)
                {
                    enemyAnimator.SetBool("rightMove", false);
                }

                Vector3 pacingForce = EnemyGroupCoordinator.Instance != null
                    ? EnemyGroupCoordinator.Instance.CalculateCirclingForce(this, playerTransform.position)
                    : Vector3.zero;

                Vector3 combinedSide = separationForce + pacingForce;
                if (combinedSide.sqrMagnitude > 0.001f)
                {
                    Vector3 sidePush = combinedSide * (moveSpeed * 0.6f) * Time.deltaTime;
                    sidePush.y = 0f;
                    Vector3 sidePos = RingBoundary.ClampToArena(transform.position + sidePush, bodyRadius);
                    transform.position = sidePos;
                }
            }
        }

        // 4. Sert iç içe geçmeleri anında çöz (Push-back)
        ResolveOverlaps();
    }

    /// <summary>
    /// Yakındaki diğer canlı düşmanları tespit edip onlardan uzaklaştıracak bir itme kuvveti üretir
    /// </summary>
    Vector3 CalculateSeparationForce()
    {
        Vector3 force = Vector3.zero;
        EnemyController[] allEnemies = FindObjectsOfType<EnemyController>();

        foreach (var other in allEnemies)
        {
            if (other == null || other == this || other.IsDead) continue;

            Vector3 diff = transform.position - other.transform.position;
            diff.y = 0f;
            float dist = diff.magnitude;

            if (dist > 0.001f && dist < separationRadius)
            {
                // Ne kadar yakınsa o kadar kuvvetle ters yöne iter
                float factor = 1f - (dist / separationRadius);
                force += diff.normalized * (factor * separationStrength);
            }
        }

        return force;
    }

    /// <summary>
    /// Hem diğer düşmanlarla hem de oyuncuyla temas ettiğinde gövdelerin iç içe girmesini engeller (Fiziksel İtme)
    /// </summary>
    void ResolveOverlaps()
    {
        // 1. Düşman - Düşman çarpışma engeli
        EnemyController[] allEnemies = FindObjectsOfType<EnemyController>();
        foreach (var other in allEnemies)
        {
            if (other == null || other == this || other.IsDead) continue;

            Vector3 diff = transform.position - other.transform.position;
            diff.y = 0f;
            float dist = diff.magnitude;
            float minDist = bodyRadius * 1.2f; // İki düşmanın temas çapı

            if (dist < minDist && dist > 0.001f)
            {
                float overlap = minDist - dist;
                Vector3 pushDir = diff.normalized;
                transform.position += pushDir * (overlap * 0.5f);
            }
        }

        // 2. Düşman - Oyuncu çarpışma engeli (Düşman oyuncunun içine giremez)
        if (playerTransform != null && playerController != null && !playerController.IsDead)
        {
            Vector3 diff = transform.position - playerTransform.position;
            diff.y = 0f;
            float dist = diff.magnitude;
            float minPlayerDist = bodyRadius + 0.45f; // Oyuncu gövdesi + Düşman gövdesi

            if (dist < minPlayerDist && dist > 0.001f)
            {
                float overlap = minPlayerDist - dist;
                Vector3 pushDir = diff.normalized;
                transform.position += pushDir * overlap;
            }
        }

        // Düşman gövdesini arena sınırları ve köşe direkleri içinde tut
        transform.position = RingBoundary.ClampToArena(transform.position, bodyRadius);
    }

    /// <summary>
    /// Adaptif yapay zeka (AdaptiveCombatBrain) tarafından çağrılır:
    /// Oyuncu boşa vurduğunda veya açığı yakalandığında bekleme süresini sıfırlayıp anında kontra saldırı başlatır.
    /// </summary>
    public bool TryTriggerCounterAttack()
    {
        if (isDead || isAttacking || isTakingHit || isBlocking || isPendingUltiDeath) return false;
        if (playerTransform == null || playerController == null || playerController.IsDead) return false;

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        if (dist <= maxHitDistance * 1.15f)
        {
            AttackPlayer();
            return true;
        }
        return false;
    }

    void AttackPlayer()
    {
        if (isDead || isTakingHit || isAttacking || isBlocking || isPendingUltiDeath) return;
        if (playerTransform == null || playerController == null || playerController.IsDead) return;

        // 1. Oyuncuya tam cepheden yüzünü dön
        Vector3 dir = (playerTransform.position - transform.position).normalized;
        dir.y = 0f;
        if (dir != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(dir);
        }

        if (enemyAnimator != null)
        {
            enemyAnimator.SetBool("rightMove", false);
            enemyAnimator.SetBool("leftMove", false);
        }

        isAttacking = true;
        if (EnemyGroupCoordinator.Instance != null)
        {
            EnemyGroupCoordinator.Instance.NotifyAttackStarted(this);
        }

        // Oyuncunun canını kontrol et: Can <= uppercutDamage ise veya dövüş dinamizmi için aparkat yap (Adaptif Zeka yönetir)
        float playerHealth = playerController != null ? playerController.GetHealth() : 100f;
        bool isFinisher = playerHealth <= uppercutDamage;
        bool isUppercutAttack = isFinisher || (Random.value < uppercutChance);

        if (isUppercutAttack)
        {
            Debug.Log($"<color=red>[DÜŞMAN APARKAT ATTI!]</color> Hasar: {uppercutDamage} (Bitirici: {isFinisher})");
            if (enemyAnimator != null)
            {
                enemyAnimator.CrossFadeInFixedTime("Uppercut", 0.12f);
            }
            if (currentAttackCoroutine != null) StopCoroutine(currentAttackCoroutine);
            // Aparkat: isFromRight=false → oyuncuya ağır sol/üstten vuruş (Left Damage)
            currentAttackCoroutine = StartCoroutine(EnemyAttackRoutine(0.35f, true, false));
        }
        else
        {
            // Normal yumruk (PunchLeft veya PunchRight) — sağ mı sol mu seçildiğini kaydet
            bool useRightPunch = Random.value > 0.5f;
            string punchTrigger = useRightPunch ? "PunchRight" : "PunchLeft";
            if (enemyAnimator != null)
            {
                enemyAnimator.ResetTrigger("PunchLeft");
                enemyAnimator.ResetTrigger("PunchRight");
                enemyAnimator.SetTrigger(punchTrigger);
            }
            if (currentAttackCoroutine != null) StopCoroutine(currentAttackCoroutine);
            // Düşman PunchRight attığında oyuncunun sağ tarafına çarpar → isFromRight=true
            // Düşman PunchLeft attığında oyuncunun sol tarafına çarpar → isFromRight=false
            currentAttackCoroutine = StartCoroutine(EnemyAttackRoutine(0.30f, false, useRightPunch));
        }
    }

    IEnumerator EnemyAttackRoutine(float windup, bool isUppercut, bool isFromRight = true)
    {
        yield return new WaitForSeconds(windup);

        // Darbe yemişse (StopAttack çağrılmışsa), ölmüşse veya oyuncu ölmüşse hasar verme!
        if (isDead || isTakingHit || isPendingUltiDeath)
        {
            isAttacking = false;
            yield break;
        }

        if (playerTransform != null && playerController != null && !playerController.IsDead)
        {
            if (playerController.IsCastingUlti)
            {
                Debug.Log("<color=magenta>[DÜŞMAN VURAMADI]</color> Oyuncu Ulti atarken dokunulmaz!");
            }
            else
            {
                float currentDistance = Vector3.Distance(transform.position, playerTransform.position);
                Vector3 dirToPlayer = (playerTransform.position - transform.position).normalized;
                dirToPlayer.y = 0f;
                float dot = Vector3.Dot(transform.forward, dirToPlayer);

                if (currentDistance <= maxHitDistance && dot > 0.25f)
                {
                    float damageToDeal = isUppercut ? uppercutDamage : attackDamage;
                    playerController.TakeDamage(damageToDeal, isUppercut, isFromRight);
                }
                else
                {
                    Debug.Log("<color=yellow>[DÜŞMAN ISKALADI]</color> Oyuncu menzil dışına çıktı!");
                }
            }
        }

        // Saldırı sonrası toparlanma süresi (Recovery)
        float recovery = isUppercut ? 0.50f : 0.35f;
        yield return new WaitForSeconds(recovery);

        if (!isDead && !isTakingHit && !isBlocking && enemyAnimator != null)
        {
            enemyAnimator.CrossFadeInFixedTime("Idle", 0.15f);
        }

        isAttacking = false;
        currentAttackCoroutine = null;
        nextAttackTime = Time.time + attackCooldown;
        if (EnemyGroupCoordinator.Instance != null)
        {
            EnemyGroupCoordinator.Instance.NotifyAttackFinished(this);
        }
    }

    public void StopAttack()
    {
        bool wasAttacking = isAttacking || currentAttackCoroutine != null;
        if (currentAttackCoroutine != null)
        {
            StopCoroutine(currentAttackCoroutine);
            currentAttackCoroutine = null;
        }
        isAttacking = false;

        if (enemyAnimator != null)
        {
            enemyAnimator.ResetTrigger("PunchLeft");
            enemyAnimator.ResetTrigger("PunchRight");
        }

        if (wasAttacking && EnemyGroupCoordinator.Instance != null)
        {
            EnemyGroupCoordinator.Instance.NotifyAttackCancelled(this);
        }
    }

    // Hasar alma fonksiyonu
    public void TakeDamage(float damageAmount, bool isUppercut = false, bool isUlti = false)
    {
        if (isDead) return;

        // OYUNCU ÖNCE VURDUYSA: Düşmanın devam eden saldırısı ANINDA İPTAL EDİLİR!
        StopAttack();

        // Grup koordinatörüne bildir (Diğer düşmanların saldırmasını engelleyip oyuncuya nefes payı verir)
        if (EnemyGroupCoordinator.Instance != null)
        {
            EnemyGroupCoordinator.Instance.NotifyPlayerHitEnemy(this);
        }

        // Önceki sersemleme coroutine'ini durdur
        if (hitStunCoroutine != null)
        {
            StopCoroutine(hitStunCoroutine);
            hitStunCoroutine = null;
        }

        bool fromUlti = isUlti || (playerController != null && playerController.IsCastingUlti);

        // 1. DÜŞMAN ZATEN BLOKTAYSA (GARD ALMIŞSA)
        if (isBlocking)
        {
            // Aparkat veya Ulti ile gardı parçala (Guard Break)
            if (isUppercut || isUlti || damageAmount >= 20f)
            {
                Debug.Log($"<color=magenta>[GARD KIRILDI! / GUARD BREAK!]</color> {gameObject.name} gard almışken güçlü darbe yedi! Gard parçalandı! Hasar: {damageAmount}");
                StopBlocking();
                consecutiveHitsTaken = 0;
            }
            // Normal yumruk garda çarpar, hasar almaz
            else
            {
                Debug.Log($"<color=yellow>[DÜŞMAN BLOKLADI!]</color> {gameObject.name} gelen normal yumruğu gardıyla savuşturdu! (Hasar Alınmadı)");
                NotifyUIEnemyHit();
                return;
            }
        }
        // 2. DÜŞMAN BLOKTA DEĞİLKEN DARBE ALIYOR (Normal, Aparkat fark etmeksizin)
        else
        {
            // Kombo zaman aşımı kontrolü (araya 1.8 saniyeden fazla süre girdiyse sayacı sıfırla)
            if (Time.time - lastHitTakenTime > comboResetThreshold)
            {
                consecutiveHitsTaken = 0;
            }
            lastHitTakenTime = Time.time;
            consecutiveHitsTaken++;
        }

        health = Mathf.Max(0f, health - damageAmount);
        NotifyUIEnemyHit();
        Debug.Log($"<color=orange>[DÜŞMAN DARBE ALDI]</color> {gameObject.name} -{damageAmount} can kaybetti! Kalan Can: {health} (Aparkat: {isUppercut}, Ulti: {fromUlti}, Darbe Sayacı: {consecutiveHitsTaken}/{hitsToTriggerBlock})");

        // Darbe alınca hafif geriye çekilme (Knockback) kullanıcı isteğiyle kaldırıldı
        // SetPosition();

        // Aparkat veya Ulti darbelerinde ekstra sarsıntı tepkisi (Kullanıcı isteğiyle kaldırıldı)
        if (isUppercut || isUlti || damageAmount >= 20f)
        {
            // transform.position += (-transform.forward) * (knockbackDistance * 1.5f);
            // Vector3 pushPos = transform.position + (-transform.forward) * (knockbackDistance * 1.5f);
            // pushPos = RingBoundary.ClampToArena(pushPos, bodyRadius);
            // transform.position = pushPos;
        }

        // ÖLÜM KONTROLÜ
        if (health <= 0)
        {
            if (fromUlti)
            {
                Debug.Log($"<color=magenta>[DÜŞMAN ULTİ İLE YENİLDİ!]</color> {gameObject.name} Ulti animasyonu bitene kadar sersemleyecek, ardından nakavt olacak!");
                StartCoroutine(DelayedDeathAfterUlti());
                return;
            }

            Die();
            return;
        }

        // 3. ART ARDA 2 DARBE KONTROLÜ (Normal+Normal, Aparkat+Normal, Aparkat+Aparkat vb.)
        // Ulti darbesinde düşman gard alamaz, doğrudan Ulti sarsıntısına girer!
        if (!fromUlti && !isBlocking && consecutiveHitsTaken >= hitsToTriggerBlock && Time.time >= nextBlockAvailableTime)
        {
            consecutiveHitsTaken = 0;
            StartBlocking();
            return; // Düşman gard aldı, GetHit animasyonu Center Block'u ezmesin!
        }

        // 4. DARBE ANİMASYONU VE SERSEMLEME (HIT STUN):
        // Düşman darbe aldığında saldırı yapamaz, hareket edemez ve animasyon net şekilde oynatılır
        hitStunCoroutine = StartCoroutine(EnemyHitStunRoutine(isUppercut || fromUlti || damageAmount >= 20f, fromUlti));
        nextAttackTime = Mathf.Max(nextAttackTime, Time.time + attackCooldown * 0.8f);
    }

    IEnumerator EnemyHitStunRoutine(bool isHeavy, bool isUltiHit = false)
    {
        isTakingHit = true;
        if (enemyAnimator != null)
        {
            enemyAnimator.SetBool("rightMove", false);
            enemyAnimator.SetBool("leftMove", false);
            if (isHeavy)
            {
                enemyAnimator.ResetTrigger("GetHit");
                enemyAnimator.SetTrigger("GetHeadHit");
                enemyAnimator.CrossFadeInFixedTime("Head Hit", 0.06f);
            }
            else
            {
                enemyAnimator.ResetTrigger("GetHeadHit");
                enemyAnimator.SetTrigger("GetHit");
                enemyAnimator.CrossFadeInFixedTime("Hit", 0.06f);
            }
        }

        if (isUltiHit)
        {
            if (playerController == null)
            {
                playerController = FindObjectOfType<PlayerController>();
            }

            float timeout = 4.0f;
            float hitAnimTimer = 0.35f;
            while (playerController != null && playerController.IsCastingUlti && timeout > 0f && !isDead)
            {
                hitAnimTimer -= Time.deltaTime;
                timeout -= Time.deltaTime;
                if (hitAnimTimer <= 0f && enemyAnimator != null)
                {
                    enemyAnimator.ResetTrigger("GetHeadHit");
                    enemyAnimator.SetTrigger("GetHit");
                    enemyAnimator.CrossFadeInFixedTime("Hit", 0.05f);
                    hitAnimTimer = 0.35f;
                }
                yield return null;
            }
        }
        else
        {
            float stunDuration = isHeavy ? 0.60f : 0.42f;
            yield return new WaitForSeconds(stunDuration);
        }

        isTakingHit = false;
        hitStunCoroutine = null;

        if (!isDead && !isAttacking && !isBlocking && !isPendingUltiDeath && enemyAnimator != null)
        {
            enemyAnimator.CrossFadeInFixedTime("Idle", 0.15f);
        }
    }

    /// <summary>
    /// Ulti darbesiyle ölen düşmanların, oyuncunun Ulti animasyonu tamamen bitene kadar sersemleyip
    /// hemen ardından nakavt (Knockout) animasyonunu oynatmasını sağlar.
    /// </summary>
    IEnumerator DelayedDeathAfterUlti()
    {
        if (isPendingUltiDeath || isDead) yield break;
        isPendingUltiDeath = true;

        StopBlocking();

        // Ulti darbesi anında düşman kafadan şiddetli darbe tepkisi (Head Hit) ile sersemlesin
        if (enemyAnimator != null)
        {
            enemyAnimator.ResetTrigger("GetHit");
            enemyAnimator.SetTrigger("GetHeadHit");
            enemyAnimator.CrossFadeInFixedTime("Head Hit", 0.08f);
        }

        if (playerController == null)
        {
            playerController = FindObjectOfType<PlayerController>();
        }

        // Oyuncunun Ulti animasyonu tamamlanana kadar bekle ve bu sırada sürekli darbe (Right Hit / Head Hit) animasyonu oynat
        float timeout = 4.0f; // Güvenlik zaman aşımı
        float hitAnimTimer = 0f;

        while (playerController != null && playerController.IsCastingUlti && timeout > 0f)
        {
            if (hitAnimTimer <= 0f)
            {
                if (enemyAnimator != null)
                {
                    enemyAnimator.ResetTrigger("GetHeadHit");
                    enemyAnimator.SetTrigger("GetHit");
                    // Titreme hissiyatı için Hit animasyonunu baştan oynat
                    enemyAnimator.CrossFadeInFixedTime("Hit", 0.05f);
                }
                hitAnimTimer = 0.35f; // Her 0.35 saniyede bir darbe almış gibi tepki versin
            }

            hitAnimTimer -= Time.deltaTime;
            timeout -= Time.deltaTime;
            yield return null;
        }

        // Oyuncu ultiyi bitirip gardına dönerken yere yığılma başlasın
        yield return new WaitForSeconds(0.1f);

        isPendingUltiDeath = false;
        Die();
    }

    /// <summary>
    /// Düşmanı gard (blok) pozisyonuna sokar
    /// </summary>
    public void StartBlocking()
    {
        if (isDead || isBlocking) return;

        if (blockCoroutine != null)
        {
            StopCoroutine(blockCoroutine);
        }
        blockCoroutine = StartCoroutine(BlockRoutine());
    }

    IEnumerator BlockRoutine()
    {
        isBlocking = true;
        Debug.Log($"<color=cyan>[DÜŞMAN GARDA GEÇTİ]</color> {gameObject.name} gardını kaldırdı (Blok başladı, Süre: {blockDuration:F1}s)!");

        // Blok süresince düşmanın saldırı sayacını ileriye ertele
        nextAttackTime = Mathf.Max(nextAttackTime, Time.time + blockDuration + 0.4f);

        if (enemyAnimator != null && !isDead)
        {
            enemyAnimator.ResetTrigger("GetHit");
            enemyAnimator.ResetTrigger("GetHeadHit");
            enemyAnimator.CrossFadeInFixedTime("Center Block", 0.1f);
        }

        yield return new WaitForSeconds(blockDuration);

        StopBlocking();
    }

    /// <summary>
    /// Gardı indirip normal dövüş (Idle) pozisyonuna döner
    /// </summary>
    public void StopBlocking()
    {
        if (!isBlocking) return;

        isBlocking = false;
        nextBlockAvailableTime = Time.time + blockCooldown;

        if (blockCoroutine != null)
        {
            StopCoroutine(blockCoroutine);
            blockCoroutine = null;
        }

        if (enemyAnimator != null && !isDead)
        {
            enemyAnimator.CrossFadeInFixedTime("Idle", 0.15f);
        }
    }

    /// <summary>
    /// Aktif zorluk seviyesine göre blok süresini kalibre eder (GameBalanceSO üzerinden)
    /// </summary>
    public void ApplyDifficultyBlockSettings(Difficulty diff)
    {
        if (gameBalanceConfig == null)
        {
            gameBalanceConfig = Resources.Load<GameBalanceSO>("GameBalanceConfig");
        }

        bool appliedFromProfile = false;
        if (gameBalanceConfig != null)
        {
            DifficultyWaveProfile profile = gameBalanceConfig.GetProfile(diff);
            if (profile != null)
            {
                hitsToTriggerBlock = profile.hitsToTriggerBlock;
                blockChance = profile.blockChance;
                blockDuration = profile.blockDuration;
                blockCooldown = profile.blockCooldown;
                appliedFromProfile = true;
            }
        }

        if (!appliedFromProfile)
        {
            hitsToTriggerBlock = 2;
            blockChance = 1.0f;
            blockCooldown = 0.5f;

            switch (diff)
            {
                case Difficulty.Easy:
                    blockDuration = 1.0f;
                    break;
                case Difficulty.Medium:
                    blockDuration = 1.2f;
                    break;
                case Difficulty.Hard:
                    blockDuration = 1.4f;
                    break;
            }
        }

        AdaptiveCombatBrain brain = GetComponent<AdaptiveCombatBrain>();
        if (brain != null)
        {
            brain.RefreshCanonicalBaseStats();
            brain.SyncWithSharedTracker();
        }
    }

    private void NotifyUIEnemyHit()
    {
        if (uiManager == null)
        {
            uiManager = FindObjectOfType<UIManager>();
        }
        if (uiManager != null)
        {
            uiManager.NotifyEnemyHit(this);
        }
    }

    // Geriye dönük uyumluluk için eski metot
    public void DecreaseHealt()
    {
        TakeDamage(10f);
    }

    // Geriye kaçış / Darbe tepkisi (Knockback)
    public void SetPosition()
    {
        // StartCoroutine(KnockBack());
    }

    IEnumerator KnockBack()
    {
        yield return new WaitForSeconds(0.6f);

        // Karakterin geri yönüne (veya Z ekseninde geriye) hafifçe iter (Kullanıcı isteğiyle iptal edildi)
        // transform.position += new Vector3(0, 0, knockbackDistance);
        // Vector3 kbPos = transform.position + new Vector3(0, 0, knockbackDistance);
        // kbPos = RingBoundary.ClampToArena(kbPos, bodyRadius);
        // transform.position = kbPos;
    }

    void Die(bool isUppercut = false)
    {
        if (isDead) return;
        isDead = true;
        isPendingUltiDeath = false;

        if (EnemyGroupCoordinator.Instance != null)
        {
            EnemyGroupCoordinator.Instance.UnregisterEnemy(this);
        }

        StopAttack();
        StopBlocking();
        if (hitStunCoroutine != null)
        {
            StopCoroutine(hitStunCoroutine);
            hitStunCoroutine = null;
        }
        isTakingHit = false;

        if (enemyAnimator != null)
        {
            enemyAnimator.SetBool("rightMove", false);
            enemyAnimator.SetBool("leftMove", false);
        }

        Debug.Log($"<color=red>[DÜŞMAN YENİLDİ]</color> {gameObject.name} nakavt oldu (Aparkat: {isUppercut})!");

        // Sadece sahnedeki TÜM düşmanlar öldüyse oyuncu Show Pose'a girsin
        if (playerController == null)
        {
            playerController = FindObjectOfType<PlayerController>();
        }

        EnemyController[] allEnemies = FindObjectsOfType<EnemyController>();
        bool anyLivingEnemy = false;
        foreach (var e in allEnemies)
        {
            if (e != null && e != this && !e.IsDead && !e.IsPendingUltiDeath)
            {
                anyLivingEnemy = true;
                break;
            }
        }

        if (!anyLivingEnemy && playerController != null && !playerController.IsDead)
        {
            playerController.SetEnemyDead(true);
        }

        StartCoroutine(WaitPunch());

        if (enemyAnimator != null)
        {
            enemyAnimator.SetBool("isDead", true);
            enemyAnimator.SetBool("isUppercutDead", isUppercut);
            enemyAnimator.ResetTrigger("GetHit");
            enemyAnimator.ResetTrigger("GetHeadHit");
            string deadAnim = isUppercut ? "Uppercut Nakavt" : "Knockout";
            enemyAnimator.CrossFadeInFixedTime(deadAnim, 0.08f);
            StartCoroutine(WaitPos());
        }

        // Öldükten sonra cesede vurulmaya devam edilmemesi için collider'ı kapat
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }

        StartCoroutine(DieRoutine());
    }

    IEnumerator DieRoutine()
    {
        // Nakavt olduktan sonra yerde birkaç saniye (3.5 saniye) kalsın
        yield return new WaitForSeconds(3.5f);

        // Ardından yok olsun (EnemySpawner bunu görüp yeni düşmanı/dalgayı başlatır)
        Destroy(gameObject);
    }

    IEnumerator WaitPos()
    {
        yield return new WaitForSeconds(1.2f);
        transform.position = new Vector3(transform.position.x, fallenYPosition, transform.position.z);
        Vector3 fallPos = new Vector3(transform.position.x, fallenYPosition, transform.position.z);
        fallPos = RingBoundary.ClampToArena(fallPos, bodyRadius);
        transform.position = fallPos;
    }

    IEnumerator WaitPunch()
    {
        yield return new WaitForSeconds(0.6f);
    }

    public float GetHealth()
    {
        return health;
    }

    private void OnDrawGizmosSelected()
    {
        // Gövde temas alanı (Kırmızı)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, bodyRadius);

        // Ayrışma ve Kuşatma alanı (Sarı)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, separationRadius);
    }
}
