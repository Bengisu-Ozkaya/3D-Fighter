using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyController : MonoBehaviour
{
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

    // Saldırı Yapay Zekası
    [Header("Saldırı & Takip Ayarları")]
    [Tooltip("Düşmanın saldırıya geçeceği yaklaşma mesafesi")]
    [SerializeField] float attackRange = 1.1f;       // Yumruk yaklaşma mesafesi
    [Tooltip("Yumruğun temas edebileceği maksimum mesafe (Oyuncu geri kaçtıysa ıskalar)")]
    [SerializeField] float maxHitDistance = 1.25f;
    [SerializeField] float attackCooldown = 1.8f;    // Kaç saniyede bir saldıracak
    [SerializeField] float moveSpeed = 1.2f;        // Oyuncuya yaklaşma hızı (0 yapılırsa yerinde durur)
    [SerializeField] float attackDamage = 10f;      // Player'a vereceği hasar
    private Transform playerTransform;
    private PlayerController playerController;
    private float nextAttackTime = 0f;

    void Start()
    {
        // Aktif zorluk moduna göre saldırı hasarını ve canını belirle
        EnemySpawner spawner = FindObjectOfType<EnemySpawner>();
        if (spawner != null && spawner.IsGameStarted)
        {
            attackDamage = spawner.GetCurrentDifficultyDamage();
            SetHealth(spawner.GetCurrentDifficultyEnemyHealth());
        }

        //Enemy
        if (maxHealth <= 0) maxHealth = 30f;
        if (health <= 0) health = maxHealth;
        if (enemyAnimator == null) enemyAnimator = GetComponent<Animator>();
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
    }

    /// <summary>
    /// Spawner tarafından zorluk moduna uygun hasar değerini atar
    /// </summary>
    public void SetAttackDamage(float damage)
    {
        attackDamage = damage;
    }

    [Header("Pozisyon & Yükseklik (Y) Ayarları (Unity Inspector'dan Düzenlenebilir)")]
    [Tooltip("Normal ayaktayken ve yürürken Y pozisyonu")]
    [SerializeField] float standingYPosition = 0f;

    [Tooltip("Düşman nakavt olup yere düştüğündeki Y pozisyonu")]
    [SerializeField] float fallenYPosition = 0.45f;

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
        if (enemyAnimator != null)
        {
            enemyAnimator.SetBool("isDeadEnemy", dead);
            if (!dead)
            {
                enemyAnimator.CrossFadeInFixedTime("Idle", 0.2f);
            }
        }
    }

    void Update()
    {
        if (health <= 0 && !isDead)
        {
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

        // 2. Diğer düşmanlardan kaçınma (Separation) vektörünü hesapla
        Vector3 separationForce = CalculateSeparationForce();

        // 3. Eğer oyuncu saldırı mesafesinden uzaktaysa ona doğru yürü
        if (distance > attackRange)
        {
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
            // Düşmanlar saldırı mesafesinde olsa bile üst üste binmesinler, hafifçe yana açılsınlar
            if (separationForce.sqrMagnitude > 0.01f)
            {
                Vector3 sidePush = separationForce * (moveSpeed * 0.7f) * Time.deltaTime;
                sidePush.y = 0f;
                transform.position += sidePush;
                Vector3 sidePos = transform.position + sidePush;
                sidePos = RingBoundary.ClampToArena(sidePos, bodyRadius);
                transform.position = sidePos;
            }

            if (Time.time >= nextAttackTime)
            {
                AttackPlayer();
                nextAttackTime = Time.time + attackCooldown;
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

    void AttackPlayer()
    {
        // 1. Oyuncuya tam cepheden yüzünü dön
        if (playerTransform != null)
        {
            Vector3 dir = (playerTransform.position - transform.position).normalized;
            dir.y = 0f;
            if (dir != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(dir);
            }
        }

        // Oyuncunun canını kontrol et: Can <= (attackDamage * 2) ise bitirici aparkat yap
        float playerHealth = playerController != null ? playerController.GetHealth() : 100f;
        bool isFinisher = playerHealth <= (attackDamage * 2f);

        if (isFinisher)
        {
            Debug.Log($"<color=red>[DÜŞMAN BİTİRİCİ APARKAT ATTI!]</color> Oyuncu Canı: {playerHealth} <= {attackDamage * 2f}");
            if (enemyAnimator != null)
            {
                enemyAnimator.CrossFadeInFixedTime("Uppercut", 0.12f);
            }
            StartCoroutine(DamagePlayerWithDelay(0.35f, true));
        }
        else
        {
            // Normal yumruk (PunchLeft veya PunchRight)
            string punchTrigger = Random.value > 0.5f ? "PunchRight" : "PunchLeft";
            if (enemyAnimator != null)
            {
                enemyAnimator.SetTrigger(punchTrigger);
            }
            StartCoroutine(DamagePlayerWithDelay(0.35f, false));
        }
    }

    IEnumerator DamagePlayerWithDelay(float delay, bool isFinisherUppercut = false)
    {
        yield return new WaitForSeconds(delay);

        // Düşman ölmediyse ve oyuncu hala hayattaysa hasarı uygula
        if (!isDead && playerTransform != null && playerController != null && !playerController.IsDead)
        {
            float currentDistance = Vector3.Distance(transform.position, playerTransform.position);
            Vector3 dirToPlayer = (playerTransform.position - transform.position).normalized;
            dirToPlayer.y = 0f;
            float dot = Vector3.Dot(transform.forward, dirToPlayer);

            // Sadece oyuncu gerçekten vuruş mesafesindeyse (<= maxHitDistance) ve düşman oyuncuya bakıyorsa hasar ver
            if (currentDistance <= maxHitDistance && dot > 0.35f)
            {
                // Bitirici aparkat ise bitirici hasar uygula (en az attackDamage * 2)
                float damageToDeal = isFinisherUppercut ? Mathf.Max(attackDamage * 2f, 20f) : attackDamage;
                playerController.TakeDamage(damageToDeal, isFinisherUppercut);
            }
            else
            {
                Debug.Log("<color=yellow>[DÜŞMAN ISKALADI]</color> Oyuncu menzil dışına çıktı!");
            }
        }

        // Aparkat tamamlandıktan sonra düşmanı gard (Idle) pozisyonuna geri döndür
        if (isFinisherUppercut)
        {
            yield return new WaitForSeconds(0.65f);
            if (enemyAnimator != null && !isDead)
            {
                enemyAnimator.CrossFadeInFixedTime("Idle", 0.2f);
            }
        }
    }

    // Hasar alma fonksiyonu
    public void TakeDamage(float damageAmount, bool isUppercut = false)
    {
        StartCoroutine(WaitPunch());

        if (isDead) return;

        health -= damageAmount;
        Debug.Log($"<color=orange>[DÜŞMAN DARBE ALDI]</color> {gameObject.name} -{damageAmount} can kaybetti! Kalan Can: {health} (Aparkat: {isUppercut})");

        // Darbe alınca hafif geriye çekilme (Knockback)
        SetPosition();

        // 20 ve üzeri güçlü darbelerde (Aparkat) ekstra sarsıntı tepkisi ver
        if (isUppercut || damageAmount >= 20f)
        {
            transform.position += (-transform.forward) * (knockbackDistance * 1.5f);
            Vector3 pushPos = transform.position + (-transform.forward) * (knockbackDistance * 1.5f);
            pushPos = RingBoundary.ClampToArena(pushPos, bodyRadius);
            transform.position = pushPos;
        }

        if (health <= 0)
        {
            Die();
        }
        else
        {
            if (enemyAnimator != null)
            {
                if (isUppercut || damageAmount >= 20f)
                {
                    enemyAnimator.ResetTrigger("GetHit");
                    enemyAnimator.SetTrigger("GetHeadHit");
                    enemyAnimator.CrossFadeInFixedTime("Head Hit", 0.08f);
                }
                else
                {
                    enemyAnimator.ResetTrigger("GetHeadHit");
                    enemyAnimator.SetTrigger("GetHit");
                }
            }
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
        StartCoroutine(KnockBack());
    }

    IEnumerator KnockBack()
    {
        yield return new WaitForSeconds(0.6f);

        // Karakterin geri yönüne (veya Z ekseninde geriye) hafifçe iter
        transform.position += new Vector3(0, 0, knockbackDistance);
        Vector3 kbPos = transform.position + new Vector3(0, 0, knockbackDistance);
        kbPos = RingBoundary.ClampToArena(kbPos, bodyRadius);
        transform.position = kbPos;
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log($"<color=red>[DÜŞMAN YENİLDİ]</color> {gameObject.name} nakavt oldu!");

        // Sadece sahnedeki TÜM düşmanlar öldüyse oyuncu Show Pose'a girsin
        if (playerController == null)
        {
            playerController = FindObjectOfType<PlayerController>();
        }

        EnemyController[] allEnemies = FindObjectsOfType<EnemyController>();
        bool anyLivingEnemy = false;
        foreach (var e in allEnemies)
        {
            if (e != null && e != this && !e.IsDead)
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
            enemyAnimator.ResetTrigger("GetHit");
            enemyAnimator.ResetTrigger("GetHeadHit");
            enemyAnimator.CrossFadeInFixedTime("Knockout", 0.08f);
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
