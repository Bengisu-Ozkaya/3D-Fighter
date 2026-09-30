using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("UI Panelleri")]
    [SerializeField] GameObject startPanel;
    [SerializeField] GameObject victoryPanel;
    [SerializeField] GameObject gameOverPanel;
    [SerializeField] GameObject mobileControlPanel;

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

        BindGameOverButtons();
    }

    void Start()
    {
        if (startPanel != null) startPanel.SetActive(true);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (mobileControlPanel != null)
        {
            mobileControlPanel.SetActive(startPanel == null || !startPanel.activeSelf);
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

    public void EasyMode()
    {
        if (startPanel != null) startPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (mobileControlPanel != null) mobileControlPanel.SetActive(true);
        if (enemySpawner != null)
        {
            enemySpawner.StartEasyMode();
        }
    }

    public void MidMode()
    {
        if (startPanel != null) startPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (mobileControlPanel != null) mobileControlPanel.SetActive(true);
        if (enemySpawner != null)
        {
            enemySpawner.StartMidMode();
        }
    }

    public void HardMode()
    {
        if (startPanel != null) startPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (mobileControlPanel != null) mobileControlPanel.SetActive(true);
        if (enemySpawner != null)
        {
            enemySpawner.StartHardMode();
        }
    }

    /// <summary>
    /// Tüm dalgalar bittiğinde zafer panelini açar
    /// </summary>
    public void ShowVictoryPanel()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (mobileControlPanel != null) mobileControlPanel.SetActive(false);
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
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (mobileControlPanel != null) mobileControlPanel.SetActive(true);

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
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (startPanel != null) startPanel.SetActive(false);
        if (mobileControlPanel != null) mobileControlPanel.SetActive(true);

        // Oyuncuyu sıfırla
        if (playerController == null) playerController = FindObjectOfType<PlayerController>();
        if (playerController != null)
        {
            playerController.ResetPlayerState();
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
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
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
    }
}
