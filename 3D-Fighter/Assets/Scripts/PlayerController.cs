using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
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
    [SerializeField] float maxPunchRange = 25f;

    [Tooltip("Yumruk atarken karakterin ileriye doğru attığı doğal boks adımı mesafesi (metre) - İleri hamleyi kapatmak için 0")]
    [SerializeField] float punchStepDistance = 0f;

    [Tooltip("Yumruğun aktif kalıp temas arayacağı süre (saniye). Boks animasyonunun uzanma ve geri çekilme aralığı.")]
    [SerializeField] float punchActiveDuration = 0.40f;

    [Tooltip("Bir yumruğun baştan sona tamamlanma ve gard pozisyonuna dönüş süresi (saniye). Bu süre dolmadan yeni yumruk atılamaz.")]
    [SerializeField] float punchDuration = 0.55f;
    [Tooltip("Normal yumruk vuruşunun vereceği hasar miktarı (Kolay ve Orta: 10, Zor: 20)")]
    [SerializeField] float punchDamage = 10f;
    [Tooltip("E tuşuna basıldığında atılan aparkatın hasar miktarı (Kolay ve Orta: 20, Zor: 30)")]
    [SerializeField] float uppercutDamage = 20f;
    [SerializeField] LayerMask targetLayers = ~0;

    [Header("El Kemiği Referansları (Boşsa Otomatik Bulunur)")]
    [SerializeField] Transform rightFist;
    [SerializeField] Transform leftFist;

    [Header("Oyuncu Sağlık Ayarları")]
    public float playerHealth = 100f;
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

    private bool isHitStunned = false;
    public bool IsHitStunned => isHitStunned;

    private Coroutine punchRoutine = null;
    private Coroutine uppercutRoutine = null;
    private Coroutine hitStunRoutine = null;
    private float nextPunchAvailableTime = 0f;

    // Ortiz animasyon sistemi: hareket animasyonu çakışmasını önlemek için mevcut hareketi takip et
    private enum MoveAnim { None, Idle, Forward, Backward, Left, Right, LeftPivot, RightPivot }
    private MoveAnim currentMoveAnim = MoveAnim.None;
    private const float MOVE_ANIM_BLEND = 0.10f; // CrossFade süresi (saniye)
    private Coroutine blockingCoroutine = null;   // Blocking giriş animasyonu coroutine'i

    [Header("Vuruş Zamanlamaları (Windup & Cooldown)")]
    [Tooltip("Yumruğun temas anından önceki savurma / uzanma gecikmesi (saniye).")]
    [SerializeField] private float punchWindupTime = 0.14f;
    [Tooltip("Aparkatın temas anından önceki yükselme gecikmesi (saniye).")]
    [SerializeField] private float uppercutWindupTime = 0.18f;
    [Tooltip("Yumruktan sonra yeni yumruk atılabilmesi için bekleme süresi")]
    [SerializeField] private float punchCooldown = 0.05f;

    // 0 GC Bellek optimizasyonu
    private readonly Collider[] hitColliders = new Collider[6];

    [SerializeField] private GameObject startPanel;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private EnemySpawner enemySpawner;
    [Header("Mobil Kontroller")]
    [SerializeField] private VirtualJoystick joystick;
    private int playerDoBlock;

    [Header("Ulti Yeteneği")]
    public bool usingUlti = true;
    [Tooltip("Ulti vuruşunun vereceği hasar")]
    [SerializeField] private float ultiDamage = 30f;
    [Tooltip("Ulti yeteneğinin tekrar dolma süresi (Cooldown - saniye)")]
    [SerializeField] private float ultiCooldown = 3f;
    [Tooltip("Ulti vuruşunun etki alanı yarıçapı")]
    [SerializeField] private float ultiRange = 2.0f;

    private bool isCastingUlti = false;
    public bool IsCastingUlti => isCastingUlti;

    [Header("Ulti Hands Efekti")]
    [Tooltip("Ulti sırasında doğacak Hands nesnesi (Assets/Fighter Animation/Hands.fbx)")]
    [SerializeField] private GameObject handsPrefab;
    [Tooltip("Ulti sırasında doğacak el sayısı")]
    [SerializeField] private int ultiHandsCount = 10;
    [Tooltip("Eller arasındaki doğma gecikmesi (saniye)")]
    [SerializeField] private float ultiHandsSpawnInterval = 0.12f;
    [Tooltip("Ellerin doğacağı başlangıç Y yüksekliği")]
    [SerializeField] private float ultiHandsSpawnY = 3.33f;
    [Tooltip("Ellerin yok olacağı hedef Y yüksekliği")]
    [SerializeField] private float ultiHandsTargetY = 0;
    [Tooltip("Ellerin yukarı çıkış hızı")]
    [SerializeField] private float ultiHandsSpeed = 3.5f;
    [Tooltip("X ekseni minimum doğma konumu")]
    [SerializeField] private float ultiHandsMinX = -2.5f;
    [Tooltip("X ekseni maksimum doğma konumu")]
    [SerializeField] private float ultiHandsMaxX = -0.33f;
    [Tooltip("Z ekseni minimum doğma konumu")]
    [SerializeField] private float ultiHandsMinZ = 1f;
    [Tooltip("Z ekseni maksimum doğma konumu")]
    [SerializeField] private float ultiHandsMaxZ = 4f;
    [Tooltip("Doğan ellerin rotasyonu")]
    [SerializeField] private Vector3 ultiHandsRotation = Vector3.zero;

    private Coroutine ultiHandsCoroutine;

    [Header("Ulti Ses Efektleri")]
    [Tooltip("Ulti devreye girdiğinde ilk çalınacak ses (Assets/Sounds/GÖKTE NE VAR.mp3)")]
    [SerializeField] private AudioClip ultiSoundClip;
    [Tooltip("İlk sesin şiddeti (0 ile 1 arası)")]
    [Range(0f, 1f)]
    [SerializeField] private float ultiSoundVolume = 1f;

    [Tooltip("İlk ses bittikten sonra çalınacak yumruk sesi (Assets/Sounds/YUMRUK.mp3)")]
    [SerializeField] private AudioClip ultiPunchSoundClip;
    [Tooltip("Yumruk sesinin şiddeti (0 ile 1 arası)")]
    [Range(0f, 1f)]
    [SerializeField] private float ultiPunchSoundVolume = 1f;
    [Tooltip("İlk ses bittikten sonra yumruk sesinden önce eklenebilecek gecikme (saniye)")]
    [SerializeField] private float punchSoundDelay = 0f;

    [Tooltip("Sesin çalınacağı AudioSource bileşeni (Boş bırakılırsa otomatik eklenir)")]
    [SerializeField] private AudioSource playerAudioSource;

    private Coroutine ultiSoundCoroutine;
    private Coroutine ultiCooldownCoroutine;

    [Header("Ulti Işık & Atmosfer Efekti (Cinematic Ulti Lighting)")]
    [Tooltip("Ulti atılırken sahadaki ışıkların estetik ve sinematik bir renk atmosferine geçmesini sağlar")]
    [SerializeField] private bool enableUltiLightShow = true;
    [Tooltip("Işıkların renk değiştirme ve yumuşak dalgalanma hızı (Gözü yormayan sinematik tempo)")]
    [SerializeField] private float ultiLightSpeed = 2.0f;
    [Tooltip("Ulti sırasında arena ışıklarının parlaklık çarpanı (Gözü almaması için dengeli seviye)")]
    [SerializeField] private float ultiLightIntensityMultiplier = 1.2f;
    [Tooltip("Ulti sırasında oyuncu üzerinde doğacak dengeli kahraman aura ışığı")]
    [SerializeField] private bool spawnUltiHeroLight = true;

    private Coroutine ultiLightCoroutine;
    private GameObject ultiHeroLightObj;

    // Gözü yormayan, asil ve sinematik dövüş aurası renk paleti (Kör edici neonlar yerine estetik dengeli tonlar)
    private readonly Color[] ultiColors = new Color[]
    {
        new Color(0.95f, 0.72f, 0.28f), // Sıcak Şampanya / Altın Kehribar (Asil Şampiyon Aurası)
        new Color(0.58f, 0.35f, 0.85f), // Mistik Ametist Moru (Gözü Yormayan Derin Ton)
        new Color(0.22f, 0.58f, 0.90f), // Göksel Safir Mavisi (Yumuşak Göksel Işık)
        new Color(0.90f, 0.45f, 0.28f), // Sıcak Gün Batımı / Mercan (Dengeli Sıcak Ton)
        new Color(0.25f, 0.75f, 0.72f), // Yumuşak Zümrüt Turkuaz (Doğal Parlama)
        new Color(0.82f, 0.25f, 0.40f)  // Asil Yakut Kırmızısı (Doygun ve Zarif)
    };

    [SerializeField] Image playerHealthBar;

    void Awake()
    {
        usingUlti = true; // Ulti oyun başında hazır değildir; oyun modu seçildikten sonra yüklenmeye başlar
        if (ultiLightIntensityMultiplier > 1.5f) ultiLightIntensityMultiplier = 1.2f;
        if (ultiLightSpeed > 3.5f) ultiLightSpeed = 2.0f;
#if UNITY_EDITOR
        if (handsPrefab == null)
        {
            handsPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Fighter Animation/Hands.fbx");
        }
        if (ultiSoundClip == null)
        {
            ultiSoundClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/GÖKTE NE VAR.mp3");
        }
        if (ultiPunchSoundClip == null)
        {
            ultiPunchSoundClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/YUMRUK.mp3");
        }
#endif
    }
    void Start()
    {
        startPosition = new Vector3(transform.position.x, standingYPosition, transform.position.z);
        transform.position = startPosition;
        startRotation = transform.rotation;

        punchDamage = 10f;
        uppercutDamage = 20f;
        punchStepDistance = 0f;

        if (uiManager == null)
        {
            uiManager = FindObjectOfType<UIManager>();
        }

        if (enemySpawner == null)
        {
            enemySpawner = FindObjectOfType<EnemySpawner>();
        }

        if (joystick == null)
        {
            joystick = VirtualJoystick.Instance ?? FindObjectOfType<VirtualJoystick>(true);
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
        if (maxPunchRange <= 0f) maxPunchRange = 25f;

        // StartPanel kontrolü: Eğer startPanel sahnede yoksa veya kapalıysa ve oyun başladıysa ulti cooldown başlat
        if (startPanel == null)
        {
            GameObject sp = GameObject.Find("Start Panel") ?? GameObject.Find("StartPanel");
            if (sp != null) startPanel = sp;
        }
        if (startPanel == null || !startPanel.activeInHierarchy)
        {
            EnemySpawner spawner = FindObjectOfType<EnemySpawner>();
            if (spawner != null && spawner.IsGameStarted)
            {
                StartUltiCooldown();
            }
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
        // Oyun duraklatıldıysa (Pause) hiçbir girdi veya hareket işlenmesin
        if (Time.timeScale == 0f) return;

        // Oyuncu öldüyse, ayağa kalkıyorsa veya tüm dalgalar bittiyse hareket edip yumruk atamasın
        if (isDead || isGameCompleted || isStandingUp) return;

        // Düşmandan darbe alındığında (Hit Stun): Hasar alma animasyonu oynar, hareket ve vuruş kilitlenir!
        if (isHitStunned)
        {
            if (isBlocking)
            {
                isBlocking = false;
            }
            FaceOpponentOnPunch();
            return;
        }

        // 1. Blok Kontrolü (Klavye Space Tuşu veya Mobil Blok Butonu)
        bool wantBlock = Input.GetKey(KeyCode.Space) || (playerDoBlock == 1);

        if (wantBlock)
        {
            if (!isBlocking && !isPunching && !isHitStunned)
            {
                isBlocking = true;
                currentMoveAnim = MoveAnim.None;
                if (playerAnim != null)
                {
                    playerAnim.SetBool("isBlocking", true);
                    playerAnim.SetBool("Forward_Move", false);
                    playerAnim.SetBool("Backward_Move", false);
                    playerAnim.SetBool("Left_Move", false);
                    playerAnim.SetBool("Right_Move", false);
                    // Önce Blocking giriş animasyonunu oynat, ardından Block Idle'a geç
                    if (blockingCoroutine != null) StopCoroutine(blockingCoroutine);
                    blockingCoroutine = StartCoroutine(BlockingEnterRoutine());
                }
            }
        }
        else
        {
            if (isBlocking)
            {
                isBlocking = false;
                currentMoveAnim = MoveAnim.None;
                if (playerAnim != null)
                {
                    playerAnim.SetBool("isBlocking", false);
                }
                if (blockingCoroutine != null)
                {
                    StopCoroutine(blockingCoroutine);
                    blockingCoroutine = null;
                }
                if (playerAnim != null && !isHitStunned)
                {
                    playerAnim.CrossFadeInFixedTime("Idle", 0.15f);
                    currentMoveAnim = MoveAnim.Idle;
                }
            }
        }

        // Blok yaparken hareket ve vuruş yapılmasın; yüzünü rakibe dönük tutsun
        if (isBlocking)
        {
            FaceOpponentOnPunch();
            return;
        }

        // 2. Karakter Hareketi
        HandleMovement();

        if (Input.GetKeyDown(KeyCode.R))
        {
            if (!usingUlti && !isPunching && !isBlocking && !isDead && !isGameCompleted && !isStandingUp && !isHitStunned)
            {
                ExecuteUlti();
            }
        }

        // 3. Aparkat Tuşu ("E" Tuşu - 20 Can Hasarı)
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (!isPunching && !isHitStunned)
            {
                ExecuteUppercut();
            }
        }

        // 4. Normal Yumruk Tuşu (F veya Sol Tık)
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (!isPunching && !isHitStunned)
            {
                ExecutePunch();
            }
        }
    }

    void LateUpdate()
    {
        Vector3 pos = transform.position;

        // Karakterin durumuna göre Y yüksekliği (Inspector üzerinden ayarlanabilir)
        pos.y = GetCurrentTargetY();

        if (!isDead)
        {
            pos = ResolveCollisionWithEnemies(pos);
            pos = RingBoundary.ClampToArena(pos, playerBodyRadius);
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
        proposedPos = RingBoundary.ClampToArena(proposedPos, playerBodyRadius);
        return proposedPos;
    }

    void HandleMovement()
    {
        // Yumruk atarken veya hasar sersemlemesindeyken hareket etmesin ve yönünü doğrudan rakibe kilitlesin
        if (isPunching || isHitStunned)
        {
            FaceOpponentOnPunch();
            return;
        }

        float h = 0f;
        float v = 0f;

        if (Input.GetKey(KeyCode.W)) v -= 1f;
        if (Input.GetKey(KeyCode.S)) v += 1f;
        if (Input.GetKey(KeyCode.A)) h += 1f;
        if (Input.GetKey(KeyCode.D)) h -= 1f;

        // Mobil Joystick Girişi
        if (joystick == null)
        {
            joystick = VirtualJoystick.Instance ?? FindObjectOfType<VirtualJoystick>(true);
        }

        if (joystick != null)
        {
            Vector2 joy = joystick.InputDirection;
            if (joy.sqrMagnitude > 0.001f)
            {
                // joy.x: Sağa (+1) çekildiğinde D tuşu gibi h azalır (-=)
                // joy.x: Sola (-1) çekildiğinde A tuşu gibi h artar (+=)
                h -= joy.x;
                // joy.y: Yukarı (+1) çekildiğinde W tuşu gibi v azalır (-=)
                // joy.y: Aşağı (-1) çekildiğinde S tuşu gibi v artar (+=)
                v -= joy.y;
            }
        }

        Vector3 rawMove = new Vector3(h, 0f, v);
        Vector3 moveDir = rawMove.sqrMagnitude > 1f ? rawMove.normalized : rawMove;

        if (moveDir.sqrMagnitude > 0.001f)
        {
            // 1. Pozisyonu hareket yönünde ilerlet (Dünya koordinatlarında)
            Vector3 targetPos = transform.position + moveDir * speed * Time.deltaTime;
            targetPos.y = standingYPosition;

            // Düşmanların içinden geçmeyi engelle ve kenarından yumuşakça kaydır
            targetPos = ResolveCollisionWithEnemies(targetPos);
            targetPos = RingBoundary.ClampToArena(targetPos, playerBodyRadius);
            transform.position = targetPos;
        }

        // 2. Karakterin rotasyonu:
        // Oyuncu sağa, sola veya geriye hareket ederken asla yönü dönmesin;
        // sırtı her zaman kameraya dönük kalsın ve yüzü rakibe / ileriye baksın.
        Vector3 facingDir = GetCameraForward();
        EnemyController targetEnemy = (enemyController != null && !enemyController.IsDead)
            ? enemyController
            : GetClosestLivingEnemy();

        if (targetEnemy != null && !targetEnemy.IsDead)
        {
            Vector3 dirToEnemy = targetEnemy.transform.position - transform.position;
            dirToEnemy.y = 0f;
            // Düşman oyuncunun önündeyse (kameranın baktığı genel doğrultuda) düşmana odaklansın
            if (dirToEnemy.sqrMagnitude > 0.05f && Vector3.Dot(dirToEnemy.normalized, GetCameraForward()) > 0.1f)
            {
                facingDir = dirToEnemy;
            }
        }

        if (facingDir != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(facingDir.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
        }

        // 3. Animasyon: Kullanıcının kendi belirlediği SetBool sistemi
        if (playerAnim != null)
        {
            if (v < -0.1f) // W veya Joystick İleri
            {
                playerAnim.SetBool("Forward_Move", true);
                playerAnim.SetBool("Left_Move", false);
                playerAnim.SetBool("Right_Move", false);
                playerAnim.SetBool("Backward_Move", false);
            }
            else if (h > 0.1f) // A veya Joystick Sol
            {
                playerAnim.SetBool("Left_Move", true);
                playerAnim.SetBool("Forward_Move", false);
                playerAnim.SetBool("Right_Move", false);
                playerAnim.SetBool("Backward_Move", false);
            }
            else if (h < -0.1f) // D veya Joystick Sağ
            {
                playerAnim.SetBool("Right_Move", true);
                playerAnim.SetBool("Forward_Move", false);
                playerAnim.SetBool("Left_Move", false);
                playerAnim.SetBool("Backward_Move", false);
            }
            else if (v > 0.1f) // S veya Joystick Geri
            {
                playerAnim.SetBool("Backward_Move", true);
                playerAnim.SetBool("Forward_Move", false);
                playerAnim.SetBool("Left_Move", false);
                playerAnim.SetBool("Right_Move", false);
            }
            else
            {
                playerAnim.SetBool("Backward_Move", false);
                playerAnim.SetBool("Forward_Move", false);
                playerAnim.SetBool("Left_Move", false);
                playerAnim.SetBool("Right_Move", false);
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

    /// <summary>
    /// Blok başladığında Blocking giriş animasyonunu oynatır,
    /// animasyon bitince Block Idle pozuna geçer.
    /// Oyuncu bloktan çıkarsa Coroutine otomatik durdurulur.
    /// </summary>
    private IEnumerator BlockingEnterRoutine()
    {
        if (playerAnim == null) yield break;

        // 1. Blocking giriş animasyonunu oynat
        playerAnim.CrossFadeInFixedTime("Blocking", 0.08f);

        // 2. Animator'ın state'e geçmesi için bir kare bekle
        yield return null;

        // 3. "Blocking" animasyonunun bitmesini bekle
        float timeout = 2.0f; // Maksimum bekleme süresi (donmayı önler)
        float elapsed = 0f;
        while (elapsed < timeout)
        {
            // Bloktan çıkıldıysa dur (isBlocking false oldu)
            if (!isBlocking) yield break;

            AnimatorStateInfo info = playerAnim.GetCurrentAnimatorStateInfo(0);
            if (info.IsName("Blocking") && info.normalizedTime >= 0.85f)
            {
                break; // Animasyon bitti
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 4. Hâlâ blok yapıyorsa Block Idle'a geç
        if (isBlocking && playerAnim != null)
        {
            playerAnim.CrossFadeInFixedTime("Block Idle", 0.1f);
        }

        blockingCoroutine = null;
    }

    void ExecutePunch()
    {
        if (isPunching || isHitStunned || isBlocking || isDead || isGameCompleted || isStandingUp) return;
        if (Time.time < nextPunchAvailableTime) return;

        isPunching = true;
        hasHitCurrentPunch = false;
        currentMoveAnim = MoveAnim.None; // Hareket animasyonunu kes

        // Yumruk atarken yakında rakip varsa yüzünü doğrudan rakibe hizala
        FaceOpponentOnPunch();

        int fistIndex = isPunchRight ? 1 : 0;
        // isPunchRight=true → Sol yumruk (Left Punch), isPunchRight=false → Sağ yumruk (Right Punch)
        string animName = isPunchRight ? "Left Punch" : "Right Punch";
        string triggerName = isPunchRight ? "PunchLeft" : "PunchRight";

        if (playerAnim != null)
        {
            // Hareket bool'larını sıfırla ki yumruk hemen devreye girsin
            playerAnim.SetBool("Forward_Move", false);
            playerAnim.SetBool("Backward_Move", false);
            playerAnim.SetBool("Left_Move", false);
            playerAnim.SetBool("Right_Move", false);

            playerAnim.ResetTrigger("PunchLeft");
            playerAnim.ResetTrigger("PunchRight");
            playerAnim.CrossFadeInFixedTime(animName, 0.05f);
        }

        // Sıradaki yumruğu değiştir (Sağ -> Sol -> Sağ)
        isPunchRight = !isPunchRight;

        // Boks adımı ve vuruş kontrolünü başlat
        if (punchRoutine != null) StopCoroutine(punchRoutine);
        punchRoutine = StartCoroutine(PunchRoutine(fistIndex));
    }

    /// <summary>
    /// E tuşuna basıldığında çalışan 20 hasarlık güçlü aparkat vuruşu
    /// </summary>
    void ExecuteUppercut()
    {
        if (isPunching || isHitStunned || isBlocking || isDead || isGameCompleted || isStandingUp) return;
        if (Time.time < nextPunchAvailableTime) return;

        isPunching = true;
        hasHitCurrentPunch = false;
        currentMoveAnim = MoveAnim.None; // Hareket animasyonunu kes

        FaceOpponentOnPunch();

        if (playerAnim != null)
        {
            playerAnim.SetBool("Forward_Move", false);
            playerAnim.SetBool("Backward_Move", false);
            playerAnim.SetBool("Left_Move", false);
            playerAnim.SetBool("Right_Move", false);
            playerAnim.ResetTrigger("Uppercut");
            playerAnim.CrossFadeInFixedTime("Uppercut", 0.10f);
        }

        if (uppercutRoutine != null) StopCoroutine(uppercutRoutine);
        uppercutRoutine = StartCoroutine(UppercutRoutine());
    }

    void ExecuteUlti()
    {
        if (usingUlti || isPunching || isBlocking || isDead || isGameCompleted || isStandingUp || isHitStunned) return;
        usingUlti = true;
        isPunching = true;
        isCastingUlti = true;
        hasHitCurrentPunch = false;

        Debug.Log("<color=magenta>[ULTİ DEVREYE GİRDİ!]</color> Oyuncu Ulti animasyonunu başlattı!");

        // Ulti ses dizisini (GÖKTE NE VAR.mp3 ardından YUMRUK.mp3) sadece 1 kez çal
        PlayUltiSoundSequence();

        FaceOpponentOnPunch();

        if (playerAnim != null)
        {
            playerAnim.SetBool("Forward_Move", false);
            playerAnim.SetBool("Backward_Move", false);
            playerAnim.SetBool("Left_Move", false);
            playerAnim.SetBool("Right_Move", false);
            playerAnim.ResetTrigger("Ulti");
            playerAnim.CrossFadeInFixedTime("Ulti", 0.2f);
        }

        StartCoroutine(UltiRoutine());
    }

    /// <summary>
    /// AudioSource bileşenini hazırlar. Yoksa otomatik ekler.
    /// </summary>
    private void EnsureAudioSource()
    {
        if (playerAudioSource == null)
        {
            playerAudioSource = GetComponent<AudioSource>();
            if (playerAudioSource == null)
            {
                playerAudioSource = gameObject.AddComponent<AudioSource>();
                playerAudioSource.playOnAwake = false;
                playerAudioSource.loop = false;
                playerAudioSource.spatialBlend = 0f; // 2D net ses
            }
        }
    }

    /// <summary>
    /// Ulti devreye girdiğinde önce 'GÖKTE NE VAR.mp3' sesini, o bittikten hemen sonra
    /// 'YUMRUK.mp3' sesini loop olmadan 1'er kez çalar.
    /// </summary>
    private void PlayUltiSoundSequence()
    {
        if (ultiSoundCoroutine != null)
        {
            StopCoroutine(ultiSoundCoroutine);
        }
        ultiSoundCoroutine = StartCoroutine(UltiSoundSequenceRoutine());
    }

    private IEnumerator UltiSoundSequenceRoutine()
    {
#if UNITY_EDITOR
        if (ultiSoundClip == null)
        {
            ultiSoundClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/GÖKTE NE VAR.mp3");
        }
        if (ultiPunchSoundClip == null)
        {
            ultiPunchSoundClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/YUMRUK.mp3");
        }
#endif
        EnsureAudioSource();

        // 1. "GÖKTE NE VAR.mp3" sesini çal
        if (ultiSoundClip != null)
        {
            playerAudioSource.PlayOneShot(ultiSoundClip, ultiSoundVolume);
            Debug.Log("<color=yellow>[ULTİ SESİ]</color> 'GÖKTE NE VAR.mp3' çalındı. Süre: " + ultiSoundClip.length + "s");

            // İlk sesin tamamlanmasını bekle
            yield return new WaitForSeconds(ultiSoundClip.length);
        }

        // İsteğe bağlı ek gecikme
        if (punchSoundDelay > 0f)
        {
            yield return new WaitForSeconds(punchSoundDelay);
        }

        // 2. Ardından "YUMRUK.mp3" sesini çal
        if (ultiPunchSoundClip != null)
        {
            playerAudioSource.PlayOneShot(ultiPunchSoundClip, ultiPunchSoundVolume);
            Debug.Log("<color=red>[ULTİ SESİ]</color> 'YUMRUK.mp3' çalındı!");
        }
        else
        {
            Debug.LogWarning("[PlayerController] ultiPunchSoundClip bulunamadı! 'Assets/Sounds/YUMRUK.mp3' dosyasını kontrol ediniz.");
        }
    }

    IEnumerator UltiRoutine()
    {
        // Çılgın renkli Ulti ışık şovunu başlat!
        StartUltiLightShow();

        // Ulti sırasında Fighter Animation altındaki Hands öğesinden 10 el doğur ve yukarı hareket ettir
        if (ultiHandsCoroutine != null)
        {
            StopCoroutine(ultiHandsCoroutine);
        }
        ultiHandsCoroutine = StartCoroutine(SpawnUltiHandsRoutine());


        // 2. Vuruşun temas anına kadar bekleme süresi (~0.7 saniye)
        yield return new WaitForSeconds(0.7f);

        // 3. Vuruş anı penceresi (~1.0 saniye boyunca temas ara)
        float hitWindow = 1.0f;
        float hitTimer = 0f;
        bool hasDealtUltiDamage = false;

        while (hitTimer < hitWindow)
        {
            if (!hasDealtUltiDamage)
            {
                hasDealtUltiDamage = CheckUltiContact();
            }
            hitTimer += Time.deltaTime;
            yield return null;
        }

        // 4. Kalan animasyon süresini bekle (~1.2 saniye)
        yield return new WaitForSeconds(1.2f);

        if (playerAnim != null && !isDead && !isGameCompleted)
        {
            playerAnim.CrossFadeInFixedTime("Idle", 0.2f);
        }

        isPunching = false;
        isCastingUlti = false; // Ulti animasyonu tamamen bitti!

        // Işık şovunu durdur ve arena ışıklarını normale döndür
        StopUltiLightShow();

        // 5. Cooldown süresini başlat
        StartUltiCooldown();
    }

    /// <summary>
    /// Ulti animasyonu oynarken Fighter Animation altındaki Hands modelinden
    /// belirtilen koordinatlarda aralıklı olarak 10 adet el doğurur.
    /// </summary>
    IEnumerator SpawnUltiHandsRoutine()
    {
#if UNITY_EDITOR
        if (handsPrefab == null)
        {
            handsPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Fighter Animation/Hands.fbx");
        }
#endif
        if (handsPrefab == null)
        {
            Debug.LogWarning("[PlayerController] handsPrefab atanmamış ve 'Assets/Fighter Animation/Hands.fbx' bulunamadı!");
            yield break;
        }

        for (int i = 0; i < ultiHandsCount; i++)
        {
            // Kullanıcının belirttiği koordinatlar: X (-2.5f ile -0.33f), Z (1f ile 4f), Y sabit (0f zemin)
            float randX = Random.Range(ultiHandsMinX, ultiHandsMaxX);
            float randZ = Random.Range(ultiHandsMinZ, ultiHandsMaxZ);
            Vector3 spawnPos = new Vector3(randX, ultiHandsSpawnY, randZ);

            Quaternion spawnRot = Quaternion.Euler(ultiHandsRotation);
            GameObject handObj = Instantiate(handsPrefab, spawnPos, spawnRot);
            handObj.SetActive(true);

            // UltiHandEffect bileşeni ile hedef Y'ye doğru hareket eder ve ulaştığında yok olur
            UltiHandEffect effect = handObj.GetComponent<UltiHandEffect>();
            if (effect == null)
            {
                effect = handObj.AddComponent<UltiHandEffect>();
            }
            effect.Initialize(spawnPos.y, ultiHandsTargetY, ultiHandsSpeed);

            Debug.Log($"<color=magenta>[ULTİ HANDS]</color> El #{i + 1}/{ultiHandsCount} spawnlandı! Konum: {spawnPos}, Hedef Y: {ultiHandsTargetY}, Hız: {ultiHandsSpeed}");

            if (ultiHandsSpawnInterval > 0f)
            {
                yield return new WaitForSeconds(ultiHandsSpawnInterval);
            }
        }
    }

    bool CheckUltiContact()
    {
        bool hitAny = false;
        EnemyController[] allEnemies = FindObjectsOfType<EnemyController>();

        foreach (var enemy in allEnemies)
        {
            if (enemy == null || enemy.IsDead) continue;

            float dist = Vector3.Distance(transform.position, enemy.transform.position);
            if (dist <= ultiRange)
            {
                Vector3 dir = (enemy.transform.position - transform.position).normalized;
                dir.y = 0f;
                if (Vector3.Dot(transform.forward, dir) >= 0.1f)
                {
                    enemy.TakeDamage(ultiDamage, true, true); // isUppercut: true, isUlti: true
                    Debug.Log($"<color=magenta>[ULTİ İSABET ETTİ!]</color> {enemy.name} düşmanına {ultiDamage} hasar verildi!");
                    enemyController = enemy;
                    hitAny = true;
                }
            }
        }
        return hitAny;
    }

    /// <summary>
    /// Oyun modu seçildiğinde veya Ulti kullanıldıktan sonra çağrılır.
    /// Ulti yeteneğinin yüklenmesini (Cooldown süresini) başlatır.
    /// </summary>
    public void StartUltiCooldown()
    {
        usingUlti = true; // Ulti henüz hazır değil, doluyor

        if (ultiCooldownCoroutine != null)
        {
            StopCoroutine(ultiCooldownCoroutine);
        }
        ultiCooldownCoroutine = StartCoroutine(WaitForUlti());
        Debug.Log($"<color=cyan>[ULTİ YÜKLENİYOR]</color> Ulti {ultiCooldown} saniye sonra hazır olacak.");
    }

    IEnumerator WaitForUlti()
    {
        usingUlti = true;
        yield return new WaitForSeconds(ultiCooldown);
        usingUlti = false;
        ultiCooldownCoroutine = null;
        Debug.Log("<color=green>[ULTİ TEKRAR HAZIR!]</color>");
    }

    /// <summary>
    /// Ulti sırasında sahadaki ışıkları çılgın bir neon parti / fırtına şovuna geçirir.
    /// </summary>
    private void StartUltiLightShow()
    {
        if (!enableUltiLightShow) return;
        if (ultiLightCoroutine != null)
        {
            StopCoroutine(ultiLightCoroutine);
        }
        ultiLightCoroutine = StartCoroutine(UltiLightShowRoutine());
    }

    /// <summary>
    /// Ulti bittiğinde ışık şovunu durdurur ve sahneyi normal ışıklarına döndürür.
    /// </summary>
    private void StopUltiLightShow()
    {
        if (ultiLightCoroutine != null)
        {
            StopCoroutine(ultiLightCoroutine);
            ultiLightCoroutine = null;
        }

        if (ultiHeroLightObj != null)
        {
            Destroy(ultiHeroLightObj);
            ultiHeroLightObj = null;
        }

        RestoreArenaLighting();
    }

    private IEnumerator UltiLightShowRoutine()
    {
        // 1. Sahnedeki Point Light'ları topla
        List<Light> pointLights = new List<Light>();
        if (enemySpawner == null) enemySpawner = FindObjectOfType<EnemySpawner>();
        if (enemySpawner != null)
        {
            pointLights.AddRange(enemySpawner.GetArenaPointLights());
        }

        if (pointLights.Count == 0)
        {
            foreach (var l in FindObjectsOfType<Light>())
            {
                if (l != null && l.type == LightType.Point)
                {
                    pointLights.Add(l);
                }
            }
        }

        // Directional Light'ı bul (Gökyüzü / Güneş ışığı)
        Light dirLight = RenderSettings.sun;
        if (dirLight == null)
        {
            foreach (var l in FindObjectsOfType<Light>())
            {
                if (l != null && l.type == LightType.Directional)
                {
                    dirLight = l;
                    break;
                }
            }
        }

        // Orijinal renk ve parlaklıkları kaydet
        Dictionary<Light, Color> origColors = new Dictionary<Light, Color>();
        Dictionary<Light, float> origIntensities = new Dictionary<Light, float>();

        foreach (var pl in pointLights)
        {
            if (pl != null && !origColors.ContainsKey(pl))
            {
                origColors[pl] = pl.color;
                origIntensities[pl] = pl.intensity;
            }
        }

        Color origDirColor = dirLight != null ? dirLight.color : Color.white;
        float origDirIntensity = dirLight != null ? dirLight.intensity : 1f;

        // 2. Oyuncunun üzerinde dengeli ve gözü yormayan Hero Aura Işığı oluştur (Kör etmeyen yumuşak ışık)
        Light heroLight = null;
        if (spawnUltiHeroLight)
        {
            if (ultiHeroLightObj != null) Destroy(ultiHeroLightObj);
            ultiHeroLightObj = new GameObject("Ulti_Hero_Aura_Light");
            ultiHeroLightObj.transform.position = transform.position + Vector3.up * 1.2f;
            ultiHeroLightObj.transform.SetParent(transform);

            heroLight = ultiHeroLightObj.AddComponent<Light>();
            heroLight.type = LightType.Point;
            heroLight.range = 6f;
            heroLight.intensity = 2.5f;
            heroLight.color = ultiColors[0];
        }

        Debug.Log("<color=cyan>[ULTİ IŞIK EFEKTİ]</color> Arenada sinematik ve dengeli ışık atmosferi başladı!");

        float timer = 0f;
        while (isCastingUlti)
        {
            timer += Time.deltaTime * ultiLightSpeed;

            // Directional Light: Sahneyi hafifçe sinematik loşlaştır, renkler belirginleşsin ama saha ve karakterler net görünsün
            if (dirLight != null)
            {
                dirLight.color = Color.Lerp(origDirColor, new Color(0.85f, 0.88f, 1f), 0.2f);
                dirLight.intensity = origDirIntensity * 0.75f;
            }

            // Hero Işığı: Oyuncunun etrafında yumuşak geçişli asil şampiyon aurası (kör etmeyen zarif parıltı)
            if (heroLight != null)
            {
                int heroC1 = Mathf.FloorToInt(timer * 0.8f) % ultiColors.Length;
                int heroC2 = (heroC1 + 1) % ultiColors.Length;
                float heroT = (timer * 0.8f) - Mathf.Floor(timer * 0.8f);
                heroLight.color = Color.Lerp(ultiColors[heroC1], ultiColors[heroC2], heroT);
                heroLight.intensity = 2.5f + 0.8f * Mathf.Sin(timer * 2.2f);
            }

            // Arena Point Lights: Yumuşak renk geçişi ve gözü rahatlatan hafif dalgalanma (flaş/strobe yok)
            for (int i = 0; i < pointLights.Count; i++)
            {
                Light pl = pointLights[i];
                if (pl == null) continue;

                float phase = timer + (i * 0.35f);
                int c1 = Mathf.FloorToInt(phase) % ultiColors.Length;
                int c2 = (c1 + 1) % ultiColors.Length;
                float t = phase - Mathf.Floor(phase);

                pl.color = Color.Lerp(ultiColors[c1], ultiColors[c2], t);

                float baseInt = origIntensities.ContainsKey(pl) ? origIntensities[pl] : 10f;
                float gentlePulse = 1.0f + 0.12f * Mathf.Sin(timer * 2.2f + i * 0.75f);
                pl.intensity = baseInt * ultiLightIntensityMultiplier * gentlePulse;
            }

            yield return null;
        }

        // Temizle ve normale dön
        if (ultiHeroLightObj != null)
        {
            Destroy(ultiHeroLightObj);
            ultiHeroLightObj = null;
        }

        if (dirLight != null)
        {
            dirLight.color = origDirColor;
            dirLight.intensity = origDirIntensity;
        }

        RestoreArenaLighting(pointLights, origColors, origIntensities);
    }

    private void RestoreArenaLighting(List<Light> pointLights = null, Dictionary<Light, Color> origColors = null, Dictionary<Light, float> origIntensities = null)
    {
        if (enemySpawner == null) enemySpawner = FindObjectOfType<EnemySpawner>();
        if (enemySpawner != null)
        {
            enemySpawner.UpdateArenaLighting(enemySpawner.CurrentWave, immediate: false);
        }
        else if (pointLights != null && origColors != null)
        {
            foreach (var pl in pointLights)
            {
                if (pl != null && origColors.ContainsKey(pl))
                {
                    pl.color = origColors[pl];
                    pl.intensity = (origIntensities != null && origIntensities.ContainsKey(pl)) ? origIntensities[pl] : 10f;
                }
            }
        }
    }

    IEnumerator UppercutRoutine()
    {
        // 1. Aparkat kolunun yükselme ve savrulma hazırlığı (Windup)
        if (uppercutWindupTime > 0f)
        {
            yield return new WaitForSeconds(uppercutWindupTime);
        }

        // 3. Aparkatın zirveye ulaştığı temas penceresi
        float activeTimer = 0f;
        float uppercutActiveDuration = 0.25f;
        while (activeTimer < uppercutActiveDuration)
        {
            if (hasHitCurrentPunch) break;

            CheckPhysicalUppercutContact();

            activeTimer += Time.deltaTime;
            yield return null;
        }

        // 4. Kolun geri çekilmesi ve garda dönüş
        yield return new WaitForSeconds(0.35f);

        currentMoveAnim = MoveAnim.Idle;

        nextPunchAvailableTime = Time.time + punchCooldown;
        isPunching = false;
        uppercutRoutine = null;
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
                        hitEnemy.TakeDamage(uppercutDamage, true); // 20 Hasar ve Aparkat (Head Hit)!
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
                    closeEnemy.TakeDamage(uppercutDamage, true); // 20 Hasar ve Aparkat (Head Hit)!
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
            if (dirToEnemy.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(dirToEnemy.normalized);
            }
        }
        else
        {
            Vector3 camFwd = GetCameraForward();
            if (camFwd.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(camFwd);
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
            // Normal dalgalar arasında Show Pose'a geçmiyoruz, oyuncu Idle'da kalır ve serbestçe hareket edebilir
            playerAnim.SetBool("isDeadEnemy", false);

            if (dead && !isCastingUlti)
            {
                isPunching = false;
                isPunchActive = false;
                currentMoveAnim = MoveAnim.None;
                playerAnim.CrossFadeInFixedTime("Idle", 0.15f);
                currentMoveAnim = MoveAnim.Idle;
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
            currentMoveAnim = MoveAnim.None;
            playerAnim.SetBool("isDeadEnemy", false);
            playerAnim.SetBool("leftMove", false);
            playerAnim.SetBool("rightMove", false);
            playerAnim.CrossFadeInFixedTime("Idle", 0.2f);
            currentMoveAnim = MoveAnim.Idle;
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

        if (uiManager == null)
        {
            uiManager = FindObjectOfType<UIManager>();
        }
        if (uiManager != null)
        {
            uiManager.ShowVictoryPanel();
        }
    }

    /// <summary>
    /// Oyunu yeniden başlatırken veya ana menüye dönerken oyuncunun canını, pozisyonunu ve durumlarını sıfırlar
    /// </summary>
    public void ResetPlayerState()
    {
        StopAllCoroutines();
        StopUltiLightShow();

        isDead = false;
        isStandingUp = false;
        isEnemyDead = false;
        isGameCompleted = false;
        isPunching = false;
        isPunchActive = false;
        isHitStunned = false;
        punchRoutine = null;
        uppercutRoutine = null;
        hitStunRoutine = null;
        isBlocking = false;
        playerDoBlock = 0;
        hasHitCurrentPunch = false;
        if (blockingCoroutine != null) { StopCoroutine(blockingCoroutine); blockingCoroutine = null; }

        if (joystick != null)
        {
            joystick.ResetJoystick();
        }

        maxPlayerHealth = 100f;
        playerHealth = maxPlayerHealth;
        punchDamage = 10f;
        uppercutDamage = 20f;
        ultiDamage = 30f;
        usingUlti = true;
        ultiCooldownCoroutine = null;
        if (playerHealthBar != null)
        {
            playerHealthBar.fillAmount = 1f;
        }
        transform.position = new Vector3(startPosition.x, standingYPosition, startPosition.z);
        transform.rotation = startRotation;

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = true;
        }

        if (playerAnim != null)
        {
            currentMoveAnim = MoveAnim.None;
            playerAnim.SetBool("isDead", false);
            playerAnim.SetBool("isDeadEnemy", false);
            playerAnim.SetBool("leftMove", false);
            playerAnim.SetBool("rightMove", false);
            playerAnim.CrossFadeInFixedTime("Idle", 0.1f);
            currentMoveAnim = MoveAnim.Idle;
        }
    }

    IEnumerator PunchRoutine(int fistIndex)
    {
        // 1. Yumruk uzanma / savrulma gecikmesi (Windup - animasyonun hedefe doğru ilerlemesi)
        if (punchWindupTime > 0f)
        {
            yield return new WaitForSeconds(punchWindupTime);
        }

        // 2. Yumruk temas arama penceresi
        isPunchActive = true;
        float activeTimer = 0f;

        while (activeTimer < punchActiveDuration)
        {
            if (hasHitCurrentPunch) break;

            CheckPhysicalFistContact(fistIndex);

            activeTimer += Time.deltaTime;
            yield return null;
        }

        isPunchActive = false;

        // 3. Kolun geri çekilmesi ve boksörün garda dönüş süresi
        float remainingDuration = punchDuration - (punchWindupTime + activeTimer);
        if (remainingDuration > 0f)
        {
            yield return new WaitForSeconds(remainingDuration);
        }

        // Yumruk tamamen bitti
        currentMoveAnim = MoveAnim.Idle;
        nextPunchAvailableTime = Time.time + punchCooldown;
        isPunching = false;
        punchRoutine = null;
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

        // 2. Menzil Kontrolü: Oyuncunun önündeki canlı düşman maxPunchRange içindeyse vur
        if (!hasHitCurrentPunch)
        {
            EnemyController targetEnemy = (enemyController != null && !enemyController.IsDead)
                ? enemyController
                : GetClosestLivingEnemy();

            if (targetEnemy != null && !targetEnemy.IsDead)
            {
                float distToEnemy = Vector3.Distance(transform.position, targetEnemy.transform.position);
                if (distToEnemy <= maxPunchRange)
                {
                    Vector3 dirToEnemy = (targetEnemy.transform.position - transform.position).normalized;
                    dirToEnemy.y = 0f;
                    if (Vector3.Dot(transform.forward, dirToEnemy) >= 0.20f)
                    {
                        hasHitCurrentPunch = true;
                        isPunchActive = false;
                        targetEnemy.TakeDamage(punchDamage);
                        enemyController = targetEnemy;
                        Debug.Log($"<color=green>[YUMRUK İSABET ETTİ!]</color> {targetEnemy.name} düşmanına {punchDamage} hasar verildi (Mesafe: {distToEnemy:F1}m)!");
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

    public void TakeDamage(float damageAmount, bool isUppercut = false, bool isFromRight = true)
    {
        if (isDead) return;

        // Blok Kontrolü: Oyuncu Space tuşuyla blok yapıyorsa hasar almaz
        if (isBlocking)
        {
            Debug.Log("<color=green>[BLOK BAŞARILI!]</color> Oyuncu saldırıyı blokladı, hasar almadı!");
            return;
        }

        // Ulti Dokunulmazlığı: Kullanıcı ulti attığında düşmanlardan darbe ve hasar alamaz
        if (isCastingUlti || (playerAnim != null && playerAnim.GetCurrentAnimatorStateInfo(0).IsName("Ulti")))
        {
            Debug.Log("<color=magenta>[ULTİ DOKUNULMAZLIĞI]</color> Oyuncu ulti atarken darbe alamaz!");
            return;
        }

        // SALDIRIYI KES (INTERRUPT): Düşman önce vurduysa oyuncunun vuruşu İPTAL EDİLİR!
        InterruptPlayerAttack();

        playerHealth -= damageAmount;

        // Can Barı
        if (playerHealthBar != null && maxPlayerHealth > 0f)
        {
            playerHealthBar.fillAmount = playerHealth / maxPlayerHealth;
        }

        Debug.Log($"<color=cyan>[OYUNCU DARBE ALDI]</color> Kalan Can: {playerHealth} (Aparkat: {isUppercut})");

        // Aparkat darbesinde sarsıntı tepkisi / geri itme
        if (isUppercut || damageAmount >= 20f)
        {
            Vector3 pushedPos = transform.position + (-transform.forward) * 0.25f;
            pushedPos = RingBoundary.ClampToArena(pushedPos, playerBodyRadius);
            transform.position = pushedPos;
        }

        if (playerHealth <= 0)
        {
            Die();
        }
        else
        {
            // HASAR ALMA ANİMASYONU VE DARBE SERSEMLEMESİ (HIT STUN):
            // Düşman vurduğunda oyuncu hasar alma animasyonunu oynar ve sersemleme bitene kadar vuruş yapamaz!
            if (hitStunRoutine != null)
            {
                StopCoroutine(hitStunRoutine);
            }
            hitStunRoutine = StartCoroutine(PlayerHitStunRoutine(isUppercut || damageAmount >= 20f, isFromRight));
        }
    }

    /// <summary>
    /// Oyuncunun devam eden herhangi bir saldırısını (yumruk/aparkat) derhal iptal eder
    /// </summary>
    public void InterruptPlayerAttack()
    {
        isPunching = false;
        isPunchActive = false;
        hasHitCurrentPunch = false;

        if (punchRoutine != null)
        {
            StopCoroutine(punchRoutine);
            punchRoutine = null;
        }

        if (uppercutRoutine != null)
        {
            StopCoroutine(uppercutRoutine);
            uppercutRoutine = null;
        }

        if (playerAnim != null)
        {
            playerAnim.ResetTrigger("PunchLeft");
            playerAnim.ResetTrigger("PunchRight");
        }
    }

    private IEnumerator PlayerHitStunRoutine(bool isHeavyHit, bool isFromRight = true)
    {
        isHitStunned = true;
        currentMoveAnim = MoveAnim.None; // Hareket animasyonunu kes

        if (playerAnim != null)
        {
            playerAnim.ResetTrigger("PunchLeft");
            playerAnim.ResetTrigger("PunchRight");

            if (isHeavyHit)
            {
                // Ağır vuruş (aparkat veya yüksek hasar): Left Damage animasyonu (mirrored)
                playerAnim.ResetTrigger("GetHit");
                playerAnim.SetTrigger("GetHeadHit");
                playerAnim.CrossFadeInFixedTime("Left Damage", 0.06f);
            }
            else
            {
                // Normal vuruş: saldırının geldiği tarafa göre animasyon seç
                // Düşman right punch → oyuncunun sağ tarafına çarpar → Right Damage
                // Düşman left punch  → oyuncunun sol tarafına çarpar → Left Damage
                playerAnim.ResetTrigger("GetHeadHit");
                playerAnim.SetTrigger("GetHit");
                string damageAnim = isFromRight ? "Right Damage" : "Left Damage";
                playerAnim.CrossFadeInFixedTime(damageAnim, 0.06f);
            }
        }

        float stunDuration = isHeavyHit ? 0.55f : 0.40f;
        yield return new WaitForSeconds(stunDuration);

        if (!isDead && !isGameCompleted && playerAnim != null)
        {
            playerAnim.CrossFadeInFixedTime("Idle", 0.15f);
            currentMoveAnim = MoveAnim.Idle;
        }

        isHitStunned = false;
        hitStunRoutine = null;
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;
        isStandingUp = false;
        isHitStunned = false;

        InterruptPlayerAttack();
        if (hitStunRoutine != null)
        {
            StopCoroutine(hitStunRoutine);
            hitStunRoutine = null;
        }

        isBlocking = false;
        playerDoBlock = 0;
        if (blockingCoroutine != null) { StopCoroutine(blockingCoroutine); blockingCoroutine = null; }
        StopCoroutine(nameof(UltiRoutine));
        if (ultiHandsCoroutine != null)
        {
            StopCoroutine(ultiHandsCoroutine);
            ultiHandsCoroutine = null;
        }
        if (ultiSoundCoroutine != null)
        {
            StopCoroutine(ultiSoundCoroutine);
            ultiSoundCoroutine = null;
        }
        StopUltiLightShow();

        Debug.Log("<color=red>[OYUNCU NAKAVT OLDU!]</color>");

        if (playerAnim != null)
        {
            currentMoveAnim = MoveAnim.None;
            playerAnim.SetBool("isDead", true);
            playerAnim.SetBool("leftMove", false);
            playerAnim.SetBool("rightMove", false);
            playerAnim.SetBool("isDeadEnemy", false);
            playerAnim.ResetTrigger("GetHit");
            playerAnim.ResetTrigger("GetHeadHit");
            playerAnim.ResetTrigger("PunchLeft");
            playerAnim.ResetTrigger("PunchRight");
            // Aparkat nakavt animasyonu (Uppercut Nakavt) oynatılır
            playerAnim.CrossFadeInFixedTime("Uppercut Nakavt", 0.08f);
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
                if ((stateInfo.IsName("Uppercut Nakavt") || stateInfo.IsName("Defeat")) && stateInfo.normalizedTime >= 0.95f)
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
            EnemySpawner spawner = FindObjectOfType<EnemySpawner>();
            int deadWave = spawner != null ? spawner.CurrentWave : 1;
            uiManager.ShowGameOverPanel(deadWave);
        }
        else
        {
            Debug.LogWarning("PlayerController: UIManager bulunamadı, GameOver paneli açılamadı!");
        }
    }

    /// <summary>
    /// Environment değiştiğinde oyuncuyu yeni sahadaki spawn noktasına taşır ve zemin yüksekliğini ayarlar
    /// </summary>
    public void TeleportToArena(Vector3 targetPos, Quaternion targetRot, float groundY = 0f)
    {
        standingYPosition = groundY;
        fallenYPosition = groundY + -0.3f;
        standUpYPosition = groundY;

        transform.position = targetPos;
        transform.rotation = targetRot;

        startPosition = targetPos;
        startRotation = targetRot;

        isGameCompleted = false;
        isEnemyDead = false;
        isDead = false;
        isStandingUp = false;
        isPunching = false;
        isPunchActive = false;
        isHitStunned = false;
        punchRoutine = null;
        uppercutRoutine = null;
        hitStunRoutine = null;
        isBlocking = false;
        playerDoBlock = 0;
        hasHitCurrentPunch = false;

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = true;
        }

        if (joystick != null)
        {
            joystick.ResetJoystick();
        }

        if (playerAnim != null)
        {
            currentMoveAnim = MoveAnim.None;
            playerAnim.SetBool("isDead", false);
            playerAnim.SetBool("isDeadEnemy", false);
            playerAnim.SetBool("leftMove", false);
            playerAnim.SetBool("rightMove", false);
            playerAnim.CrossFadeInFixedTime("Idle", 0.15f);
            currentMoveAnim = MoveAnim.Idle;
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

        // Can Barını doldur
        playerHealthBar.fillAmount = playerHealth / maxPlayerHealth;

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
        isHitStunned = false;
        punchRoutine = null;
        uppercutRoutine = null;
        hitStunRoutine = null;

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = true;
        }

        if (playerAnim != null)
        {
            playerAnim.SetBool("isDead", false);
            currentMoveAnim = MoveAnim.None;
            playerAnim.CrossFadeInFixedTime("Idle", 0.2f);
            currentMoveAnim = MoveAnim.Idle;
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
    /// Oyuncunun maksimum canını ayarlar. restoreToFull true ise can tamamen dolar, false ise mevcut can korunur
    /// </summary>
    public void SetMaxHealth(float maxHp, bool restoreToFull = false)
    {
        maxPlayerHealth = maxHp;
        if (restoreToFull)
        {
            playerHealth = maxPlayerHealth;
        }
        else if (playerHealth > maxPlayerHealth)
        {
            playerHealth = maxPlayerHealth;
        }

        if (playerHealthBar != null && maxPlayerHealth > 0)
        {
            playerHealthBar.fillAmount = playerHealth / maxPlayerHealth;
        }
    }

    public float GetMaxHealth() => maxPlayerHealth;

    /// <summary>
    /// Oyuncunun ulti hasarını ayarlar
    /// </summary>
    public void SetUltiDamage(float damage)
    {
        ultiDamage = damage;
    }

    public float GetUltiDamage() => ultiDamage;

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

    public void PunchButton()
    {
        if (!isPunching && !isHitStunned)
        {
            ExecutePunch();
        }
    }

    public void UltiButton()
    {
        if (!usingUlti && !isPunching && !isBlocking && !isDead && !isHitStunned)
        {
            ExecuteUlti();
        }
    }

    public void UppercutButton()
    {
        if (!isPunching && !isHitStunned)
        {
            ExecuteUppercut();
        }
    }

    public void Blocking(int doBlock)
    {
        if (isHitStunned && doBlock != 0) return;
        this.playerDoBlock = doBlock;
    }
}