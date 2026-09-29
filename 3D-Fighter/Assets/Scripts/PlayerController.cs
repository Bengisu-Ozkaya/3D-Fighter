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
    [Tooltip("Normal yumruk vuruşunun vereceği hasar miktarı")]
    [SerializeField] float punchDamage = 20f;
    [Tooltip("E tuşuna basıldığında atılan aparkatın hasar miktarı")]
    [SerializeField] float uppercutDamage = 30f;
    [SerializeField] LayerMask targetLayers = ~0;

    [Header("El Kemiği Referansları (Boşsa Otomatik Bulunur)")]
    [SerializeField] Transform rightFist;
    [SerializeField] Transform leftFist;

    [Header("Oyuncu Sağlık Ayarları")]
    [SerializeField] float playerHealth = 100f;
    [SerializeField] float maxPlayerHealth = 100f;
    [SerializeField] float respawnDelay = 3f;

    [Header("Çarpışma & Gövde Ayarları")]
    [Tooltip("Oyuncunun fiziksel gövde yarıçapı. Düşmanların içinden geçmeyi engeller.")]
    [SerializeField] float playerBodyRadius = 0.45f;

    [Header("Pozisyon & Yükseklik (Y) Ayarları (Unity Inspector'dan Düzenlenebilir)")]
    [Tooltip("Normal ayaktayken, yürürken ve dövüşürken Y pozisyonu")]
    [SerializeField] float standingYPosition = 0f;

    [Tooltip("Nakavt olup yere düşme (Knockout) animasyonundaki Y pozisyonu")]
    [SerializeField] float fallenYPosition = 0.45f;

    [Tooltip("Yerden ayağa kalkma (Stand Up) animasyonundaki Y pozisyonu")]
    [SerializeField] float standUpYPosition = 0.45f;

    private bool isDead = false;
    public bool IsDead => isDead;

    private bool isStandingUp = false;
    public bool IsStandingUp => isStandingUp;

    private bool isEnemyDead = false;
    public bool IsEnemyDead => isEnemyDead;

    private bool isGameCompleted = false;
    public bool IsGameCompleted => isGameCompleted;

    private bool isBlocking = false;
    public bool IsBlocking => isBlocking;

    private Vector3 startPosition;
    private Quaternion startRotation;

    bool isPunchRight = false;
    bool isPunching = false;
    bool isPunchActive = false;
    bool hasHitCurrentPunch = false;

    // 0 GC Bellek optimizasyonu
    private readonly Collider[] hitColliders = new Collider[6];

    [SerializeField] private GameObject startPanel;
    [SerializeField] private UIManager uiManager;

    void Start()
    {
        startPosition = new Vector3(transform.position.x, standingYPosition, transform.position.z);
        transform.position = startPosition;
        startRotation = transform.rotation;

        if (uiManager == null)
        {
            uiManager = FindObjectOfType<UIManager>();
        }

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
        // Oyuncu öldüyse, karşı taraf ölüp Show Pose yapılıyorsa veya tüm dalgalar bittiyse hareket edip yumruk atamasın
        if (isDead || isEnemyDead || isGameCompleted) return;

        //MobilController();

        // 1. Blok Kontrolü ("F" Tuşu)
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (!isPunching)
            {
                isBlocking = true;
                if (playerAnim != null)
                {
                    playerAnim.CrossFadeInFixedTime("Center Block", 0.1f);
                }
            }
        }
        if (Input.GetKeyUp(KeyCode.F))
        {
            if (isBlocking)
            {
                isBlocking = false;
                if (playerAnim != null)
                {
                    playerAnim.CrossFadeInFixedTime("Idle", 0.15f);
                }
            }
        }

        // Blok yaparken hareket ve vuruş yapılmasın
        if (isBlocking) return;

        // 2. Karakter Hareketi
        HandleMovement();

        // 3. Aparkat Tuşu ("E" Tuşu - 20 Can Hasarı)
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (!isPunching)
            {
                ExecuteUppercut();
            }
        }

        // 4. Normal Yumruk Tuşu (Space veya Sol Tık)
        if (Input.GetKeyDown(KeyCode.Space) || (Input.GetMouseButtonDown(0) && (startPanel == null || !startPanel.activeSelf)))
        {
            if (!isPunching)
            {
                ExecutePunch();
            }
        }
    }

    void MobilController()
    {
        
    }

    void LateUpdate()
    {
        Vector3 pos = transform.position;

        // Karakterin durumuna göre Y yüksekliği (Inspector üzerinden ayarlanabilir)
        pos.y = GetCurrentTargetY();

        if (!isDead)
        {
            pos = ResolveCollisionWithEnemies(pos);
        }

        transform.position = pos;
    }

    /// <summary>
    /// Karakterin o anki animasyon ve dövüş durumuna göre hedef Y yüksekliğini döndürür (Inspector'dan ayarlanır)
    /// </summary>
    public float GetCurrentTargetY()
    {
        if (isStandingUp)
        {
            return standUpYPosition;
        }
        if (isDead)
        {
            return fallenYPosition;
        }
        return standingYPosition;
    }

    /// <summary>
    /// Oyuncunun düşmanların içinden geçmesini engeller ve temas anında kenara doğru yumuşakça kaydırır
    /// </summary>
    public Vector3 ResolveCollisionWithEnemies(Vector3 proposedPos)
    {
        EnemyController[] allEnemies = FindObjectsOfType<EnemyController>();
        foreach (var enemy in allEnemies)
        {
            if (enemy == null || enemy.IsDead) continue;

            Vector3 diff = proposedPos - enemy.transform.position;
            diff.y = 0f;
            float dist = diff.magnitude;
            float minDist = playerBodyRadius + 0.45f; // Oyuncu yarıçapı + Düşman yarıçapı

            if (dist < minDist)
            {
                if (dist > 0.001f)
                {
                    // Düşmanın içinden geçmeyi engelle, temas sınırına teğet kaydır
                    proposedPos = enemy.transform.position + diff.normalized * minDist;
                }
                else
                {
                    // Tam çakışma durumunda geriye doğru it
                    proposedPos = enemy.transform.position + (-transform.forward) * minDist;
                }
            }
        }
        proposedPos.y = GetCurrentTargetY();
        return proposedPos;
    }

    void HandleMovement()
    {
        float h = 0f;
        float v = 0f;

        if (Input.GetKey(KeyCode.D)) h -= 1f;
        if (Input.GetKey(KeyCode.A)) h += 1f;
        if (Input.GetKey(KeyCode.W)) v -= 1f;
        if (Input.GetKey(KeyCode.S)) v += 1f;

        Vector3 moveDir = new Vector3(h, 0f, v).normalized;

        if (moveDir != Vector3.zero)
        {
            // 1. Pozisyonu hareket yönünde ilerlet (Dünya koordinatlarında)
            Vector3 targetPos = transform.position + moveDir * speed * Time.deltaTime;
            targetPos.y = standingYPosition;

            // Düşmanların içinden geçmeyi engelle ve kenarından yumuşakça kaydır
            targetPos = ResolveCollisionWithEnemies(targetPos);
            transform.position = targetPos;

            // 2. Karakterin rotasyonu:
            // S tuşuna basılıp geri çekilirken karakter arkasını kameraya dönmesin;
            // yüzü rakibe / kameranın baktığı yöne baksın, sırtı kameraya dönük geri adım atsın.
            Vector3 facingDir;
            if (Input.GetKey(KeyCode.S))
            {
                EnemyController targetEnemy = (enemyController != null && !enemyController.IsDead)
                    ? enemyController
                    : GetClosestLivingEnemy();

                if (targetEnemy != null && !targetEnemy.IsDead)
                {
                    facingDir = (targetEnemy.transform.position - transform.position);
                    facingDir.y = 0f;
                    if (facingDir == Vector3.zero)
                    {
                        facingDir = GetCameraForward();
                    }
                }
                else
                {
                    facingDir = GetCameraForward();
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

    Vector3 GetCameraForward()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 camFwd = cam.transform.forward;
            camFwd.y = 0f;
            if (camFwd.sqrMagnitude > 0.001f)
            {
                return camFwd.normalized;
            }
        }
        return transform.forward;
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

    /// <summary>
    /// E tuşuna basıldığında çalışan 20 hasarlık güçlü aparkat vuruşu
    /// </summary>
    void ExecuteUppercut()
    {
        if (isPunching) return;
        isPunching = true;
        hasHitCurrentPunch = false;

        FaceOpponentOnPunch();

        if (playerAnim != null)
        {
            playerAnim.CrossFadeInFixedTime("Uppercut", 0.12f);
        }

        StartCoroutine(UppercutRoutine());
    }

    IEnumerator UppercutRoutine()
    {
        // 1. Öne doğru boksör hamlesi
        float stepDuration = 0.16f;
        if (punchStepDistance > 0f)
        {
            float stepTimer = 0f;
            Vector3 startPos = new Vector3(transform.position.x, standingYPosition, transform.position.z);
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            Vector3 stepTarget = startPos + fwd.normalized * (punchStepDistance * 1.25f);
            stepTarget.y = standingYPosition;
            while (stepTimer < stepDuration)
            {
                stepTimer += Time.deltaTime;
                float t = Mathf.Clamp01(stepTimer / stepDuration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                Vector3 newPos = Vector3.Lerp(startPos, stepTarget, smoothT);
                newPos.y = standingYPosition;
                newPos = ResolveCollisionWithEnemies(newPos);
                transform.position = newPos;
                yield return null;
            }
        }

        // 2. Aparkatın yükselme ve temas penceresi (0.15s - 0.45s arası her karede temas ara)
        float activeTimer = 0f;
        float uppercutActiveDuration = 0.35f;
        while (activeTimer < uppercutActiveDuration)
        {
            if (hasHitCurrentPunch) break;

            CheckPhysicalUppercutContact();

            activeTimer += Time.deltaTime;
            yield return null;
        }

        // 3. Kolun geri çekilmesi ve garda dönüş
        yield return new WaitForSeconds(0.45f);

        if (playerAnim != null && !isDead && !isGameCompleted)
        {
            playerAnim.CrossFadeInFixedTime("Idle", 0.18f);
        }

        isPunching = false;
    }

    void CheckPhysicalUppercutContact()
    {
        // 1. Fiziksel el temas alanı kontrolü (Sağ el aparkat)
        Vector3 fistPos = (rightFist != null)
            ? rightFist.position + transform.forward * 0.15f + Vector3.up * 0.1f
            : transform.position + transform.forward * 0.65f + Vector3.up * 1.0f;

        int hitCount = Physics.OverlapSphereNonAlloc(fistPos, punchRadius * 1.5f, hitColliders, targetLayers);

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = hitColliders[i];
            if (col == null || col.transform.root == transform.root) continue;

            EnemyController hitEnemy = col.GetComponentInParent<EnemyController>() ?? col.GetComponent<EnemyController>();
            if (hitEnemy != null && !hitEnemy.IsDead)
            {
                float distToEnemy = Vector3.Distance(transform.position, hitEnemy.transform.position);
                if (distToEnemy <= maxPunchRange * 1.35f)
                {
                    Vector3 dirToEnemy = (hitEnemy.transform.position - transform.position).normalized;
                    dirToEnemy.y = 0f;
                    if (Vector3.Dot(transform.forward, dirToEnemy) >= 0.15f)
                    {
                        hasHitCurrentPunch = true;
                        hitEnemy.TakeDamage(uppercutDamage); // 20 Hasar!
                        Debug.Log($"<color=green>[GÜÇLÜ APARKAT İSABET ETTİ!]</color> {hitEnemy.name} düşmanına {uppercutDamage} hasar verildi!");
                        enemyController = hitEnemy;
                        return;
                    }
                }
            }
        }

        // 2. Güvenilir Yakın Dövüş Kontrolü: Oyuncunun tam önündeki canlı düşman menzildeyse doğrudan vur
        EnemyController closeEnemy = (enemyController != null && !enemyController.IsDead)
            ? enemyController
            : GetClosestLivingEnemy();

        if (closeEnemy != null && !closeEnemy.IsDead)
        {
            float dist = Vector3.Distance(transform.position, closeEnemy.transform.position);
            if (dist <= maxPunchRange * 1.3f)
            {
                Vector3 dir = (closeEnemy.transform.position - transform.position).normalized;
                dir.y = 0f;
                if (Vector3.Dot(transform.forward, dir) >= 0.2f)
                {
                    hasHitCurrentPunch = true;
                    closeEnemy.TakeDamage(uppercutDamage); // 20 Hasar!
                    Debug.Log($"<color=green>[GÜÇLÜ APARKAT İSABET ETTİ!]</color> {closeEnemy.name} düşmanına {uppercutDamage} hasar verildi!");
                    enemyController = closeEnemy;
                }
            }
        }
    }

    EnemyController GetClosestLivingEnemy()
    {
        EnemyController[] allEnemies = FindObjectsOfType<EnemyController>();
        EnemyController closest = null;
        float minDistSq = float.MaxValue;
        Vector3 myPos = transform.position;

        foreach (var enemy in allEnemies)
        {
            if (enemy == null || enemy.IsDead) continue;
            float sqDist = (enemy.transform.position - myPos).sqrMagnitude;
            if (sqDist < minDistSq)
            {
                minDistSq = sqDist;
                closest = enemy;
            }
        }
        return closest;
    }

    void FaceOpponentOnPunch()
    {
        EnemyController enemy = (enemyController != null && !enemyController.IsDead)
            ? enemyController
            : GetClosestLivingEnemy();

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

    /// <summary>
    /// Tüm dalgalar bittiğinde çağrılır; oyuncuyu Idle moduna alır ve kontrolleri kilitler
    /// </summary>
    public void SetGameCompleted()
    {
        isGameCompleted = true;
        isEnemyDead = false;

        if (playerAnim != null)
        {
            playerAnim.SetBool("isDeadEnemy", false);
            playerAnim.SetBool("leftMove", false);
            playerAnim.SetBool("rightMove", false);
            playerAnim.CrossFadeInFixedTime("Idle", 0.2f);
        }

        isPunching = false;
        isPunchActive = false;
    }

    /// <summary>
    /// Tüm dalgalar bittiğinde zafer pozunu (Show Pose) başlatır ve kontrolleri kilitler
    /// </summary>
    public void PlayVictoryShowPose()
    {
        StartCoroutine(PlayVictoryShowPoseRoutine());
    }

    /// <summary>
    /// Zafer pozunu başlatır ve animasyon baştan sona oynayıp bitene kadar bekler
    /// </summary>
    public IEnumerator PlayVictoryShowPoseRoutine()
    {
        isGameCompleted = true;
        isEnemyDead = true;
        isPunching = false;
        isPunchActive = false;
        StopCoroutine(nameof(PunchRoutine));

        if (playerAnim != null)
        {
            playerAnim.SetBool("leftMove", false);
            playerAnim.SetBool("rightMove", false);
            playerAnim.SetBool("isDeadEnemy", true);
            playerAnim.CrossFadeInFixedTime("Show Pose", 0.2f);
        }

        // Animator'ın Show Pose state'ine geçmesi için kısa bir süre tanı
        yield return new WaitForSeconds(0.25f);

        // Animasyonun son karesine kadar (normalizedTime >= 0.98f) oynatılmasını bekle
        float timer = 0f;
        float maxTimeout = 7.5f; // Spawn Wait klibi ~6.3 saniyedir

        while (timer < maxTimeout)
        {
            timer += Time.deltaTime;

            if (playerAnim != null)
            {
                AnimatorStateInfo stateInfo = playerAnim.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.IsName("Show Pose"))
                {
                    if (stateInfo.normalizedTime >= 0.98f)
                    {
                        break;
                    }
                }
            }

            yield return null;
        }

        // Animasyon bittikten sonra panelin açılması için küçük ve estetik bir bekleme
        yield return new WaitForSeconds(0.4f);
    }

    /// <summary>
    /// Oyunu yeniden başlatırken veya ana menüye dönerken oyuncunun canını, pozisyonunu ve durumlarını sıfırlar
    /// </summary>
    public void ResetPlayerState()
    {
        StopAllCoroutines();

        isDead = false;
        isStandingUp = false;
        isEnemyDead = false;
        isGameCompleted = false;
        isPunching = false;
        isPunchActive = false;
        isBlocking = false;
        hasHitCurrentPunch = false;

        playerHealth = maxPlayerHealth;
        transform.position = new Vector3(startPosition.x, standingYPosition, startPosition.z);
        transform.rotation = startRotation;

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = true;
        }

        if (playerAnim != null)
        {
            playerAnim.SetBool("isDead", false);
            playerAnim.SetBool("isDeadEnemy", false);
            playerAnim.SetBool("leftMove", false);
            playerAnim.SetBool("rightMove", false);
            playerAnim.CrossFadeInFixedTime("Idle", 0.1f);
        }
    }

    IEnumerator PunchRoutine(int fistIndex)
    {
        float stepDuration = 0.16f;
        // 1. Boksör Hamlesi (Step-in): Yumruk atarken öne doğru hafif ve doğal bir adım at (SmoothStep ile sarsıntısız)
        if (punchStepDistance > 0f)
        {
            float stepTimer = 0f;
            Vector3 startPos = new Vector3(transform.position.x, standingYPosition, transform.position.z);
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            Vector3 stepTarget = startPos + fwd.normalized * punchStepDistance;
            stepTarget.y = standingYPosition;
            while (stepTimer < stepDuration)
            {
                stepTimer += Time.deltaTime;
                float t = Mathf.Clamp01(stepTimer / stepDuration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                Vector3 newPos = Vector3.Lerp(startPos, stepTarget, smoothT);
                newPos.y = standingYPosition;
                newPos = ResolveCollisionWithEnemies(newPos);
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
        // 1. Fiziksel temas alanı kontrolü (Eldiven küresi)
        Vector3 fistPos = GetFistPosition(fistIndex);
        int hitCount = Physics.OverlapSphereNonAlloc(fistPos, punchRadius, hitColliders, targetLayers);

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = hitColliders[i];
            if (col == null || col.transform.root == transform.root) continue;

            EnemyController hitEnemy = col.GetComponentInParent<EnemyController>() ?? col.GetComponent<EnemyController>();
            if (hitEnemy != null && !hitEnemy.IsDead)
            {
                // Kararlı Mesafe Sınırı
                float distToEnemy = Vector3.Distance(transform.position, hitEnemy.transform.position);
                if (distToEnemy <= maxPunchRange)
                {
                    // Yön/Açı Kontrolü: Oyuncunun baktığı yönde olmalı
                    Vector3 dirToEnemy = (hitEnemy.transform.position - transform.position).normalized;
                    dirToEnemy.y = 0f;
                    if (Vector3.Dot(transform.forward, dirToEnemy) >= 0.25f)
                    {
                        hasHitCurrentPunch = true;
                        isPunchActive = false;
                        hitEnemy.TakeDamage(punchDamage);
                        enemyController = hitEnemy; // Odaklanılan düşmanı son vurulan düşman yap
                        return;
                    }
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
        return transform.position + transform.forward * 0.4f + Vector3.up * 0.65f;
    }

    // Editör Scene ekranında yumruk temas alanını gösterir
    private void OnDrawGizmos()
    {
        Gizmos.color = isPunchActive ? Color.green : new Color(1f, 0.4f, 0f, 0.8f);

        Vector3 rPos = GetFistPosition(0);
        Gizmos.DrawWireSphere(rPos, punchRadius);

        Vector3 lPos = GetFistPosition(1);
        Gizmos.DrawWireSphere(lPos, punchRadius);

        // Gövde temas sınırı (Düşmanların içinden geçmeyi engelleyen alan)
        Gizmos.color = new Color(0f, 0.7f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, playerBodyRadius);
    }

    public void TakeDamage(float damageAmount)
    {
        if (isDead) return;

        // Blok Kontrolü: Oyuncu F tuşuyla blok yapıyorsa hasar almaz
        if (isBlocking)
        {
            Debug.Log("<color=green>[BLOK BAŞARILI!]</color> Oyuncu saldırıyı blokladı, hasar almadı!");
            return;
        }

        isPunching = false;
        isPunchActive = false;
        StopCoroutine(nameof(PunchRoutine));
        StopCoroutine(nameof(UppercutRoutine));

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
        isStandingUp = false;

        isPunching = false;
        isPunchActive = false;
        isBlocking = false;
        StopCoroutine(nameof(PunchRoutine));
        StopCoroutine(nameof(UppercutRoutine));

        Debug.Log("<color=red>[OYUNCU NAKAVT OLDU!]</color>");

        if (playerAnim != null)
        {
            playerAnim.SetBool("isDead", true);
            playerAnim.SetBool("leftMove", false);
            playerAnim.SetBool("rightMove", false);
            playerAnim.SetBool("isDeadEnemy", false);
            playerAnim.ResetTrigger("GetHit");
            playerAnim.ResetTrigger("PunchLeft");
            playerAnim.ResetTrigger("PunchRight");
            playerAnim.CrossFadeInFixedTime("Knockout", 0.08f);
        }

        // Tüm düşmanlara oyuncunun öldüğünü bildir (Düşmanlar Show Pose'a geçsin)
        EnemyController[] allEnemiesOnDie = FindObjectsOfType<EnemyController>();
        foreach (var enemy in allEnemiesOnDie)
        {
            if (enemy != null && !enemy.IsDead)
            {
                enemy.SetPlayerDead(true);
            }
        }

        StartCoroutine(WaitPos());

        // Öldükten sonra hasar almaması için collider'ı geçici kapat
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }

        StartCoroutine(DeathRoutine());
    }

    IEnumerator DeathRoutine()
    {
        isStandingUp = false;

        // 1. Knockout (yere düşme) animasyonunun başlaması için kısa bir süre tanı
        yield return new WaitForSeconds(0.2f);

        // 2. Knockout animasyonu tamamlanana ve oyuncu yere düşene kadar bekle (~2.5 saniye)
        float fallTimer = 0f;
        while (fallTimer < 3.0f)
        {
            fallTimer += Time.deltaTime;
            if (playerAnim != null)
            {
                AnimatorStateInfo stateInfo = playerAnim.GetCurrentAnimatorStateInfo(0);
                if ((stateInfo.IsName("Knockout") || stateInfo.IsName("Defeat")) && stateInfo.normalizedTime >= 0.95f)
                {
                    break;
                }
            }
            yield return null;
        }

        // Yerde nakavt pozisyonunu sabitle (yerde yatar)
        Vector3 fallPos = transform.position;
        fallPos.y = fallenYPosition;
        transform.position = fallPos;

        // Yerde nakavt halinde kısa ve doğal bir bekleme süresi (0.5 saniye)
        yield return new WaitForSeconds(0.5f);

        // GameOver panelini aç (Kullanıcı Relive veya Home butonuna basana kadar yerde yatmaya devam eder)
        if (uiManager == null)
        {
            uiManager = FindObjectOfType<UIManager>();
        }

        if (uiManager != null)
        {
            uiManager.ShowGameOverPanel();
        }
        else
        {
            Debug.LogWarning("PlayerController: UIManager bulunamadı, GameOver paneli açılamadı!");
        }
    }

    /// <summary>
    /// Relive butonuna basıldığında çağrılır: Yerde yatan oyuncu Kip Up animasyonuyla ayağa kalkar
    /// </summary>
    public void RelivePlayer()
    {
        StopAllCoroutines();
        StartCoroutine(ReliveRoutine());
    }

    IEnumerator ReliveRoutine()
    {
        // Oyuncu yerde nakavt halinde yatarken kalkış durumuna geç
        isDead = true;
        isStandingUp = true;

        Vector3 standPos = transform.position;
        standPos.y = standUpYPosition;
        transform.position = standPos;

        // Canı tamamen yenile
        playerHealth = maxPlayerHealth;

        // Kip Up animasyonunu oynat
        string getUpAnim = (playerAnim != null && playerAnim.HasState(0, Animator.StringToHash("Kip Up"))) ? "Kip Up" : "Stand Up";

        if (playerAnim != null)
        {
            playerAnim.SetBool("isDead", false);
            playerAnim.CrossFadeInFixedTime(getUpAnim, 0.15f);
        }

        // Animator'ın kalkış state'ine geçmesi için kısa süre tanı
        yield return new WaitForSeconds(0.2f);

        // Kip Up animasyonunun oynatılıp tamamlanmasını bekle (~1.85 saniye)
        float standTimer = 0f;
        while (standTimer < 2.5f)
        {
            standTimer += Time.deltaTime;
            if (playerAnim != null)
            {
                AnimatorStateInfo stateInfo = playerAnim.GetCurrentAnimatorStateInfo(0);
                if ((stateInfo.IsName("Kip Up") || stateInfo.IsName("Stand Up")) && stateInfo.normalizedTime >= 0.95f)
                {
                    break;
                }
            }
            yield return null;
        }

        // Oyuncu tamamen ayağa kalktı; durumları sıfırla, collider'ı ve kontrolleri aç
        isStandingUp = false;
        isDead = false;

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = true;
        }

        if (playerAnim != null)
        {
            playerAnim.SetBool("isDead", false);
            playerAnim.CrossFadeInFixedTime("Idle", 0.2f);
        }

        // Tüm düşmanlara oyuncunun yeniden doğduğunu bildir (Düşmanlar Show Pose'dan çıkıp Idle'a dönsün)
        EnemyController[] allEnemiesOnRespawn = FindObjectsOfType<EnemyController>();
        foreach (var enemy in allEnemiesOnRespawn)
        {
            if (enemy != null && !enemy.IsDead)
            {
                enemy.SetPlayerDead(false);
            }
        }

        Debug.Log("<color=green>[OYUNCU KİP UP YAPARAK AYAĞA KALKTI VE YENİDEN DOĞDU!]</color>");
    }

    public float GetHealth() => playerHealth;

    /// <summary>
    /// Oyuncunun normal yumruk hasarını ayarlar
    /// </summary>
    public void SetPunchDamage(float damage)
    {
        punchDamage = damage;
    }

    public float GetPunchDamage() => punchDamage;

    /// <summary>
    /// Oyuncunun 'E' tuşu aparkat hasarını ayarlar
    /// </summary>
    public void SetUppercutDamage(float damage)
    {
        uppercutDamage = damage;
    }

    public float GetUppercutDamage() => uppercutDamage;

    IEnumerator WaitPunch()
    {
        yield return new WaitForSeconds(0.6f);
    }

    IEnumerator WaitPos()
    {
        yield return new WaitForSeconds(1.2f);
        transform.position = new Vector3(transform.position.x, fallenYPosition, transform.position.z);
    }
}