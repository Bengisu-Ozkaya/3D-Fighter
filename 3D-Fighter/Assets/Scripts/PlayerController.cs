using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Bileşen Referansları")]
    [SerializeField] Animator playerAnim;
    [SerializeField] EnemyController enemyController;
    [SerializeField] float speed = 2f;

    [Header("Yumruk Hassasiyeti (Mesafe & Boyut)")]
    [Tooltip("Yumruğun temas küresinin yarıçapı. 0.25 - 0.30 arası eldiven ve eklemleri tam kapsar.")]
    [Range(0.15f, 0.45f)]
    [SerializeField] float punchRadius = 0.28f;

    [Tooltip("Yumruk atarken karakterin ileriye doğru attığı doğal boks adımı mesafesi (metre)")]
    [SerializeField] float punchStepDistance = 0.18f;

    [Tooltip("Yumruğun aktif kalıp temas arayacağı süre (saniye). Boks animasyonunun uzanma ve geri çekilme aralığı.")]
    [SerializeField] float punchActiveDuration = 0.50f;

    [SerializeField] float punchDamage = 10f;
    [SerializeField] LayerMask targetLayers = ~0;

    [Header("El Kemiği Referansları (Boşsa Otomatik Bulunur)")]
    [SerializeField] Transform rightFist;
    [SerializeField] Transform leftFist;

    [Header("Oyuncu Sağlık Ayarları")]
    [SerializeField] float playerHealth = 100f;
    [SerializeField] float maxPlayerHealth = 100f;
    [SerializeField] float respawnDelay = 5f;

    private bool isDead = false;
    public bool IsDead => isDead;

    private Vector3 startPosition;
    private Quaternion startRotation;

    bool isPunchRight = false;
    bool isPunchActive = false;
    bool hasHitCurrentPunch = false;

    // 0 GC Bellek optimizasyonu
    private readonly Collider[] hitColliders = new Collider[6];

    void Start()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;

        // 1. Animator'ı bağla
        if (playerAnim == null)
        {
            playerAnim = GetComponent<Animator>();
        }

        // 2. Humanoid el kemiklerini otomatik bul ve bağla
        BindFistBones();

        // 3. Sahnedeki EnemyController referansını bağla
        if (enemyController == null)
        {
            enemyController = FindObjectOfType<EnemyController>();
        }
    }

    void BindFistBones()
    {
        if (playerAnim != null && playerAnim.isHuman)
        {
            if (rightFist == null)
            {
                rightFist = playerAnim.GetBoneTransform(HumanBodyBones.RightHand);
            }
            if (leftFist == null)
            {
                leftFist = playerAnim.GetBoneTransform(HumanBodyBones.LeftHand);
            }
        }
    }

    void Update()
    {
        // Oyuncu öldüyse hareket edemesin ve yumruk atamasın
        if (isDead) return;

        // Karakter Hareketi
        HandleMovement();

        // Yumruk Tuşu (Space)
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ExecutePunch();
        }
    }

    void HandleMovement()
    {
        if (Input.GetKey(KeyCode.W))
        {
            transform.Translate(Vector3.forward * speed * Time.deltaTime);
        }
        if (Input.GetKey(KeyCode.S))
        {
            transform.Translate(-Vector3.forward * speed * Time.deltaTime);
        }
        if (Input.GetKey(KeyCode.A))
        {
            transform.Translate(Vector3.left * speed * Time.deltaTime);
        }
        if (Input.GetKey(KeyCode.D))
        {
            transform.Translate(-Vector3.left * speed * Time.deltaTime);
        }
    }

    void ExecutePunch()
    {
        hasHitCurrentPunch = false;

        int fistIndex = isPunchRight ? 1 : 0;
        string triggerName = isPunchRight ? "PunchLeft" : "PunchRight";

        if (playerAnim != null)
        {
            playerAnim.SetTrigger(triggerName);
        }

        // Sıradaki yumruğu değiştir (Sağ -> Sol -> Sağ)
        isPunchRight = !isPunchRight;

        // Boks adımı ve vuruş kontrolünü başlat
        StopCoroutine(nameof(PunchRoutine));
        StartCoroutine(PunchRoutine(fistIndex));
    }

    IEnumerator PunchRoutine(int fistIndex)
    {
        // 1. Boksör Hamlesi (Step-in): Yumruk atarken öne doğru hafif ve doğal bir adım at
        if (punchStepDistance > 0f)
        {
            float stepDuration = 0.16f;
            float stepTimer = 0f;
            while (stepTimer < stepDuration)
            {
                transform.Translate(Vector3.forward * (punchStepDistance / stepDuration) * Time.deltaTime);
                stepTimer += Time.deltaTime;
                yield return null;
            }
        }

        // 2. Yumruk uzandığında temas kontrol penceresini aç
        isPunchActive = true;
        float activeTimer = 0f;

        // Animasyonun uzanma ve zirve anı boyunca her karede temas kontrol et
        while (activeTimer < punchActiveDuration)
        {
            if (hasHitCurrentPunch) break; // Zaten temas ettiyse mükerrer hasarı engelle

            CheckPhysicalFistContact(fistIndex);

            activeTimer += Time.deltaTime;
            yield return null;
        }

        isPunchActive = false;
    }

    /// <summary>
    /// Sadece elin eklem/eldiven kısmı fiziken rakip collider'ına girdiğinde hasar verir.
    /// </summary>
    void CheckPhysicalFistContact(int fistIndex)
    {
        Vector3 fistPos = GetFistPosition(fistIndex);

        // Elin etrafındaki temas küresini tara (0 GC)
        int hitCount = Physics.OverlapSphereNonAlloc(fistPos, punchRadius, hitColliders, targetLayers);

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = hitColliders[i];
            if (col == null || col.transform.root == transform.root) continue;

            if (col.TryGetComponent<EnemyController>(out var enemy) ||
                col.transform.root.TryGetComponent<EnemyController>(out enemy) ||
                col.CompareTag("Enemy"))
            {
                if (enemy == null && enemyController != null)
                {
                    enemy = enemyController;
                }

                if (enemy != null)
                {
                    hasHitCurrentPunch = true;
                    isPunchActive = false;
                    enemy.TakeDamage(punchDamage);
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Animasyon Eventi (0: Sağ El, 1: Sol El) kullanılmak istenirse doğrudan da çağrılabilir.
    /// </summary>
    public void HitCheck(int fistIndex)
    {
        if (!hasHitCurrentPunch)
        {
            CheckPhysicalFistContact(fistIndex);
        }
    }

    Vector3 GetFistPosition(int fistIndex)
    {
        Transform fist = (fistIndex == 0) ? rightFist : leftFist;
        if (fist != null)
        {
            // Humanoid 'Hand' kemiği bilekte yer aldığından, eldiven/parmak eklemleri için 12cm öne kaydırılır
            return fist.position + transform.forward * 0.12f;
        }

        // El kemiği bulunamazsa alternatif olarak omuzdan öne doğru
        return transform.position + transform.forward * 0.75f + Vector3.up * 1.1f;
    }

    // Editör Scene ekranında yumruk temas alanını gösterir
    private void OnDrawGizmos()
    {
        Gizmos.color = isPunchActive ? Color.green : new Color(1f, 0.4f, 0f, 0.8f);

        Vector3 rPos = GetFistPosition(0);
        Gizmos.DrawWireSphere(rPos, punchRadius);

        Vector3 lPos = GetFistPosition(1);
        Gizmos.DrawWireSphere(lPos, punchRadius);
    }

    public void TakeDamage(float damageAmount)
    {
        if (isDead) return;

        StartCoroutine(WaitPunch());
        playerHealth -= damageAmount;
        Debug.Log($"<color=cyan>[OYUNCU DARBE ALDI]</color> Kalan Can: {playerHealth}");

        if (playerHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log("<color=red>[OYUNCU NAKAVT OLDU!]</color>");

        if (playerAnim != null)
        {
            playerAnim.SetBool("isDead", true);
        }

        StartCoroutine(WaitPos());

        // Öldükten sonra hasar almaması için collider'ı geçici kapat
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }

        StartCoroutine(RespawnRoutine(respawnDelay));
    }

    IEnumerator RespawnRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Canı ve pozisyonu sıfırla
        playerHealth = maxPlayerHealth;
        transform.position = startPosition;
        transform.rotation = startRotation;

        if (playerAnim != null)
        {
            playerAnim.SetBool("isDead", false);
        }

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = true;
        }

        isDead = false;
        Debug.Log("<color=green>[OYUNCU YENİDEN DOĞDU!]</color>");
    }

    public float GetHealth() => playerHealth;

    IEnumerator WaitPunch()
    {
        yield return new WaitForSeconds(0.6f);
    }

    IEnumerator WaitPos()
    {
        yield return new WaitForSeconds(1.2f);
        transform.position += new Vector3(0, -0.78f, 0);
    }
}