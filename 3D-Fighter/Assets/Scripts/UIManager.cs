using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("UI Panelleri")]
    [SerializeField] private GameObject startPanel;
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject mobileControlPanel;
    [SerializeField] private GameObject healtBarPanel;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject pauseButton;
    [SerializeField] private GameObject ultiButton;
    [SerializeField] private GameObject punchButton;
    [SerializeField] private GameObject uppercutButton;
    [SerializeField] private GameObject blockButton;
    [SerializeField] private TMP_Text playerHealthText;
    [SerializeField] private GameObject characterPanel;
    [Header("Arena Seçimi")]
    [SerializeField] private GameObject arenaPanel;
    private string selectedCharacterName = "";
    private int selectedArenaIndex = 0;

    [Header("Düşman Kafa Üstü Can Barı (Enemy Health Bar)")]
    [Tooltip("Canvas altındaki 'Enemy Health Bar' nesnesi (Boş bırakılırsa otomatik bulunur)")]
    [SerializeField] private GameObject enemyHealthBarPanel;
    [Tooltip("Enemy Health Bar içindeki azalan yeşil bar (Health Bar Green)")]
    [SerializeField] private Image enemyHealthBarFill;
    [Tooltip("Enemy Health Bar içinde (varsa) sayısal can yazısı")]
    [SerializeField] private TMP_Text enemyHealthText;
    [Tooltip("Oyuncu düşmana vurduktan sonra barın ekranda kalma süresi (saniye)")]
    [SerializeField] private float enemyHealthBarVisibleDuration = 2.5f;
    [Tooltip("Düşmanın kafa kemiğinin (Head) ne kadar üzerinde görüneceği (metre)")]
    [SerializeField] private float enemyHeadHeightOffset = 0.42f;
    [Tooltip("Kafa kemiği bulunamazsa düşman tabanından yüksekliği (metre)")]
    [SerializeField] private float enemyFallbackHeightOffset = 2.05f;

    private EnemyController trackedEnemy;
    private Transform trackedEnemyHeadBone;
    private float enemyHealthBarHideTime = 0f;
    private Canvas rootCanvas;
    private RectTransform canvasRectTransform;
    private bool enemyHealthBarNormalized = false;

    [Header("Geri Sayım (Count Down Panel)")]
    [Tooltip("Canvas altındaki 'Count Down Panel' nesnesi (Boş bırakılırsa otomatik bulunur)")]
    [SerializeField] private GameObject countDownPanel;
    [Tooltip("Geri sayım metni (3, 2, 1 -> Font Size 300 | BAŞLA! -> Font Size 90)")]
    [SerializeField] private TMP_Text countDownText;
    [SerializeField] private Text legacyCountDownText;
    private Coroutine countDownCoroutine;
    public static bool IsCountingDown { get; private set; } = false;

    [Header("Dalga Sayacı (Wave Counter Panel)")]
    [Tooltip("Canvas altındaki 'Wave Counter Panel' nesnesi (Boş bırakılırsa otomatik bulunur)")]
    [SerializeField] private GameObject waveCounterPanel;
    [Tooltip("Mevcut dalgayı gösteren yazı bileşeni")]
    [SerializeField] private TMP_Text waveCounterText;
    [SerializeField] private Text legacyWaveCounterText;
    private int lastDisplayedWave = -1;
    private string waveCounterFormatTemplate = "";

    [Header("Karakter Seçimi (ScriptableObject & Carousel)")]
    [Tooltip("7 karakterin verisini tutan ScriptableObject (Boş bırakılırsa Resources/CharacterDatabase otomatik yüklenir)")]
    [SerializeField] private CharacterDatabaseSO characterDatabase;
    [Tooltip("CharacterChooseUI üzerindeki RawImage bileşeni (Assets/Character Texture içindeki RenderTexture'ları gösterir)")]
    [SerializeField] private RawImage characterChooseRawImage;
    [Tooltip("CharacterChooseUI altındaki Character Name Text bileşeni")]
    [SerializeField] private TMP_Text characterNameText;
    [Tooltip("Karakteri onaylamak için tıklanan CharacterChooseUI butonu")]
    [SerializeField] private Button characterChooseButton;
    [Tooltip("Sonraki karaktere geçen Next Button")]
    [SerializeField] private Button nextCharacterButton;
    [Tooltip("Önceki karaktere geçen Previous Button")]
    [SerializeField] private Button previousCharacterButton;
    [SerializeField] private GameObject[] characterPrefabs;
    [SerializeField] private Transform playerSpawnPoint;
    private int currentCharacterIndex = 0;
    private Difficulty selectedDifficulty;
    [Header("Referanslar")]
    [SerializeField] EnemySpawner enemySpawner;
    [SerializeField] PlayerController playerController;

    void Awake()
    {
        if (enemySpawner == null)
        {
            enemySpawner = FindObjectOfType<EnemySpawner>();
        }

        if (playerController == null)
        {
            playerController = FindObjectOfType<PlayerController>();
        }

        if (gameOverPanel == null)
        {
            FindGameOverPanel();
        }

        if (victoryPanel == null)
        {
            FindVictoryPanel();
        }

        if (mobileControlPanel == null)
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
            {
                Transform mobileT = canvas.transform.Find("Mobil Control");
                if (mobileT != null)
                {
                    mobileControlPanel = mobileT.gameObject;
                }
            }
        }

        if (pausePanel == null)
        {
            FindPausePanel();
        }

        if (pauseButton == null)
        {
            FindPauseButton();
        }

        if (ultiButton == null)
        {
            FindUltiButton();
        }

        if (punchButton == null)
        {
            FindPunchButton();
        }

        if (uppercutButton == null)
        {
            FindUppercutButton();
        }

        if (blockButton == null)
        {
            FindBlockButton();
        }

        BindGameOverButtons();
        BindVictoryButtons();
        BindPauseButtons();
        BindUltiButton();
        BindPunchButton();
        BindUppercutButton();
        BindBlockButton();

        if (characterPanel == null)
        {
            FindCharacterPanel();
        }

        EnsureCharacterDatabaseLoaded();
        BindCharacterSelectionUI();
        UpdateCharacterSelectionUI();

        if (arenaPanel == null)
        {
            FindArenaPanel();
        }

        BindArenaButtons();

        FindAndSetupEnemyHealthBar();
        FindCountDownPanel();
        FindWaveCounterPanel();
    }

    void Start()
    {
        Time.timeScale = 1f;
        IsCountingDown = false;

        // Mobil Control paneli şeffaf arka planının Pause Button veya diğer UI butonlarının tıklanmasını engellemesini önle
        if (mobileControlPanel != null)
        {
            Image bgImg = mobileControlPanel.GetComponent<Image>();
            if (bgImg != null)
            {
                bgImg.raycastTarget = false;
            }
        }

        if (startPanel != null) startPanel.SetActive(true);
        if (characterPanel != null) characterPanel.SetActive(false);
        if (arenaPanel != null) arenaPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (pauseButton != null) pauseButton.SetActive(false);
        if (healtBarPanel != null) healtBarPanel.SetActive(false);
        if (enemyHealthBarPanel != null) enemyHealthBarPanel.SetActive(false);
        if (countDownPanel != null) countDownPanel.SetActive(false);
        if (waveCounterPanel != null) waveCounterPanel.SetActive(false);
        if (mobileControlPanel != null)
        {
            mobileControlPanel.SetActive(startPanel == null || !startPanel.activeSelf);
        }

        // Başlangıçta menüdeyken Texture Arena önizlemeleri açık, seyirciler (Spectators) görünmez olsun
        if (RingBoundary.Instance != null)
        {
            RingBoundary.Instance.PrepareForMenuPreview();
        }
    }

    void Update()
    {
        if (playerController != null)
        {
            if (playerHealthText != null)
            {
                playerHealthText.SetText(Mathf.Max(0, Mathf.CeilToInt(playerController.playerHealth)).ToString());
            }

            if (ultiButton != null)
            {
                bool canShowUlti = !playerController.usingUlti &&
                                   (startPanel == null || !startPanel.activeSelf) &&
                                   (characterPanel == null || !characterPanel.activeSelf) &&
                                   (arenaPanel == null || !arenaPanel.activeSelf) &&
                                   (pausePanel == null || !pausePanel.activeSelf) &&
                                   (gameOverPanel == null || !gameOverPanel.activeSelf) &&
                                   (victoryPanel == null || !victoryPanel.activeSelf) &&
                                   (mobileControlPanel == null || mobileControlPanel.activeSelf);

                if (ultiButton.activeSelf != canShowUlti)
                {
                    ultiButton.SetActive(canShowUlti);
                }
            }
        }

        // Mevcut dalga sayısını Wave Counter Panel'de güncel tut
        UpdateWaveCounterUI();

        // Klavye kısayolu (ESC veya P) ile oyunu duraklat / devam ettir
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
        {
            if (pausePanel != null && pausePanel.activeSelf)
            {
                ContinueButton();
            }
            else if (startPanel != null && !startPanel.activeSelf &&
                     (characterPanel == null || !characterPanel.activeSelf) &&
                     (arenaPanel == null || !arenaPanel.activeSelf) &&
                     (gameOverPanel == null || !gameOverPanel.activeSelf) &&
                     (victoryPanel == null || !victoryPanel.activeSelf))
            {
                PauseGame();
            }
        }
    }

    void LateUpdate()
    {
        UpdateEnemyHealthBarTracking();
    }

    void FindCharacterPanel()
    {
        if (characterPanel != null) return;
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            Transform charT = canvas.transform.Find("Character Choose");
            if (charT != null)
            {
                characterPanel = charT.gameObject;
            }
            else
            {
                foreach (Transform child in canvas.transform)
                {
                    if (child.name.ToLower().Contains("character") && child.GetComponent<Button>() == null)
                    {
                        characterPanel = child.gameObject;
                        break;
                    }
                }
            }
        }
    }

    void FindArenaPanel()
    {
        if (arenaPanel != null) return;
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            Transform arenaT = canvas.transform.Find("Arena Choose");
            if (arenaT != null)
            {
                arenaPanel = arenaT.gameObject;
            }
            else
            {
                foreach (Transform child in canvas.transform)
                {
                    if (child.name.ToLower().Contains("arena") && child.GetComponent<Button>() == null)
                    {
                        arenaPanel = child.gameObject;
                        break;
                    }
                }
            }
        }
    }

    void BindArenaButtons()
    {
        if (arenaPanel == null) FindArenaPanel();
        if (arenaPanel == null) return;

        Button[] buttons = arenaPanel.GetComponentsInChildren<Button>(true);
        foreach (var btn in buttons)
        {
            if (btn == null) continue;
            string btnName = btn.name.ToLowerInvariant();
            string btnText = "";
            TMP_Text tmp = btn.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) btnText = tmp.text.ToLowerInvariant();
            Text txt = btn.GetComponentInChildren<Text>(true);
            if (txt != null) btnText += " " + txt.text.ToLowerInvariant();

            // Inspector'da kalmış eski persistent ChooseCharacter eventlerini devre dışı bırak
            DisablePersistentListeners(btn);
            btn.onClick.RemoveAllListeners();

            if (btnName.Contains("box") || btnText.Contains("box") || btnText.Contains("boks") || btnText.Contains("arena 1"))
            {
                btn.onClick.AddListener(() => ChooseArena(0));
            }
            else if (btnName.Contains("chinese") || btnText.Contains("chinese") || btnText.Contains("çin") || btnText.Contains("cin") || btnText.Contains("arena 2"))
            {
                btn.onClick.AddListener(() => ChooseArena(1));
            }
            else if (btnName.Contains("viking") || btnName.Contains("grand") || btnName.Contains("arena 3") || btnText.Contains("viking") || btnText.Contains("arena 3") || btnText.Contains("3"))
            {
                btn.onClick.AddListener(() => ChooseArena(2));
            }
            else
            {
                btn.onClick.AddListener(() => ChooseArena(0));
            }
        }
    }

    void FindPausePanel()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            foreach (Transform child in canvas.transform)
            {
                if (child.name.Equals("Pause Panel", System.StringComparison.OrdinalIgnoreCase) ||
                    child.name.Equals("PausePanel", System.StringComparison.OrdinalIgnoreCase) ||
                    (child.name.Contains("Pause") && child.GetComponent<Button>() == null))
                {
                    pausePanel = child.gameObject;
                    break;
                }
            }
        }
    }

    void FindPauseButton()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            Button[] allButtons = canvas.GetComponentsInChildren<Button>(true);
            foreach (var btn in allButtons)
            {
                if (btn.name.Equals("Pause Button", System.StringComparison.OrdinalIgnoreCase) ||
                    btn.name.Equals("PauseButton", System.StringComparison.OrdinalIgnoreCase))
                {
                    pauseButton = btn.gameObject;
                    break;
                }
            }
        }
    }

    void BindPauseButtons()
    {
        if (pausePanel != null)
        {
            Button[] buttons = pausePanel.GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                if (btn.name.Contains("Continue") || btn.name.Contains("Devam"))
                {
                    btn.onClick.RemoveListener(ContinueButton);
                    btn.onClick.AddListener(ContinueButton);
                }
                else if (btn.name.Contains("Home") || btn.name.Contains("Menu") || btn.name.Contains("Start"))
                {
                    btn.onClick.RemoveListener(ReturnToMainMenu);
                    btn.onClick.AddListener(ReturnToMainMenu);
                }
            }
        }

        if (pauseButton != null)
        {
            Button btn = pauseButton.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveListener(PauseGame);
                btn.onClick.AddListener(PauseGame);
            }
        }
    }

    void FindUltiButton()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            Button[] allButtons = canvas.GetComponentsInChildren<Button>(true);
            foreach (var btn in allButtons)
            {
                if (btn.name.Equals("Ulti Button", System.StringComparison.OrdinalIgnoreCase) ||
                    btn.name.Equals("UltiButton", System.StringComparison.OrdinalIgnoreCase))
                {
                    ultiButton = btn.gameObject;
                    break;
                }
            }
        }
    }

    void BindUltiButton()
    {
        if (playerController == null) playerController = FindObjectOfType<PlayerController>();
        if (ultiButton != null && playerController != null)
        {
            Button btn = ultiButton.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveListener(playerController.UltiButton);
                btn.onClick.AddListener(playerController.UltiButton);
            }
        }
    }

    void FindPunchButton()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            Transform[] allTransforms = canvas.GetComponentsInChildren<Transform>(true);
            foreach (var t in allTransforms)
            {
                if (t.name.Equals("Punch Button", System.StringComparison.OrdinalIgnoreCase) ||
                    t.name.Equals("PunchButton", System.StringComparison.OrdinalIgnoreCase) ||
                    t.name.Equals("Vurus Butonu", System.StringComparison.OrdinalIgnoreCase) ||
                    t.name.Equals("hitButton", System.StringComparison.OrdinalIgnoreCase) ||
                    (t.name.Contains("Punch") && (t.GetComponent<Button>() != null || t.GetComponent<EventTrigger>() != null || t.GetComponent<Image>() != null)) ||
                    (t.name.Contains("Hit") && (t.GetComponent<Button>() != null || t.GetComponent<EventTrigger>() != null || t.GetComponent<Image>() != null)))
                {
                    punchButton = t.gameObject;
                    break;
                }
            }
        }
    }

    void FindUppercutButton()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            Transform[] allTransforms = canvas.GetComponentsInChildren<Transform>(true);
            foreach (var t in allTransforms)
            {
                if (t.name.Equals("Uppercut Button", System.StringComparison.OrdinalIgnoreCase) ||
                    t.name.Equals("UppercutButton", System.StringComparison.OrdinalIgnoreCase) ||
                    t.name.Equals("Aparkat Butonu", System.StringComparison.OrdinalIgnoreCase) ||
                    (t.name.Contains("Uppercut") && (t.GetComponent<Button>() != null || t.GetComponent<EventTrigger>() != null || t.GetComponent<Image>() != null)))
                {
                    uppercutButton = t.gameObject;
                    break;
                }
            }
        }
    }

    void FindBlockButton()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            // Block button might be an EventTrigger or a Button, look through transforms
            Transform[] allTransforms = canvas.GetComponentsInChildren<Transform>(true);
            foreach (var t in allTransforms)
            {
                if (t.name.Equals("Block Button", System.StringComparison.OrdinalIgnoreCase) ||
                    t.name.Equals("BlockButton", System.StringComparison.OrdinalIgnoreCase) ||
                    t.name.Equals("Blok Butonu", System.StringComparison.OrdinalIgnoreCase) ||
                    (t.name.Contains("Block") && (t.GetComponent<Button>() != null || t.GetComponent<EventTrigger>() != null || t.GetComponent<Image>() != null)))
                {
                    blockButton = t.gameObject;
                    break;
                }
            }
        }
    }

    void BindPunchButton()
    {
        if (playerController == null) playerController = FindObjectOfType<PlayerController>();
        if (punchButton != null && playerController != null)
        {
            EventTrigger trigger = punchButton.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = punchButton.AddComponent<EventTrigger>();
            }

            trigger.triggers.RemoveAll(entry => entry.eventID == EventTriggerType.PointerDown);

            EventTrigger.Entry pointerDown = new EventTrigger.Entry();
            pointerDown.eventID = EventTriggerType.PointerDown;
            pointerDown.callback.AddListener((data) => { if (playerController != null) playerController.PunchButton(); });
            trigger.triggers.Add(pointerDown);

            Button btn = punchButton.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveListener(playerController.PunchButton);
                // We rely on PointerDown for faster response, but adding onClick as fallback
                btn.onClick.AddListener(playerController.PunchButton);
            }
        }
    }

    void BindUppercutButton()
    {
        if (playerController == null) playerController = FindObjectOfType<PlayerController>();
        if (uppercutButton != null && playerController != null)
        {
            EventTrigger trigger = uppercutButton.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = uppercutButton.AddComponent<EventTrigger>();
            }

            trigger.triggers.RemoveAll(entry => entry.eventID == EventTriggerType.PointerDown);

            EventTrigger.Entry pointerDown = new EventTrigger.Entry();
            pointerDown.eventID = EventTriggerType.PointerDown;
            pointerDown.callback.AddListener((data) => { if (playerController != null) playerController.UppercutButton(); });
            trigger.triggers.Add(pointerDown);

            Button btn = uppercutButton.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveListener(playerController.UppercutButton);
                btn.onClick.AddListener(playerController.UppercutButton);
            }
        }
    }

    void BindBlockButton()
    {
        if (playerController == null) playerController = FindObjectOfType<PlayerController>();
        if (blockButton != null && playerController != null)
        {
            EventTrigger trigger = blockButton.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = blockButton.AddComponent<EventTrigger>();
            }

            // Remove previous PlayerController events to avoid duplicate calls, but keep others
            trigger.triggers.RemoveAll(entry => 
            {
                return entry.eventID == EventTriggerType.PointerDown || entry.eventID == EventTriggerType.PointerUp;
            });

            EventTrigger.Entry pointerDown = new EventTrigger.Entry();
            pointerDown.eventID = EventTriggerType.PointerDown;
            pointerDown.callback.AddListener((data) => { if (playerController != null) playerController.Blocking(1); });
            trigger.triggers.Add(pointerDown);

            EventTrigger.Entry pointerUp = new EventTrigger.Entry();
            pointerUp.eventID = EventTriggerType.PointerUp;
            pointerUp.callback.AddListener((data) => { if (playerController != null) playerController.Blocking(0); });
            trigger.triggers.Add(pointerUp);
        }
    }

    void FindVictoryPanel()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            foreach (Transform child in canvas.transform)
            {
                if (child.name.Equals("Victory", System.StringComparison.OrdinalIgnoreCase) || child.name.Contains("Victory") || child.name.Contains("Win"))
                {
                    victoryPanel = child.gameObject;
                    break;
                }
            }
        }
    }

    void BindVictoryButtons()
    {
        if (victoryPanel != null)
        {
            Button[] buttons = victoryPanel.GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                if (btn.name.Contains("Restart"))
                {
                    btn.onClick.RemoveListener(RestartGame);
                    btn.onClick.AddListener(RestartGame);
                }
                else if (btn.name.Contains("Home"))
                {
                    btn.onClick.RemoveListener(ReturnToMainMenu);
                    btn.onClick.AddListener(ReturnToMainMenu);
                }
            }
        }
    }

    void FindGameOverPanel()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            foreach (Transform child in canvas.transform)
            {
                if (child.name == "Panel" || child.name.Contains("GameOver") || child.name.Contains("Game Over"))
                {
                    if (child.Find("Relive Button") != null || child.Find("Home Button") != null)
                    {
                        gameOverPanel = child.gameObject;
                        break;
                    }
                }
            }
        }
    }

    void BindGameOverButtons()
    {
        if (gameOverPanel != null)
        {
            Button[] buttons = gameOverPanel.GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                if (btn.name.Contains("Relive"))
                {
                    btn.onClick.RemoveListener(Relive);
                    btn.onClick.AddListener(Relive);
                }
                else if (btn.name.Contains("Home"))
                {
                    btn.onClick.RemoveListener(ReturnToMainMenu);
                    btn.onClick.AddListener(ReturnToMainMenu);
                }
            }
        }
    }

    /// <summary>
    /// Pause butonunu en ön katmana getirir (SetAsLastSibling) ve aktif eder.
    /// Bu sayede Mobil Control veya diğer paneller asla butonun üzerine geçip tıklamaları engelleyemez.
    /// </summary>
    public void ShowPauseButton()
    {
        if (pauseButton != null)
        {
            pauseButton.transform.SetAsLastSibling();
            pauseButton.SetActive(true);
        }
    }

    public void EasyMode()
    {
        selectedDifficulty = Difficulty.Easy;
        OpenCharacterSelectPanel();
    }

    public void MidMode()
    {
        selectedDifficulty = Difficulty.Medium;
        OpenCharacterSelectPanel();
    }

    public void HardMode()
    {
        selectedDifficulty = Difficulty.Hard;
        OpenCharacterSelectPanel();
    }

    private void OpenCharacterSelectPanel()
    {
        Time.timeScale = 1f;
        if (startPanel != null) startPanel.SetActive(false);
        if (arenaPanel != null) arenaPanel.SetActive(false);

        if (RingBoundary.Instance != null)
        {
            RingBoundary.Instance.PrepareForMenuPreview();
        }

        if (characterPanel == null) FindCharacterPanel();
        if (characterPanel != null)
        {
            characterPanel.SetActive(true);
            EnsureCharacterDatabaseLoaded();
            BindCharacterSelectionUI();
            UpdateCharacterSelectionUI();
        }
    }

    private void EnsureCharacterDatabaseLoaded()
    {
        if (characterDatabase == null)
        {
            characterDatabase = Resources.Load<CharacterDatabaseSO>("CharacterDatabase");
        }
    }

    /// <summary>
    /// Character Choose panelindeki CharacterChooseUI (RawImage + Button), Character Name Text,
    /// Next Button ve Previous Button referanslarını bulur ve olaylarını (onClick) bağlar.
    /// </summary>
    private void BindCharacterSelectionUI()
    {
        if (characterPanel == null) FindCharacterPanel();
        if (characterPanel == null) return;

        // 1. CharacterChooseUI (RawImage & Button) ve altındaki Character Name Text'i bul
        Transform chooseUiTransform = characterPanel.transform.Find("CharacterChooseUI");
        if (chooseUiTransform == null)
        {
            RawImage[] rawImages = characterPanel.GetComponentsInChildren<RawImage>(true);
            if (rawImages != null && rawImages.Length > 0)
            {
                chooseUiTransform = rawImages[0].transform;
            }
        }

        if (chooseUiTransform != null)
        {
            if (characterChooseRawImage == null)
            {
                characterChooseRawImage = chooseUiTransform.GetComponent<RawImage>();
            }

            if (characterChooseButton == null)
            {
                characterChooseButton = chooseUiTransform.GetComponent<Button>();
            }

            if (characterNameText == null)
            {
                Transform nameTextT = chooseUiTransform.Find("Character Name Text");
                if (nameTextT != null)
                {
                    characterNameText = nameTextT.GetComponent<TMP_Text>();
                }
                if (characterNameText == null)
                {
                    characterNameText = chooseUiTransform.GetComponentInChildren<TMP_Text>(true);
                }
            }
        }

        // 2. Next Button ve Previous Button'ları bul ve bağla
        Button[] buttons = characterPanel.GetComponentsInChildren<Button>(true);
        foreach (var btn in buttons)
        {
            if (btn == null) continue;
            string lowerName = btn.name.ToLowerInvariant();

            if (lowerName.Contains("next") || lowerName.Contains("ileri") || lowerName.Contains("sonraki"))
            {
                nextCharacterButton = btn;
            }
            else if (lowerName.Contains("prev") || lowerName.Contains("geri") || lowerName.Contains("onceki") || lowerName.Contains("önceki"))
            {
                previousCharacterButton = btn;
            }
            else if (characterChooseButton == null && btn.gameObject.name.IndexOf("CharacterChooseUI", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                characterChooseButton = btn;
            }
        }

        if (nextCharacterButton != null)
        {
            DisablePersistentListeners(nextCharacterButton);
            nextCharacterButton.onClick.RemoveListener(NextCharacter);
            nextCharacterButton.onClick.AddListener(NextCharacter);
        }

        if (previousCharacterButton != null)
        {
            DisablePersistentListeners(previousCharacterButton);
            previousCharacterButton.onClick.RemoveListener(PreviousCharacter);
            previousCharacterButton.onClick.AddListener(PreviousCharacter);
        }

        if (characterChooseButton != null)
        {
            DisablePersistentListeners(characterChooseButton);
            characterChooseButton.onClick.RemoveListener(ConfirmCurrentCharacter);
            characterChooseButton.onClick.AddListener(ConfirmCurrentCharacter);
        }
    }

    private void DisablePersistentListeners(Button btn)
    {
        if (btn == null) return;
        int count = btn.onClick.GetPersistentEventCount();
        for (int i = 0; i < count; i++)
        {
            btn.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);
        }
    }

    /// <summary>
    /// Next Button: Bir sonraki karaktere geçer.
    /// Son karakterdeyken (7. karakter) basıldığında 1. karaktere (indeks 0) geri döner.
    /// </summary>
    public void NextCharacter()
    {
        EnsureCharacterDatabaseLoaded();
        if (characterDatabase == null || characterDatabase.Count == 0) return;

        currentCharacterIndex = characterDatabase.WrapIndex(currentCharacterIndex + 1);
        UpdateCharacterSelectionUI();
    }

    /// <summary>
    /// Previous Button: Bir önceki karaktere geçer.
    /// İlk karakterdeyken (1. karakter) basıldığında sonuncu karaktere (7. karakter) geri döner.
    /// </summary>
    public void PreviousCharacter()
    {
        EnsureCharacterDatabaseLoaded();
        if (characterDatabase == null || characterDatabase.Count == 0) return;

        currentCharacterIndex = characterDatabase.WrapIndex(currentCharacterIndex - 1);
        UpdateCharacterSelectionUI();
    }

    /// <summary>
    /// Mevcut currentCharacterIndex'teki karakterin RenderTexture'ını ve ismini UI'a yansıtır.
    /// </summary>
    private void UpdateCharacterSelectionUI()
    {
        EnsureCharacterDatabaseLoaded();
        if (characterDatabase == null || characterDatabase.Count == 0) return;

        currentCharacterIndex = characterDatabase.WrapIndex(currentCharacterIndex);
        CharacterEntry entry = characterDatabase.GetCharacter(currentCharacterIndex);
        if (entry == null) return;

        if (characterChooseRawImage != null && entry.previewTexture != null)
        {
            characterChooseRawImage.texture = entry.previewTexture;
        }

        if (characterNameText != null)
        {
            characterNameText.SetText(entry.displayName);
        }

        selectedCharacterName = entry.characterId;
    }

    /// <summary>
    /// CharacterChooseUI üzerine tıklandığında mevcut gösterilen karakteri seçer ve Arena Seçim Paneline geçer.
    /// </summary>
    public void ConfirmCurrentCharacter()
    {
        EnsureCharacterDatabaseLoaded();
        if (characterDatabase != null && characterDatabase.Count > 0)
        {
            CharacterEntry entry = characterDatabase.GetCharacter(currentCharacterIndex);
            if (entry != null)
            {
                selectedCharacterName = entry.characterId;
            }
        }

        if (characterPanel != null)
        {
            characterPanel.SetActive(false);
        }

        OpenArenaSelectPanel();
    }

    private void OpenArenaSelectPanel()
    {
        Time.timeScale = 1f;

        // Arena seçim ekranındayken 3 arena önizleme kamerası açık, seyirciler (Spectators) görünmez olsun
        if (RingBoundary.Instance != null)
        {
            RingBoundary.Instance.PrepareForMenuPreview();
        }

        if (arenaPanel == null) FindArenaPanel();

        if (arenaPanel != null)
        {
            arenaPanel.SetActive(true);
            BindArenaButtons();
        }
        else
        {
            Debug.LogWarning("Arena paneli bulunamadı! Varsayılan arenayla oyuna başlanıyor.");
            StartGameWithSelections();
        }
    }

    /// <summary>
    /// Tüm dalgalar bittiğinde zafer panelini açar
    /// </summary>
    public void ShowVictoryPanel()
    {
        Time.timeScale = 1f;
        StopMatchCountdown();
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (pauseButton != null) pauseButton.SetActive(false);
        if (mobileControlPanel != null) mobileControlPanel.SetActive(false);
        if (healtBarPanel != null) healtBarPanel.SetActive(false);
        if (enemyHealthBarPanel != null) enemyHealthBarPanel.SetActive(false);
        if (waveCounterPanel != null) waveCounterPanel.SetActive(false);
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
        }
    }

    /// <summary>
    /// Oyuncu öldüğünde Game Over panelini açar ve kaçıncı dalgada öldüğünü gösterir
    /// </summary>
    public void ShowGameOverPanel(int deadWave = -1)
    {
        Time.timeScale = 1f;
        StopMatchCountdown();
        if (deadWave <= 0 && enemySpawner != null)
        {
            deadWave = enemySpawner.CurrentWave;
        }

        if (gameOverPanel != null)
        {
            var tmpTexts = gameOverPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in tmpTexts)
            {
                if (t.name.Contains("Text") || t.text.Contains("ÖLDÜN"))
                {
                    t.text = $"!!! ÖLDÜN !!!\n<size=70%>{deadWave}. Dalgada Kaybettin</size>";
                    break;
                }
            }

            gameOverPanel.SetActive(true);
        }
        if (pausePanel != null) pausePanel.SetActive(false);
        if (pauseButton != null) pauseButton.SetActive(false);
        if (mobileControlPanel != null) mobileControlPanel.SetActive(false);
        if (enemyHealthBarPanel != null) enemyHealthBarPanel.SetActive(false);
        if (waveCounterPanel != null) waveCounterPanel.SetActive(false);
    }

    public void ShowGameOverPanel() => ShowGameOverPanel(-1);

    /// <summary>
    /// Game Over panelini kapatır
    /// </summary>
    public void HideGameOverPanel()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Relive Butonu: Öldüğü dalgayı yeniden başlatır ve karakter Kip Up animasyonuyla ayağa kalkar
    /// </summary>
    public void Relive()
    {
        Time.timeScale = 1f;
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (healtBarPanel != null) healtBarPanel.SetActive(true);   // ← Health bar göster
        if (enemyHealthBarPanel != null) enemyHealthBarPanel.SetActive(false);
        if (waveCounterPanel == null) FindWaveCounterPanel();
        if (waveCounterPanel != null) waveCounterPanel.SetActive(true);
        if (mobileControlPanel != null) mobileControlPanel.SetActive(true);
        ShowPauseButton();

        // 1. Spawner'da oyuncunun öldüğü mevcut dalgayı yeniden başlat
        if (enemySpawner == null) enemySpawner = FindObjectOfType<EnemySpawner>();
        if (enemySpawner != null)
        {
            enemySpawner.RestartCurrentWave();
        }

        UpdateWaveCounterUI(force: true);

        // 2. Oyuncuyu Kip Up animasyonu ile ayağa kaldır
        if (playerController == null) playerController = FindObjectOfType<PlayerController>();
        if (playerController != null)
        {
            playerController.RelivePlayer();
            playerController.StartUltiCooldown();
        }

        TriggerMatchCountdown();
    }

    public void OnReliveButtonClicked() => Relive();

    public void Home() => ReturnToMainMenu();
    public void OnHomeButtonClicked() => ReturnToMainMenu();

    /// <summary>
    /// Yeniden Başla butonu: Aynı zorluk modunda oyunu 1. dalgadan yeniden başlatır
    /// </summary>
    public void RestartGame()
    {
        Time.timeScale = 1f;
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (startPanel != null) startPanel.SetActive(false);
        if (characterPanel != null) characterPanel.SetActive(false);
        if (arenaPanel != null) arenaPanel.SetActive(false);
        if (healtBarPanel != null) healtBarPanel.SetActive(true);   // ← Health bar göster
        if (enemyHealthBarPanel != null) enemyHealthBarPanel.SetActive(false);
        if (waveCounterPanel == null) FindWaveCounterPanel();
        if (waveCounterPanel != null) waveCounterPanel.SetActive(true);
        if (mobileControlPanel != null) mobileControlPanel.SetActive(true);
        ShowPauseButton();

        // Oyuncuyu sıfırla
        if (playerController == null) playerController = FindObjectOfType<PlayerController>();
        if (playerController != null)
        {
            playerController.ResetPlayerState();
            playerController.StartUltiCooldown();
        }

        FollowPlayer camFollow = FindObjectOfType<FollowPlayer>();
        if (camFollow != null)
        {
            camFollow.AlignBehindTarget(snapImmediately: true);
        }

        // Spawner'ı aynı modda yeniden başlat
        if (enemySpawner == null) enemySpawner = FindObjectOfType<EnemySpawner>();
        if (enemySpawner != null)
        {
            enemySpawner.RestartGame();
        }

        UpdateWaveCounterUI(force: true);
        TriggerMatchCountdown();
    }

    /// <summary>
    /// Ana Menü / Home butonu: Start paneline geri döner ve oyunu sıfırlar
    /// </summary>
    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f; // Oyun duraklatılmışsa zamanı normale döndür
        StopMatchCountdown();
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (pauseButton != null) pauseButton.SetActive(false);
        if (healtBarPanel != null) healtBarPanel.SetActive(false);
        if (enemyHealthBarPanel != null) enemyHealthBarPanel.SetActive(false);
        if (waveCounterPanel != null) waveCounterPanel.SetActive(false);
        if (characterPanel != null) characterPanel.SetActive(false);
        if (arenaPanel != null) arenaPanel.SetActive(false);
        if (startPanel != null) startPanel.SetActive(true);
        if (mobileControlPanel != null) mobileControlPanel.SetActive(false);

        // Oyuncuyu başlangıç durumuna döndür
        if (playerController == null) playerController = FindObjectOfType<PlayerController>();
        if (playerController != null)
        {
            playerController.ResetPlayerState();
        }

        // Spawner'ı temizle ve durdur
        if (enemySpawner == null) enemySpawner = FindObjectOfType<EnemySpawner>();
        if (enemySpawner != null)
        {
            enemySpawner.ResetSpawner();
        }

        // Ana menüye dönüldüğünde tüm seyircileri tekrar görünmez yap ve önizleme kameralarını aç
        if (RingBoundary.Instance != null)
        {
            RingBoundary.Instance.PrepareForMenuPreview();
        }
        Debug.Log("<color=cyan>[ANA MENÜ]</color> Start Panel açıldı, oyun sıfırlandı.");
    }

    /// <summary>
    /// Oyunu duraklatır (Pause) ve Pause Panelini açar
    /// </summary>
    public void PauseGame()
    {
        // Menüler, zafer veya yenilgi paneli açıkken duraklatma yapılamaz
        if (startPanel != null && startPanel.activeSelf) return;
        if (characterPanel != null && characterPanel.activeSelf) return;
        if (arenaPanel != null && arenaPanel.activeSelf) return;
        if (victoryPanel != null && victoryPanel.activeSelf) return;
        if (gameOverPanel != null && gameOverPanel.activeSelf) return;

        Time.timeScale = 0f;
        if (healtBarPanel != null) healtBarPanel.SetActive(false);  // ← Can barını gizle
        if (enemyHealthBarPanel != null) enemyHealthBarPanel.SetActive(false);
        if (waveCounterPanel != null) waveCounterPanel.SetActive(false);
        if (pausePanel != null)
        {
            pausePanel.transform.SetAsLastSibling(); // Pause panel en öne gelsin
            pausePanel.SetActive(true);
        }
        if (pauseButton != null) pauseButton.SetActive(false);
        if (mobileControlPanel != null) mobileControlPanel.SetActive(false);
        Debug.Log("<color=yellow>[OYUN DURAKLATILDI]</color> Pause Panel açıldı, oyun durdu.");
    }

    /// <summary>
    /// Duraklatılmış oyunu kaldığı yerden devam ettirir
    /// </summary>
    public void ContinueButton()
    {
        Time.timeScale = 1f;
        if (pausePanel != null) pausePanel.SetActive(false);
        if (healtBarPanel != null) healtBarPanel.SetActive(true);   // ← Can barını göster
        if (waveCounterPanel == null) FindWaveCounterPanel();
        if (waveCounterPanel != null) waveCounterPanel.SetActive(true);
        if (mobileControlPanel != null) mobileControlPanel.SetActive(true);
        ShowPauseButton();
        Debug.Log("<color=green>[OYUN DEVAM EDİYOR]</color> Pause Panel kapatıldı, oyun devam ediyor.");
    }

    /// <summary>
    /// Karakter seçim butonları tarafından çağrılır.
    /// Seçilen karakteri kaydeder ve Arena Seçim Panelini açar.
    /// </summary>
    public void ChooseCharacter(string Character)
    {
        // Eğer bu çağrı Arena Seçim Paneli açıkken geldiyse (Inspector'daki eski buton eventi veya arena tıklaması)
        if (arenaPanel != null && arenaPanel.activeSelf)
        {
            Debug.LogWarning("[UIManager] Arena panelinde ChooseCharacter tetiklendi, ChooseArena'ya yönlendiriliyor: " + Character);
            ChooseArena(Character);
            return;
        }

        EnsureCharacterDatabaseLoaded();

        // Eğer CharacterDatabaseSO aktifse ve Next/Previous carousel kullanılıyorsa,
        // Inspector'da sabit kalmış "Elvis-Player" argümanı yerine ekranda o an seçili olan karakteri (currentCharacterIndex) baz al!
        if (characterDatabase != null && characterDatabase.Count > 0)
        {
            CharacterEntry currentEntry = characterDatabase.GetCharacter(currentCharacterIndex);
            if (currentEntry != null)
            {
                selectedCharacterName = currentEntry.characterId;
            }
            else
            {
                selectedCharacterName = Character;
            }
        }
        else
        {
            selectedCharacterName = Character;
        }

        if (characterPanel != null)
        {
            characterPanel.SetActive(false);
        }

        OpenArenaSelectPanel();
    }

    /// <summary>
    /// Arena seçim butonları tarafından int index (0: Box Arena, 1: Ancient Chinese Arena, 2: Viking Arena) ile çağrılır.
    /// Texture Arena altındaki ilgili Environment'ı seçer.
    /// </summary>
    public void ChooseArena(int arenaIndex)
    {
        selectedArenaIndex = Mathf.Clamp(arenaIndex, 0, 2);

        if (arenaPanel != null)
        {
            arenaPanel.SetActive(false);
        }

        StartGameWithSelections();
    }

    /// <summary>
    /// Arena seçim butonları tarafından isimle çağrılırsa yönlendirir.
    /// </summary>
    public void ChooseArena(string arenaName)
    {
        string lower = (arenaName ?? "").ToLowerInvariant();
        if (lower.Contains("box") || lower.Contains("boks") || lower.Contains("1"))
        {
            ChooseArena(0);
        }
        else if (lower.Contains("chinese") || lower.Contains("çin") || lower.Contains("cin") || lower.Contains("2"))
        {
            ChooseArena(1);
        }
        else
        {
            ChooseArena(2);
        }
    }

    /// <summary>
    /// Karakter ve arena seçimleri tamamlandıktan sonra oyunu başlatır.
    /// </summary>
    private void StartGameWithSelections()
    {
        // 1. Seçilen arenayı aktif et
        if (RingBoundary.Instance != null)
        {
            RingBoundary.Instance.SwitchEnvironment(selectedArenaIndex);
        }

        if (enemySpawner == null) enemySpawner = FindObjectOfType<EnemySpawner>();
        if (enemySpawner != null)
        {
            enemySpawner.SetSelectedEnvironment(selectedArenaIndex);

            EnsureCharacterDatabaseLoaded();
            if (characterDatabase != null && characterDatabase.Count > 0)
            {
                CharacterEntry selectedEntry = characterDatabase.GetCharacter(currentCharacterIndex);
                enemySpawner.SetExcludedCharacter(characterDatabase, currentCharacterIndex, selectedEntry);
            }
            else
            {
                enemySpawner.SetExcludedCharacterByName(selectedCharacterName);
            }
        }

        // 2. Seçilen karakteri doğur
        SpawnPlayerCharacter(selectedCharacterName);

        // 3. HUD ve panelleri ayarla
        Time.timeScale = 1f;
        if (startPanel != null) startPanel.SetActive(false);
        if (characterPanel != null) characterPanel.SetActive(false);
        if (arenaPanel != null) arenaPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (healtBarPanel != null) healtBarPanel.SetActive(true);
        if (enemyHealthBarPanel != null) enemyHealthBarPanel.SetActive(false);
        if (waveCounterPanel == null) FindWaveCounterPanel();
        if (waveCounterPanel != null) waveCounterPanel.SetActive(true);
        if (mobileControlPanel != null) mobileControlPanel.SetActive(true);
        ShowPauseButton();

        // 4. Düşman spawner'ı seçilen zorluk modunda başlat
        if (enemySpawner != null)
        {
            switch (selectedDifficulty)
            {
                case Difficulty.Easy: enemySpawner.StartEasyMode(); break;
                case Difficulty.Medium: enemySpawner.StartMidMode(); break;
                case Difficulty.Hard: enemySpawner.StartHardMode(); break;
            }
        }

        UpdateWaveCounterUI(force: true);

        if (playerController != null)
        {
            playerController.StartUltiCooldown();
        }

        FollowPlayer camFollow = FindObjectOfType<FollowPlayer>();
        if (camFollow != null && playerController != null)
        {
            camFollow.SetTarget(playerController.transform);
            camFollow.SnapToTarget(); // Kamerayı anında yeni rotasyona (180 dereceye) hizala
        }

        // 5. 3 - 2 - 1 - BAŞLA! geri sayımını başlat
        TriggerMatchCountdown();

        Debug.Log($"<color=cyan>[OYUN BAŞLADI]</color> Karakter: {selectedCharacterName}, Arena Index: {selectedArenaIndex}, Zorluk: {selectedDifficulty}");
    }

    /// <summary>
    /// Seçilen karakter prefab'ini seçilen Texture Arena Environment'ının merkezindeki spawn noktasında oluşturur.
    /// </summary>
    private void SpawnPlayerCharacter(string character)
    {
        if (playerController == null) playerController = FindObjectOfType<PlayerController>();

        ArenaEnvironmentConfig activeArena = RingBoundary.Instance != null ? RingBoundary.Instance.CurrentArenaConfig : null;
        float arenaGroundY = activeArena != null ? activeArena.groundY : 0.1f;

        Vector3 spawnPos;
        Quaternion spawnRot;

        if (activeArena != null)
        {
            spawnPos = activeArena.playerSpawnPosition;
            spawnPos.y = arenaGroundY;
            spawnRot = Quaternion.Euler(0f, activeArena.playerSpawnRotationY, 0f);
        }
        else if (playerSpawnPoint != null)
        {
            spawnPos = playerSpawnPoint.position;
            spawnRot = Quaternion.Euler(0f, 180f, 0f);
        }
        else if (playerController != null)
        {
            spawnPos = playerController.transform.position;
            spawnRot = Quaternion.Euler(0f, 180f, 0f);
        }
        else
        {
            spawnPos = new Vector3(-1000f, 0.1f, 1.0f);
            spawnRot = Quaternion.Euler(0f, 180f, 0f);
        }

        if (playerController != null && playerController.gameObject != null)
        {
            // Eğer Main Camera eski oyuncunun child'ı olarak kalmışsa yok edilmemesi için ayır
            FollowPlayer childCam = playerController.GetComponentInChildren<FollowPlayer>(true);
            if (childCam != null)
            {
                childCam.transform.SetParent(null);
            }

            // Aynı karede FindObjectOfType<PlayerController>() çağrılarının eski objeyi bulmaması için hemen pasif yap
            playerController.gameObject.SetActive(false);
            Destroy(playerController.gameObject);
        }

        GameObject selectedPrefab = null;
        EnsureCharacterDatabaseLoaded();

        // 1. Öncelik: CharacterDatabaseSO üzerinden seçilen karakterin playerPrefab'ini al
        if (characterDatabase != null && characterDatabase.Count > 0)
        {
            CharacterEntry entry = characterDatabase.GetCharacter(currentCharacterIndex);
            if (entry != null && entry.playerPrefab != null)
            {
                selectedPrefab = entry.playerPrefab;
                selectedCharacterName = entry.displayName;
            }
            else if (!string.IsNullOrEmpty(character))
            {
                int foundIdx = characterDatabase.FindCharacterIndex(character);
                if (foundIdx >= 0)
                {
                    currentCharacterIndex = foundIdx;
                    CharacterEntry byName = characterDatabase.GetCharacter(foundIdx);
                    if (byName != null)
                    {
                        selectedPrefab = byName.playerPrefab != null ? byName.playerPrefab : byName.enemyPrefab;
                        selectedCharacterName = byName.displayName;
                    }
                }
            }
        }

        // 2. Fallback: Eski characterPrefabs dizisinden bul
        if (selectedPrefab == null && characterPrefabs != null && !string.IsNullOrEmpty(character))
        {
            foreach (var prefab in characterPrefabs)
            {
                if (prefab != null && prefab.name.ToLower().Contains(character.ToLower()))
                {
                    selectedPrefab = prefab;
                    break;
                }
            }
        }

        if (selectedPrefab != null)
        {
            GameObject newPlayer = Instantiate(selectedPrefab, spawnPos, spawnRot);
            newPlayer.SetActive(true); // Prefab eger kapali kaydedilmisse, sahnede aktif olsun
            newPlayer.tag = "Player";

            // Güvenlik kontrolü: Eğer doğan prefab üzerinde EnemyController varsa kaldır, PlayerController yoksa ekle
            EnemyController leftoverEnemy = newPlayer.GetComponent<EnemyController>();
            if (leftoverEnemy != null)
            {
                DestroyImmediate(leftoverEnemy);
            }

            playerController = newPlayer.GetComponent<PlayerController>();
            if (playerController == null)
            {
                playerController = newPlayer.AddComponent<PlayerController>();
            }

            // Seçilen Texture Arena ortamının tam koordinatına ve zemin yüksekliğine (groundY) yerleştir
            playerController.TeleportToArena(spawnPos, spawnRot, arenaGroundY);

            // Sağlık barını yeni karaktere bağla
            if (playerController != null && healtBarPanel != null)
            {
                Image[] images = healtBarPanel.GetComponentsInChildren<Image>(true);
                foreach (var img in images)
                {
                    if (img.gameObject.name.Contains("Health Bar Green") || img.gameObject.name.Contains("Green"))
                    {
                        playerController.SetHealthBar(img);
                        break;
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning("Karakter prefab'i bulunamadi veya atanmadi: " + character);
        }

        BindUltiButton();
        BindPunchButton();
        BindUppercutButton();
        BindBlockButton();
    }

    // =========================================================================
    // 1. DÜŞMAN KAFA ÜSTÜ CAN BARI (ENEMY HEALTH BAR)
    // =========================================================================

    private void EnsureCanvasReference()
    {
        if (rootCanvas == null)
        {
            rootCanvas = FindObjectOfType<Canvas>();
        }
        if (rootCanvas != null && canvasRectTransform == null)
        {
            canvasRectTransform = rootCanvas.GetComponent<RectTransform>();
        }
    }

    private void FindAndSetupEnemyHealthBar()
    {
        EnsureCanvasReference();

        if (enemyHealthBarPanel == null && rootCanvas != null)
        {
            Transform exact = rootCanvas.transform.Find("Enemy Health Bar");
            if (exact != null)
            {
                enemyHealthBarPanel = exact.gameObject;
            }
            else
            {
                Transform[] allChildren = rootCanvas.GetComponentsInChildren<Transform>(true);
                foreach (var t in allChildren)
                {
                    if (t == null || t == rootCanvas.transform) continue;
                    string lower = t.name.ToLowerInvariant();
                    if (lower.Contains("enemy") && (lower.Contains("health") || lower.Contains("healt") || lower.Contains("bar")))
                    {
                        // Alt elemanlardan (Green/Red) ziyade ana kapsayıcıyı seç
                        if (t.parent == rootCanvas.transform || enemyHealthBarPanel == null)
                        {
                            enemyHealthBarPanel = t.gameObject;
                            if (t.parent == rootCanvas.transform) break;
                        }
                    }
                }
            }
        }

        if (enemyHealthBarPanel == null) return;

        // İçindeki yeşil doluluk barını (Health Bar Green) ve (varsa) yazıyı bul
        if (enemyHealthBarFill == null)
        {
            Image[] images = enemyHealthBarPanel.GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                if (img == null) continue;
                string lower = img.gameObject.name.ToLowerInvariant();
                if (lower.Contains("green") || lower.Contains("fill"))
                {
                    enemyHealthBarFill = img;
                    break;
                }
            }

            // İsimde Green yoksa Filled tipinde olan ve Red olmayan ilk Image'ı seç
            if (enemyHealthBarFill == null)
            {
                foreach (var img in images)
                {
                    if (img == null) continue;
                    string lower = img.gameObject.name.ToLowerInvariant();
                    if (!lower.Contains("red") && !lower.Contains("bg") && img.type == Image.Type.Filled)
                    {
                        enemyHealthBarFill = img;
                        break;
                    }
                }
            }
        }

        if (enemyHealthBarFill != null)
        {
            enemyHealthBarFill.type = Image.Type.Filled;
            enemyHealthBarFill.fillMethod = Image.FillMethod.Horizontal;
        }

        if (enemyHealthText == null)
        {
            enemyHealthText = enemyHealthBarPanel.GetComponentInChildren<TMP_Text>(true);
        }

        // Kafa üstü can barı ekranda gezerken tıklamaları / dokunmaları engellemesin
        Graphic[] graphics = enemyHealthBarPanel.GetComponentsInChildren<Graphic>(true);
        foreach (var g in graphics)
        {
            if (g != null) g.raycastTarget = false;
        }

        NormalizeEnemyHealthBarLayout();
    }

    /// <summary>
    /// Eğer Enemy Health Bar, Player'ın Health Bar'ından kopyalandıysa altındaki Health Bar Red / Green
    /// nesnelerinde kalmış olabilecek (-335 gibi) yerel kaymaları sıfırlar; böylece bar tam düşmanın kafasının üstünde durur.
    /// </summary>
    private void NormalizeEnemyHealthBarLayout()
    {
        if (enemyHealthBarNormalized || enemyHealthBarPanel == null) return;
        enemyHealthBarNormalized = true;

        Transform panelT = enemyHealthBarPanel.transform;
        if (panelT.childCount == 0) return;

        // Referans merkezi olarak Health Bar Green (veya ilk RectTransform çocuk) konumunu al
        RectTransform refChild = enemyHealthBarFill != null
            ? enemyHealthBarFill.GetComponent<RectTransform>()
            : panelT.GetChild(0).GetComponent<RectTransform>();

        Vector3 refWorldPos = refChild != null ? refChild.position : panelT.position;

        List<RectTransform> directRectChildren = new List<RectTransform>();
        List<Vector3> worldOffsets = new List<Vector3>();

        for (int i = 0; i < panelT.childCount; i++)
        {
            RectTransform rt = panelT.GetChild(i) as RectTransform;
            if (rt != null)
            {
                directRectChildren.Add(rt);
                worldOffsets.Add(rt.position - refWorldPos);
            }
        }

        for (int i = 0; i < directRectChildren.Count; i++)
        {
            RectTransform rt = directRectChildren[i];
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.position = panelT.position + worldOffsets[i];
        }
    }

    /// <summary>
    /// Oyuncu bir düşmana vurduğunda çağrılır; ilgili düşmanın kafasının üzerinde can barını gösterir ve azaltır.
    /// </summary>
    public void NotifyEnemyHit(EnemyController enemy)
    {
        if (enemy == null) return;
        if (enemyHealthBarPanel == null) FindAndSetupEnemyHealthBar();
        if (enemyHealthBarPanel == null) return;

        if (trackedEnemy != enemy || trackedEnemyHeadBone == null)
        {
            trackedEnemy = enemy;
            Animator anim = enemy.GetComponent<Animator>() ?? enemy.GetComponentInChildren<Animator>();
            trackedEnemyHeadBone = (anim != null && anim.isHuman) ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
        }

        float maxHp = Mathf.Max(1f, enemy.MaxHealth);
        float curHp = Mathf.Clamp(enemy.CurrentHealth, 0f, maxHp);

        if (enemyHealthBarFill != null)
        {
            enemyHealthBarFill.fillAmount = curHp / maxHp;
        }

        if (enemyHealthText != null)
        {
            enemyHealthText.SetText(Mathf.CeilToInt(curHp).ToString());
        }

        // Düşman bu darbeyle nakavt olduysa (canı 0'a indiyse) barın sıfırlandığını kısaca (0.65 sn) gösterip gizle;
        // hayattaysa vurulduktan sonra enemyHealthBarVisibleDuration (2.5 sn) boyunca açık tut.
        float duration = (curHp <= 0f || enemy.IsDead) ? 0.65f : enemyHealthBarVisibleDuration;
        enemyHealthBarHideTime = Time.time + duration;

        if (!enemyHealthBarPanel.activeSelf && IsGameplayHUDActive())
        {
            enemyHealthBarPanel.SetActive(true);
        }

        UpdateEnemyHealthBarTracking();
    }

    private bool IsGameplayHUDActive()
    {
        return (startPanel == null || !startPanel.activeSelf) &&
               (characterPanel == null || !characterPanel.activeSelf) &&
               (arenaPanel == null || !arenaPanel.activeSelf) &&
               (pausePanel == null || !pausePanel.activeSelf) &&
               (gameOverPanel == null || !gameOverPanel.activeSelf) &&
               (victoryPanel == null || !victoryPanel.activeSelf);
    }

    private void UpdateEnemyHealthBarTracking()
    {
        if (enemyHealthBarPanel == null || !enemyHealthBarPanel.activeSelf) return;

        if (trackedEnemy == null || Time.time >= enemyHealthBarHideTime || !IsGameplayHUDActive())
        {
            enemyHealthBarPanel.SetActive(false);
            trackedEnemy = null;
            trackedEnemyHeadBone = null;
            return;
        }

        // Düşman öldüyse barın ekranda kalma süresini kısalt
        if (trackedEnemy.IsDead && enemyHealthBarHideTime > Time.time + 0.65f)
        {
            enemyHealthBarHideTime = Time.time + 0.65f;
        }

        // Can değerini anlık senkronize et
        float maxHp = Mathf.Max(1f, trackedEnemy.MaxHealth);
        float curHp = Mathf.Clamp(trackedEnemy.CurrentHealth, 0f, maxHp);
        if (enemyHealthBarFill != null)
        {
            enemyHealthBarFill.fillAmount = curHp / maxHp;
        }
        if (enemyHealthText != null)
        {
            enemyHealthText.SetText(Mathf.CeilToInt(curHp).ToString());
        }

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 headWorldPos;
        if (trackedEnemyHeadBone != null)
        {
            Vector3 horizontalBase = Vector3.Lerp(trackedEnemy.transform.position, trackedEnemyHeadBone.position, 0.5f);
            float headY = Mathf.Max(trackedEnemyHeadBone.position.y + enemyHeadHeightOffset, trackedEnemy.transform.position.y + 1.85f);
            headWorldPos = new Vector3(horizontalBase.x, headY, horizontalBase.z);
        }
        else
        {
            headWorldPos = trackedEnemy.transform.position + Vector3.up * enemyFallbackHeightOffset;
        }

        Vector3 screenPos = cam.WorldToScreenPoint(headWorldPos);
        if (screenPos.z <= 0f)
        {
            return;
        }

        // Ekran sınırları içinde tut
        float marginX = 60f;
        float marginY = 35f;
        screenPos.x = Mathf.Clamp(screenPos.x, marginX, Screen.width - marginX);
        screenPos.y = Mathf.Clamp(screenPos.y, marginY, Screen.height - marginY);

        EnsureCanvasReference();
        if (canvasRectTransform != null)
        {
            Camera uiCam = (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? rootCanvas.worldCamera
                : null;

            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRectTransform, screenPos, uiCam, out Vector3 uiWorldPos))
            {
                enemyHealthBarPanel.transform.position = uiWorldPos;
            }
            else
            {
                enemyHealthBarPanel.transform.position = screenPos;
            }
        }
        else
        {
            enemyHealthBarPanel.transform.position = screenPos;
        }
    }

    // =========================================================================
    // 2. GERİ SAYIM PANELİ (COUNT DOWN PANEL: 3 - 2 - 1 -> 300 | BAŞLA! -> 90)
    // =========================================================================

    private void FindCountDownPanel()
    {
        EnsureCanvasReference();

        if (countDownPanel == null && rootCanvas != null)
        {
            Transform exact = rootCanvas.transform.Find("Count Down Panel")
                           ?? rootCanvas.transform.Find("CountDown Panel")
                           ?? rootCanvas.transform.Find("Countdown Panel");
            if (exact != null)
            {
                countDownPanel = exact.gameObject;
            }
            else
            {
                foreach (Transform child in rootCanvas.transform)
                {
                    if (child == null) continue;
                    string lower = child.name.ToLowerInvariant();
                    if (lower.Contains("count") && lower.Contains("down"))
                    {
                        countDownPanel = child.gameObject;
                        break;
                    }
                }
            }
        }

        if (countDownPanel == null) return;

        if (countDownText == null)
        {
            countDownText = countDownPanel.GetComponentInChildren<TMP_Text>(true);
        }
        if (legacyCountDownText == null)
        {
            legacyCountDownText = countDownPanel.GetComponentInChildren<Text>(true);
        }

        if (countDownText != null)
        {
            countDownText.enableAutoSizing = false;
            countDownText.enableWordWrapping = false;
            countDownText.overflowMode = TextOverflowModes.Overflow;
            countDownText.alignment = TextAlignmentOptions.Center;
        }

        if (legacyCountDownText != null)
        {
            legacyCountDownText.resizeTextForBestFit = false;
            legacyCountDownText.horizontalOverflow = HorizontalWrapMode.Overflow;
            legacyCountDownText.verticalOverflow = VerticalWrapMode.Overflow;
            legacyCountDownText.alignment = TextAnchor.MiddleCenter;
        }

        Graphic[] graphics = countDownPanel.GetComponentsInChildren<Graphic>(true);
        foreach (var g in graphics)
        {
            if (g != null) g.raycastTarget = false;
        }
    }

    public void TriggerMatchCountdown()
    {
        FindCountDownPanel();
        if (countDownCoroutine != null)
        {
            StopCoroutine(countDownCoroutine);
            countDownCoroutine = null;
        }
        countDownCoroutine = StartCoroutine(CountdownRoutine());
    }

    public void StopMatchCountdown()
    {
        if (countDownCoroutine != null)
        {
            StopCoroutine(countDownCoroutine);
            countDownCoroutine = null;
        }
        IsCountingDown = false;
        if (countDownPanel != null)
        {
            countDownPanel.SetActive(false);
        }
    }

    private IEnumerator CountdownRoutine()
    {
        IsCountingDown = true;

        if (countDownPanel != null)
        {
            countDownPanel.transform.SetAsLastSibling();
            countDownPanel.SetActive(true);
        }

        // 3 (Font Size: 300)
        SetCountDownDisplay("3", 300f);
        yield return new WaitForSeconds(1.0f);

        // 2 (Font Size: 300)
        SetCountDownDisplay("2", 300f);
        yield return new WaitForSeconds(1.0f);

        // 1 (Font Size: 300)
        SetCountDownDisplay("1", 300f);
        yield return new WaitForSeconds(1.0f);

        // BAŞLA! (Font Size: 90)
        SetCountDownDisplay("BAŞLA!", 90f);
        IsCountingDown = false; // BAŞLA! yazısı ekrana geldiği anda dövüş başlasın
        yield return new WaitForSeconds(0.8f);

        if (countDownPanel != null)
        {
            countDownPanel.SetActive(false);
        }

        countDownCoroutine = null;
    }

    private void SetCountDownDisplay(string textValue, float fontSize)
    {
        if (countDownText != null)
        {
            countDownText.enableAutoSizing = false;
            countDownText.fontSize = fontSize;
            countDownText.SetText(textValue);
        }

        if (legacyCountDownText != null)
        {
            legacyCountDownText.resizeTextForBestFit = false;
            legacyCountDownText.fontSize = Mathf.RoundToInt(fontSize);
            legacyCountDownText.text = textValue;
        }
    }

    // =========================================================================
    // 3. DALGA SAYACI PANELİ (WAVE COUNTER PANEL)
    // =========================================================================

    private void FindWaveCounterPanel()
    {
        EnsureCanvasReference();

        if (waveCounterPanel == null && rootCanvas != null)
        {
            Transform exact = rootCanvas.transform.Find("Wave Counter Panel")
                           ?? rootCanvas.transform.Find("WaveCounter Panel")
                           ?? rootCanvas.transform.Find("WaveCounterPanel")
                           ?? rootCanvas.transform.Find("Wave Panel");
            if (exact != null)
            {
                waveCounterPanel = exact.gameObject;
            }
            else
            {
                foreach (Transform child in rootCanvas.transform)
                {
                    if (child == null) continue;
                    string lower = child.name.ToLowerInvariant();
                    if (lower.Contains("wave") && (lower.Contains("counter") || lower.Contains("panel") || lower.Contains("count")))
                    {
                        waveCounterPanel = child.gameObject;
                        break;
                    }
                }
            }
        }

        if (waveCounterPanel == null) return;

        if (waveCounterText == null)
        {
            TMP_Text[] tmpTexts = waveCounterPanel.GetComponentsInChildren<TMP_Text>(true);
            if (tmpTexts != null && tmpTexts.Length > 0)
            {
                if (tmpTexts.Length == 1)
                {
                    waveCounterText = tmpTexts[0];
                }
                else
                {
                    // Birden fazla TMP_Text varsa (örn. sabit "DALGA" başlığı + sayı alanı), sayı alanını seç
                    foreach (var t in tmpTexts)
                    {
                        string trimmed = (t.text ?? "").Trim();
                        if (int.TryParse(trimmed, out _) || t.name.ToLowerInvariant().Contains("count") || t.name.ToLowerInvariant().Contains("num"))
                        {
                            waveCounterText = t;
                            waveCounterFormatTemplate = "{0}";
                            break;
                        }
                    }
                    if (waveCounterText == null)
                    {
                        waveCounterText = tmpTexts[tmpTexts.Length - 1];
                    }
                }
            }
        }

        if (legacyWaveCounterText == null && waveCounterText == null)
        {
            legacyWaveCounterText = waveCounterPanel.GetComponentInChildren<Text>(true);
        }

        if (waveCounterText != null)
        {
            waveCounterText.enableWordWrapping = false;
            waveCounterText.overflowMode = TextOverflowModes.Overflow;

            if (string.IsNullOrEmpty(waveCounterFormatTemplate))
            {
                string initial = (waveCounterText.text ?? "").Trim();
                string lower = initial.ToLowerInvariant();
                if (lower.Contains("dalga") || lower.Contains("wave"))
                {
                    if (System.Text.RegularExpressions.Regex.IsMatch(initial, @"\d+"))
                    {
                        waveCounterFormatTemplate = System.Text.RegularExpressions.Regex.Replace(initial, @"\d+", "{0}");
                    }
                    else
                    {
                        waveCounterFormatTemplate = initial + " {0}";
                    }
                }
                else
                {
                    waveCounterFormatTemplate = "DALGA {0}";
                }
            }
        }

        Graphic[] graphics = waveCounterPanel.GetComponentsInChildren<Graphic>(true);
        foreach (var g in graphics)
        {
            if (g != null) g.raycastTarget = false;
        }
    }

    public void UpdateWaveCounterUI(bool force = false)
    {
        if (waveCounterPanel == null) return;
        if (!waveCounterPanel.activeSelf && !force) return;

        if (enemySpawner == null)
        {
            enemySpawner = FindObjectOfType<EnemySpawner>();
        }

        int currentWave = (enemySpawner != null && enemySpawner.CurrentWave > 0) ? enemySpawner.CurrentWave : 1;
        if (!force && currentWave == lastDisplayedWave) return;

        lastDisplayedWave = currentWave;
        string template = string.IsNullOrEmpty(waveCounterFormatTemplate) ? "DALGA {0}" : waveCounterFormatTemplate;
        string formatted = string.Format(template, currentWave);

        if (waveCounterText != null)
        {
            waveCounterText.SetText(formatted);
        }
        if (legacyWaveCounterText != null)
        {
            legacyWaveCounterText.text = formatted;
        }
    }
}
