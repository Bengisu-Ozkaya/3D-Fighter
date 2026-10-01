using UnityEngine;

/// <summary>
/// Ulti yeteneği sırasında ortaya çıkan Hands (El) objelerinin hareket etmesini
/// ve belirlenen hedef Y yüksekliğine ulaştığında sahneden yok edilmesini sağlar.
/// Hem yukarıdan aşağıya (Spawn: 3.33, Target: 0) hem de aşağıdan yukarıya
/// (Spawn: 0, Target: 3.33) hareketleri otomatik algılar.
/// </summary>
public class UltiHandEffect : MonoBehaviour
{
    [Header("Hareket Ayarları")]
    [Tooltip("Objenin hareket hızı (metre/saniye)")]
    [SerializeField] private float moveSpeed = 3.5f;

    [Tooltip("Objenin ulaştığında Destroy edileceği Y yüksekliği")]
    [SerializeField] private float targetY = 0f;

    [Header("Güvenlik Ayarı")]
    [Tooltip("Herhangi bir sebeple hedefe ulaşılamazsa azami yaşam süresi (saniye)")]
    [SerializeField] private float maxLifetime = 6f;

    private float startY;
    private bool isMovingUp = false;
    private bool initialized = false;

    /// <summary>
    /// PlayerController üzerinden başlangıç Y, hedef Y ve hızı ayarlar.
    /// </summary>
    public void Initialize(float spawnY, float destroyY, float speed)
    {
        startY = spawnY;
        targetY = destroyY;
        moveSpeed = speed;
        isMovingUp = targetY > startY;
        initialized = true;
    }

    private void Start()
    {
        if (!initialized)
        {
            startY = transform.position.y;
            isMovingUp = targetY > startY;
        }

        // Güvenlik: Her ihtimale karşı azami süre sonunda yok et
        Destroy(gameObject, maxLifetime);
    }

    private void Update()
    {
        if (isMovingUp)
        {
            // Aşağıdan yukarıya hareket (+Y)
            transform.position += Vector3.down * (moveSpeed * Time.deltaTime);

            if (transform.position.y <= targetY)
            {
                Destroy(gameObject);
            }
        }
        else
        {
            // Yukarıdan aşağıya hareket (-Y)
            transform.position += Vector3.down * (moveSpeed * Time.deltaTime);

            if (transform.position.y <= targetY)
            {
                Destroy(gameObject);
            }
        }
    }
}
