using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Mobil dokunmatik ekranlar için Sanal Joystick (Virtual Joystick) kontrolcüsü.
/// JoyStickBackground ve JoyStick Button panelleriyle tam uyumlu çalışır.
/// </summary>
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public static VirtualJoystick Instance { get; private set; }

    [Header("UI Bileşenleri")]
    [Tooltip("Joystick dış halkası/arka planı (JoyStickBackground). Boşsa otomatik bulunur.")]
    [SerializeField] private RectTransform background;

    [Tooltip("Sürüklenen joystick butonu/kolu (JoyStick Button). Boşsa otomatik bulunur.")]
    [SerializeField] private RectTransform handle;

    [Header("Joystick Ayarları")]
    [Tooltip("Joystick kolunun merkezden azami uzaklaşma mesafesi (piksel). 0 ise otomatik hesaplanır.")]
    [SerializeField] private float movementRange = 90f;

    [Tooltip("Küçük parmak titremelerini engellemek için ölü bölge (0 - 0.3 arası)")]
    [Range(0f, 0.3f)]
    [SerializeField] private float deadzone = 0.05f;

    [Tooltip("Parmağı bıraktığınızda kolun merkeze yumuşak dönüş hızı.")]
    [SerializeField] private float snapSpeed = 20f;

    private Vector2 inputVector = Vector2.zero;
    private Canvas parentCanvas;
    private bool isDragging = false;

    /// <summary>
    /// Normalize edilmiş yön vektörü (-1 ile +1 arası, X ve Y)
    /// </summary>
    public Vector2 InputDirection => inputVector;

    /// <summary>
    /// Yatay eksen: Sol (-1) ... Sağ (+1)
    /// </summary>
    public float Horizontal => inputVector.x;

    /// <summary>
    /// Dikey eksen: Aşağı (-1) ... Yukarı (+1)
    /// </summary>
    public float Vertical => inputVector.y;

    void Awake()
    {
        Instance = this;
        ResolveReferences();
        SetupForwarder();
    }

    void OnEnable()
    {
        if (Instance == null) Instance = this;
        ResetJoystick();
    }

    void OnDisable()
    {
        ResetJoystick();
    }

    /// <summary>
    /// Gerekli RectTransform referanslarını otomatik olarak hiyerarşiden bağlar.
    /// </summary>
    private void ResolveReferences()
    {
        if (parentCanvas == null)
        {
            parentCanvas = GetComponentInParent<Canvas>();
        }

        // 1. Arka plan ve Kol atanmadıysa otomatik bul
        if (background == null && handle == null)
        {
            // Eğer script JoyStickBackground üzerindeyse:
            if (transform.childCount > 0)
            {
                background = GetComponent<RectTransform>();
                handle = transform.GetChild(0).GetComponent<RectTransform>();
            }
            // Eğer script JoyStick Button üzerindeyse:
            else if (transform.parent != null)
            {
                handle = GetComponent<RectTransform>();
                background = transform.parent.GetComponent<RectTransform>();
            }
        }
        else if (background == null)
        {
            background = GetComponent<RectTransform>();
        }
        else if (handle == null && background != null && background.childCount > 0)
        {
            handle = background.GetChild(0).GetComponent<RectTransform>();
        }

        // 2. Yarıçap (movementRange) otomatik hesaplama (300px bg ve 100px handle için ~90-100px idealdir)
        if (movementRange <= 0f && background != null)
        {
            float bgSize = Mathf.Min(background.rect.width, background.rect.height);
            float handleSize = (handle != null) ? Mathf.Min(handle.rect.width, handle.rect.height) : (bgSize * 0.33f);
            movementRange = Mathf.Max(20f, (bgSize - handleSize) * 0.5f);
        }
    }

    /// <summary>
    /// JoyStick Button'a tıklandığında da sürüklemenin kusursuz çalışması için olay yönlendirici bağlar.
    /// </summary>
    private void SetupForwarder()
    {
        if (handle != null && handle != GetComponent<RectTransform>())
        {
            var forwarder = handle.GetComponent<JoystickHandleForwarder>();
            if (forwarder == null)
            {
                forwarder = handle.gameObject.AddComponent<JoystickHandleForwarder>();
            }
            forwarder.Init(this);
        }
    }

    private Camera GetEventCamera()
    {
        if (parentCanvas == null) parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null && parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null; // ScreenSpaceOverlay modunda kamera null olmalıdır
        }
        return (parentCanvas != null && parentCanvas.worldCamera != null) ? parentCanvas.worldCamera : Camera.main;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isDragging = true;
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (background == null) return;

        Camera cam = GetEventCamera();
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(background, eventData.position, cam, out Vector2 localPoint))
        {
            float maxRadius = (movementRange > 0f) ? movementRange : 90f;

            // Kolun konumunu dairesel yarıçap içinde sınırla
            Vector2 clamped = Vector2.ClampMagnitude(localPoint, maxRadius);

            if (handle != null)
            {
                handle.anchoredPosition = clamped;
            }

            // -1 ile +1 arasında normalize edilmiş girdi
            Vector2 normalized = clamped / maxRadius;

            if (normalized.magnitude < deadzone)
            {
                inputVector = Vector2.zero;
            }
            else
            {
                inputVector = normalized;
            }
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
        inputVector = Vector2.zero;

        if (snapSpeed <= 0f && handle != null)
        {
            handle.anchoredPosition = Vector2.zero;
        }
    }

    void Update()
    {
        // Parmak kaldırıldığında kolun merkeze yumuşakça dönmesi
        if (!isDragging && handle != null && handle.anchoredPosition != Vector2.zero)
        {
            if (snapSpeed > 0f)
            {
                handle.anchoredPosition = Vector2.Lerp(handle.anchoredPosition, Vector2.zero, Time.deltaTime * snapSpeed);
                if (handle.anchoredPosition.sqrMagnitude < 0.01f)
                {
                    handle.anchoredPosition = Vector2.zero;
                }
            }
            else
            {
                handle.anchoredPosition = Vector2.zero;
            }
        }
    }

    public void ResetJoystick()
    {
        isDragging = false;
        inputVector = Vector2.zero;
        if (handle != null)
        {
            handle.anchoredPosition = Vector2.zero;
        }
    }
}

/// <summary>
/// JoyStick Button üzerine tıklandığında veya sürüklendiğinde olayları ana VirtualJoystick'e ileten yardımcı bileşen.
/// </summary>
public class JoystickHandleForwarder : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private VirtualJoystick joystick;

    public void Init(VirtualJoystick targetJoystick)
    {
        joystick = targetJoystick;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (joystick != null) joystick.OnPointerDown(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (joystick != null) joystick.OnDrag(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (joystick != null) joystick.OnPointerUp(eventData);
    }
}
