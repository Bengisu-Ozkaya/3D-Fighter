using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Bileşen Referansları")]
    [SerializeField] Animator playerAnim;
    [SerializeField] EnemyController enemyController;
    [SerializeField] float speed = 2f;
    [Tooltip("Karakterin hareket yönüne doğru yumuşak dönme hızı")]
    [SerializeField] float rotationSpeed = 14f;

    [Header("Yumruk Hassasiyeti (Mesafe & Boyut)")]
    [Tooltip("Yumruğun temas küresinin yarıçapı. 0.25 - 0.30 arası eldiven ve eklemleri tam kapsar.")]
    [Range(0.15f, 0.45f)]
    [SerializeField] float punchRadius = 0.28f;

    [Tooltip("Yumruk atarken karakterin ileriye doğru attığı doğal boks adımı mesafesi (metre)")]
    [SerializeField] float punchStepDistance = 0.08f;

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
        startPosition = new Vector3(transform.position.x, 0f, transform.position.z);
        transform.position = startPosition;
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

        // Yedek: Kemik hiyerarşisinde isim ile arama (Mixamo Ch43 ve benzeri modeller için)
        if (rightFist == null)
        {
            rightFist = FindDeepChild(transform, "RightHand") ?? FindDeepChild(transform, "mixamorig:RightHand");
        }
        if (leftFist == null)
        {
            leftFist = FindDeepChild(transform, "LeftHand") ?? FindDeepChild(transform, "mixamorig:LeftHand");
        }
    }

    Transform FindDeepChild(Transform parent, string boneName)
    {
        foreach (Transform child in parent)
        {
            if (child.name.Equals(boneName, System.StringComparison.OrdinalIgnoreCase))
                return child;
            Transform result = FindDeepChild(child, boneName);
            if (result != null)
                return result;
        }
        return null;
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
        float h = 0f;
        float v = 0f;

        if (Input.GetKey(KeyCode.D)) h += 1f;
        if (Input.GetKey(KeyCode.A)) h -= 1f;
        if (Input.GetKey(KeyCode.W)) v += 1f;
        if (Input.GetKey(KeyCode.S)) v -= 1f;

        Vector3 moveDir = new Vector3(h, 0f, v).normalized;

        if (moveDir != Vector3.zero)
        {
            // 1. Pozisyonu hareket yönünde ilerlet (Dünya koordinatlarında)
            transform.position += moveDir * speed * Time.deltaTime;

            // 2. Karakterin yönünü hareket yönüne doğru yumuşakça çevir
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
        }

        // 3. Animasyon parametrelerini güncelle
        if (playerAnim != null)
        {
            if (h < -0.1f)
            {
                playerAnim.SetBool("leftMove", true);
                playerAnim.SetBool("rightMove", false);
            }
            else if (h > 0.1f)
            {
                playerAnim.SetBool("rightMove", true);
                playerAnim.SetBool("leftMove", false);
            }
            else if (Mathf.Abs(v) > 0.1f)
            {
                // Düz ileri veya geri giderken adım animasyonunu oynat
                playerAnim.SetBool("leftMove", false);
                playerAnim.SetBool("rightMove", true);
            }
            else
            {
                playerAnim.SetBool("leftMove", false);
                playerAnim.SetBool("rightMove", false);
            }
        }
    }

    void ExecutePunch()
    {
        hasHitCurrentPunch = false;

        // Yumruk atarken yakında rakip varsa yüzünü doğrudan rakibe hizala
        FaceOpponentOnPunch();

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

    void FaceOpponentOnPunch()
    {
        EnemyController enemy = enemyController;
        if (enemy == null)
        {
            enemy = FindObjectOfType<EnemyController>();
        }

        if (enemy != null && !enemy.IsDead)
        {
            Vector3 dirToEnemy = enemy.transform.position - transform.position;
            dirToEnemy.y = 0f;
            if (dirToEnemy.sqrMagnitude > 0.05f && dirToEnemy.magnitude <= 3.5f)
            {
                transform.rotation = Quaternion.LookRotation(dirToEnemy.normalized);
            }
        }
    }

    IEnumerator PunchRoutine(int fistIndex)
    {
        // 1. Boksör Hamlesi (Step-in): Yumruk atarken öne doğru hafif ve doğal bir adım at (SmoothStep ile sarsıntısız)
        if (punchStepDistance > 0f)
        {
            float stepDuration = 0.16f;
            float stepTimer = 0f;
            Vector3 startPos = transform.position;
            Vector3 stepTarget = startPos + transform.forward * punchStepDistance;
            while (stepTimer < stepDuration)
            {
                stepTimer += Time.deltaTime;
                float t = Mathf.Clamp01(stepTimer / stepDuration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                transform.position = Vector3.Lerp(startPos, stepTarget, smoothT);
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

                if (enemy == null)
                {
                    enemy = FindObjectOfType<EnemyController>();
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
        else
        {
            if (playerAnim != null)
            {
                playerAnim.SetTrigger("GetHit");
            }
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
        transform.position = new Vector3(startPosition.x, -0.26f, startPosition.z);
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
        transform.position = new Vector3(transform.position.x, 0f, transform.position.z);
    }
}