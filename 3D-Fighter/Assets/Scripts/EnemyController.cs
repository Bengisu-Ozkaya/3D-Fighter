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

    void LateUpdate()
    {
        // Düşman ayaktayken animasyonların dikey kaydırmasını engeller ve Y pozisyonunu kesinlikle 0'a kilitler
        if (!isDead)
        {
            Vector3 pos = transform.position;
            if (pos.y != 0f)
            {
                pos.y = 0f;
                transform.position = pos;
            }
        }
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

        // 2. Eğer oyuncu uzaktaysa ona doğru yürü
        if (distance > attackRange)
        {
            if (moveSpeed > 0)
            {
                Vector3 target = new Vector3(playerTransform.position.x, 0f, playerTransform.position.z);
                transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
            }
        }

        // 3. Yumruk mesafesindeyse bekleme süresi doldukça saldır
        else
        {
            if (Time.time >= nextAttackTime)
            {
                AttackPlayer();
                nextAttackTime = Time.time + attackCooldown;
            }
        }
    }

    void AttackPlayer()
    {
        // Animator'daki PunchLeft veya PunchRight trigger'larından birini rastgele tetikle
        string punchTrigger = Random.value > 0.5f ? "PunchRight" : "PunchLeft";
        if (enemyAnimator != null)
        {
            enemyAnimator.SetTrigger(punchTrigger);
        }
        // Yumruğun animasyon uzanma anında (örneğin 0.3 saniye sonra) hasar vermesi için Coroutine
        StartCoroutine(DamagePlayerWithDelay(0.35f));
    }

    IEnumerator DamagePlayerWithDelay(float delay)
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
                playerController.TakeDamage(attackDamage);
            }
            else
            {
                Debug.Log("<color=yellow>[DÜŞMAN ISKALADI]</color> Oyuncu menzil dışına çıktı!");
            }
        }
    }

    // Hasar alma fonksiyonu
    public void TakeDamage(float damageAmount)
    {
        StartCoroutine(WaitPunch());

        if (isDead) return;

        health -= damageAmount;
        Debug.Log($"<color=orange>[DÜŞMAN DARBE ALDI]</color> {gameObject.name} -{damageAmount} can kaybetti! Kalan Can: {health}");

        // Darbe alınca hafif geriye çekilme (Knockback)
        SetPosition();

        if (health <= 0)
        {
            Die();
        }
        else
        {
            if (enemyAnimator != null)
            {
                enemyAnimator.SetTrigger("GetHit");
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
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log($"<color=red>[DÜŞMAN YENİLDİ]</color> {gameObject.name} nakavt oldu!");

        // Oyuncuya düşmanın öldüğünü bildir (Oyuncu Show Pose'a girsin)
        if (playerController == null)
        {
            playerController = FindObjectOfType<PlayerController>();
        }
        if (playerController != null && !playerController.IsDead)
        {
            playerController.SetEnemyDead(true);
        }

        StartCoroutine(WaitPunch());

        if (enemyAnimator != null)
        {
            enemyAnimator.SetBool("isDead", true);
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
        // Ölüm animasyonunun oynaması için 1 saniye bekle
        yield return new WaitForSeconds(4f);

        // Düşmanı yok et (EnemySpawner yok edildiğini görüp yenisini oluşturacak)
        Destroy(gameObject);
    }

    IEnumerator WaitPos()
    {
        yield return new WaitForSeconds(1.2f);
        transform.position = new Vector3(transform.position.x, -0.26f, transform.position.z);
    }

    IEnumerator WaitPunch()
    {
        yield return new WaitForSeconds(0.6f);
    }

    public float GetHealth()
    {
        return health;
    }
}
