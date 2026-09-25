using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dövüş oyunları için sarsıntısız, akıcı ve kararlı kamera takip sistemi.
/// Karakterin adım ve yumruk animasyonlarındaki dikey ve yatay titreşimleri sönümler.
/// </summary>
public class FollowPlayer : MonoBehaviour
{
    [Header("Hedef Oyuncu")]
    [Tooltip("Takip edilecek oyuncu transformu. Boş bırakılırsa Player etiketi veya PlayerController üzerinden otomatik bulunur.")]
    [SerializeField] private Transform target;

    [Header("Pozisyon ve Mesafe")]
    [Tooltip("Kameranın oyuncuya göre mesafesi (X: Yatay, Y: Yükseklik, Z: Arkadan uzaklık)")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 1.7f, -2.4f);

    [Tooltip("Pozisyon yumuşatma süresi (Saniye). Değer ne kadar küçükse takip o kadar sıkı, ne kadar büyükse o kadar akıcıdır.")]
    [Range(0.05f, 0.4f)]
    [SerializeField] private float smoothTime = 0.12f;

    [Header("Sarsıntı Önleme (Stabilizasyon)")]
    [Tooltip("Aktifken karakterin adım ve yumruk atarkenki dikey (Y) titremelerini sönümleyerek kamerayı sabit yükseklikte tutar.")]
    [SerializeField] private bool stabilizeVerticalMovement = true;

    [Tooltip("Kameranın bakış açısını sabit tutarak animasyonlardaki rotasyon titremelerini tamamen engeller.")]
    [SerializeField] private bool useFixedAngle = true;

    [Tooltip("useFixedAngle aktifken kameranın aşağıya bakış açısı (derece)")]
    [SerializeField] private float fixedPitchAngle = 14f;

    [Header("Dönüş Takibi (useFixedAngle kapalıysa)")]
    [Tooltip("Oyuncu döndüğünde kameranın oyuncunun arkasını takip etme süresi")]
    [Range(0.05f, 0.5f)]
    [SerializeField] private float rotationSmoothTime = 0.2f;

    [Header("Nakavt (Ölüm) Kamera Ayarları")]
    [Tooltip("Ölüm anında kameranın odaklanacağı X, Y, Z ofseti. (Y: Yükseklik 1.6f, Z: İleri/Geri kaydırma ofseti)")]
    [SerializeField] private Vector3 knockoutOffset = new Vector3(0f, 1.6f, 0f);

    [Tooltip("Nakavt kamerasının dikey bakış açısı (X rotasyonu)")]
    [SerializeField] private float knockoutPitchAngle = 90f;
    [SerializeField] private float knockoutYAngle = 180f;

    [Tooltip("Ölüm anında üstten bakışa geçiş yumuşatma süresi (Saniye)")]
    [SerializeField] private float knockoutSmoothTime = 0.9f;

    [Tooltip("Aktifken kamerayı karakterin ayak pivotuna değil, yere yatan kalça/gövde kemiğine (Hips) otomatik ortalar. Kusursuz ve dengeli bir üstten bakış sağlar.")]
    [SerializeField] private bool autoCenterOnFallenBody = true;

    [Header("Optimizasyon")]
    [Tooltip("Kamera Player'ın alt objesiyse (child), hiyerarşi sarsıntısını önlemek için Start'ta bağımsız hale getirir.")]
    [SerializeField] private bool detachFromParentOnStart = true;

    private PlayerController playerController;
    private Transform playerHipsBone;
    private Vector3 currentVelocity;
    private float currentYawVelocity;
    private float currentYaw;
    private float fixedGroundY;

    void Awake()
    {
        // Eğer kamera oyuncunun child'ı ise serbest bırak (bağımsız ve sarsıntısız takip için)
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
            currentYaw = transform.eulerAngles.y;
            // İlk karede kamerayı doğrudan hedefin arkasına yerleştir (ekran kaymasını önle)
            transform.position = CalculateDesiredPosition(target.position);

            if (useFixedAngle)
            {
                transform.rotation = Quaternion.Euler(fixedPitchAngle, 0f, 0f);
            }
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

        bool isPlayerDead = (playerController != null && playerController.IsDead);

        if (isPlayerDead)
        {
            // --- OYUNCU ÖLDÜĞÜNDE (ÜSTTEN NAKAVT BAKIŞI) ---
            // Odak noktası: Otomatik gövde merkezleme açıksa yere yatan kalça/gövde kemiği (Hips), yoksa ana obje pozisyonu
            Vector3 centerPos = (autoCenterOnFallenBody && playerHipsBone != null)
                                ? playerHipsBone.position
                                : target.position;

            // X ve Z oyuncunun merkezine göre ortalanırken, Z ofseti ve Y yüksekliği (1.6f) doğrudan uygulanır
            Vector3 desiredKnockoutPos = new Vector3(
                centerPos.x + knockoutOffset.x,
                knockoutOffset.y,
                centerPos.z + knockoutOffset.z
            );

            transform.position = Vector3.SmoothDamp(transform.position, desiredKnockoutPos, ref currentVelocity, knockoutSmoothTime);

            // 2. Rotasyon: Yavaşça X = 90 dereceye (tam dik üstten bakışa) yumuşak geçiş yap
            Quaternion desiredKnockoutRot = Quaternion.Euler(knockoutPitchAngle, 0f, knockoutYAngle);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredKnockoutRot, Time.deltaTime * (2.8f / knockoutSmoothTime));
            return;
        }

        // --- NORMAL OYUN / DÖVÜŞ KAMERA TAKİBİ ---
        Vector3 targetBasePos = target.position;

        // Dikey sarsıntı önleme: Karakterin animasyondaki yukarı/aşağı sekmesini filtrele
        if (stabilizeVerticalMovement)
        {
            // Zemin seviyesi değişimlerini çok yavaş yumuşatarak takip et
            fixedGroundY = Mathf.Lerp(fixedGroundY, targetBasePos.y, Time.deltaTime * 3f);
            targetBasePos.y = fixedGroundY;
        }

        // 1. Pozisyon Takibi (SmoothDamp ile kritik sönümleme - sıfır titreme)
        Vector3 desiredPos = CalculateDesiredPosition(targetBasePos);
        transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref currentVelocity, smoothTime);

        // 2. Rotasyon Takibi (Titreşimi engelleyen kararlı yönelim / Nakavttan dönerken yumuşak slerp)
        Quaternion targetRot;
        if (useFixedAngle)
        {
            // Sabit pitch ve yaw: Ringdeki dövüşü en net ve titreşimsiz gösteren profesyonel mod
            targetRot = Quaternion.Euler(fixedPitchAngle, 0f, 0f);
        }
        else
        {
            // Yumuşak yaw takibi (sadece Y ekseninde akıcı dönüş)
            float targetYaw = target.eulerAngles.y;
            currentYaw = Mathf.SmoothDampAngle(currentYaw, targetYaw, ref currentYawVelocity, rotationSmoothTime);
            targetRot = Quaternion.Euler(fixedPitchAngle, currentYaw, 0f);
        }

        // Nakavt modundan (90 dereceden) normale dönerken yumuşakça slerp yap
        if (Quaternion.Angle(transform.rotation, targetRot) > 0.05f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 3.5f);
        }
        else
        {
            transform.rotation = targetRot;
        }
    }

    private Vector3 CalculateDesiredPosition(Vector3 basePos)
    {
        if (useFixedAngle)
        {
            return basePos + offset;
        }
        else
        {
            Quaternion yawRot = Quaternion.Euler(0f, currentYaw, 0f);
            return basePos + yawRot * offset;
        }
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
}
