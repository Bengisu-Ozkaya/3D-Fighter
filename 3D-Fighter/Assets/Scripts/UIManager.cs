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
    [Header("Karakter Seçimi")]
    [SerializeField] private GameObject[] characterPrefabs;
    [SerializeField] private Transform playerSpawnPoint;
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
    }

    void Start()
    {
        Time.timeScale = 1f;

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
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (pauseButton != null) pauseButton.SetActive(false);
        if (healtBarPanel != null) healtBarPanel.SetActive(false);
        if (mobileControlPanel != null)
        {
            mobileControlPanel.SetActive(startPanel == null || !startPanel.activeSelf);
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

        // Klavye kısayolu (ESC veya P) ile oyunu duraklat / devam ettir
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
        {
            if (pausePanel != null && pausePanel.activeSelf)
            {
                ContinueButton();
            }
            else if (startPanel != null && !startPanel.activeSelf &&
                     (gameOverPanel == null || !gameOverPanel.activeSelf) &&
                     (victoryPanel == null || !victoryPanel.activeSelf))
            {
                PauseGame();
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
                // Simple cleanup: we can just clear them all if we are setting it up dynamically
                // But to be safe, just clear all triggers for PointerDown/PointerUp to reset our logic
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
        if (characterPanel != null) characterPanel.SetActive(true);
    }

    /// <summary>
    /// Tüm dalgalar bittiğinde zafer panelini açar
    /// </summary>
    public void ShowVictoryPanel()
    {
        Time.timeScale = 1f;
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (pauseButton != null) pauseButton.SetActive(false);
        if (mobileControlPanel != null) mobileControlPanel.SetActive(false);
        if (healtBarPanel != null) healtBarPanel.SetActive(false);
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
        if (mobileControlPanel != null) mobileControlPanel.SetActive(true);
        ShowPauseButton();

        // 1. Spawner'da oyuncunun öldüğü mevcut dalgayı yeniden başlat
        if (enemySpawner == null) enemySpawner = FindObjectOfType<EnemySpawner>();
        if (enemySpawner != null)
        {
            enemySpawner.RestartCurrentWave();
        }

        // 2. Oyuncuyu Kip Up animasyonu ile ayağa kaldır
        if (playerController == null) playerController = FindObjectOfType<PlayerController>();
        if (playerController != null)
        {
            playerController.RelivePlayer();
            playerController.StartUltiCooldown();
        }
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
        if (healtBarPanel != null) healtBarPanel.SetActive(true);   // ← Health bar göster
        if (mobileControlPanel != null) mobileControlPanel.SetActive(true);
        ShowPauseButton();

        // Oyuncuyu sıfırla
        if (playerController == null) playerController = FindObjectOfType<PlayerController>();
        if (playerController != null)
        {
            playerController.ResetPlayerState();
            playerController.StartUltiCooldown();
        }

        // Spawner'ı aynı modda yeniden başlat
        if (enemySpawner == null) enemySpawner = FindObjectOfType<EnemySpawner>();
        if (enemySpawner != null)
        {
            enemySpawner.RestartGame();
        }
    }

    /// <summary>
    /// Ana Menü / Home butonu: Start paneline geri döner ve oyunu sıfırlar
    /// </summary>
    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f; // Oyun duraklatılmışsa zamanı normale döndür
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (pauseButton != null) pauseButton.SetActive(false);
        if (healtBarPanel != null) healtBarPanel.SetActive(false);
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
        Debug.Log("<color=cyan>[ANA MENÜ]</color> Start Panel açıldı, oyun sıfırlandı.");
    }

    /// <summary>
    /// Oyunu duraklatır (Pause) ve Pause Panelini açar
    /// </summary>
    public void PauseGame()
    {
        // Start paneli, zafer veya yenilgi paneli açıkken duraklatma yapılamaz
        if (startPanel != null && startPanel.activeSelf) return;
        if (victoryPanel != null && victoryPanel.activeSelf) return;
        if (gameOverPanel != null && gameOverPanel.activeSelf) return;

        Time.timeScale = 0f;
        if (healtBarPanel != null) healtBarPanel.SetActive(false);  // ← Can barını gizle
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
        if (mobileControlPanel != null) mobileControlPanel.SetActive(true);
        ShowPauseButton();
        Debug.Log("<color=green>[OYUN DEVAM EDİYOR]</color> Pause Panel kapatıldı, oyun devam ediyor.");
    }

    public void ChooseCharacter(string Character)
    {
        if (characterPanel != null) characterPanel.SetActive(false);
        
        if (playerController == null) playerController = FindObjectOfType<PlayerController>();
        Vector3 spawnPos = Vector3.zero;
        Quaternion spawnRot = Quaternion.identity;

        if (playerSpawnPoint != null)
        {
            spawnPos = playerSpawnPoint.position;
            spawnRot = playerSpawnPoint.rotation;
        }
        else if (playerController != null)
        {
            spawnPos = playerController.transform.position;
            spawnRot = playerController.transform.rotation;
        }

        if (playerController != null && playerController.gameObject != null)
        {
            Destroy(playerController.gameObject);
        }

        GameObject selectedPrefab = null;
        if (characterPrefabs != null)
        {
            foreach (var prefab in characterPrefabs)
            {
                if (prefab != null && prefab.name.ToLower().Contains(Character.ToLower()))
                {
                    selectedPrefab = prefab;
                    break;
                }
            }
        }

        if (selectedPrefab != null)
        {
            GameObject newPlayer = Instantiate(selectedPrefab, spawnPos, spawnRot);
            newPlayer.SetActive(true); // <--- Prefab eger kapali kaydedilmisse, sahnede aktif olsun
            playerController = newPlayer.GetComponent<PlayerController>();
        }
        else
        {
            Debug.LogWarning("Karakter prefab'i bulunamadi veya atanmadi: " + Character);
        }

        BindUltiButton();
        BindPunchButton();
        BindUppercutButton();
        BindBlockButton();

        Time.timeScale = 1f;
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (healtBarPanel != null) healtBarPanel.SetActive(true);
        if (mobileControlPanel != null) mobileControlPanel.SetActive(true);
        ShowPauseButton(); 

        if (enemySpawner != null)
        {
            switch (selectedDifficulty)
            {
                case Difficulty.Easy: enemySpawner.StartEasyMode(); break;
                case Difficulty.Medium: enemySpawner.StartMidMode(); break;
                case Difficulty.Hard: enemySpawner.StartHardMode(); break;
            }
        }
        
        if (playerController != null)
        {
            playerController.StartUltiCooldown();
        }
        
        FollowPlayer camFollow = FindObjectOfType<FollowPlayer>();
        if (camFollow != null && playerController != null)
        {
            camFollow.SetTarget(playerController.transform);
        }
        
        Debug.Log("Oyuna baslandi! Secilen karakter: " + Character + ", Zorluk: " + selectedDifficulty);
    }
}
