using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GTA tarzı akıcı ve dinamik 3. Şahıs (Third Person) kamera takip sistemi.
/// Oyuncunun hareketleriyle bütünleşir, fare/dokunma ile serbestçe etrafa bakmayı sağlar.
/// </summary>
public class FollowPlayer : MonoBehaviour
{
    [Header("Hedef Oyuncu")]
    [Tooltip("Takip edilecek oyuncu transformu. Boş bırakılırsa Player etiketi veya PlayerController üzerinden otomatik bulunur.")]
    [SerializeField] private Transform target;

    [Header("GTA Tarzı Kamera Ayarları")]
    [Tooltip("Kameranın karakterden uzaklığı")]
    [SerializeField] private float distance = 2.8f;
    [Tooltip("Kameranın hedefe bakarkenki yükseklik ofseti (Örn: 1.5 omuz/baş hizası)")]
    [SerializeField] private float heightOffset = 1.5f;
    [Tooltip("Kameranın yatay ofseti (Over-the-shoulder görünümü için)")]
    [SerializeField] private float sideOffset = 0f;
    [Tooltip("Kamera pozisyonunun hedefe yumuşakça gitme süresi")]
    [SerializeField] private float smoothTime = 0.12f;

    [Header("Fare (Serbest Bakış) Kontrolü")]
    [Tooltip("Fare veya ekran kaydırma ile etrafa bakma aktif mi?")]
    [SerializeField] private bool enableFreeLook = true;
    [SerializeField] private float mouseSensitivity = 3f;
    [Tooltip("Kameranın yukarı/aşağı bakış açısı sınırları")]
    [SerializeField] private float pitchMin = -15f;
    [SerializeField] private float pitchMax = 60f;

    [Header("Otomatik Hizalama (Auto-Align)")]
    [Tooltip("Oyuncu hareket ettiğinde kamera otomatik olarak karakterin arkasına geçsin mi?")]
    [SerializeField] private bool autoAlignToPlayer = true;
    [Tooltip("Kameranın oyuncunun arkasına geçme hızı")]
    [SerializeField] private float autoAlignSpeed = 2f;

    [Header("Sarsıntı Önleme")]
    [Tooltip("Karakterin koşarken veya yumruk atarkenki yukarı/aşağı sekmelerini yoksayar")]
    [SerializeField] private bool stabilizeVerticalMovement = true;

    [Header("Nakavt (Ölüm) Kamera Ayarları")]
    [SerializeField] private Vector3 knockoutOffset = new Vector3(0f, 1.6f, 0f);
    [SerializeField] private float knockoutPitchAngle = 90f;
    [SerializeField] private float knockoutYAngle = 180f;
    [SerializeField] private float knockoutSmoothTime = 0.9f;
    [SerializeField] private bool autoCenterOnFallenBody = true;

    [Header("Optimizasyon")]
    [SerializeField] private bool detachFromParentOnStart = true;

    private PlayerController playerController;
    private Transform playerHipsBone;
    private Vector3 currentVelocity;
    
    // Kamera açıları
    private float yaw;
    private float pitch = 15f;
    
    // Stabilizasyon
    private float fixedGroundY;

    // Mouse Input kontrolü
    private float lastMouseInputTime;
    private Vector3 lastTargetPos;

    void Awake()
    {
        if (detachFromParentOnStart && transform.parent != null)
        {
            transform.SetParent(null);
        }
    }

    void Start()
    {
        FindTargetIfNeeded();
        if (target != null)
        {
            fixedGroundY = target.position.y;
            yaw = target.eulerAngles.y;
            pitch = 15f;

            lastTargetPos = target.position;

            // İlk karede kamerayı tam arkaya koy (lag'siz)
            Vector3 targetPos = target.position;
            targetPos.y = fixedGroundY;
            Vector3 focusPoint = targetPos + Vector3.up * heightOffset;
            
            transform.position = CalculateCameraPosition(focusPoint, yaw, pitch);
            transform.LookAt(focusPoint);
        }
    }

    void LateUpdate()
    {
        if (target == null)
        {
            FindTargetIfNeeded();
            if (target == null) return;
        }

        if (playerController == null)
        {
            CachePlayerComponents();
        }

        bool isKnockoutView = (playerController != null && playerController.IsDead && !playerController.IsStandingUp);

        if (isKnockoutView)
        {
            // --- OYUNCU YERDE NAKAVT HALİNDEYKEN (ÜSTTEN NAKAVT BAKIŞI) ---
            Vector3 centerPos = (autoCenterOnFallenBody && playerHipsBone != null)
                                ? playerHipsBone.position
                                : target.position;

            Vector3 desiredKnockoutPos = new Vector3(
                centerPos.x + knockoutOffset.x,
                knockoutOffset.y,
                centerPos.z + knockoutOffset.z
            );

            transform.position = Vector3.SmoothDamp(transform.position, desiredKnockoutPos, ref currentVelocity, knockoutSmoothTime);

            Quaternion desiredKnockoutRot = Quaternion.Euler(knockoutPitchAngle, 0f, knockoutYAngle);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredKnockoutRot, Time.deltaTime * (2.8f / knockoutSmoothTime));
            return;
        }

        // --- NORMAL OYUN / GTA TARZI KAMERA TAKİBİ ---
        
        Vector3 targetBasePos = target.position;

        if (stabilizeVerticalMovement)
        {
            // Y ekseni sarsıntılarını çok yavaş takip et
            fixedGroundY = Mathf.Lerp(fixedGroundY, targetBasePos.y, Time.deltaTime * 3f);
            targetBasePos.y = fixedGroundY;
        }

        Vector3 focusPoint = targetBasePos + Vector3.up * heightOffset;

        // 1. Serbest Bakış (Mouse / Touch Input)
        if (enableFreeLook)
        {
            float inputX = Input.GetAxis("Mouse X");
            float inputY = Input.GetAxis("Mouse Y");

            if (Mathf.Abs(inputX) > 0.01f || Mathf.Abs(inputY) > 0.01f)
            {
                yaw += inputX * mouseSensitivity;
                pitch -= inputY * mouseSensitivity;
                pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);
                lastMouseInputTime = Time.time;
            }
        }

        // 2. Otomatik Hizalama (Karakter hareket ediyorsa ve oyuncu kamerayı çevirmiyorsa)
        if (autoAlignToPlayer && Time.time - lastMouseInputTime > 1.5f)
        {
            Vector3 posDiff = targetBasePos - lastTargetPos;
            posDiff.y = 0; // Sadece yatay hareketi dikkate al
            bool isMoving = posDiff.sqrMagnitude > (0.1f * Time.deltaTime);

            bool isBusy = playerController != null && (playerController.IsBlocking || playerController.IsHitStunned || playerController.IsCastingUlti);
            
            if (!isBusy && isMoving)
            {
                // Karakter hareket halindeyken, kamerayı yumuşakça karakterin sırtına doğru çevir (GTA hissi)
                float targetYaw = target.eulerAngles.y;
                yaw = Mathf.LerpAngle(yaw, targetYaw, Time.deltaTime * autoAlignSpeed);
            }
        }

        lastTargetPos = targetBasePos;

        // 3. İstenen Kamera Pozisyonunu Hesapla
        Vector3 desiredPos = CalculateCameraPosition(focusPoint, yaw, pitch);

        // 4. Pozisyonu Yumuşat (SmoothDamp)
        float currentSmoothTime = (playerController != null && playerController.IsStandingUp) ? 0.6f : smoothTime;
        transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref currentVelocity, currentSmoothTime);

        // 5. Odak Noktasına Bak
        transform.LookAt(focusPoint);
    }

    private Vector3 CalculateCameraPosition(Vector3 focusPoint, float currentYaw, float currentPitch)
    {
        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0);
        Vector3 offsetPos = new Vector3(sideOffset, 0, -distance);
        return focusPoint + rotation * offsetPos;
    }


    private void FindTargetIfNeeded()
    {
        if (target != null)
        {
            CachePlayerComponents();
            return;
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            target = playerObj.transform;
            CachePlayerComponents();
            return;
        }

        PlayerController pc = FindObjectOfType<PlayerController>();
        if (pc != null)
        {
            target = pc.transform;
            CachePlayerComponents();
        }
    }

    private void CachePlayerComponents()
    {
        if (target == null) return;

        if (playerController == null)
        {
            playerController = target.GetComponent<PlayerController>() ?? target.GetComponentInParent<PlayerController>() ?? FindObjectOfType<PlayerController>();
        }

        if (playerHipsBone == null)
        {
            Animator anim = target.GetComponentInChildren<Animator>();
            if (anim != null && anim.isHuman)
            {
                playerHipsBone = anim.GetBoneTransform(HumanBodyBones.Hips);
            }

            if (playerHipsBone == null)
            {
                playerHipsBone = FindDeepChild(target, "Hips") ?? FindDeepChild(target, "mixamorig:Hips");
            }
        }
    }

    private Transform FindDeepChild(Transform parent, string boneName)
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

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        playerController = null;
        playerHipsBone = null;
        CachePlayerComponents();
    }

    public void SnapToTarget()
    {
        FindTargetIfNeeded();
        if (target != null)
        {
            fixedGroundY = target.position.y;
            currentVelocity = Vector3.zero;
            yaw = target.eulerAngles.y;
            
            Vector3 focusPoint = target.position + Vector3.up * heightOffset;
            focusPoint.y = fixedGroundY + heightOffset;

            transform.position = CalculateCameraPosition(focusPoint, yaw, pitch);
            transform.LookAt(focusPoint);
        }
    }
}
