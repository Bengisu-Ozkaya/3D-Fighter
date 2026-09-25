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
    [SerializeField] float attackRange = 1.6f;       // Yumruk mesafesi
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

        //Player
        playerController = FindObjectOfType<PlayerController>();
        if (playerController != null)
        {
            playerTransform = playerController.transform;
        }
    }

    private bool isDead = false;

    void Update()
    {
        if (health <= 0 && !isDead)
        {
            Die();
            return;
        }

        if (!isDead && playerTransform != null)
        {
            // Eğer oyuncu öldüyse saldırmayı ve hareketi durdur, Idle'da bekle
            if (playerController != null && playerController.IsDead)
            {
                return;
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
                transform.position = Vector3.MoveTowards(transform.position, playerTransform.position, moveSpeed * Time.deltaTime);
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
            if (currentDistance <= attackRange + 0.5f)
            {
                playerController.TakeDamage(attackDamage);
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
        transform.position += new Vector3(0, -0.78f, 0);
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
