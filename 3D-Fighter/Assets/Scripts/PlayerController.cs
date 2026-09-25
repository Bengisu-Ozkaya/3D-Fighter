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
    [Tooltip("Yumruğun temas küresinin yarıçapı. 0.15 - 0.25 arası eldiven ve eklemleri tam kapsar.")]
    [Range(0.12f, 0.35f)]
    [SerializeField] float punchRadius = 0.20f;

    [Tooltip("Yumruğun rakibe ulaşabileceği azami mesafe (metre). Bu mesafeden uzaktaki rakiplere hasar verilemez.")]
    [SerializeField] float maxPunchRange = 1.25f;

    [Tooltip("Yumruk atarken karakterin ileriye doğru attığı doğal boks adımı mesafesi (metre)")]
    [SerializeField] float punchStepDistance = 0.08f;

    [Tooltip("Yumruğun aktif kalıp temas arayacağı süre (saniye). Boks animasyonunun uzanma ve geri çekilme aralığı.")]
    [SerializeField] float punchActiveDuration = 0.40f;

    [Tooltip("Bir yumruğun baştan sona tamamlanma ve gard pozisyonuna dönüş süresi (saniye). Bu süre dolmadan yeni yumruk atılamaz.")]
    [SerializeField] float punchDuration = 0.55f;

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

    private bool isEnemyDead = false;
    public bool IsEnemyDead => isEnemyDead;

    private Vector3 startPosition;
    private Quaternion startRotation;

    bool isPunchRight = false;
    bool isPunching = false;
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
        if (playerAnim != null)
        {
            playerAnim.applyRootMotion = false;
        }

        // 2. Humanoid el kemiklerini otomatik bul ve bağla
        BindFistBones();

        // 3. Sahnedeki EnemyController referansını bağla
        if (enemyController == null)
        {
            enemyController = FindObjectOfType<EnemyController>();
        }

        // Kararlı yakın dövüş mesafesi kalibrasyonu
        if (punchRadius > 0.22f) punchRadius = 0.20f;
        if (maxPunchRange <= 0f || maxPunchRange > 1.4f) maxPunchRange = 1.25f;
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
        // Oyuncu öldüyse veya karşı taraf ölüp Show Pose yapılıyorsa hareket edip yumruk atamasın
        if (isDead || isEnemyDead) return;

        // Karakter Hareketi
        HandleMovement();

        // Yumruk Tuşu (Space) - Önceki yumruk tamamen bitmeden yeni yumruk tetiklenemez
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (!isPunching)
            {
                ExecutePunch();
            }
        }
    }

    void LateUpdate()
    {
        // Karakter ayaktayken animasyonların dikey kaydırmasını engeller ve Y pozisyonunu kesinlikle 0'a kilitler
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
            Vector3 newPos = transform.position + moveDir * speed * Time.deltaTime;
            newPos.y = 0f;
            transform.position = newPos;

            // 2. Karakterin rotasyonu:
            // S tuşuna basıldığında (v < -0.1f) arkasını kameraya dönmesin;
            // yüzü rakibe/ileriye dönük kalsın (sırtı kameraya dönük şekilde geri gitsin)
            Vector3 facingDir;
            if (v < -0.1f)
            {
                if (enemyController != null && !enemyController.IsDead)
                {
                    facingDir = (enemyController.transform.position - transform.position);
                    facingDir.y = 0f;
                    if (facingDir == Vector3.zero) facingDir = Vector3.forward;
                }
                else
                {
                    facingDir = Vector3.forward;
                }
            }
            else
            {
                facingDir = moveDir;
            }

            if (facingDir != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(facingDir.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
            }
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
        if (isPunching) return;
        isPunching = true;
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

    /// <summary>
    /// Karşı taraf (Düşman) öldüğünde veya yeniden doğduğunda çağrılır
    /// </summary>
    public void SetEnemyDead(bool dead)
    {
        isEnemyDead = dead;
        if (playerAnim != null)
        {
            playerAnim.SetBool("isDeadEnemy", dead);
            if (dead)
            {
                // Yürüyüş animasyonlarını sıfırla ki Show Pose'a temiz geçsin
                playerAnim.SetBool("leftMove", false);
                playerAnim.SetBool("rightMove", false);

                // Devam eden yumruk coroutine'ini sıfırla
                isPunching = false;
                isPunchActive = false;
            }
            else
            {
                // Düşman yeniden doğdu: Show Pose animasyonunu derhal kes ve Idle'a yumuşakça geçiş yap!
                playerAnim.CrossFadeInFixedTime("Idle", 0.2f);
                isPunching = false;
                isPunchActive = false;
            }
        }
    }

    /// <summary>
    /// EnemySpawner yeni düşman yarattığında oyuncuyu bilgilendirir
    /// </summary>
    public void OnEnemySpawned(EnemyController newEnemy)
    {
        enemyController = newEnemy;
        SetEnemyDead(false);
    }

    IEnumerator PunchRoutine(int fistIndex)
    {
        float stepDuration = 0.16f;
        // 1. Boksör Hamlesi (Step-in): Yumruk atarken öne doğru hafif ve doğal bir adım at (SmoothStep ile sarsıntısız)
        if (punchStepDistance > 0f)
        {
            float stepTimer = 0f;
            Vector3 startPos = new Vector3(transform.position.x, 0f, transform.position.z);
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            Vector3 stepTarget = startPos + fwd.normalized * punchStepDistance;
            stepTarget.y = 0f;
            while (stepTimer < stepDuration)
            {
                stepTimer += Time.deltaTime;
                float t = Mathf.Clamp01(stepTimer / stepDuration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                Vector3 newPos = Vector3.Lerp(startPos, stepTarget, smoothT);
                newPos.y = 0f;
                transform.position = newPos;
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

        // 3. Kolun geri çekilmesi ve boksörün garda dönüş süresi (Animasyonun bitmesini bekle)
        float totalElapsed = (punchStepDistance > 0f ? stepDuration : 0f) + activeTimer;
        float remainingDuration = punchDuration - totalElapsed;
        if (remainingDuration > 0f)
        {
            yield return new WaitForSeconds(remainingDuration);
        }

        // Yumruk tamamen bitti, artık sıradaki yumruk atılabilir!
        isPunching = false;
    }

    /// <summary>
    /// Sadece elin eklem/eldiven kısmı fiziken rakip collider'ına girdiğinde ve mesafe uygunsa hasar verir.
    /// </summary>
    void CheckPhysicalFistContact(int fistIndex)
    {
        EnemyController enemy = enemyController;
        if (enemy == null || enemy.IsDead)
        {
            enemy = FindObjectOfType<EnemyController>();
        }

        if (enemy == null || enemy.IsDead) return;

        // 1. Kararlı Mesafe Sınırı: Karakter ile düşman arasındaki mesafe maxPunchRange'den büyükse hasar verilemez
        float distToEnemy = Vector3.Distance(transform.position, enemy.transform.position);
        if (distToEnemy > maxPunchRange) return;

        // 2. Yön/Açı Kontrolü: Oyuncunun yüzü düşmana dönük olmalı (arkası veya ters yön dönükken vuramaz)
        Vector3 dirToEnemy = (enemy.transform.position - transform.position).normalized;
        dirToEnemy.y = 0f;
        if (Vector3.Dot(transform.forward, dirToEnemy) < 0.30f) return;

        // 3. Fiziksel temas alanı kontrolü (Eldiven küresi)
        Vector3 fistPos = GetFistPosition(fistIndex);
        int hitCount = Physics.OverlapSphereNonAlloc(fistPos, punchRadius, hitColliders, targetLayers);

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = hitColliders[i];
            if (col == null || col.transform.root == transform.root) continue;

            if (col.transform.root == enemy.transform ||
                col.GetComponentInParent<EnemyController>() == enemy ||
                col.gameObject == enemy.gameObject ||
                col.CompareTag("Enemy"))
            {
                hasHitCurrentPunch = true;
                isPunchActive = false;
                enemy.TakeDamage(punchDamage);
                return;
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

        isPunching = false;
        isPunchActive = false;
        StopCoroutine(nameof(PunchRoutine));

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

        isPunching = false;
        isPunchActive = false;
        StopCoroutine(nameof(PunchRoutine));

        Debug.Log("<color=red>[OYUNCU NAKAVT OLDU!]</color>");

        if (playerAnim != null)
        {
            playerAnim.SetBool("isDead", true);
        }

        // Düşmana oyuncunun öldüğünü bildir (Düşman Show Pose'a geçsin)
        if (enemyController == null)
        {
            enemyController = FindObjectOfType<EnemyController>();
        }
        if (enemyController != null && !enemyController.IsDead)
        {
            enemyController.SetPlayerDead(true);
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
        transform.position = new Vector3(startPosition.x, 0f, startPosition.z);
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

        // Düşmana oyuncunun yeniden doğduğunu bildir (Düşman Show Pose'dan çıkıp Idle'a dönsün)
        if (enemyController == null)
        {
            enemyController = FindObjectOfType<EnemyController>();
        }
        if (enemyController != null && !enemyController.IsDead)
        {
            enemyController.SetPlayerDead(false);
        }

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