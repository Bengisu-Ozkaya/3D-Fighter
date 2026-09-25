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

    [Header("Optimizasyon")]
    [Tooltip("Kamera Player'ın alt objesiyse (child), hiyerarşi sarsıntısını önlemek için Start'ta bağımsız hale getirir.")]
    [SerializeField] private bool detachFromParentOnStart = true;

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

        // 2. Rotasyon Takibi (Titreşimi engelleyen kararlı yönelim)
        if (useFixedAngle)
        {
            // Sabit pitch ve yaw: Ringdeki dövüşü en net ve titreşimsiz gösteren profesyonel mod
            transform.rotation = Quaternion.Euler(fixedPitchAngle, 0f, 0f);
        }
        else
        {
            // Yumuşak yaw takibi (sadece Y ekseninde akıcı dönüş)
            float targetYaw = target.eulerAngles.y;
            currentYaw = Mathf.SmoothDampAngle(currentYaw, targetYaw, ref currentYawVelocity, rotationSmoothTime);
            transform.rotation = Quaternion.Euler(fixedPitchAngle, currentYaw, 0f);
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
        if (target != null) return;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            target = playerObj.transform;
            return;
        }

        PlayerController pc = FindObjectOfType<PlayerController>();
        if (pc != null)
        {
            target = pc.transform;
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}
